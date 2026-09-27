// PPT 默认模式按需读取有序 PNG，不创建 PowerPoint 放映对象。
using System.IO;

namespace ScpCv.PlayerWorker.Adapters;

public sealed class SlideImagePlaybackAdapter : IAsyncDisposable
{
    private string[] _pages = [];

    public int PageCount => _pages.Length;

    public int CurrentPage { get; private set; }

    public Task OpenAsync(string directory, int initialPage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var full = Path.GetFullPath(directory);
        if (!Directory.Exists(full)) throw new FileNotFoundException("文稿逐页图片目录不存在。", full);
        var pages = Directory.GetFiles(full, "page-*.png").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (pages.Length is < 1 or > 500) throw new InvalidOperationException("文稿逐页图片数量必须为 1 到 500。");
        for (var index = 1; index <= pages.Length; index++)
        {
            var expected = Path.Combine(full, $"page-{index:0000}.png");
            if (!string.Equals(pages[index - 1], expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"文稿第 {index} 页图片缺失或顺序错误。");
        }
        _pages = pages;
        CurrentPage = Math.Clamp(initialPage, 1, pages.Length);
        return Task.CompletedTask;
    }

    public async Task<byte[]> RenderPageAsync(int page, CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > _pages.Length) throw new ArgumentOutOfRangeException(nameof(page));
        var bytes = await File.ReadAllBytesAsync(_pages[page - 1], cancellationToken).ConfigureAwait(false);
        CurrentPage = page;
        return bytes;
    }

    public ValueTask DisposeAsync()
    {
        _pages = [];
        CurrentPage = 0;
        return ValueTask.CompletedTask;
    }
}
