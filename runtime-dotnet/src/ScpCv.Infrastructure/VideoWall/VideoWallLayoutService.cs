// 大屏映射草稿的保存、预览与已验证固定预设应用。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.Infrastructure.VideoWall;

public sealed class VideoWallLayoutService(
    IDbContextFactory<ControlDbContext> contextFactory,
    WriteCoordinator writes,
    RuntimeStateService runtime,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<VideoWallLayoutStateDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var state = await database.RuntimeStates.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var draft = DeserializeDraft(state.WallLayoutDraftJson);
        return new VideoWallLayoutStateDto
        {
            Draft = draft is null ? null : ToDto(draft),
            DraftRevision = state.WallLayoutDraftRevision,
            ActivePreset = state.BigScreenMode == BigScreenMode.Single ? "single" : "double",
        };
    }

    public async Task<VideoWallLayoutStateDto> SaveDraftAsync(
        WallLayout layout,
        CancellationToken cancellationToken = default)
    {
        var normalized = Validate(layout);
        await writes.ExecuteAsync(async (database, token) =>
        {
            var state = await database.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
            state.WallLayoutDraftJson = JsonSerializer.Serialize(normalized);
            state.WallLayoutDraftRevision = checked(state.WallLayoutDraftRevision + 1);
            state.UpdatedAt = _timeProvider.GetUtcNow();
        }, cancellationToken).ConfigureAwait(false);
        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VideoWallLayoutStateDto> ApplyDraftAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var state = await database.RuntimeStates.AsNoTracking().SingleAsync(cancellationToken).ConfigureAwait(false);
        var draft = DeserializeDraft(state.WallLayoutDraftJson)
            ?? throw new VideoWallLayoutException("尚未保存大屏布局。", "layout_missing");
        var preset = WallLayoutPolicy.CapturedPreset(draft);
        if (preset is null)
            throw new VideoWallLayoutException("此手动布局的实体控制帧尚待现场抓包；没有向墙面发送任何命令。", "protocol_unavailable");
        await runtime.SetRuntimeModeAsync(preset == BigScreenMode.Single ? "single" : "double", cancellationToken)
            .ConfigureAwait(false);
        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<VideoWallLayoutStateDto> ApplyPresetAsync(
        string preset,
        CancellationToken cancellationToken = default)
    {
        var mode = preset switch
        {
            "window_1_fullscreen" => "single",
            "window_1_left_window_2_right" => "double",
            _ => throw new VideoWallLayoutException($"未知的大屏预设：{preset}。", "invalid_preset"),
        };
        await runtime.SetRuntimeModeAsync(mode, cancellationToken).ConfigureAwait(false);
        return await GetAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WallLayout Validate(WallLayout layout)
    {
        try { return WallLayoutPolicy.Validate(layout); }
        catch (ArgumentException exception) { throw new VideoWallLayoutException(exception.Message, "layout_invalid"); }
    }

    private static WallLayout? DeserializeDraft(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try
        {
            var layout = JsonSerializer.Deserialize<WallLayout>(json)
                ?? throw new VideoWallLayoutException("保存的布局草稿为空。", "layout_invalid");
            return Validate(layout);
        }
        catch (JsonException exception)
        {
            throw new VideoWallLayoutException($"保存的布局草稿无法解析：{exception.Message}", "layout_invalid");
        }
    }

    private static VideoWallLayoutDto ToDto(WallLayout layout)
    {
        var preset = WallLayoutPolicy.CapturedPreset(layout) switch
        {
            BigScreenMode.Single => "window_1_fullscreen",
            BigScreenMode.Double => "window_1_left_window_2_right",
            _ => string.Empty,
        };
        return new VideoWallLayoutDto
        {
            Name = layout.Name,
            Preset = preset,
            Mappings = layout.Mappings.Select(mapping => new VideoWallMappingDto
            {
                Region = mapping.Region.ToString().ToLowerInvariant(),
                Input = new VideoWallInputDto
                {
                    Kind = mapping.Input.Kind switch
                    {
                        WallInputKind.Window1 => "window_1",
                        WallInputKind.Window2 => "window_2",
                        WallInputKind.Laptop => "laptop",
                        _ => "ip_stream",
                    },
                    IpAddress = mapping.Input.IpAddress,
                },
            }).ToArray(),
            CanApply = preset.Length > 0,
            UnavailableReason = preset.Length > 0 ? string.Empty : "对应控制帧尚待现场抓包，当前仅能保存和预览。",
        };
    }
}

public sealed class VideoWallLayoutException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
