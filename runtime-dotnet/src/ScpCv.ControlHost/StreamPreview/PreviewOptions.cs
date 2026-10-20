// 预览独立预算：仅服务端依赖；配置不改变正式播放和系统网络策略。
using System.Text.Json.Serialization;

namespace ScpCv.ControlHost.StreamPreview;

public sealed class PreviewOptions
{
    public const string SectionName = "StreamPreview";
    public string ApiBase { get; set; } = "http://127.0.0.1:9997/";
    public int LocalRtspPort { get; set; } = 8554;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public int MaximumFeeds { get; set; } = 6;
    public int MaximumLeases { get; set; } = 128;
    public int EgressKilobitsPerSecond { get; set; } = 8000;
    public int LeaseSeconds { get; set; } = 30;
    public int IdleGraceSeconds { get; set; } = 3;
    public int NetworkUtilizationPercent { get; set; } = 40;
    public string[] AllowedWallInputAddresses { get; set; } = [];

    /// <summary>:returns: 仅接受回环管理API、有界资源和非空可执行文件配置，不访问设备。</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(ApiBase, UriKind.Absolute, out var uri) || uri.Scheme != "http" ||
            !uri.IsLoopback || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(FfmpegPath) || LocalRtspPort is < 1 or > 65535 ||
            MaximumFeeds is < 1 or > 16 || MaximumLeases is < 1 or > 256 ||
            EgressKilobitsPerSecond is < 128 or > 100000 || LeaseSeconds is < 10 or > 30 ||
            IdleGraceSeconds is < 0 or > 3 || NetworkUtilizationPercent is < 10 or > 80)
            throw new InvalidOperationException("直播预览配置无效：管理API必须回环且资源预算必须有界");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewRequest(
    [property: JsonPropertyName("source_id")] long? SourceId = null,
    [property: JsonPropertyName("input")] PreviewInput? Input = null,
    [property: JsonPropertyName("quality")] string Quality = "low");

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PreviewInput(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("ip_address")] string? IpAddress = null);

public sealed class PreviewException(string detail, string code = "preview_unavailable", int statusCode = 503) : Exception(detail)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
