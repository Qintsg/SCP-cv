// 原Worker释放凭据只有原分享和OPEN代次，不允许泄漏接收授权或媒体信息。
using System.Text.Json;
using ScpCv.Contracts.Ipc;

namespace ScpCv.Contracts.Tests;

public sealed class ScreenShareReleaseContractTests
{
    private static readonly string[] ReleaseFields = ["share_id", "source_generation"];
    /// <summary>:returns: IPC释放证据的两个字段精确往返，不引入公开凭证。</summary>
    [Fact]
    public void NativeReleaseEvidenceRoundTripsOnlyOriginalIdentity()
    {
        var evidence = new ScreenShareReleaseEvidenceDto { ShareId = Guid.NewGuid(), SourceGeneration = 37 };
        var json = JsonSerializer.SerializeToElement(evidence);
        Assert.Equal(ReleaseFields, json.EnumerateObject().Select(item => item.Name).Order().ToArray());
        Assert.Equal(evidence, json.Deserialize<ScreenShareReleaseEvidenceDto>());
    }
}
