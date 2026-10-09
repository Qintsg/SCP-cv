// 从已验证PNG生成只含图片的静态PDF，绝不携带PPT媒体、脚本或交互附件。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class RasterPresentationPdf
{
    /// <summary>:param directory: 当前作业有序PNG。:param count: 已核页数。:returns: 创建同源静态PDF，不覆盖既有产物。</summary>
    internal static void Create(string directory, int count)
    {
        using var output = new FileStream(Path.Combine(directory, "presentation.pdf"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var positions = new List<long> { 0 };
        void Write(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value); output.Write(bytes, 0, bytes.Length);
        }
        void Object(int id, string value)
        {
            if (id != positions.Count) throw new InvalidOperationException("PDF对象顺序不连续");
            positions.Add(output.Position); Write(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + value + "\nendobj\n");
        }
        Write("%PDF-1.4\n%SCP-cv-raster-only\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Count " + count + " /Kids [" +
            string.Join(" ", Enumerable.Range(0, count).Select(index => (3 + 3 * index).ToString(CultureInfo.InvariantCulture) + " 0 R")) + "] >>");
        var codec = ImageCodecInfo.GetImageEncoders().Single(item => item.MimeType == "image/jpeg");
        for (var index = 0; index < count; index++)
        {
            var first = 3 + 3 * index;
            using var source = new Bitmap(Path.Combine(directory, "page-" + (index + 1).ToString("D4", CultureInfo.InvariantCulture) + ".png"));
            using var opaque = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(opaque))
            {
                graphics.Clear(Color.White);
                // PNG含144 DPI而新Bitmap默认96 DPI；必须显式按像素绘制，不能受物理分辨率缩成2/3。
                graphics.DrawImage(source, new Rectangle(0, 0, opaque.Width, opaque.Height),
                    new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            }
            using var encoded = new MemoryStream();
            using (var quality = new EncoderParameters(1))
            {
                quality.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                opaque.Save(encoded, codec, quality);
            }
            var width = (source.Width / 2.0).ToString("0.###", CultureInfo.InvariantCulture);
            var height = (source.Height / 2.0).ToString("0.###", CultureInfo.InvariantCulture);
            Object(first, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + width + " " + height +
                "] /Resources << /XObject << /Im0 " + (first + 2) + " 0 R >> >> /Contents " + (first + 1) + " 0 R >>");
            var content = "q " + width + " 0 0 " + height + " 0 0 cm /Im0 Do Q\n";
            Object(first + 1, "<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "endstream");
            positions.Add(output.Position);
            Write((first + 2) + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + source.Width + " /Height " + source.Height +
                " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + encoded.Length + " >>\nstream\n");
            encoded.Position = 0; encoded.CopyTo(output); Write("\nendstream\nendobj\n");
        }
        var xref = output.Position;
        Write("xref\n0 " + positions.Count + "\n0000000000 65535 f \n");
        foreach (var position in positions.Skip(1)) Write(position.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write("trailer\n<< /Size " + positions.Count + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
    }
}
