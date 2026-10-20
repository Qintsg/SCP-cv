// Windows原生PDF物理像素渲染：自建无声两页夹具，不打开用户窗口，不修改系统显示。
using System.Buffers.Binary;
using System.Text;
using ScpCv.PlayerWorker.Adapters;

namespace ScpCv.Windows.Tests;

[Trait("Category", "Physical")]
public sealed class Pdf4kRenderPhysicalTests
{
    [Theory]
    [InlineData(3840, 2160)]
    [InlineData(1920, 1080)]
    public async Task PngUsesTargetPixelsAndCacheKeepsSameResolution(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scp-cv-pdf4k-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, Fixture());
            await using var adapter = new PdfPlaybackAdapter(width, height); await adapter.OpenAsync(path);
            var first = await adapter.RenderPageAsync(1); var second = await adapter.RenderPageAsync(2);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, first[..8]);
            Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(first.AsSpan(16, 4)));
            Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(first.AsSpan(20, 4)));
            Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(second.AsSpan(16, 4)));
            Assert.Equal(first, await adapter.RenderPageAsync(1)); Assert.Equal(1, adapter.CurrentPage);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static byte[] Fixture()
    {
        const string first = "0.1 0.3 0.8 rg 0 0 960 540 re f\n";
        const string second = "0.8 0.3 0.1 rg 0 0 960 540 re f\n";
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 960 540] /Resources << >> /Contents 5 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 960 540] /Resources << >> /Contents 6 0 R >>",
            $"<< /Length {first.Length} >>\nstream\n{first}endstream", $"<< /Length {second.Length} >>\nstream\n{second}endstream"];
        var builder = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        { offsets.Add(builder.Length); builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n"); }
        var xref = builder.Length; builder.Append("xref\n0 7\n0000000000 65535 f \n");
        foreach (var offset in offsets) builder.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
