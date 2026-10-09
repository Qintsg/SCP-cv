// 只读格式兼容矩阵：OOXML真实内容与非OOXML原路分开，不把元数据通过当原生文稿可播放。
using System.IO.Compression;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationPackageMetadataTests
{
    /// <summary>:param extension: 已接受的OOXML或改名扩展。:returns: 合法演示包仍解析同一页提示。</summary>
    [Theory]
    [InlineData(".pptm")]
    [InlineData(".ppsx")]
    [InlineData(".ppsm")]
    [InlineData(".potx")]
    [InlineData(".potm")]
    [InlineData(".ppt")]
    [InlineData(".odp")]
    public void RenamedValidOoxmlKeepsTheSameMetadata(string extension)
    {
        var path = Package(null, "slides/slide2.xml");
        var renamed = Path.ChangeExtension(path, extension);
        File.Copy(path, renamed);
        var metadata = new ScpCv.Runtime.Presentations.PresentationPackageMetadata(renamed);
        Assert.Equal(("title-2", "second"), Assert.Single(metadata.Unwrapped[1]));
    }

    /// <summary>:returns: 普通ODP的ZIP不是OOXML演示包，不据缺ppt/presentation.xml把它误拒绝。</summary>
    [Fact]
    public void NonOoxmlZipRetainsExistingOpenDocumentPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "scp-cv-odf-" + Guid.NewGuid().ToString("N") + ".odp");
        using (var file = File.Create(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.presentation");
        Assert.Empty(new ScpCv.Runtime.Presentations.PresentationPackageMetadata(path).Unwrapped);
    }

    /// <summary>:returns: 非ZIP旧二进制源不改变既有路由，不把OLE头夹具当实际旧PPT验收。</summary>
    [Fact]
    public void NonZipBinaryRetainsExistingLegacyPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "scp-cv-legacy-" + Guid.NewGuid().ToString("N") + ".ppt");
        File.WriteAllBytes(path, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
        Assert.Empty(new ScpCv.Runtime.Presentations.PresentationPackageMetadata(path).Unwrapped);
    }

    /// <summary>:returns: 带ZIP头却损坏的改名包不能转回旧格式以绕过预检。</summary>
    [Fact]
    public void BrokenZipHeaderIsRejectedEvenWithLegacyExtension()
    {
        var path = Path.Combine(Path.GetTempPath(), "scp-cv-broken-" + Guid.NewGuid().ToString("N") + ".ppt");
        File.WriteAllBytes(path, [0x50, 0x4B, 0x03, 0x04, 0x00]);
        Assert.Throws<InvalidDataException>(() => new ScpCv.Runtime.Presentations.PresentationPackageMetadata(path));
    }
}
