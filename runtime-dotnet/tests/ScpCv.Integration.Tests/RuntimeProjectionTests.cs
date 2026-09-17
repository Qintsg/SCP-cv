using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Playback;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed class RuntimeProjectionTests
{
    [Fact]
    public async Task DisplayTransportHeartbeatKeepsPlayerOnlineBetweenCommands()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var worker = Guid.NewGuid();
        var ownership = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display,
            1,
            worker,
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            process.SessionId,
            group.GroupEpoch,
            "[\"wpf\"]",
            PreviousOwnerExitConfirmed: true));

        var runtime = new RuntimeStateService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider);

        // 仅完成注册还不算“最近见过播放器”。
        Assert.False((await runtime.GetSessionAsync(1)).PlayerOnline);

        var accepted = await fixture.RuntimeAuthority.RecordHeartbeatAsync(
            CommandTargetKind.Display,
            1,
            worker,
            ownership.OwnerEpoch,
            uiProgress: false);
        Assert.True(accepted);

        // 传输心跳必须让会话重新在线，否则前端会以“PlayerWorker 当前离线”拒绝控制命令。
        Assert.True((await runtime.GetSessionAsync(1)).PlayerOnline);
    }

    [Fact]
    public async Task DisplayStateReportWithNullSourceIdIsAccepted()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var worker = Guid.NewGuid();
        var ownership = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display,
            1,
            worker,
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            process.SessionId,
            group.GroupEpoch,
            "[\"wpf\",\"libvlc\"]",
            PreviousOwnerExitConfirmed: true));

        await fixture.Writes.ExecuteAsync(async (database, cancellationToken) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, cancellationToken);
            session.DesiredGeneration = 3;
            session.ObservedGeneration = 0;
        });
        var runtime = new RuntimeStateService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider);
        var audio = new BackgroundAudioService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider);
        var publisher = new RuntimeProjectionPublisher(
            fixture.Writes,
            new SseEventHub(runtime, audio, new SseEventStreamOptions()),
            fixture.TimeProvider);

        // PlayerWorker 在无源时把 source_id 序列化为 null；旧实现会在这里抛
        // InvalidOperationException，导致 ControlHost 回复 error 帧、Worker 弹框停机。
        var accepted = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display,
            1,
            worker,
            ownership.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 3,
                State = System.Text.Json.JsonSerializer.SerializeToElement(new
                {
                    source_generation = 3,
                    source_id = (long?)null,
                    playback_state = "idle",
                    playback_mode = "",
                    adapter_kind = "",
                    current_slide = 0,
                    total_slides = 0,
                    position_ms = 0,
                    duration_ms = 0,
                    error_message = "",
                }),
            });

        Assert.True(accepted.Accepted);
        await using var check = fixture.Database.CreateDbContext();
        var sessionAfter = await check.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(3, sessionAfter.ObservedGeneration);
        Assert.Null(sessionAfter.ActualSourceId);
    }

    [Fact]
    public async Task AudioStateReportCannotOverwriteNewerSourceGeneration()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var startRequest = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(startRequest);
        await fixture.RuntimeAuthority.ArmAsync(startRequest, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        var worker = Guid.NewGuid();
        var ownership = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Audio,
            1,
            worker,
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            process.SessionId,
            group.GroupEpoch,
            "[\"libvlc\"]",
            PreviousOwnerExitConfirmed: true));

        await fixture.Writes.ExecuteAsync(async (database, cancellationToken) =>
        {
            var state = await database.BackgroundAudioStates.SingleAsync(cancellationToken);
            state.DesiredGeneration = 2;
            state.ObservedGeneration = 0;
            state.PlaybackState = PlaybackState.Loading;
        });
        var runtime = new RuntimeStateService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider);
        var audio = new BackgroundAudioService(
            fixture.Database,
            fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()),
            fixture.TimeProvider);
        var publisher = new RuntimeProjectionPublisher(
            fixture.Writes,
            new SseEventHub(runtime, audio, new SseEventStreamOptions()),
            fixture.TimeProvider);

        var stale = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Audio,
            1,
            worker,
            ownership.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 1,
                State = System.Text.Json.JsonSerializer.SerializeToElement(new { playback_state = "playing" }),
            });
        Assert.False(stale.Accepted);
        Assert.Equal("stale_generation", stale.Reason);

        await using (var check = fixture.Database.CreateDbContext())
        {
            Assert.Equal(PlaybackState.Loading, (await check.BackgroundAudioStates.SingleAsync()).PlaybackState);
        }

        var current = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Audio,
            1,
            worker,
            ownership.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 2,
                State = System.Text.Json.JsonSerializer.SerializeToElement(new { playback_state = "playing" }),
            });
        Assert.True(current.Accepted);
        await using var final = fixture.Database.CreateDbContext();
        var stateAfter = await final.BackgroundAudioStates.SingleAsync();
        Assert.Equal(PlaybackState.Playing, stateAfter.PlaybackState);
        Assert.Equal(2, stateAfter.ObservedGeneration);
    }
}
