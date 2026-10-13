// LibreOffice声音私有路径门禁：无配置零访问，已配置按窗口分离且缺文件不回退原安装。
using ScpCv.Runtime.Audio;

namespace ScpCv.Windows.Tests;

public sealed class PrivateOfficeAudioPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scp-cv-private-office-" + Guid.NewGuid().ToString("N"));

    public PrivateOfficeAudioPathsTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, PrivateAudioEnginePaths.MarkerName), PrivateAudioEnginePaths.Marker + "\n");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void UnconfiguredOutputDoesNotAccessAnEngineRoot(string? endpoint)
    {
        Assert.Null(PrivateAudioEnginePaths.Office(1, endpoint, "not-a-root"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void ConfiguredOutputRejectsRetiredOrUnknownWindow(int window)
    {
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Office(window, "explicit", _root));
    }

    [Fact]
    public void EachWindowHasItsOwnOfficeImagesAndUnoConfiguration()
    {
        var first = CreateProgram(1); var second = CreateProgram(2);
        Assert.Equal(first, PrivateAudioEnginePaths.Office(1, "explicit", _root));
        Assert.Equal(second, PrivateAudioEnginePaths.Office(2, "explicit", _root));
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("soffice.exe")]
    [InlineData("soffice.bin")]
    [InlineData("uno.ini")]
    public void MissingRequiredImageOrConfigurationCannotFallBack(string missing)
    {
        var folder = CreateProgram(1); File.Delete(Path.Combine(folder, missing));
        Assert.Throws<InvalidOperationException>(() => PrivateAudioEnginePaths.Office(1, "explicit", _root));
    }

    /// <summary>:param window: 自有测试窗口。:returns: 普通测试文件目录；不包含可执行程序。</summary>
    private string CreateProgram(int window)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_root, "window-" + window, "libreoffice", "program")).FullName;
        foreach (var name in new[] { "soffice.exe", "soffice.bin", "uno.ini" }) File.WriteAllText(Path.Combine(folder, name), "owned test image");
        return folder;
    }

    /// <summary>:returns: 只清理本测试创建的独立目录，不接触原安装或D4副本。</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);
}
