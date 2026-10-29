// 文件描述：用真实WPF窗口与图片验证维护排序、媒体保持和有序观测。
// 项目：SCP-cv；文件：WindowForegroundTests.cs；作者：Qintsg；日期：2026-10-29。
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Interop;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class WindowForegroundTests
{
    private static readonly bool[] ForegroundStates = [false, true];
    /// <summary>:returns: 20轮真实窗口排序不重建HWND或当前图片，位置调整和换源保持维护状态。</summary>
    [Fact]
    public async Task NativeWindowSurvivesTwentyVisibilityCyclesAndSourceChanges()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(async () =>
            {
                var window = new PlayerWindow { ShowActivated = false, Title = "SCP-cv 窗口维护验证" };
                var administrator = new System.Windows.Window { ShowActivated = false, Title = "SCP-cv 管理窗口验证", Left = 40, Top = 40, Width = 320, Height = 180 };
                var host = new PlayerRuntimeHost(window, 1);
                try
                {
                    window.AssignBounds(40, 40, 320, 180); window.Show();
                    administrator.Show();
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    var handle = window.NativeHandle;
                    Assert.NotEqual(nint.Zero, handle);
                    Assert.True(window.WindowForeground);
                    Assert.True(GetWindowRect(handle, out var original));
                    var image = ImagePath();
                    await host.ExecuteAsync(Lease("OPEN", new { source_id = 1, source_type = "image", uri = image, autoplay = true }), CancellationToken.None);
                    long sequence = 0;
                    for (var index = 0; index < 20; index++)
                    {
                        foreach (var foreground in ForegroundStates)
                        {
                            var result = await host.ExecuteAsync(Lease("SET_WINDOW_FOREGROUND", new { foreground }), CancellationToken.None);
                            Assert.Equal("completed", result.Status);
                            Assert.Equal(foreground, window.WindowForeground);
                            Assert.True(foreground ? IsAbove(handle, new WindowInteropHelper(administrator).Handle) : IsAbove(new WindowInteropHelper(administrator).Handle, handle));
                            Assert.Equal(foreground, result.ActualState.GetProperty("window_foreground").GetBoolean());
                            Assert.True(result.ActualState.GetProperty("window_observation_only").GetBoolean());
                            var next = result.ActualState.GetProperty("window_observation_sequence").GetInt64();
                            Assert.True(next > sequence); sequence = next;
                            Assert.Equal(handle, window.NativeHandle); Assert.True(window.IsVisible);
                            Assert.True(GetWindowRect(handle, out var current)); Assert.Equal(original, current);
                            window.AssignBounds(original.Left, original.Top, original.Right - original.Left, original.Bottom - original.Top);
                            Assert.Equal(foreground, window.WindowForeground);
                            var sample = await host.SampleProgressAsync(CancellationToken.None);
                            Assert.NotNull(sample);
                            Assert.Equal(1, sample.State.GetProperty("source_id").GetInt64());
                            Assert.Equal("playing", sample.State.GetProperty("playback_state").GetString());
                        }
                    }
                    window.SetWindowForeground(false);
                    await host.ExecuteAsync(Lease("OPEN", new { source_id = 2, source_type = "image", uri = image, autoplay = true }) with { SourceGeneration = 2 }, CancellationToken.None);
                    Assert.False(window.WindowForeground);
                    var changed = await host.SampleProgressAsync(CancellationToken.None);
                    Assert.NotNull(changed); Assert.Equal(2, changed.State.GetProperty("source_id").GetInt64());
                    window.SetWindowForeground(true); Assert.True(window.WindowForeground);
                    completion.SetResult();
                }
                catch (Exception error) { completion.SetException(error); }
                finally
                {
                    await host.DisposeAsync(); window.Close(); administrator.Close(); dispatcher.InvokeShutdown();
                }
            });
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(40));
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "窗口线程未退出。");
    }

    /// <summary>:returns: 项目真实图片路径。</summary>
    private static string ImagePath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null)
        {
            var path = Path.Combine(root.FullName, "frontend", "src", "assets", "sspu-ailab-transparent.png");
            if (File.Exists(path)) return path;
            root = root.Parent;
        }
        throw new FileNotFoundException("项目测试图片缺失。");
    }

    /// <summary>:param command: 生产命令。:param args: 实际参数。:returns: 当前窗口执行所需租约。</summary>
    private static CommandLeaseDto Lease(string command, object args) => new()
    {
        CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = command, SourceGeneration = 1, SourceRevision = 1,
        Args = JsonSerializer.SerializeToElement(args).EnumerateObject().ToDictionary(property => property.Name, property => property.Value),
    };

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct WindowRectangle(int Left, int Top, int Right, int Bottom);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out WindowRectangle rectangle);

    /// <summary>:param upper: 预期位于上方的真实窗口。:param lower: 下方窗口。:returns: Win32顺序是否符合预期。</summary>
    private static bool IsAbove(nint upper, nint lower)
    {
        for (var current = GetWindow(upper, 2); current != 0; current = GetWindow(current, 2))
            if (current == lower) return true;
        return false;
    }

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint window, uint command);
}
