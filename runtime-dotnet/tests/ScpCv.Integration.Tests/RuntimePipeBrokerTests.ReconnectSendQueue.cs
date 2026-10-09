// 仅自有外部管道与固定数量Wake建立发送积压；重连必须结束旧发送且不污染新连接Ready。
using System.Diagnostics;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>:returns: 真实旧管道不读取形成512条有界发送积压，退休后全部完成且新Ready/Wake仍健康。</summary>
    [Fact]
    public async Task SameInstanceReconnectCompletesBoundedOldWakeQueueAndPreservesNewReadiness()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var instance = Guid.NewGuid();
        var registry = new RegisteredProcessRegistry();
        registry.Register(CurrentIdentity(process, "player-1", instance));
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        await using var original = await ConnectAsync(server.PipeName);
        using var sendLifetime = new CancellationTokenSource();
        var sends = new List<Task>();
        try
        {
            await WriteHelloAsync(original, process, "player-1", instance, DisplayTarget(1));
            var welcome = await ReadAsync(original);
            Assert.Equal("welcome", welcome.MessageType);
            await WriteAsync(original, Frame("worker_ready", instance, welcome.OwnerEpoch, DisplayTarget(1), ReadyForReconnect()));
            Assert.True((await ReadAsync(original)).Payload.GetProperty("accepted").GetBoolean());

            // 从此不读取旧pipe；数量固定，不依赖无限压力或私有gate字段。
            for (var index = 0; index < 512; index++)
                sends.Add(broker.WakeAsync(new CommandWakeSignal(CommandTargetKind.Display, 1, index), sendLifetime.Token).AsTask());
            var allSends = Task.WhenAll(sends);
            await Assert.ThrowsAsync<TimeoutException>(() => allSends.WaitAsync(TimeSpan.FromMilliseconds(200)));
            Assert.Contains(sends, send => !send.IsCompleted);

            await using var replacement = await ConnectAsync(server.PipeName);
            await WriteHelloAsync(replacement, process, "player-1", instance, DisplayTarget(1));
            var reconnected = await ReadAsync(replacement);
            Assert.Equal("welcome", reconnected.MessageType);
            Assert.Equal(welcome.OwnerEpoch, reconnected.OwnerEpoch);
            await allSends.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.All(sends, send => Assert.True(send.IsCompletedSuccessfully, "退休连接的旧Wake必须有界结束，不能留下gate waiter。"));

            await WriteAsync(replacement, Frame("worker_ready", instance, reconnected.OwnerEpoch, DisplayTarget(1), ReadyForReconnect()));
            Assert.True((await ReadAsync(replacement)).Payload.GetProperty("accepted").GetBoolean());
            Assert.DoesNotContain("player-1", broker.GetRuntimeReadiness(group.GroupEpoch).MissingRoles);
            await broker.WakeAsync(new CommandWakeSignal(CommandTargetKind.Display, 1, 513));
            Assert.Equal("wake", (await ReadAsync(replacement)).MessageType);
            Assert.DoesNotContain("player-1", broker.GetRuntimeReadiness(group.GroupEpoch).MissingRoles);
        }
        finally
        {
            // 独立caller取消只用于失败收尾；公共Stop仍需等待连接真实退休，不把预算超时当成功。
            sendLifetime.Cancel();
            await original.DisposeAsync();
            try
            {
                if (sends.Count != 0)
                {
                    try { await Task.WhenAll(sends).WaitAsync(TimeSpan.FromSeconds(5)); }
                    catch (OperationCanceledException) { }
                }
            }
            finally { await broker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }
}
