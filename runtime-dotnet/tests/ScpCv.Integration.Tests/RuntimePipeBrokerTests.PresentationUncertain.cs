// 真实Worker→Named Pipe→持久命令/会话的原生不确定终态回归，不启动Office或播放器。
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Presentations;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>:returns: 已下发原生结果不明必须持久化uncertain，保留最后实际源/布局和占用，不重放。</summary>
    [Theory]
    [InlineData("transport_timeout", CommandStatus.Uncertain)]
    [InlineData("transport_disconnect", CommandStatus.Uncertain)]
    [InlineData("transport_close", CommandStatus.Uncertain)]
    [InlineData("replacement_cleanup", CommandStatus.Uncertain)]
    [InlineData("known_package", CommandStatus.Failed)]
    [InlineData("known_io", CommandStatus.Failed)]
    public async Task PresentationTransportUncertaintySurvivesWorkerAndPersistentResultPipeline(string failureKind, CommandStatus expected)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        var sourceId = await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var source = new MediaSource
            {
                Name = "uncertain-original", SourceType = MediaSourceType.Presentation, Uri = "owned.pptx",
                SourceRevision = 1, ContentDigest = new string('a', 64),
            };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync(token);
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, token);
            session.MediaSourceId = source.Id; session.ActualSourceId = source.Id;
            session.ActualAdapterKind = "libreoffice"; session.ActualFitText = true;
            session.PlaybackMode = PlaybackMode.LibreOffice; session.PlaybackState = PlaybackState.Playing;
            session.DesiredGeneration = 7; session.ObservedGeneration = 7; session.CleanupPending = true;
            return source.Id;
        });
        var command = await fixture.Commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "CLOSE", "{}", 7, 1));
        using var process = Process.GetCurrentProcess();
        var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server);
        await broker.StartAsync(CancellationToken.None);
        var instance = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "player-1", instance));
        await using var worker = new RuntimeWorkerSession(server.PipeName,
            new RuntimeWorkerIdentity("player-1", instance, DisplayTarget(1), ["libreoffice"]));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
        var executions = 0;
        var running = worker.RunAsync((_, _) =>
        {
            Interlocked.Increment(ref executions);
            throw failureKind switch
            {
                "transport_timeout" => new PresentationTransportException("原生控制超时", new TimeoutException("已下发等待超时")),
                "transport_disconnect" => new PresentationTransportException("原生回复断管", new EndOfStreamException("已下发结果缺失")),
                "transport_close" => new PresentationTransportException("原生关闭完成不确定", new IOException("关闭未确认")),
                "replacement_cleanup" => new PresentationTransportException("旧关闭与候选清理均未确认",
                    new AggregateException(new PresentationTransportException("旧关闭回执缺失", new EndOfStreamException()),
                        new IOException("候选清理失败"))),
                "known_package" => new InvalidDataException("纯包预检拒绝，尚未启动引擎"),
                _ => new IOException("尚未启动引擎的已知文件读取失败"),
            };
        }, stopping.Token);
        try
        {
            CommandRecord? terminal = null;
            while (terminal is null)
            {
                await using var database = fixture.Database.CreateDbContext();
                var current = await database.CommandRecords.AsNoTracking().SingleAsync(item => item.CommandId == command.CommandId, budget.Token);
                if (current.Status is CommandStatus.Failed or CommandStatus.Uncertain) terminal = current;
                else await Task.Delay(10, budget.Token);
            }
            Assert.Equal(expected, terminal.Status);
            if (expected == CommandStatus.Uncertain)
            {
                Assert.Contains("PresentationTransportException", terminal.ResultEvidenceJson, StringComparison.Ordinal);
                Assert.Equal("presentation_transport_uncertain", terminal.ResultCode);
            }
            else Assert.StartsWith("worker_exception:", terminal.ResultCode, StringComparison.Ordinal);
            var runtime = new RuntimeStateService(fixture.Database, fixture.Writes,
                new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
            var observed = (await runtime.GetSessionsAsync()).Single(item => item.WindowId == 1);
            while (observed.PlaybackState != "error")
            {
                await Task.Delay(10, budget.Token);
                observed = (await runtime.GetSessionsAsync()).Single(item => item.WindowId == 1);
            }
            Assert.Equal(sourceId, observed.ActualSourceId);
            Assert.True(observed.ActualFitText);
            Assert.Equal("libreoffice", observed.ActualAdapterKind);
            Assert.True(observed.CleanupPending);
            Assert.Equal(1, executions);
            Assert.Equal(7, observed.ObservedGeneration);
        }
        finally
        {
            stopping.Cancel();
            try { await running.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
            await broker.StopAsync(CancellationToken.None);
        }
    }
}
