// 只读OOXML演示包元数据与外部关系门禁；识别改名内容，两个运行时同源，不加载UNO或保存原件。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace ScpCv.Runtime.Presentations;

internal sealed class PresentationPackageMetadata
{
    private static readonly XNamespace Presentation = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    internal Dictionary<int, List<(string Name, string Text)>> Unwrapped { get; } = new Dictionary<int, List<(string, string)>>();

    /// <summary>:param path: 只读原件。:returns: 无UNO/进程副作用的结构门禁与文字语义提示。</summary>
    internal PresentationPackageMetadata(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var declaredOoxml = extension is ".pptx" or ".pptm" or ".ppsx" or ".ppsm" or ".potx" or ".potm";
        using var file = File.OpenRead(path);
        using var archive = OpenArchive(file, declaredOoxml);
        if (archive == null) return;
        if (!declaredOoxml && archive.GetEntry("ppt/presentation.xml") == null) return;
        if (archive.Entries.Count > 10000 || archive.GetEntry("ppt/presentation.xml") == null)
            throw new InvalidDataException("不是受支持的PPTX容器或容器项目过多");
        var order = SlideOrder(archive);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith(".rels", StringComparison.Ordinal) && !order.ContainsKey(entry.FullName)) continue;
            total += entry.Length;
            if (entry.Length > 4 * 1024 * 1024 || total > 32 * 1024 * 1024) throw new InvalidDataException("PPTX元数据超出安全预算");
            var xml = Read(entry);
            if (entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
            {
                ValidateRelationships(xml);
                continue;
            }
            var hints = new List<(string, string)>();
            foreach (var shape in xml.Descendants(Presentation + "sp"))
            {
                var body = shape.Element(Presentation + "txBody")?.Element(Drawing + "bodyPr");
                if ((string?)body?.Attribute("wrap") != "none" || body?.Element(Drawing + "spAutoFit") == null) continue;
                var name = (string?)shape.Descendants(Presentation + "cNvPr").FirstOrDefault()?.Attribute("name");
                var text = string.Concat(shape.Descendants(Drawing + "t").Select(item => item.Value));
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(text)) hints.Add((name!, text));
            }
            Unwrapped[order[entry.FullName]] = hints;
        }
    }

    /// <summary>:param file: 自有只读流。:param declaredOoxml: 文件名声明OOXML。:returns: 可解析ZIP，或保持非ZIP旧格式原路；声明/带ZIP头但损坏时拒绝。</summary>
    private static ZipArchive? OpenArchive(FileStream file, bool declaredOoxml)
    {
        try { return new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException) when (!declaredOoxml)
        {
            // 不能仅按后缀或首字节辨识完整包；先让ZIP读取器按目录识别，再保留旧二进制格式。
            file.Position = 0;
            if (file.ReadByte() == 'P' && file.ReadByte() == 'K') throw;
            return null;
        }
    }

    /// <summary>:param xml: 关系元数据。:returns: 只接受省略模式/明确Internal与相对包引用。</summary>
    private static void ValidateRelationships(XDocument xml)
    {
        foreach (var item in xml.Descendants().Where(item => item.Name.LocalName == "Relationship"))
        {
            if (item.Attribute("TargetMode") is { } mode && mode.Value != "Internal")
                throw new InvalidDataException("PPTX含外部或非法关系模式，自动播放/转换不访问外链");
            var target = (string?)item.Attribute("Target");
            if (target == null || string.IsNullOrWhiteSpace(target) || target.StartsWith("//", StringComparison.Ordinal) ||
                !Uri.TryCreate(target, UriKind.Relative, out _))
                throw new InvalidDataException("PPTX内部关系必须是相对包引用");
        }
    }

    /// <summary>:param entry: 有界元数据项。:returns: 禁DTD/外部实体的XML。</summary>
    private static XDocument Read(ZipArchiveEntry entry)
    {
        if (entry.Length > 4 * 1024 * 1024) throw new InvalidDataException("PPTX页关系超限");
        using var input = entry.Open();
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(reader);
    }

    /// <summary>:param archive: 只读容器。:returns: 声明顺序，不把slide文件名当页码。</summary>
    private static Dictionary<string, int> SlideOrder(ZipArchive archive)
    {
        var relationXml = Read(archive.GetEntry("ppt/_rels/presentation.xml.rels") ?? throw new InvalidDataException("PPTX页关系缺失"));
        ValidateRelationships(relationXml);
        var relationships = relationXml.Descendants().Where(item => item.Name.LocalName == "Relationship")
            .ToDictionary(item => (string)item.Attribute("Id")!, item => (string)item.Attribute("Target")!);
        XNamespace relationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var slide in Read(archive.GetEntry("ppt/presentation.xml")!).Descendants(Presentation + "sldId"))
        {
            var id = (string?)slide.Attribute(relationship + "id");
            if (id == null || !relationships.TryGetValue(id, out var target)) throw new InvalidDataException("PPTX页关系不完整");
            var uri = new Uri(new Uri("http://package.invalid/ppt/presentation.xml"), target);
            var path = uri.AbsolutePath.TrimStart('/');
            if (uri.Host != "package.invalid" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                !path.StartsWith("ppt/", StringComparison.Ordinal) || archive.GetEntry(path) == null)
                throw new InvalidDataException("PPTX页关系越界");
            if (result.Count >= 500 || result.ContainsKey(path)) throw new InvalidDataException("PPTX页数超限或关系重复");
            result.Add(path, result.Count + 1);
        }
        return result;
    }
}
