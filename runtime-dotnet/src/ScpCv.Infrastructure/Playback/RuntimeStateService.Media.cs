// 打开媒体源时根据页图准备状态与实验开关选择真实播放适配器。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Presentations;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService
{
    /// <summary>:param windowId: 输出窗口。:param sourceId: 原件。:param autoplay: 默认播放。:param targetSlide: 初始页。:param cancellationToken: 寿命。:returns: 默认LibreOffice打开。</summary>
    public Task<IReadOnlyList<PlaybackSessionDto>> OpenSourceAsync(int windowId, long sourceId, bool autoplay, int targetSlide, CancellationToken cancellationToken = default) =>
        OpenSourceAsync(windowId, sourceId, autoplay, targetSlide, null, cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> OpenSourceAsync(
        int windowId,
        long sourceId,
        bool autoplay,
        int targetSlide,
        string? presentationMode,
        CancellationToken cancellationToken = default)
        => OpenSourceAsync(windowId, sourceId, autoplay, targetSlide, presentationMode, false, cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> OpenSourceAsync(int windowId, long sourceId, bool autoplay,
        int targetSlide, string? presentationMode, bool fitText, CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        if (sourceId <= 0)
            throw new PlaybackServiceException("source_id 必须大于 0", "invalid_source");

        return EnqueueDisplayAsync(windowId, "OPEN", "{}", async (database, session, command, token) =>
        {
            var source = await database.MediaSources.SingleOrDefaultAsync(item => item.Id == sourceId, token)
                .ConfigureAwait(false) ?? throw new PlaybackServiceException($"媒体源 id={sourceId} 不存在");
            if (source.SystemKey.StartsWith("camera:", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(source.Uri))
                throw new PlaybackServiceException("该内置摄像机尚未绑定播放地址。", "camera_unconfigured");
            PresentationSelection? presentation = null;
            if (source.SourceType == MediaSourceType.Presentation)
            {
                if (presentationMode is not (null or "" or "libreoffice" or "slide_images" or "pdf"))
                    throw new PlaybackServiceException("演示文稿模式无效", "invalid_presentation_mode");
                try { presentation = PresentationPlaybackSelector.Select(source, presentationMode, fitText); }
                catch (PresentationPreparationException exception)
                {
                    throw new PlaybackServiceException(exception.Message, "presentation_not_prepared");
                }
                if (presentation.Mode == PlaybackMode.LibreOffice && !File.Exists(source.Uri))
                    throw new PlaybackServiceException("演示文稿原件不存在", "presentation_not_prepared");
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
                uri = string.IsNullOrEmpty(presentation?.PlaybackUri) ? source.Uri : presentation.PlaybackUri,
                content_digest = source.ContentDigest,
                autoplay,
                loop = session.LoopEnabled,
                volume = session.Volume,
                muted = session.IsMuted,
                target_slide = targetSlide,
                presentation_mode = presentation?.Mode switch
                {
                    PlaybackMode.Pdf => "pdf",
                    PlaybackMode.SlideImages => "slide_images",
                    PlaybackMode.PowerPoint => "powerpoint",
                    PlaybackMode.LibreOffice => "libreoffice",
                    _ => string.Empty,
                },
                // 原始PDF没有文字适配导入步骤，不把未执行的请求选项报告为实际布局。
                fit_text = fitText && presentation is not null && !source.Uri.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase),
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
