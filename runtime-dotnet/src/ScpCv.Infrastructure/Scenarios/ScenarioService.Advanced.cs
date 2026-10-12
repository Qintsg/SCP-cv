// 预设现场高级项先预检全部绑定，未配置零副作用；当前只确认屏幕音量。
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
                new[] { audio.ScreenVolume, audio.HandheldVolume, audio.HangingVolume }.Any(value => value is < 0 or > 100))
                throw new ScenarioServiceException("高级音量或来源无效。");
        }
        return JsonSerializer.Serialize(advanced);
    }

    private void PreflightAdvanced(ScenarioAdvancedDto advanced)
    {
        _ = ValidateAdvanced(advanced);
        if (advanced.CameraPositions.Any(position => position.Value is not null))
            throw new ScenarioServiceException("摄像机位置控制绑定尚未完成，预设未执行。", code: "scenario_advanced_unavailable");
        if (advanced.Audio is null) return;
        try { VenueDeviceService.Validate(advanced.Audio); }
        catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_unavailable"); }
        if (advanced.Audio.ScreenVolume is not null && venueDevices is null)
            throw new ScenarioServiceException("现场音量服务未配置，预设未执行。", code: "scenario_advanced_unavailable");
    }

    private async Task ApplyAdvancedAsync(ScenarioAdvancedDto advanced, CancellationToken cancellationToken)
    {
        if (advanced.Audio?.ScreenVolume is null) return;
        try
        {
            var result = await venueDevices!.ApplyAsync(advanced.Audio, cancellationToken).ConfigureAwait(false);
            if (result.Status is not ("confirmed" or "simulated" or "unchanged"))
                throw new ScenarioServiceException("预设画面/系统配置已执行，现场音量阶段未确认：" + result.Detail, code: "scenario_advanced_uncertain");
        }
        catch (VenueDeviceException exception)
        { throw new ScenarioServiceException("预设画面/系统配置已执行，现场音量失败：" + exception.Message, code: "scenario_advanced_uncertain"); }
    }
}
