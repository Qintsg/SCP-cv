// 上传转换专用 Office STA 导出实测；只读源文件，不启动放映。
using ScpCv.PowerPointHost.Interop;
using ScpCv.PowerPointHost.Sta;

namespace ScpCv.Windows.Tests;

public sealed class PowerPointSlideExportTests
{
    [Fact]
    [Trait("Category", "Physical")]
    public async Task ExistingPresentationExportsOrderedPngPagesWithoutSlideShow()
    {
        var source = Environment.GetEnvironmentVariable("SCP_CV_TEST_PPTX");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            throw new InvalidOperationException("请将 SCP_CV_TEST_PPTX 指向本机只读测试文稿。");
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-ppt-export-{Guid.NewGuid():N}");
        try
        {
            using var sta = new OfficeStaDispatcher();
            using var adapter = new PowerPointComAdapter(sta);
            var result = await adapter.ExportSlidesAsync(Guid.NewGuid(), source, root);

            Assert.True(result.Succeeded, result.Detail);
            Assert.Equal(9, result.PageCount);
            var files = Directory.GetFiles(root, "page-*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Assert.Equal(9, files.Length);
            Assert.EndsWith("page-0001.png", files[0], StringComparison.Ordinal);
            Assert.All(files, file => Assert.True(new FileInfo(file).Length > 1024));
        }
        finally
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-ppt-export-", StringComparison.Ordinal) &&
                Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }
}
