// 软件声音输出的纯选择策略；只接受明确Windows Render端点，不改系统默认输出。
using System;
using System.Collections.Generic;
using System.Linq;
namespace ScpCv.Contracts.Runtime;

public static class AudioOutputBinding
{
    public const string EnvironmentVariable = "SCP_CV_AUDIO_OUTPUT_DEVICE_ID";
    private const string Prefix = "{0.0.0.00000000}.";

    /// <summary>:param configured: 可信启动配置。:param available: 当前枚举输出ID。:returns: 唯一明确端点或未配置；缺失不回退。</summary>
    public static string? Resolve(string? configured, IEnumerable<string> available)
    {
        if (configured is null || string.IsNullOrWhiteSpace(configured)) return null;
        var id = configured.Trim();
        if (!id.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(id.Substring(Prefix.Length), "B", out var endpoint) || endpoint == Guid.Empty)
            throw new InvalidOperationException("软件音频输出必须是完整的Windows输出端点ID，不能使用名称或默认设备。");
        var matches = available.Where(candidate => Matches(id, candidate)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("绑定的软件音频输出不可用或不唯一，未回退到系统默认设备。");
        return matches[0];
    }

    /// <summary>:param expected: 明确绑定。:param actual: 原生当前端点。:returns: 完整ID相等，不把默认或未知当绑定成功。</summary>
    public static bool Matches(string expected, string? actual) =>
        !string.IsNullOrEmpty(actual) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}
