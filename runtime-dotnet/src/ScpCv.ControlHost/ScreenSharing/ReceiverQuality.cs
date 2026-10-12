// 接收质量仅有限数字；拒绝额外地址/SDP字段，不参与实际播出身份授予。
using System.Text.Json;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed record ReceiverQuality(int? Width, int? Height, double? Fps, double? BitrateMbps)
{
    public void Validate()
    {
        if (Width is < 1 or > 32768 || Height is < 1 or > 32768 ||
            Fps is { } fps && (!double.IsFinite(fps) || fps is < 0 or > 240) ||
            BitrateMbps is { } rate && (!double.IsFinite(rate) || rate is < 0 or > 1000)) throw Invalid();
    }

    public static ReceiverQuality? Read(JsonElement body)
    {
        if (!body.TryGetProperty("quality", out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(property =>
            property.Name is not ("width" or "height" or "fps" or "bitrate_mbps"))) throw Invalid();
        var width = Number(value, "width");
        var height = Number(value, "height");
        if (width != Math.Truncate(width ?? 0) && width is not null || height != Math.Truncate(height ?? 0) && height is not null) throw Invalid();
        // 数字范围在转为int之前检查，避免溢出成为500。
        if (width is < 1 or > 32768 || height is < 1 or > 32768) throw Invalid();
        var result = new ReceiverQuality(width is null ? null : (int)width, height is null ? null : (int)height,
            Number(value, "fps"), Number(value, "bitrate_mbps"));
        result.Validate();
        return result;
    }

    private static double? Number(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null) return null;
        if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number)) throw Invalid();
        return number;
    }
    private static ScreenShareException Invalid() => new("接收质量统计无效。", "share_quality_invalid");
}
