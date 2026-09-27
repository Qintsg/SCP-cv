// 打开媒体源时根据页图准备状态与实验开关选择真实播放适配器。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    public Task<IReadOnlyList<PlaybackSessionDto>> OpenSourceAsync(
        int windowId,
        long sourceId,
        bool autoplay,
        int targetSlide,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        if (sourceId <= 0)
            throw new PlaybackServiceException("source_id 必须大于 0", "invalid_source");

        return EnqueueDisplayAsync(windowId, "OPEN", "{}", async (database, session, command, token) =>
        {
            var source = await database.MediaSources.SingleOrDefaultAsync(item => item.Id == sourceId, token)
                .ConfigureAwait(false) ?? throw new PlaybackServiceException($"媒体源 id={sourceId} 不存在");
            PresentationSelection? presentation = null;
            if (source.SourceType == MediaSourceType.Presentation)
            {
                var experimental = await database.RuntimeStates.AsNoTracking()
                    .Select(state => state.ExperimentalPowerPointEnabled)
                    .SingleAsync(token).ConfigureAwait(false);
                try { presentation = PresentationPlaybackSelector.Select(source, experimental); }
                catch (PresentationPreparationException exception)
                {
                    throw new PlaybackServiceException(exception.Message, "presentation_not_prepared");
                }
            }

            session.MediaSourceId = source.Id;
            session.PlaybackMode = PlaybackMode.None;
            session.PlaybackState = PlaybackState.Loading;
            session.ErrorMessage = string.Empty;
            session.CurrentSlide = targetSlide;
            session.PendingCommand = command.Command;
            session.DesiredGeneration = checked(session.DesiredGeneration + 1);
            session.CommandArgsJson = JsonSerializer.Serialize(new
            {
                source_id = source.Id,
                source_type = SourceTypeName(source.SourceType),
                uri = source.Uri,
                content_digest = source.ContentDigest,
                autoplay,
                loop = session.LoopEnabled,
                target_slide = targetSlide,
                presentation_mode = presentation?.Mode switch
                {
                    PlaybackMode.Pdf => "pdf",
                    PlaybackMode.SlideImages => "slide_images",
                    PlaybackMode.PowerPoint => "powerpoint",
                    _ => string.Empty,
                },
                slide_images_directory = presentation?.SlidesDirectory ?? string.Empty,
                slide_images_page_count = presentation?.PageCount ?? 0,
                fallback_uri = string.Empty,
                fallback_digest = string.Empty,
                fallback_fresh = false,
            });
            command.ArgsJson = session.CommandArgsJson;
            command.SourceGeneration = session.DesiredGeneration;
            command.SourceRevision = source.SourceRevision;
        }, cancellationToken);
    }
}
