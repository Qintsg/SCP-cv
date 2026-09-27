// 页图播放器的真实状态上报必须保留到对外会话，不得被枚举解析丢弃。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.Tests;

public sealed class SlideImageStateProjectionTests
{
    [Fact]
    public async Task ReportedSlideImagesModeAppearsInPlaybackSession()
    {
        using var factory = new ControlHostApplicationFactory();
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        var publisher = factory.Services.GetRequiredService<RuntimeProjectionPublisher>();
        var runtime = factory.Services.GetRequiredService<RuntimeStateService>();
        var workerId = Guid.NewGuid();
        long sourceId;
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var source = new MediaSource
            {
                SourceType = MediaSourceType.Presentation,
                Name = "已准备文稿",
                Uri = "deck.pptx",
                IsAvailable = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            database.MediaSources.Add(source);
            await database.SaveChangesAsync();
            sourceId = source.Id;
            var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == 1);
            session.MediaSourceId = source.Id;
            session.DesiredGeneration = 7;
            database.WorkerOwnerships.Add(new WorkerOwnership
            {
                TargetKind = CommandTargetKind.Display,
                TargetId = 1,
                WorkerInstanceId = workerId,
                OwnerEpoch = 2,
                ProcessId = 12345,
                ProcessStartTime = DateTimeOffset.UtcNow,
                LogonSessionId = 1,
                LastTransportHeartbeat = DateTimeOffset.UtcNow,
                Status = WorkerOwnershipState.Online,
            });
            await database.SaveChangesAsync();
        }
        var state = JsonSerializer.SerializeToElement(new
        {
            source_generation = 7,
            source_id = sourceId,
            playback_state = "playing",
            playback_mode = "slide_images",
            adapter_kind = "slide_images",
            current_slide = 1,
            total_slides = 9,
        });

        var accepted = await publisher.ApplyStateReportAsync(
            CommandTargetKind.Display, 1, workerId, 2,
            new StateReportDto { SourceGeneration = 7, State = state });
        var sessionSnapshot = await runtime.GetSessionAsync(1);

        Assert.True(accepted.Accepted);
        Assert.Equal("slide_images", sessionSnapshot.PlaybackMode);
    }
}
