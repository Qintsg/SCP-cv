// 预设高级项全部先预检；摄像机调用及音频只报告实际发送阶段，不假称物理确认。
using System.Text.Json;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.Infrastructure.Scenarios;

public sealed partial class ScenarioService
{
    private static ScenarioAdvancedDto ReadAdvanced(string json)
    {
        try { return JsonSerializer.Deserialize<ScenarioAdvancedDto>(json) ?? new(); }
        catch (JsonException) { throw new ScenarioServiceException("高级预设配置无效，未调用默认设备位置。", code: "scenario_advanced_invalid"); }
    }

    /// <summary>:param advanced: 要保存的值。:returns: 仅结构合法，保存不等于实体可执行。</summary>
    private static string ValidateAdvanced(ScenarioAdvancedDto advanced)
    {
        if (advanced.CameraPositions is null) throw new ScenarioServiceException("camera_positions必须为对象。");
        foreach (var position in advanced.CameraPositions)
        {
            if (position.Key is not ("rear" or "center1" or "center2" or "left" or "right") || position.Value is < 1 or > 255)
                throw new ScenarioServiceException("摄像机位置必须为五个内置可移动摄像机及1–255，未配置用null。");
        }
        if (advanced.Audio is { } audio)
        {
            if (audio.Source is not (null or "ip81" or "scp_cv") ||
                new[] { audio.ScreenVolume, audio.ComputerVolume, audio.HandheldVolume, audio.HeadsetVolume, audio.HangingVolume }.Any(value => value is < 0 or > 100))
                throw new ScenarioServiceException("高级音量或来源无效。");
            try { advanced = advanced with { Audio = VenueAudioAliases.Normalize(audio) }; }
            catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_invalid"); }
        }
        return JsonSerializer.Serialize(advanced);
    }

    private async Task PreflightAdvancedAsync(ScenarioAdvancedDto advanced, CancellationToken token)
    {
        _ = ValidateAdvanced(advanced);
        foreach (var position in advanced.CameraPositions.Where(pair => pair.Value is not null))
        {
            if (venueCameras is null) throw new ScenarioServiceException("云台服务未装配，预设未执行。", code: "scenario_advanced_unavailable");
            try { VenueCameraController.ValidatePosition(position.Key, position.Value!.Value, "call"); }
            catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_unavailable"); }
        }
        if (advanced.Audio is null) return;
        try { VenueDeviceService.Validate(advanced.Audio); }
        catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_unavailable"); }
        if (HasAudio(advanced.Audio) && venueDevices is null)
            throw new ScenarioServiceException("现场音量服务未配置，预设未执行。", code: "scenario_advanced_unavailable");
        try { if (venueDevices is not null) await venueDevices.PreflightAudioAsync(advanced.Audio, token).ConfigureAwait(false); }
        catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_unavailable"); }
    }

    private async Task ApplyAdvancedAsync(ScenarioAdvancedDto advanced, CancellationToken cancellationToken)
    {
        var unconfirmed = new List<string>();
        try
        {
            foreach (var position in advanced.CameraPositions.Where(pair => pair.Value is not null))
            {
                var camera = await venueCameras!.PositionAsync(position.Key, position.Value!.Value, "call", cancellationToken).ConfigureAwait(false);
                if (camera.Status != "simulated") unconfirmed.Add(camera.Detail);
            }
            if (advanced.Audio is { } audio && HasAudio(audio))
            {
                var result = await venueDevices!.ApplyAsync(audio, cancellationToken).ConfigureAwait(false);
                if (result.Status is not ("confirmed" or "simulated" or "unchanged")) unconfirmed.Add(result.Detail);
            }
            if (unconfirmed.Count > 0)
                throw new ScenarioServiceException("预设画面/系统配置已执行，现场阶段未确认：" + string.Join("；", unconfirmed), code: "scenario_advanced_uncertain");
        }
        catch (VenueDeviceException exception)
        { throw new ScenarioServiceException("预设画面/系统配置已执行，现场音量失败：" + exception.Message, code: "scenario_advanced_uncertain"); }
    }

    private static bool HasAudio(VenueAudioSettings audio) => audio.ScreenVolume is not null || audio.ComputerVolume is not null ||
        audio.HandheldVolume is not null || audio.HeadsetVolume is not null || audio.HangingVolume is not null || audio.Source is not null;
}
