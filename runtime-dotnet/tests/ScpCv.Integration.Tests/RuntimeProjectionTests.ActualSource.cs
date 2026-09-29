// 当前归属的实际源清空及不完整、迟到报告的 SQLite 投影回归。
using System.Diagnostics;
using System.Text.Json;
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

public sealed partial class RuntimeProjectionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CurrentDisplayExplicitNullClearsPreviouslyObservedActualSource(int windowId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, windowId);
        await SeedActualSourceAsync(fixture, windowId);
        var publisher = CreateActualSourcePublisher(fixture);

        var accepted = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display,
            windowId,
            owner.WorkerInstanceId,
            owner.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 3,
                State = JsonSerializer.SerializeToElement(new
                {
                    source_id = (long?)null,
                    playback_state = "idle",
                    position_ms = 0,
                    duration_ms = 0,
                }),
            });

        Assert.True(accepted.Accepted);
        // 公共会话的意图源已是空；实际源同样必须由 Worker 的明确空值清除，
        // 不能仅以 HTTP/SSE 的 idle 或空意图源判定持久投影正确。
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId);
        Assert.Equal(PlaybackState.Idle, session.PlaybackState);
        Assert.Null(session.MediaSourceId);
        Assert.Equal(3, session.ObservedGeneration);
        Assert.Null(session.ActualSourceId);
        Assert.Equal(0, session.PositionMs);
        Assert.Equal(0, session.DurationMs);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"source_id\":\"39\"}")]
    [InlineData("{\"source_id\":true}")]
    [InlineData("{\"source_id\":{}}")]
    [InlineData("{\"source_id\":[]}")]
    [InlineData("{\"source_id\":39.5}")]
    [InlineData("{\"source_id\":9223372036854775808}")]
    [InlineData("{\"unrecognized_source_id\":null}")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task MissingOrMalformedActualSourceDoesNotImplyResourceClosed(string stateJson)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var accepted = await CreateActualSourcePublisher(fixture).ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            new StateReportDto { SourceGeneration = 3, State = JsonSerializer.Deserialize<JsonElement>(stateJson) });

        Assert.True(accepted.Accepted);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(48, session.ActualSourceId);
        Assert.Equal(3, session.ObservedGeneration);
        Assert.Equal(700, session.PositionMs);
        Assert.Equal(3000, session.DurationMs);
    }

    [Fact]
    public async Task ActualSourceNullDoesNotChangeNullProgressFallbackSemantics()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var accepted = await CreateActualSourcePublisher(fixture).ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 3,
                State = JsonSerializer.SerializeToElement(new
                {
                    source_id = (long?)null,
                    position_ms = (long?)null,
                    duration_ms = (long?)null,
                }),
            });

        Assert.True(accepted.Accepted);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Null(session.ActualSourceId);
        Assert.Equal(700, session.PositionMs);
        Assert.Equal(3000, session.DurationMs);
    }

    [Theory]
    [InlineData(39L)]
    [InlineData(long.MaxValue)]
    public async Task PositiveActualSourceIsProjectedWithoutTruncation(long sourceId)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var accepted = await CreateActualSourcePublisher(fixture).ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 3,
                State = JsonSerializer.SerializeToElement(new { source_id = sourceId }),
            });

        Assert.True(accepted.Accepted);
        await using var database = fixture.Database.CreateDbContext();
        Assert.Equal(sourceId, (await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).ActualSourceId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullActualSourceCannotBypassWorkerInstanceOrOwnerEpochFence(bool wrongInstance)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var accepted = await CreateActualSourcePublisher(fixture).ApplyStateReportAsync(
            CommandTargetKind.Display, 1,
            wrongInstance ? Guid.NewGuid() : owner.WorkerInstanceId,
            wrongInstance ? owner.OwnerEpoch : owner.OwnerEpoch - 1,
            NullActualSourceReport(3));

        Assert.False(accepted.Accepted);
        Assert.Equal("worker_fenced", accepted.Reason);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(48, session.ActualSourceId);
        Assert.Equal(2, session.ObservedGeneration);
        Assert.Equal(PlaybackState.Playing, session.PlaybackState);
    }

    [Theory]
    [InlineData(2L)]
    [InlineData(4L)]
    public async Task NullActualSourceCannotBypassExpectedSourceGeneration(long sourceGeneration)
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var accepted = await CreateActualSourcePublisher(fixture).ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            NullActualSourceReport(sourceGeneration));

        Assert.False(accepted.Accepted);
        Assert.Equal("stale_generation", accepted.Reason);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(48, session.ActualSourceId);
        Assert.Equal(2, session.ObservedGeneration);
        Assert.Equal(PlaybackState.Playing, session.PlaybackState);
    }

    [Fact]
    public async Task LateCloseFromPreviousGroupCannotClearReplacementWorkersSource()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var oldOwner = await RegisterCurrentDisplayAsync(fixture, 1);
        var oldGroup = await fixture.RuntimeAuthority.GetGroupAsync();
        Assert.True(await fixture.RuntimeAuthority.TryFaultSupervisorExitAsync(oldGroup.GroupEpoch, 901, Guid.NewGuid()));
        var newOwner = await RegisterCurrentDisplayAsync(fixture, 1);
        Assert.True(newOwner.OwnerEpoch > oldOwner.OwnerEpoch);
        await SeedActualSourceAsync(fixture, 1);
        var publisher = CreateActualSourcePublisher(fixture);
        var current = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, newOwner.WorkerInstanceId, newOwner.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 3,
                State = JsonSerializer.SerializeToElement(new { source_id = 39, playback_state = "playing" }),
            });
        Assert.True(current.Accepted);

        // 旧组即使报告了恰好相同的源 generation，也不能把新组资源清空。
        var late = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, oldOwner.WorkerInstanceId, oldOwner.OwnerEpoch,
            NullActualSourceReport(3));
        Assert.False(late.Accepted);
        Assert.Equal("worker_fenced", late.Reason);
        await using var database = fixture.Database.CreateDbContext();
        var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(39, session.ActualSourceId);
        Assert.Equal(PlaybackState.Playing, session.PlaybackState);
        Assert.Equal(3, session.ObservedGeneration);
    }

    [Fact]
    public async Task ReopenAfterConfirmedCloseCannotBeClearedByLateCloseGeneration()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var owner = await RegisterCurrentDisplayAsync(fixture, 1);
        await SeedActualSourceAsync(fixture, 1);
        var publisher = CreateActualSourcePublisher(fixture);
        Assert.True((await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            NullActualSourceReport(3))).Accepted);
        await using (var closed = fixture.Database.CreateDbContext())
            Assert.Null((await closed.PlaybackSessions.SingleAsync(item => item.WindowId == 1)).ActualSourceId);
        await fixture.Writes.ExecuteAsync(async (database, token) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1, token);
            session.DesiredGeneration = 4;
        });
        Assert.True((await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            new StateReportDto
            {
                SourceGeneration = 4,
                State = JsonSerializer.SerializeToElement(new { source_id = 39, playback_state = "playing" }),
            })).Accepted);

        var late = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, owner.WorkerInstanceId, owner.OwnerEpoch,
            NullActualSourceReport(3));
        Assert.False(late.Accepted);
        Assert.Equal("stale_generation", late.Reason);
        await using var final = fixture.Database.CreateDbContext();
        var reopened = await final.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
        Assert.Equal(39, reopened.ActualSourceId);
        Assert.Equal(4, reopened.ObservedGeneration);
        Assert.Equal(PlaybackState.Playing, reopened.PlaybackState);
    }

    private static StateReportDto NullActualSourceReport(long sourceGeneration) => new()
    {
        SourceGeneration = sourceGeneration,
        State = JsonSerializer.SerializeToElement(new { source_id = (long?)null, playback_state = "idle" }),
    };

    private static async Task<WorkerOwnership> RegisterCurrentDisplayAsync(ControlHostFixture fixture, int windowId)
    {
        var requestId = Guid.NewGuid();
        var group = await fixture.RuntimeAuthority.BeginStartAsync(requestId);
        await fixture.RuntimeAuthority.ArmAsync(requestId, group.GroupEpoch);
        using var process = Process.GetCurrentProcess();
        return await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Display,
            windowId,
            Guid.NewGuid(),
            process.Id,
            new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
            process.SessionId,
            group.GroupEpoch,
            "[\"wpf\",\"libvlc\"]",
            PreviousOwnerExitConfirmed: true));
    }

    private static Task SeedActualSourceAsync(ControlHostFixture fixture, int windowId) =>
        fixture.Writes.ExecuteAsync(async (database, cancellationToken) =>
        {
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId, cancellationToken);
            session.MediaSourceId = null;
            session.ActualSourceId = 48;
            session.DesiredGeneration = 3;
            session.ObservedGeneration = 2;
            session.PlaybackState = PlaybackState.Playing;
            session.PositionMs = 700;
            session.DurationMs = 3000;
        });

    private static RuntimeProjectionPublisher CreateActualSourcePublisher(ControlHostFixture fixture)
    {
        var commands = new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier());
        var runtime = new RuntimeStateService(fixture.Database, fixture.Writes, commands, fixture.TimeProvider);
        var audio = new BackgroundAudioService(fixture.Database, fixture.Writes, commands, fixture.TimeProvider);
        return new RuntimeProjectionPublisher(
            fixture.Writes,
            new SseEventHub(runtime, audio, new SseEventStreamOptions()),
            fixture.TimeProvider);
    }
}
