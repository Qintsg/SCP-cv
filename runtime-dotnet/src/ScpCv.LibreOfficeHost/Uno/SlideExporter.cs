// 独立只读转换实例导出PNG，使用透明度保留避免公式fallback黑块。
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using ScpCv.Presentation.Protocol;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.document;
using unoidl.com.sun.star.util;
using static ScpCv.LibreOfficeHost.Uno.UnoConnection;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class SlideExporter
{
    /// <summary>:param connection: 独立转换引擎。:param request: 已核原件摘要/输出目录的请求。:returns: 有序PNG数量。</summary>
    internal static PresentationObservation Export(UnoConnection connection, PresentationRequest request)
    {
        var package = new PresentationPackageReader(request.SourcePath);
        if (Directory.Exists(request.OutputDirectory) && Directory.GetFileSystemEntries(request.OutputDirectory).Length != 0)
            throw new InvalidDataException("输出目录已有内容，结果不明时禁止重放");
        Directory.CreateDirectory(request.OutputDirectory);
        var document = PresentationConversionLoader.Load(connection, request.SourcePath, request.OutputDirectory);
        try
        {
            if (!((XStorable)document).isReadonly()) throw new InvalidDataException("转换文稿未只读加载");
            package.ApplyLayout(document);
            PresentationTextLayout.Apply(document, request.FitText);
            var pages = ((XDrawPagesSupplier)document).getDrawPages();
            if (pages.getCount() < 1 || pages.getCount() > 500) throw new InvalidDataException("文稿页数超出安全预算");
            using var artifactHash = SHA256.Create();
            for (var index = 0; index < pages.getCount(); index++)
            {
                var page = (XDrawPage)pages.getByIndex(index).Value;
                var props = (XPropertySet)page;
                var width = (int)props.getPropertyValue("Width").Value;
                var height = (int)props.getPropertyValue("Height").Value;
                if (width <= 0 || height <= 0) throw new InvalidDataException("页尺寸无效");
                const int pixels = 1920;
                var pixelHeight = (int)Math.Round((double)pixels * height / width);
                var path = Path.Combine(request.OutputDirectory, "page-" + (index + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".png");
                var exporter = connection.Context.getServiceManager().createInstanceWithContext("com.sun.star.drawing.GraphicExportFilter", connection.Context);
                ((XExporter)exporter).setSourceDocument((XComponent)page);
                if (!((XFilter)exporter).filter(new[]
                {
                    Property("URL", new Uri(path).AbsoluteUri), Property("MediaType", "image/png"),
                    Property("FilterData", new[] { Property("PixelWidth", pixels), Property("PixelHeight", pixelHeight), Property("Translucent", true) }),
                })) throw new InvalidOperationException("PNG导出被拒绝");
                using var bitmap = new Bitmap(path);
                if (bitmap.Width != pixels || bitmap.Height != pixelHeight) throw new InvalidDataException("PNG尺寸或可读性失败");
                var bytes = File.ReadAllBytes(path);
                artifactHash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            RasterPresentationPdf.Create(request.OutputDirectory, pages.getCount());
            var pdfBytes = File.ReadAllBytes(Path.Combine(request.OutputDirectory, "presentation.pdf"));
            artifactHash.TransformBlock(pdfBytes, 0, pdfBytes.Length, null, 0);
            artifactHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return new PresentationObservation
            {
                Confirmed = true, SourceId = request.Scope.SourceId, TotalSlides = pages.getCount(),
                ArtifactDigest = BitConverter.ToString(artifactHash.Hash ?? throw new InvalidOperationException("制品摘要缺失")).Replace("-", string.Empty),
            };
        }
        finally { ((XCloseable)document).close(true); }
    }
}
