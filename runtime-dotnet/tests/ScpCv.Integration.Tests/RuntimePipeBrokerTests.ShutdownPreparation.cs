// 真认证管道验证Supervisor准备确认先于任何Worker通知；伪造nonce不能放行停止。
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Ipc;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    [Fact]
    public async Task MissingPreparationAcknowledgmentCannotNotifyAnyWorker()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid(); var starting = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, starting.GroupEpoch);
        using var process = Process.GetCurrentProcess(); var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server); await broker.StartAsync(CancellationToken.None);
        var supervisorId = Guid.NewGuid(); var officeId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "supervisor", supervisorId));
        await using var supervisor = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(supervisor, process, "supervisor", supervisorId, null); _ = await ReadAsync(supervisor);
        registry.Register(CurrentIdentity(process, "office", officeId));
        await using var office = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(office, process, "office", officeId, null); _ = await ReadAsync(office);
        try
        {
            var stopping = broker.NotifyRuntimeShutdownAsync("controlled_stop");
            var notice = await ReadAsync(supervisor);
            Assert.Equal("shutdown_request", notice.MessageType);
            // 不读取office流，PeekNamedPipe确认其尚无任何字节，避免人为挂起另一个reader。
            await Assert.ThrowsAsync<TimeoutException>(() => stopping);
            Assert.True(PeekNamedPipe(office.SafePipeHandle.DangerousGetHandle(), IntPtr.Zero, 0, IntPtr.Zero, out var available, IntPtr.Zero));
            Assert.Equal(0u, available);
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task ShutdownRequiresCurrentSupervisorAcknowledgmentBeforeNotifyingOffice()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var request = Guid.NewGuid(); var starting = await fixture.RuntimeAuthority.BeginStartAsync(request);
        await fixture.RuntimeAuthority.ArmAsync(request, starting.GroupEpoch);
        using var process = Process.GetCurrentProcess(); var registry = new RegisteredProcessRegistry();
        var server = new NamedPipeServer(Guid.NewGuid(), process.SessionId, registry);
        using var broker = CreateBroker(fixture, registry, server); await broker.StartAsync(CancellationToken.None);
        var supervisorId = Guid.NewGuid(); var officeId = Guid.NewGuid();
        registry.Register(CurrentIdentity(process, "supervisor", supervisorId));
        await using var supervisor = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(supervisor, process, "supervisor", supervisorId, null); var welcome = await ReadAsync(supervisor);
        registry.Register(CurrentIdentity(process, "office", officeId));
        await using var office = await ConnectAsync(server.PipeName);
        await WriteHelloAsync(office, process, "office", officeId, null); _ = await ReadAsync(office);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        try
        {
            var stopping = broker.NotifyRuntimeShutdownAsync("controlled_stop", budget.Token);
            var notice = await ReadAsync(supervisor);
            Assert.Equal("shutdown_request", notice.MessageType);
            Assert.Equal(welcome.OwnerEpoch, notice.OwnerEpoch);
            Assert.Equal(starting.GroupEpoch, notice.Payload.GetProperty("group_epoch").GetInt64());
            var officeNotice = ReadAsync(office);
            await WriteAsync(supervisor, Frame("shutdown_prepared", supervisorId, welcome.OwnerEpoch, null, new { shutdown_id = Guid.NewGuid() }));
            Assert.False((await ReadAsync(supervisor)).Payload.GetProperty("accepted").GetBoolean());
            Assert.False(stopping.IsCompleted); Assert.False(officeNotice.IsCompleted);
            await WriteAsync(supervisor, Frame("shutdown_prepared", supervisorId, welcome.OwnerEpoch, null, new { shutdown_id = notice.Payload.GetProperty("shutdown_id").GetGuid() }));
            Assert.True((await ReadAsync(supervisor)).Payload.GetProperty("accepted").GetBoolean());
            await stopping.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("shutdown_request", (await officeNotice).MessageType);
        }
        finally { await broker.StopAsync(CancellationToken.None); }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PeekNamedPipe(IntPtr pipe, IntPtr buffer, uint size, IntPtr read, out uint available, IntPtr remaining);
}
