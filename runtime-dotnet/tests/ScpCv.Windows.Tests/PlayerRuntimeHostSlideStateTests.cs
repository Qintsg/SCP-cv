// 经公共命令入口验证页图切到图片后清除文稿页码；不显示窗口或激活 VLC/Office/音频。
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class PlayerRuntimeHostSlideStateTests
{
    [Fact]
    public async Task SwitchingFromSlideImagesToImageClearsPresentationProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-player-slide-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await OnHiddenDispatcherAsync(async () =>
            {
                var slides = Path.Combine(root, "slides");
                Directory.CreateDirectory(slides);
                WritePng(Path.Combine(slides, "page-0001.png"), 255, 0, 0);
                WritePng(Path.Combine(slides, "page-0002.png"), 0, 255, 0);
                var imagePath = Path.Combine(root, "image.png");
                WritePng(imagePath, 0, 0, 255);

                var window = new PlayerWindow();
                try
                {
                    await using var host = new PlayerRuntimeHost(window, 1);
                    var document = await host.ExecuteAsync(OpenLease(101, 1, new
                    {
                        source_id = 101,
                        uri = slides,
                        source_type = "ppt",
                        presentation_mode = "slide_images",
                        slide_images_directory = slides,
                        target_slide = 2,
                        autoplay = true,
                    }), CancellationToken.None);

                    Assert.Equal("completed", document.Status);
                    Assert.Equal("slide_images", document.ActualState.GetProperty("adapter_kind").GetString());
                    Assert.Equal("slide_images", document.ActualState.GetProperty("playback_mode").GetString());
                    Assert.Equal(2, document.ActualState.GetProperty("current_slide").GetInt32());
                    Assert.Equal(2, document.ActualState.GetProperty("total_slides").GetInt32());

                    var image = await host.ExecuteAsync(OpenLease(102, 2, new
                    {
                        source_id = 102,
                        uri = imagePath,
                        source_type = "image",
                        autoplay = true,
                    }), CancellationToken.None);

                    Assert.False(window.IsVisible);
                    Assert.False(window.IsLoaded);
                    Assert.Equal(nint.Zero, window.NativeHandle);
                    Assert.Equal("completed", image.Status);
                    Assert.Equal(102, image.ActualState.GetProperty("source_id").GetInt64());
                    Assert.Equal(2, image.ActualState.GetProperty("source_generation").GetInt64());
                    Assert.Equal("image", image.ActualState.GetProperty("adapter_kind").GetString());
                    Assert.Equal(string.Empty, image.ActualState.GetProperty("playback_mode").GetString());
                    Assert.Equal("playing", image.ActualState.GetProperty("playback_state").GetString());
                    var presentationProgress = (
                        image.ActualState.GetProperty("current_slide").GetInt32(),
                        image.ActualState.GetProperty("total_slides").GetInt32());
                    Assert.Equal((0, 0), presentationProgress);
                }
                finally { window.Close(); }
            });
        }
        finally { DeleteOwnedFixtureDirectory(root); }
    }

    [Fact]
    public async Task FailedImageSwitchKeepsActivePresentationProgress()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-player-slide-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await OnHiddenDispatcherAsync(async () =>
            {
                WritePng(Path.Combine(root, "page-0001.png"), 255, 0, 0);
                WritePng(Path.Combine(root, "page-0002.png"), 0, 255, 0);
                var window = new PlayerWindow();
                try
                {
                    await using var host = new PlayerRuntimeHost(window, 1);
                    await host.ExecuteAsync(OpenLease(101, 1, new
                    {
                        source_id = 101,
                        uri = root,
                        source_type = "ppt",
                        presentation_mode = "slide_images",
                        slide_images_directory = root,
                        target_slide = 2,
                        autoplay = true,
                    }), CancellationToken.None);

                    await Assert.ThrowsAsync<FileNotFoundException>(() => host.ExecuteAsync(OpenLease(102, 2, new
                    {
                        source_id = 102,
                        uri = Path.Combine(root, "missing-image.png"),
                        source_type = "image",
                        autoplay = true,
                    }), CancellationToken.None));
                    // 只改内存中的循环意图以取得公开快照，不调用真实播放器或设备。
                    var unchanged = await host.ExecuteAsync(OpenLease(103, 1, new { enabled = false }) with
                    {
                        Command = "SET_LOOP",
                    }, CancellationToken.None);

                    Assert.False(window.IsVisible);
                    Assert.Equal(nint.Zero, window.NativeHandle);
                    Assert.Equal(101, unchanged.ActualState.GetProperty("source_id").GetInt64());
                    Assert.Equal("slide_images", unchanged.ActualState.GetProperty("playback_mode").GetString());
                    Assert.Equal("playing", unchanged.ActualState.GetProperty("playback_state").GetString());
                    Assert.Equal((2, 2), (
                        unchanged.ActualState.GetProperty("current_slide").GetInt32(),
                        unchanged.ActualState.GetProperty("total_slides").GetInt32()));
                }
                finally { window.Close(); }
            });
        }
        finally { DeleteOwnedFixtureDirectory(root); }
    }

    /// <summary>只经对外租约字段调用真实 PlayerRuntimeHost，不反射私有状态。</summary>
    /// <param name="sequence">本轮命令序号。</param>
    /// <param name="generation">新的源代次。</param>
    /// <param name="arguments">与生产命令相同的参数集合。</param>
    /// <returns>可交给公共 ExecuteAsync 的 OPEN 命令。</returns>
    private static CommandLeaseDto OpenLease(long sequence, long generation, object arguments) => new()
    {
        CommandId = Guid.NewGuid(),
        Command = "OPEN",
        TargetSequence = sequence,
        SourceGeneration = generation,
        SourceRevision = 1,
        Args = JsonSerializer.SerializeToElement(arguments).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value),
    };

    /// <summary>生成自有有效 PNG，避免假图片或外部素材错误掩盖状态残留。</summary>
    /// <param name="path">本轮精确临时文件路径。</param>
    /// <param name="red">红色分量。</param>
    /// <param name="green">绿色分量。</param>
    /// <param name="blue">蓝色分量。</param>
    private static void WritePng(string path, byte red, byte green, byte blue)
    {
        byte[] pixel = [blue, green, red, 255];
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixel, 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>只清理本轮已验证位于系统 temp 下的 GUID 测试目录。</summary>
    /// <param name="root">由测试生成的自有目录。</param>
    private static void DeleteOwnedFixtureDirectory(string root)
    {
        var full = Path.GetFullPath(root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(full).StartsWith("scp-cv-player-slide-state-", StringComparison.Ordinal) &&
            Directory.Exists(full))
            Directory.Delete(full, recursive: true);
    }

    /// <summary>使用自有 STA 消息泵执行 WPF 对象操作，但不调用 Show 或创建原生播放器。</summary>
    /// <param name="action">只涉及生成页图和图片表面的测试操作。</param>
    /// <returns>操作及自有线程退出后的完成任务。</returns>
    private static async Task OnHiddenDispatcherAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
                finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
        finally { Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "隐藏页图回归的自有 STA 线程未退出。"); }
    }
}
