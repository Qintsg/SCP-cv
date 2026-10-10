// 接收页音频回执的有限严格解析；请求值不是原生结果，轨道存在也不是可听声音证明。
using System.IO;
using System.Text.Json;

namespace ScpCv.PlayerWorker.ScreenSharing;

internal sealed record ScreenShareAudioReceipt(int Volume, bool Muted, bool HasAudio)
{
    /// <summary>:param json: ExecuteScriptAsync的真实JSON结果。:param volume: 原请求。:param muted: 原请求。:param activation: 激活回执包含帧数。:returns: 严格且与请求一致的实际属性；畸形/拒绝不推断成功。</summary>
    internal static ScreenShareAudioReceipt Parse(string json, int volume, bool muted, bool activation = false)
    {
        if (json.Length > 2048 || volume is < 0 or > 100) throw InvalidReceipt();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object) throw InvalidReceipt();
            var properties = value.EnumerateObject().ToArray();
            if (properties.Length != (activation ? 5 : 4) ||
                properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length ||
                properties.Any(item => item.Name is not ("accepted" or "volume" or "muted" or "has_audio") &&
                    (!activation || item.Name != "frames")) ||
                !value.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True ||
                !value.TryGetProperty("volume", out var actualVolume) || actualVolume.ValueKind != JsonValueKind.Number ||
                !actualVolume.TryGetDouble(out var level) || !double.IsFinite(level) || level is < 0 or > 100 ||
                Math.Abs(level - volume) > 1e-9 ||
                !Boolean(value, "muted", out var actualMuted) || !Boolean(value, "has_audio", out var hasAudio) ||
                actualMuted != (muted || !hasAudio)) throw InvalidReceipt();
            if (activation && (!value.TryGetProperty("frames", out var frames) || frames.ValueKind != JsonValueKind.Number ||
                !frames.TryGetInt64(out var count) || count < 0)) throw InvalidReceipt();
            // HTML的0.29*100可为28.999999999999996；只归一化已严格匹配原请求的浮点回读。
            return new(volume, actualMuted, hasAudio);
        }
        catch (JsonException) { throw InvalidReceipt(); }
    }

    /// <summary>:param value: 接收回执。:param key: 固定字段。:param result: 实际布尔。:returns: 数字/字符串均不能冒替原生布尔。</summary>
    private static bool Boolean(JsonElement value, string key, out bool result)
    {
        result = false;
        if (!value.TryGetProperty(key, out var field) || field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        result = field.GetBoolean(); return true;
    }

    /// <summary>:returns: 固定诊断不回显原生JSON、地址或私有授权。</summary>
    private static IOException InvalidReceipt() => new("屏幕共享接收端未提供有效音频回执。");
}
