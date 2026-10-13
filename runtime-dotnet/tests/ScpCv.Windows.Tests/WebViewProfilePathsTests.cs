// 网页/共享资料目录的纯值隔离回归；不启动WebView、不修改环境或用户资料。
using ScpCv.PlayerWorker.Audio;

namespace ScpCv.Windows.Tests;

public sealed class WebViewProfilePathsTests
{
    [Fact]
    public void DefaultKeepsExistingWindowAndSharingLocations()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SCP-cv", "WebView2");
        Assert.Equal(Path.Combine(root, "player-1"), WebViewProfilePaths.For(1, false, null));
        Assert.Equal(Path.Combine(root, "screen-share", "player-2"), WebViewProfilePaths.For(2, true, null));
    }

    [Fact]
    public void ExplicitInstallationRootKeepsBothWindowsAndSharingSeparateWithoutCreatingFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-profiles-" + Guid.NewGuid().ToString("N"));
        var paths = new[] { WebViewProfilePaths.For(1, false, root), WebViewProfilePaths.For(2, false, root),
            WebViewProfilePaths.For(1, true, root), WebViewProfilePaths.For(2, true, root) };
        Assert.Equal(4, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(paths, path => Assert.StartsWith(root + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase));
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative")]
    [InlineData("C:relative")]
    [InlineData("\\relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("https://example.test/profiles")]
    [InlineData("C:\\")]
    public void InvalidExplicitRootsNeverSilentlyUseExistingProfiles(string root) =>
        Assert.Throws<InvalidOperationException>(() => WebViewProfilePaths.For(1, false, root));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void RemovedWindowsCannotCreateProfiles(int window) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => WebViewProfilePaths.For(window, false, null));

    [Fact]
    public void ExplicitRootRejectsSdkOverrideToAnotherProfileBeforeContentIsLoaded()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-profiles-" + Guid.NewGuid().ToString("N"));
        var expected = WebViewProfilePaths.For(1, false, root);
        Assert.Throws<InvalidOperationException>(() => WebViewProfilePaths.Verify(root, expected, Path.Combine(root, "other")));
        WebViewProfilePaths.Verify(root, expected, expected.ToUpperInvariant() + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void UnconfiguredRootKeepsExistingTrustedSdkOverrideBehavior() =>
        WebViewProfilePaths.Verify(null, Path.Combine(Path.GetTempPath(), "default"), Path.Combine(Path.GetTempPath(), "override"));
}
