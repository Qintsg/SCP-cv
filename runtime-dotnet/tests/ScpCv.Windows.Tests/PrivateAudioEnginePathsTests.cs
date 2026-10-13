// 专用引擎目录的独立边界回归；不启动原生引擎、不更改环境变量或系统声音策略。
using ScpCv.Runtime.Audio;

namespace ScpCv.Windows.Tests;

public sealed class PrivateAudioEnginePathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-private-engines-" + Guid.NewGuid().ToString("N"));

    public PrivateAudioEnginePathsTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("relative-engines")]
    [InlineData("E:relative-engines")]
    [InlineData("\\relative-engines")]
    [InlineData("\\\\server\\share\\engines")]
    [InlineData("//server/share/engines")]
    public void NonAbsoluteOrRemoteRootIsRejectedBeforeFileSystemAccess(string? configured)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Root(configured));
        Assert.Contains("根目录", error.Message);
    }

    [Fact]
    public void MissingMarkerCannotAuthorizeAnInstalledEngineDirectory()
    {
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Root(_root));
    }

    [Theory]
    [InlineData("foreign engine")]
    [InlineData("SCP-cv private audio engines v2")]
    public void ForeignMarkerIsRejected(string marker)
    {
        File.WriteAllText(Path.Combine(_root, PrivateAudioEnginePaths.MarkerName), marker);
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Root(_root));
    }

    [Fact]
    public void OversizedMarkerIsRejectedWithoutLoadingItsContent()
    {
        File.WriteAllText(Path.Combine(_root, PrivateAudioEnginePaths.MarkerName), PrivateAudioEnginePaths.Marker + "\n" + new string('x', 4096));
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Root(_root));
    }

    [Fact]
    public void PlainMarkedLocalRootAndContainedImageAreAccepted()
    {
        File.WriteAllText(Path.Combine(_root, PrivateAudioEnginePaths.MarkerName), PrivateAudioEnginePaths.Marker + "\n");
        var folder = Directory.CreateDirectory(Path.Combine(_root, "window-1", "webview")).FullName;
        var image = Path.Combine(folder, "msedgewebview2.exe"); File.WriteAllText(image, "owned test image");
        Assert.Equal(_root, PrivateAudioEnginePaths.Root(_root));
        PrivateAudioEnginePaths.ValidateImage(image, _root);
    }

    [Fact]
    public void SiblingPrefixAndMissingImagesDoNotAuthorizePolicyChanges()
    {
        var image = Path.Combine(_root, "msedgewebview2.exe"); File.WriteAllText(image, "owned test image");
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.ValidateImage(image, _root + "-other"));
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.ValidateImage(Path.Combine(_root, "missing.exe"), _root));
    }

    /// <summary>:returns: 只删除本测试创建的临时目录；不接触部署副本。</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
