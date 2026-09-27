// PPT 默认页图适配器按页读取、导航并拒绝不完整的制品。
using ScpCv.PlayerWorker.Adapters;

namespace ScpCv.Windows.Tests;

public sealed class SlideImagePlaybackAdapterTests
{
    [Fact]
    public async Task OpensOrderedPagesAndNavigatesWithoutOffice()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-slide-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "page-0001.png"), [1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(root, "page-0002.png"), [4, 5, 6]);
            await using var adapter = new SlideImagePlaybackAdapter();

            await adapter.OpenAsync(root, 2);

            Assert.Equal(2, adapter.PageCount);
            Assert.Equal(2, adapter.CurrentPage);
            Assert.Equal([4, 5, 6], await adapter.RenderPageAsync(2));
            Assert.Equal([1, 2, 3], await adapter.RenderPageAsync(1));
            Assert.Equal(1, adapter.CurrentPage);
        }
        finally
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-slide-images-", StringComparison.Ordinal) &&
                Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }
}
