// 预案电视三态约束与旧字段默认保持；保存只验结构，激活再核实体协议。
using ScpCv.Contracts.Http;

namespace ScpCv.Infrastructure.DisplayRouting;

public static class ScenarioTelevisionPolicy
{
    /// <summary>:param values: 两台电视配置。:returns: 规范化值，缺省由DTO构造为keep；显式null不默认为成功。</summary>
    public static ScenarioTelevisionsDto Validate(ScenarioTelevisionsDto values)
    {
        if (values is null) throw new DisplayRoutingException("televisions必须为对象。");
        ValidateItem(values.TvLeft); ValidateItem(values.TvRight);
        return values;
    }

    /// <summary>:param values: 已验证配置。:returns: 仅显式设置目标，保持不进入硬件。</summary>
    public static IEnumerable<(string Target, VideoWallInputDto Input)> Changes(ScenarioTelevisionsDto values)
    {
        Validate(values);
        if (values.TvLeft.State == "set") yield return ("tv_left", values.TvLeft.Input!);
        if (values.TvRight.State == "set") yield return ("tv_right", values.TvRight.Input!);
    }

    /// <summary>:param item: 单台配置。:remarks: 不接受keep夹带输入，避免编辑器遗留值被误调用。</summary>
    private static void ValidateItem(ScenarioTelevisionDto item)
    {
        if (item is null || item.State is not ("keep" or "set") ||
            item.State == "keep" && item.Input is not null || item.State == "set" && item.Input is null)
            throw new DisplayRoutingException("电视配置必须为keep，或set并选择输入。");
        if (item.Input is not { } input) return;
        if (input.Kind is not ("window_1" or "window_2" or "window_3" or "window_4" or "laptop" or "ip_stream") ||
            input.Kind == "ip_stream" && string.IsNullOrEmpty(input.IpAddress) ||
            input.Kind != "ip_stream" && !string.IsNullOrEmpty(input.IpAddress))
            throw new DisplayRoutingException("电视输入结构无效。");
    }
}
