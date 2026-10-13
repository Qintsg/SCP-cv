// 155读回与中控三路请求值分开；全项预检、串行发送、明确部分结果，不冒充DSP状态。
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Devices;

public sealed class VenueDeviceService(IDbContextFactory<ControlDbContext> contexts, WriteCoordinator writes,
    IVenueAudioTransport transport, CameraCatalog cameras, VenueJoinDispatcher? joins = null,
    VenueCameraController? cameraControl = null, IVenueSoftwareAudioBinding? softwareAudio = null) : IDisposable
{
    private readonly SemaphoreSlim _audioGate = new(1, 1);

    /// <summary>:returns: 只读能力与最后确认配置，不自动访问现场网络。</summary>
    public async Task<object> GetAsync(CancellationToken cancellationToken = default)
    {
        var ids = await cameras.SourceIdsAsync(cancellationToken).ConfigureAwait(false);
        var softwareReason = softwareAudio is null ? "软件声音输出未装配。" : await softwareAudio.UnavailableReasonAsync(cancellationToken).ConfigureAwait(false);
        return new
        {
            cameras = cameras.Definitions.Select(camera => new
            {
                id = camera.Id, name = camera.Name, path = "/Camera/" + camera.Name,
                source_id = ids.GetValueOrDefault(camera.Id), stream_configured = camera.Uri.Length > 0,
                movable = camera.Movable, control_available = camera.Movable && cameraControl is not null,
                position_count = camera.Movable ? VenueCameraController.PositionCount : 0,
                motion_intent = cameraControl?.Intent(camera.Id) ?? "idle",
                unavailable_reason = !camera.Movable ? "固定监控不支持转动。" : cameraControl is null ? "云台服务未装配。" : string.Empty,
            }),
            audio = await SettingsAsync(cancellationToken).ConfigureAwait(false),
            capabilities = new { screen_volume = true, computer_volume = joins is not null, handheld_volume = joins is not null,
                headset_volume = joins is not null, hanging_volume = joins is not null, source = transport.SupportsSourceSelection,
                source_ip81 = transport.SupportsSourceSelection, source_scp_cv = transport.SupportsSourceSelection && softwareReason.Length == 0,
                reason = !transport.SupportsSourceSelection ? "来源传输未装配。" : softwareReason.Length > 0 ? softwareReason : "两种来源可选择；发送结果不是实体路由读回。",
                headset_label = "悬挂（头戴）话筒", hanging_label = "悬挂（头戴）话筒" },
            server_time_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            simulation = transport.IsSimulation,
        };
    }

    /// <summary>:param id: 内置摄像机。:returns: 缺绑定时明确拒绝，且没有网络写出。</summary>
    public void ValidateCameraControl(string id)
    {
        var camera = cameras.Definitions.SingleOrDefault(camera => camera.Id == id)
            ?? throw new VenueDeviceException("摄像机不存在。");
        if (!camera.Movable || cameraControl is null)
            throw new VenueDeviceException(camera.Movable ? "云台服务未装配，操作未执行。" : "固定监控不支持运动或位置预设。");
    }

    /// <summary>:param settings: 仅提交要改动的字段。:returns: 全部字段预检，不允许先音量后发现来源失败。</summary>
    public static void Validate(VenueAudioSettings settings)
    {
        foreach (var volume in new[] { settings.ScreenVolume, settings.ComputerVolume, settings.HandheldVolume, settings.HeadsetVolume, settings.HangingVolume })
            if (volume is < 0 or > 100) throw new VenueDeviceException("音量必须是0–100的整数。");
        _ = VenueAudioAliases.Normalize(settings);
        if (settings.Source is not (null or "ip81" or "scp_cv")) throw new VenueDeviceException("音量来源只能是IP81或SCP-cv。");
    }

    public async Task<VenueAudioSettings> SettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var json = await db.RuntimeStates.Where(state => state.Id == RuntimeState.SingletonId)
            .Select(state => state.VenueAudioJson).SingleAsync(cancellationToken).ConfigureAwait(false);
        try { return VenueAudioAliases.Normalize(JsonSerializer.Deserialize<VenueAudioSettings>(json) ?? new()); }
        catch (JsonException) { throw new VenueDeviceException("现场音频持久配置无效，未按默认值覆盖设备。"); }
    }

    public async Task<VenueAudioResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var actual = await transport.ReadAsync(cancellationToken).ConfigureAwait(false);
            await RememberAsync(actual, cancellationToken).ConfigureAwait(false);
            return Result(actual, "屏幕音量已读回；未修改设备。");
        }
        finally { _audioGate.Release(); }
    }

    public async Task<VenueAudioResult> ApplyAsync(VenueAudioSettings settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);
        settings = VenueAudioAliases.Normalize(settings);
        ValidateAssembly(settings);
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 持久别名冲突也在首包前拒绝，不能先改155再发现无法保存请求值。
            _ = await SettingsAsync(cancellationToken).ConfigureAwait(false);
            await ValidateSourceAsync(settings, cancellationToken).ConfigureAwait(false);
            return await ApplyCoreAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally { _audioGate.Release(); }
    }

    /// <summary>:param settings: 预设/直接请求的完整增量。:returns: 所有项装配预检，首包之前完成。</summary>
    public async Task PreflightAudioAsync(VenueAudioSettings settings, CancellationToken token = default)
    {
        Validate(settings);
        ValidateAssembly(VenueAudioAliases.Normalize(settings));
        await ValidateSourceAsync(settings, token).ConfigureAwait(false);
        _ = await SettingsAsync(token).ConfigureAwait(false);
    }

    /// <summary>:returns: 切墙后重发当前音量恰好一次；首次未配置先读取实际值，不强行设为100。</summary>
    public async Task<VenueAudioResult> ReapplyAsync(CancellationToken cancellationToken = default)
    {
        await _audioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = await SettingsAsync(cancellationToken).ConfigureAwait(false);
            Validate(settings);
            ValidateAssembly(settings);
            await ValidateSourceAsync(settings, cancellationToken).ConfigureAwait(false);
            var value = settings.ScreenVolume ?? await transport.ReadAsync(cancellationToken).ConfigureAwait(false);
            return await ApplyCoreAsync(settings with { ScreenVolume = value }, cancellationToken).ConfigureAwait(false);
        }
        catch (VenueDeviceException exception) { return new() { Status = "uncertain", Detail = "画面指令已发送；音频阶段失败：" + exception.Message }; }
        finally { _audioGate.Release(); }
    }

    private void ValidateAssembly(VenueAudioSettings settings)
    {
        if (joins is null && (settings.ComputerVolume is not null || settings.HandheldVolume is not null || settings.HeadsetVolume is not null))
            throw new VenueDeviceException("中控滑杆服务未装配，未发送任何音量配置。");
    }

    /// <summary>:param settings: 直接请求/重发/预设。:param token: 查询寿命。:returns: 所有设备写出之前核来源传输和实时软件就绪。</summary>
    private async Task ValidateSourceAsync(VenueAudioSettings settings, CancellationToken token)
    {
        if (settings.Source is null) return;
        if (!transport.SupportsSourceSelection) throw new VenueDeviceException("来源传输未装配，未发送任何配置。");
        if (settings.Source != "scp_cv") return;
        var reason = softwareAudio is null ? "软件声音输出未装配。" : await softwareAudio.UnavailableReasonAsync(token).ConfigureAwait(false);
        if (reason.Length > 0) throw new VenueDeviceException(reason + "未发送任何配置。");
    }

    private async Task<VenueAudioResult> ApplyCoreAsync(VenueAudioSettings settings, CancellationToken token)
    {
        var sent = new List<string>();
        var completed = new VenueAudioSettings();
        VenueAudioResult result = new() { Status = "unchanged", Detail = "未配置项保持原状。" };
        string? current = null;
        try
        {
            if (settings.Source is { } source)
            {
                current = "source";
                await transport.SelectSourceAsync(source, token).ConfigureAwait(false);
                sent.Add(current);
                completed = completed with { Source = source };
                await RememberAsync(completed, token).ConfigureAwait(false);
                result = result with { Source = source };
            }
            if (settings.ScreenVolume is { } screen)
            {
                current = "screen_volume";
                result = await SendAndConfirmAsync(screen, sent, token).ConfigureAwait(false);
                if (result.Status is "mismatch" or "sent_unconfirmed") return result with { Source = completed.Source, SentFields = sent.ToArray() };
            }
            foreach (var item in new (string Field, int Join, int? Value)[]
            { ("headset_volume", 1, settings.HeadsetVolume), ("handheld_volume", 2, settings.HandheldVolume), ("computer_volume", 3, settings.ComputerVolume) })
            {
                if (item.Value is not { } value) continue;
                current = item.Field;
                await joins!.AnalogAsync(item.Join, value, token).ConfigureAwait(false);
                sent.Add(current);
                completed = item.Join switch
                { 1 => completed with { HeadsetVolume = value }, 2 => completed with { HandheldVolume = value }, _ => completed with { ComputerVolume = value } };
                // 每个已写出项单独记忆，后续失败不抹掉前面的请求值。请求值不是物理读回。
                await RememberAsync(completed, token).ConfigureAwait(false);
            }
            if (completed.HandheldVolume is not null || completed.HeadsetVolume is not null || completed.ComputerVolume is not null || completed.Source is not null)
                return result with { Status = transport.IsSimulation && (joins is null || joins.IsSimulation) ? "simulated" : "sent_unconfirmed", Source = completed.Source, SentFields = sent.ToArray(),
                    Detail = transport.IsSimulation && (joins is null || joins.IsSimulation) ? "仿真结果，未访问设备。" : "所选来源/滑杆配置已发送；实体路由与DSP实际增益未读回，155读回不能替代确认。" };
            return result with { SentFields = sent.ToArray() };
        }
        catch (Exception exception) when (exception is VenueDeviceException or DbUpdateException or OperationCanceledException)
        {
            if (sent.Count == 0 && exception is OperationCanceledException) throw;
            return result with { Status = "uncertain", Source = completed.Source, SentFields = sent.ToArray(), FailedField = current,
                Detail = $"音频阶段{current}失败或结果不确定；已写出：{string.Join("、", sent)}。未自动重试。{exception.Message}" };
        }
    }

    private async Task<VenueAudioResult> SendAndConfirmAsync(int value, List<string> sent, CancellationToken cancellationToken)
    {
        await transport.WriteAsync(value, cancellationToken).ConfigureAwait(false);
        sent.Add("screen_volume");
        int actual;
        try { actual = await transport.ReadAsync(cancellationToken).ConfigureAwait(false); }
        catch (VenueDeviceException exception) { return new() { Status = "sent_unconfirmed", Detail = "配置已发送但未确认：" + exception.Message }; }
        await RememberAsync(actual, cancellationToken).ConfigureAwait(false);
        if (actual != value) return new() { Status = "mismatch", ScreenVolume = actual, Detail = $"请求{value}，读回{actual}；未自动重试。" };
        return Result(actual, "屏幕音量配置已发送且读回一致。");
    }

    private Task RememberAsync(int actual, CancellationToken cancellationToken) => RememberAsync(new VenueAudioSettings { ScreenVolume = actual }, cancellationToken);

    private Task RememberAsync(VenueAudioSettings fields, CancellationToken cancellationToken) => writes.ExecuteAsync(async (db, token) =>
    {
        var state = await db.RuntimeStates.SingleAsync(token).ConfigureAwait(false);
        VenueAudioSettings previous;
        try { previous = VenueAudioAliases.Normalize(JsonSerializer.Deserialize<VenueAudioSettings>(state.VenueAudioJson) ?? new()); }
        catch (JsonException) { throw new VenueDeviceException("音频配置无效，未覆盖已有配置。"); }
        state.VenueAudioJson = JsonSerializer.Serialize(previous with
        { ScreenVolume = fields.ScreenVolume ?? previous.ScreenVolume, ComputerVolume = fields.ComputerVolume ?? previous.ComputerVolume,
            HeadsetVolume = fields.HeadsetVolume ?? previous.HeadsetVolume, HangingVolume = null, HandheldVolume = fields.HandheldVolume ?? previous.HandheldVolume,
            Source = fields.Source ?? previous.Source });
    }, cancellationToken);

    private VenueAudioResult Result(int value, string detail) => new()
    { Status = transport.IsSimulation ? "simulated" : "confirmed", ScreenVolume = value,
        Detail = transport.IsSimulation ? "仿真结果，未访问实体设备。" : detail };

    public void Dispose() { _audioGate.Dispose(); GC.SuppressFinalize(this); }
}
