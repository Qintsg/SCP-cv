// 真实只读ZIP/XML包门禁回归，不加载UNO、网络或Office；原件摘要必须保持。
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ScpCv.Runtime.Presentations;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationPackageMetadataTests
{
    /// <summary>:returns: 声明顺序与无换行语义仍保持，读取不写回原件。</summary>
    [Fact]
    public void MetadataKeepsDeclaredOrderAndDoesNotModifyOriginal()
    {
        var path = Package(null, "slides/slide2.xml");
        var original = SHA256.HashData(File.ReadAllBytes(path));
        var metadata = new PresentationPackageMetadata(path);
        Assert.Equal(("title-2", "second"), Assert.Single(metadata.Unwrapped[1]));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(path)));
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
    }

    /// <summary>:returns: 非Internal的明确模式必须在任何引擎动作前拒绝，不能只识别External字面。</summary>
    [Fact]
    public void NonInternalModeCannotBypassExternalRelationshipGate()
    {
        var path = Package("true", "slides/slide2.xml");
        Assert.Throws<InvalidDataException>(() => new PresentationPackageMetadata(path));
    }

    /// <summary>:param target: 原门禁会映射为本地页的非法绝对引用。:returns: 必须拒绝，不能改变原引用语义。</summary>
    [Theory]
    [InlineData("https://package.invalid/ppt/slides/slide2.xml")]
    [InlineData("ftp://package.invalid/ppt/slides/slide2.xml")]
    [InlineData("http://package.invalid/ppt/slides/slide2.xml?remote=1")]
    public void AbsoluteInternalSlideTargetCannotBeReinterpretedAsLocalPage(string target) =>
        Assert.Throws<InvalidDataException>(() => new PresentationPackageMetadata(Package("Internal", target)));

    /// <summary>:param mode: 合法省略/明确模式。:returns: 相对内部页正常接受。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("Internal")]
    public void ValidInternalRelationshipStillWorks(string? mode) =>
        Assert.Single(new PresentationPackageMetadata(Package(mode, "slides/slide2.xml")).Unwrapped);

    /// <summary>:param mode: 非Internal模式。:returns: 非页关系同样经过拒绝门禁。</summary>
    [Theory]
    [InlineData("External")]
    [InlineData("external")]
    [InlineData("false")]
    [InlineData("")]
    public void OtherRelationshipCannotBypassModeGate(string mode)
    {
        var path = Package(null, "slides/slide2.xml");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
            Add(archive, "ppt/slides/_rels/slide2.xml.rels", "<Relationships><Relationship Id=\"bad\" Target=\"../media/a.mp4\" TargetMode=\"" + mode + "\"/></Relationships>");
        Assert.Throws<InvalidDataException>(() => new PresentationPackageMetadata(path));
    }

    /// <summary>:param target: 越界/非法页引用。:returns: 拒绝，不解包或访问网络。</summary>
    [Theory]
    [InlineData("//package.invalid/ppt/slides/slide2.xml")]
    [InlineData("../../outside.xml")]
    [InlineData("slides/slide2.xml?remote=1")]
    [InlineData("slides/slide2.xml#other")]
    public void InvalidRelativePageReferenceIsRejected(string target) =>
        Assert.Throws<InvalidDataException>(() => new PresentationPackageMetadata(Package(null, target)));

    /// <summary>:returns: XML的外部实体/DTD在解析时拒绝，不交给引擎。</summary>
    [Fact]
    public void DtdIsRejectedWithoutResolvingExternalEntities()
    {
        var path = Package(null, "slides/slide2.xml");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            archive.GetEntry("ppt/slides/slide2.xml")!.Delete();
            Add(archive, "ppt/slides/slide2.xml", "<!DOCTYPE sld SYSTEM \"https://example.invalid/never-fetch\"><sld/>");
        }
        Assert.Throws<System.Xml.XmlException>(() => new PresentationPackageMetadata(path));
    }

    /// <summary>:param mode: 关系模式。:param target: 页引用。:returns: 本次自有最小ZIP。</summary>
    private static string Package(string? mode, string target)
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "sample.pptx");
        using var file = File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace package = "http://schemas.openxmlformats.org/package/2006/relationships";
        Add(archive, "ppt/presentation.xml", new XElement(p + "presentation", new XElement(p + "sldIdLst",
            new XElement(p + "sldId", new XAttribute(r + "id", "r1")))).ToString(SaveOptions.DisableFormatting));
        var relation = new XElement(package + "Relationship", new XAttribute("Id", "r1"), new XAttribute("Target", target));
        if (mode is not null) relation.Add(new XAttribute("TargetMode", mode));
        Add(archive, "ppt/_rels/presentation.xml.rels", new XElement(package + "Relationships", relation).ToString(SaveOptions.DisableFormatting));
        Add(archive, "ppt/slides/slide2.xml", """
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:sp><p:cNvPr name="title-2"/><p:txBody><a:bodyPr wrap="none"><a:spAutoFit/></a:bodyPr><a:p><a:r><a:t>second</a:t></a:r></a:p></p:txBody></p:sp>
            </p:sld>
            """);
        return path;
    }

    /// <summary>:param archive: 本次新容器。:param name: 固定条目。:param text: XML。:returns: 写完的夹具条目。</summary>
    private static void Add(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
