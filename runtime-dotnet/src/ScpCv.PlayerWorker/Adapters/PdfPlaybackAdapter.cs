// Windows PDF按页渲染与相邻缓存；命中缓存也在同一门禁更新当前页。
using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.IO;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ScpCv.PlayerWorker.Adapters;

/// <summary>Windows.Data.Pdf 后端；对外页码始终从 1 开始，并只缓存当前页及相邻页。</summary>
public sealed class PdfPlaybackAdapter(int targetWidth = 3840, int targetHeight = 2160) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<int, byte[]> _renderedPages = new();
    private PdfDocument? _document;
    private int _disposed;
    private double _rasterScaleX;
    private double _rasterScaleY;

    public int PageCount => checked((int)(_document?.PageCount ?? 0));
    public int CurrentPage { get; private set; }

    public async Task OpenAsync(string path, int initialPage = 1, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("PDF 放映只支持 Windows 播放端。");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("PDF 文件不存在。", fullPath);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(fullPath).AsTask(cancellationToken).ConfigureAwait(false);
            _document = await PdfDocument.LoadFromFileAsync(file).AsTask(cancellationToken).ConfigureAwait(false);
            _renderedPages.Clear();
            ValidatePage(initialPage);
            CurrentPage = initialPage;
        }
        finally
        {
            _gate.Release();
        }

        await RenderPageAsync(initialPage, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> RenderPageAsync(int pageNumber, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ValidatePage(pageNumber);
            if (_renderedPages.TryGetValue(pageNumber, out var cached))
            {
                CurrentPage = pageNumber;
                return cached;
            }
            var bytes = await RenderCoreAsync(pageNumber, cancellationToken).ConfigureAwait(false);
            _renderedPages[pageNumber] = bytes;
            CurrentPage = pageNumber;

            foreach (var neighbor in new[] { pageNumber - 1, pageNumber + 1 }.Where(page => page >= 1 && page <= PageCount))
            {
                if (!_renderedPages.ContainsKey(neighbor))
                {
                    _renderedPages[neighbor] = await RenderCoreAsync(neighbor, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var stale in _renderedPages.Keys.Where(page => Math.Abs(page - pageNumber) > 1))
            {
                _renderedPages.TryRemove(stale, out _);
            }

            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _document = null;
            _renderedPages.Clear();
            _gate.Dispose();
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private async Task<byte[]> RenderCoreAsync(int pageNumber, CancellationToken cancellationToken)
    {
        using var page = _document!.GetPage(checked((uint)(pageNumber - 1)));
        // WinRT DestinationWidth/Height是DIP而非物理像素。用有界64DIP探针校准实际渲染器，
        // 不猜测线程/主显示器DPI，也不修改进程或系统DPI设置；同一适配器缓存该比例。
        if (_rasterScaleX == 0)
        {
            var probe = await RenderImageAsync(page, 64, 64, cancellationToken).ConfigureAwait(false);
            var measured = PngSize(probe);
            _rasterScaleX = measured.Width / 64d; _rasterScaleY = measured.Height / 64d;
        }
        var scale = Math.Min(Math.Clamp(targetWidth, 1, 8192) / page.Size.Width,
            Math.Clamp(targetHeight, 1, 8192) / page.Size.Height);
        return await RenderImageAsync(page, checked((uint)Math.Max(1, Math.Round(page.Size.Width * scale / _rasterScaleX))),
            checked((uint)Math.Max(1, Math.Round(page.Size.Height * scale / _rasterScaleY))), cancellationToken).ConfigureAwait(false);
    }

    private static (int Width, int Height) PngSize(byte[] bytes)
    {
        if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("原生PDF渲染未返回有效PNG，未采用未知像素比例。");
        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        return width > 0 && height > 0 ? (width, height) : throw new InvalidDataException("PDF栅格尺寸无效。");
    }

    private static async Task<byte[]> RenderImageAsync(PdfPage page, uint width, uint height, CancellationToken cancellationToken)
    {
        using var stream = new InMemoryRandomAccessStream();
        var options = new PdfPageRenderOptions
        {
            DestinationWidth = width,
            DestinationHeight = height,
            BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
        };
        await page.RenderToStreamAsync(stream, options).AsTask(cancellationToken).ConfigureAwait(false);
        stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        var length = checked((uint)stream.Size);
        await reader.LoadAsync(length).AsTask(cancellationToken).ConfigureAwait(false);
        var bytes = new byte[checked((int)length)];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private void ValidatePage(int pageNumber)
    {
        if (_document is null) throw new InvalidOperationException("尚未打开 PDF 文档。");
        if (pageNumber < 1 || pageNumber > PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), $"页码必须位于 1 到 {PageCount} 之间。");
    }
}
