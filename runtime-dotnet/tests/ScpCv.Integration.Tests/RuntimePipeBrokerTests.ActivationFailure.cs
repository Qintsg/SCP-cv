// 真实Worker/管道/独立SQLite验证确认空输出的失败结果；不启动Office，不冒称完整Player连线。
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class RuntimePipeBrokerTests
{
    /// <summary>:returns: 已确认两个资源均释放后的空输出必须清实际源/布局/模式；desired源不因失败伪变为已播出。</summary>
    [Fact]
    public async Task ConfirmedEmptyActivationResultClearsActualPresentationWithoutChangingDesiredSource()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var requestId = Guid.NewGuid();
        var starting = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, starting.GroupEpoch);
        var desiredId = await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var oldSource = new MediaSource { Name = "owned-previous", SourceType = MediaSourceType.Presentation,
                Uri = "old.pptx", SourceRevision = 1, ContentDigest = new string('a', 64) };
            var desired = new MediaSource { Name = "owned-candidate", SourceType = MediaSourceType.Presentation,
                Uri = "candidate.pptx", SourceRevision = 1, ContentDigest = new string('b', 64) };
            database.MediaSources.AddRange(oldSource, desired);
            await database.SaveChangesAsync(token);
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, token);
            session.MediaSourceId = desired.Id; session.ActualSourceId = oldSource.Id;
            session.ActualAdapterKind = "libreoffice"; session.ActualFitText = true;
            session.PlaybackMode = PlaybackMode.LibreOffice; session.PlaybackState = PlaybackState.Playing;
            session.CurrentSlide = 6; session.TotalSlides = 7;
            session.DesiredGeneration = 7; session.ObservedGeneration = 6;
            return desired.Id;
        });
        var command = await fixture.Commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "OPEN", "{}", 7, 1));
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
        var running = worker.RunAsync((lease, _) =>
        {
            Interlocked.Increment(ref executions);
            // 此层注入Player公开结果形态；Player的释放/切入另由真实宿主测试证明。
            return Task.FromResult(new WorkerExecutionResult("failed", "presentation_activation_failed",
                JsonSerializer.SerializeToElement(new { source_generation = lease.SourceGeneration, source_id = (long?)null,
                    playback_state = "error", playback_mode = "none", adapter_kind = "", actual_fit_text = (bool?)null,
                    current_slide = 0, total_slides = 0, position_ms = 0, duration_ms = 0, error_message = "候选切入失败，两资源确认已释放" })));
        }, stopping.Token);
        try
        {
            var runtime = new RuntimeStateService(fixture.Database, fixture.Writes,
                new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
            var observed = (await runtime.GetSessionsAsync()).Single(item => item.WindowId == 1);
            while (observed.PlaybackState != "error" || observed.ObservedGeneration != 7)
            { await Task.Delay(10, budget.Token); observed = (await runtime.GetSessionsAsync()).Single(item => item.WindowId == 1); }
            await using var database = fixture.Database.CreateDbContext();
            var terminal = await database.CommandRecords.AsNoTracking().SingleAsync(item => item.CommandId == command.CommandId, budget.Token);
            Assert.Equal(CommandStatus.Failed, terminal.Status);
            Assert.Equal("presentation_activation_failed", terminal.ResultCode);
            Assert.Equal(desiredId, observed.SourceId);
            Assert.Null(observed.ActualSourceId);
            Assert.Null(observed.ActualFitText);
            Assert.Equal(string.Empty, observed.ActualAdapterKind);
            // 内部None按既有HTTP合同表示为空串，不新增第二种REST模式。
            Assert.Equal(string.Empty, observed.PlaybackMode);
            Assert.Equal(0, observed.CurrentSlide); Assert.Equal(0, observed.TotalSlides);
            Assert.Equal(1, executions);
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
