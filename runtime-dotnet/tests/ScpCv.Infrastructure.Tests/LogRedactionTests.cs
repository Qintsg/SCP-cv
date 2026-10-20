// 日志秘密字段及HTTP/媒体地址脱敏的独立回归，所有凭据均为虚构。
using ScpCv.Infrastructure.Diagnostics;

namespace ScpCv.Infrastructure.Tests;

public sealed class LogRedactionTests
{
    [Theory]
    [InlineData("rtsp")]
    [InlineData("rtmp")]
    [InlineData("srt")]
    [InlineData("https")]
    public void StreamCredentialsAndPassphraseAreRedacted(string scheme)
    {
        var value = Assert.IsType<string>(LogRedaction.Value("source_uri",
            $"{scheme}://fixture-name:fixture-secret@example.test/live?passphrase=query-secret&channel=5"));
        Assert.DoesNotContain("fixture-name", value, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-secret", value, StringComparison.Ordinal);
        Assert.DoesNotContain("query-secret", value, StringComparison.Ordinal);
        Assert.Contains("example.test/live", value, StringComparison.Ordinal);
        Assert.Contains("channel=5", value, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveKeysAreRedactedCaseInsensitively()
    {
        var redacted = LogRedaction.Properties(new Dictionary<string, object?>
        {
            ["username"] = "admin",
            ["PASSWORD"] = "plain-text",
            ["claim_token"] = "execution-secret",
            ["Authorization"] = "Bearer session-secret",
        });

        Assert.Equal("admin", redacted["username"]);
        Assert.Equal(LogRedaction.RedactedValue, redacted["PASSWORD"]);
        Assert.Equal(LogRedaction.RedactedValue, redacted["claim_token"]);
        Assert.Equal(LogRedaction.RedactedValue, redacted["Authorization"]);
    }

    [Fact]
    public void CredentialedUrlKeepsDestinationButRemovesSecrets()
    {
        var redacted = Assert.IsType<string>(LogRedaction.Value(
            "source_uri",
            "https://user:pass@example.test/media?id=7&access_token=secret&quality=high"));

        Assert.Contains("example.test/media", redacted, StringComparison.Ordinal);
        Assert.Contains("id=7", redacted, StringComparison.Ordinal);
        Assert.Contains("quality=high", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("user", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString(LogRedaction.RedactedValue), redacted, StringComparison.Ordinal);
    }
}
