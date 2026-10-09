using Microsoft.EntityFrameworkCore;
// 两块大屏的运行状态、媒体命令与硬件输出意图协调。
using System.Text.Json;
using System.Text.Json.Serialization;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Playback;

public sealed partial class RuntimeStateService(
    IDbContextFactory<ControlDbContext> contextFactory,
    WriteCoordinator writes,
    CommandCoordinator commands,
    TimeProvider? timeProvider = null,
    IDisplayTopologyProvider? displayTopology = null,
    ISystemAudioController? systemAudio = null,
    IVideoWallController? videoWall = null,
    BigScreenOutputOptions? bigScreenOutputs = null,
    VideoWallProtocolOptions? wallProtocol = null,
    VideoWallOperationGate? wallGate = null)
{
    private static readonly int[] Windows = [1, 2];
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly CommandCoordinator _commands = commands;
    private readonly IDisplayTopologyProvider _displayTopology = displayTopology ?? new SimulationDisplayTopologyProvider();
    private readonly ISystemAudioController _systemAudio = systemAudio ?? new SimulationSystemAudioController();
    private readonly IVideoWallController _videoWall = videoWall ?? new SimulationVideoWallController();
    private readonly BigScreenOutputOptions _bigScreenOutputs = bigScreenOutputs ?? new BigScreenOutputOptions();
    private readonly VideoWallProtocolOptions _wallProtocol = wallProtocol ?? new();
    private readonly VideoWallOperationGate _wallGate = wallGate ?? new();

    public async Task<IReadOnlyList<PlaybackSessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var sessions = await database.PlaybackSessions.AsNoTracking()
            .Include(session => session.MediaSource)
            .Where(session => session.WindowId <= WindowId.Maximum)
            .OrderBy(session => session.WindowId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return sessions.Select(session => ToSessionDto(session, _timeProvider.GetUtcNow())).ToArray();
    }

    public async Task<PlaybackSessionDto> GetSessionAsync(int windowId, CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var session = await database.PlaybackSessions.AsNoTracking()
            .Include(item => item.MediaSource)
            .SingleAsync(item => item.WindowId == windowId, cancellationToken).ConfigureAwait(false);
        return ToSessionDto(session, _timeProvider.GetUtcNow());
    }

    public async Task<RuntimeStateDto> GetRuntimeAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var runtime = await database.RuntimeStates.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        return ToRuntimeDto(runtime);
    }


    public Task<IReadOnlyList<PlaybackSessionDto>> ControlAsync(
        int windowId,
        string command,
        CancellationToken cancellationToken = default)
    {
        var normalized = command.Trim().ToLowerInvariant();
        if (normalized is not ("play" or "pause" or "stop" or "next_effect" or "previous_effect"))
        {
            throw new PlaybackServiceException($"无效的播放控制动作：{command}");
        }

        return EnqueueDisplayAsync(
            windowId,
            normalized.ToUpperInvariant(),
            "{}",
            async (database, session, command, token) =>
            {
                if (session.MediaSourceId is null)
                {
                    throw new PlaybackServiceException($"窗口 {windowId} 当前没有打开的媒体源");
                }

                session.PendingCommand = command.Command;
                command.SourceGeneration = session.DesiredGeneration;
                command.SourceRevision = await database.MediaSources
                    .Where(source => source.Id == session.MediaSourceId.Value)
                    .Select(source => source.SourceRevision)
                    .SingleAsync(token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PlaybackSessionDto>> NavigateAsync(
        int windowId,
        string action,
        int? targetIndex,
        long? positionMs,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        var normalized = action.Trim().ToLowerInvariant();
        if (normalized is not ("next" or "previous" or "first" or "last" or "goto" or "seek"))
        {
            throw new PlaybackServiceException($"无效的导航动作：{action}", "invalid_navigation");
        }
        if (normalized == "goto" && targetIndex is not > 0)
        {
            throw new PlaybackServiceException("goto 必须提供正整数 target_index。", "invalid_navigation");
        }

        var commandName = normalized switch
        {
            "next" => "NEXT",
            "previous" => "PREV",
            "first" or "last" or "goto" => "GOTO",
            "seek" => "SEEK",
            _ => normalized.ToUpperInvariant(),
        };
        return EnqueueDisplayAsync(windowId, commandName, "{}", async (database, session, command, token) =>
        {
            if ((normalized is "goto" or "first" or "last") && targetIndex is < 1)
            {
                throw new PlaybackServiceException("target_index 必须大于 0", "invalid_navigation");
            }

            if (normalized == "seek" && positionMs is < 0)
            {
                throw new PlaybackServiceException("position_ms 不能为负数", "invalid_navigation");
            }

            if (session.MediaSourceId is null)
            {
                throw new PlaybackServiceException($"窗口 {windowId} 当前没有打开的媒体源");
            }

            var effectiveTarget = normalized switch
            {
                "first" => 1,
                "last" when session.TotalSlides > 0 => session.TotalSlides,
                _ => targetIndex,
            };
            session.PendingCommand = command.Command;
            session.CommandArgsJson = JsonSerializer.Serialize(new { action = normalized, target_index = targetIndex, position_ms = positionMs });
            command.ArgsJson = session.CommandArgsJson;
            if (normalized is "first" or "last")
            {
                session.CommandArgsJson = JsonSerializer.Serialize(new { action = "goto", target_index = effectiveTarget, position_ms = positionMs });
                command.ArgsJson = session.CommandArgsJson;
            }
            command.SourceGeneration = session.DesiredGeneration;
            command.SourceRevision = await database.MediaSources
                .Where(source => source.Id == session.MediaSourceId.Value)
                .Select(source => source.SourceRevision)
                .SingleAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PlaybackSessionDto>> ControlPptMediaAsync(
        int windowId,
        string action,
        string? mediaId,
        int? mediaIndex,
        CancellationToken cancellationToken = default)
    {
        ValidateWindow(windowId);
        var normalized = action.Trim().ToLowerInvariant();
        if (normalized is not ("play" or "pause" or "stop" or "toggle"))
        {
            throw new PlaybackServiceException($"无效的 PPT 媒体动作：{action}", "invalid_media_action");
        }

        return EnqueueDisplayAsync(windowId, "PPT_MEDIA", "{}", async (database, session, command, token) =>
        {
            if (session.MediaSourceId is null)
            {
                throw new PlaybackServiceException($"窗口 {windowId} 当前没有打开的媒体源");
            }

            session.PendingCommand = "PPT_MEDIA";
            session.CommandArgsJson = JsonSerializer.Serialize(new { action = normalized, media_id = mediaId ?? string.Empty, media_index = mediaIndex });
            command.ArgsJson = session.CommandArgsJson;
            command.SourceGeneration = session.DesiredGeneration;
            command.SourceRevision = await database.MediaSources
                .Where(source => source.Id == session.MediaSourceId.Value)
                .Select(source => source.SourceRevision)
                .SingleAsync(token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PlaybackSessionDto>> ResetPowerPointAsync(CancellationToken cancellationToken = default) =>
        ResetPowerPointQueuedAsync(cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> CloseAsync(int windowId, CancellationToken cancellationToken = default) =>
        EnqueueDisplayAsync(
            windowId,
            "CLOSE",
            "{}",
            async (database, session, command, token) =>
            {
                var sourceId = session.MediaSourceId;
                var sourceRevision = sourceId is null
                    ? 0
                    : await database.MediaSources.Where(source => source.Id == sourceId.Value)
                        .Select(source => source.SourceRevision).SingleOrDefaultAsync(token).ConfigureAwait(false);
                session.MediaSourceId = null;
                session.PlaybackMode = PlaybackMode.None;
                session.PlaybackState = PlaybackState.Idle;
                session.ErrorMessage = string.Empty;
                session.PendingCommand = command.Command;
                session.CommandArgsJson = JsonSerializer.Serialize(new { source_id = sourceId });
                session.DesiredGeneration = checked(session.DesiredGeneration + 1);
                command.ArgsJson = session.CommandArgsJson;
                command.SourceGeneration = session.DesiredGeneration;
                command.SourceRevision = sourceRevision;
            },
            cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> SetLoopAsync(
        int windowId,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        EnqueueDisplayAsync(
            windowId,
            "SET_LOOP",
            JsonSerializer.Serialize(new { enabled }),
            (session, command) =>
            {
                session.LoopEnabled = enabled;
                session.PendingCommand = command.Command;
            },
            cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> SetVolumeAsync(
        int windowId,
        int volume,
        CancellationToken cancellationToken = default)
    {
        if (volume is < 0 or > 100)
        {
            throw new PlaybackServiceException("窗口音量必须在 0 到 100 之间");
        }

        return EnqueueDisplayAsync(
            windowId,
            "SET_VOLUME",
            JsonSerializer.Serialize(new { volume }),
            (session, command) =>
            {
                session.Volume = volume;
                session.PendingCommand = command.Command;
            },
            cancellationToken);
    }

    public Task<IReadOnlyList<PlaybackSessionDto>> SetMuteAsync(
        int windowId,
        bool muted,
        CancellationToken cancellationToken = default) =>
        EnqueueDisplayAsync(
            windowId,
            "SET_MUTE",
            JsonSerializer.Serialize(new { muted }),
            (session, command) =>
            {
                session.IsMuted = muted;
                session.PendingCommand = "SET_MUTE";
            },
            cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> ResetAllAsync(CancellationToken cancellationToken = default) =>
        ResetAllQueuedAsync(cancellationToken);

    public Task<IReadOnlyList<PlaybackSessionDto>> ShowIdsAsync(CancellationToken cancellationToken = default) =>
        ShowIdsQueuedAsync(cancellationToken);

    private Task<IReadOnlyList<PlaybackSessionDto>> EnqueueDisplayAsync(
        int windowId,
        string commandName,
        string initialArgsJson,
        Action<PlaybackSession, CommandRecord> mutation,
        CancellationToken cancellationToken) =>
        EnqueueDisplayAsync(
            windowId,
            commandName,
            initialArgsJson,
            (database, session, command, _) =>
            {
                mutation(session, command);
                return Task.CompletedTask;
            },
            cancellationToken);

    internal async Task<IReadOnlyList<PlaybackSessionDto>> EnqueueDisplayAsync(
        int windowId,
        string commandName,
        string initialArgsJson,
        Func<ControlDbContext, PlaybackSession, CommandRecord, CancellationToken, Task> mutation,
        CancellationToken cancellationToken)
    {
        ValidateWindow(windowId);
        var submitted = await _commands.EnqueueAsync(
            new EnqueueCommand(CommandTargetKind.Display, windowId, commandName, initialArgsJson, 0, 0),
            async (database, command, token) =>
            {
                var session = await database.PlaybackSessions.SingleAsync(item => item.WindowId == windowId, token).ConfigureAwait(false);
                if (session.PlaybackMode == PlaybackMode.LibreOffice && commandName is "PLAY" or "PAUSE" or "STOP" or "SEEK" or "SET_VOLUME" or "SET_MUTE" or "SET_LOOP" or "PPT_MEDIA")
                    throw new PlaybackServiceException("LibreOffice当前未支持统一媒体控制", "unsupported_media_control");
                await mutation(database, session, command, token).ConfigureAwait(false);
                var earlierPending = await database.CommandRecords
                    .Where(item => item.TargetKind == CommandTargetKind.Display && item.TargetId == windowId && item.Status == CommandStatus.Pending)
                    .OrderBy(item => item.TargetSequence)
                    .FirstOrDefaultAsync(token).ConfigureAwait(false);
                if (earlierPending is not null && earlierPending.TargetSequence < command.TargetSequence)
                {
                    session.PendingCommand = earlierPending.Command;
                    session.CommandArgsJson = earlierPending.ArgsJson;
                }
                else
                {
                    session.PendingCommand = command.Command;
                    session.CommandArgsJson = command.ArgsJson;
                }
                session.LastUpdatedAt = NextTimestamp(session.LastUpdatedAt);
            },
            cancellationToken).ConfigureAwait(false);
        var sessions = await GetSessionsAsync(cancellationToken).ConfigureAwait(false);
        return sessions.Select(session => session.WindowId == windowId
            ? session with { SubmittedCommandId = submitted.CommandId } : session).ToArray();
    }

    public static PlaybackSessionDto ToSessionDto(PlaybackSession session, DateTimeOffset now)
    {
        var source = session.MediaSource;
        var sourceType = source is null ? string.Empty : MediaSourceService.SourceTypeName(source.SourceType);
        return new PlaybackSessionDto
        {
            WindowId = session.WindowId,
            SessionId = session.Id,
            SourceId = session.MediaSourceId,
            SourceName = source?.Name ?? "无",
            SourceType = sourceType,
            SourceTypeLabel = source is null ? "无" : SourceTypeLabel(source.SourceType),
            SourceUri = source?.Uri ?? string.Empty,
            PlaybackMode = sourceType == "ppt" ? PlaybackModeName(session.PlaybackMode) : string.Empty,
            PlaybackState = EnumName(session.PlaybackState),
            PlaybackStateLabel = PlaybackStateLabel(session.PlaybackState),
            ErrorMessage = session.ErrorMessage,
            DisplayMode = "single",
            DisplayModeLabel = "单屏",
            TargetDisplayLabel = session.TargetDisplayLabel.Length == 0 ? "未选择" : session.TargetDisplayLabel,
            CurrentSlide = session.CurrentSlide,
            TotalSlides = session.TotalSlides,
            PositionMs = session.ActualAdapterKind == "libreoffice" ? null : session.PositionMs,
            DurationMs = session.ActualAdapterKind == "libreoffice" ? null : session.DurationMs,
            ActualSourceId = session.ActualSourceId,
            ActualAdapterKind = session.ActualAdapterKind,
            ActualFitText = session.ActualFitText,
            DesiredGeneration = session.DesiredGeneration,
            ObservedGeneration = session.ObservedGeneration,
            CleanupPending = session.CleanupPending,
            PendingCommand = session.PendingCommand,
            PlayerOnline = session.PlayerLastSeenAt is not null && now - session.PlayerLastSeenAt.Value <= TimeSpan.FromSeconds(5),
            PlayerLastSeenAt = session.PlayerLastSeenAt?.ToString("O") ?? string.Empty,
            LastUpdatedAt = session.LastUpdatedAt.ToString("O"),
            Volume = session.Volume,
            IsMuted = session.IsMuted,
            LoopEnabled = session.LoopEnabled,
        };
    }

    private static RuntimeStateDto ToRuntimeDto(RuntimeState runtime) => new()
    {
        BigScreenMode = EnumName(runtime.BigScreenMode),
        VolumeLevel = runtime.VolumeLevel,
        MutedWindows = VideoWallStateProjection.MutedWindows(runtime),
    };


    private static void ValidateWindow(int windowId)
    {
        if (!Windows.Contains(windowId))
        {
            throw new PlaybackServiceException($"无效的窗口编号：{windowId}，有效范围 1-2", "invalid_window");
        }
    }

    private static string EnumName<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    private static string PlaybackModeName(PlaybackMode mode) => mode switch
    {
        PlaybackMode.PowerPoint => "powerpoint",
        PlaybackMode.LibreOffice => "libreoffice",
        PlaybackMode.Pdf => "pdf",
        PlaybackMode.SlideImages => "slide_images",
        _ => string.Empty,
    };

    private static string SourceTypeName(MediaSourceType type) => type switch
    {
        MediaSourceType.Presentation => "ppt",
        MediaSourceType.CustomStream => "custom_stream",
        MediaSourceType.RtspStream => "rtsp",
        MediaSourceType.SrtStream => "srt",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string PlaybackStateLabel(PlaybackState state) => state switch
    {
        PlaybackState.Idle => "待机",
        PlaybackState.Loading => "加载中",
        PlaybackState.Playing => "播放中",
        PlaybackState.Paused => "已暂停",
        PlaybackState.Stopped => "已停止",
        PlaybackState.Error => "错误",
        _ => string.Empty,
    };

    private static string SourceTypeLabel(MediaSourceType type) => type switch
    {
        MediaSourceType.Presentation => "演示文稿",
        MediaSourceType.Video => "视频",
        MediaSourceType.Audio => "音频",
        MediaSourceType.Image => "图片",
        MediaSourceType.Web => "网页",
        MediaSourceType.CustomStream => "自定义流",
        MediaSourceType.RtspStream => "RTSP 流",
        MediaSourceType.SrtStream => "SRT 流",
        _ => string.Empty,
    };
}

public sealed record SystemVolumeDto
{
    [JsonPropertyName("level")]
    public int Level { get; init; }

    [JsonPropertyName("muted")]
    public bool Muted { get; init; }

    [JsonPropertyName("system_synced")]
    public bool SystemSynced { get; init; }

    [JsonPropertyName("backend")]
    public string Backend { get; init; } = string.Empty;
}

public sealed class PlaybackServiceException(string message, string code = "playback_error") : Exception(message)
{
    public string Code { get; } = code;
}
