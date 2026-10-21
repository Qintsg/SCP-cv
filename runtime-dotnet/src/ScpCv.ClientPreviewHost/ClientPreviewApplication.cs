// Electron私有宿主：stdin EOF/短心跳独立于上游读取；崩溃关闭独占Job，不依赖正常退出钩子。
using System.Text;
using System.Text.Json;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ClientPreviewHost;

internal static class ClientPreviewApplication
{
    private static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
    /// <summary>:returns: 固定退出码；stderr从不输出URI、凭据或FFmpeg原始错误。</summary>
    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return 4;
        using var lifetime = new CancellationTokenSource();
        using var watch = WatchInput(lifetime);
        try
        {
            var configuration = await watch.Configuration.ConfigureAwait(false);
            var executable = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
            await using var process = PreviewProcess.Start(executable, PreviewArguments.Build(configuration));
            using var stop = lifetime.Token.UnsafeRegister(static state => { if (OperatingSystem.IsWindows()) ((PreviewProcess)state!).RequestStop(); }, process);
            await Console.Error.WriteLineAsync(JsonSerializer.Serialize(new { @event = "started", pid = process.ProcessId })).ConfigureAwait(false);
            await process.Output.CopyToAsync(Console.OpenStandardOutput(), lifetime.Token).ConfigureAwait(false);
            return lifetime.IsCancellationRequested ? 0 : 3;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return 0; }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or System.ComponentModel.Win32Exception or PreviewException)
        { await Console.Error.WriteLineAsync("{\"event\":\"failed\",\"code\":\"native_preview_process_failed\"}").ConfigureAwait(false); return 3; }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            // Windows控制台管道读取不能可靠取消；不得在退出时等待被阻塞的后台stdin读取。
        }
    }

    /// <summary>:param lifetime: EOF或4秒心跳截止时立即取消。:returns: 首配置与独立生命周期监视。</summary>
    private static InputWatch WatchInput(CancellationTokenSource lifetime)
    {
        var configuration = new TaskCompletionSource<PreviewConfiguration>(TaskCreationOptions.RunContinuationsAsynchronously);
        var timer = new Timer(_ => Stop(), null, TimeSpan.FromSeconds(4), Timeout.InfiniteTimeSpan);
        // ThreadPool任务是后台读取者；独立Timer即使在stdin不返回时仍会杀掉自有Job。
        _ = Task.Run(ReadInput);
        return new(configuration.Task, timer);

        void Stop()
        {
            configuration.TrySetCanceled();
            try { lifetime.Cancel(); } catch (ObjectDisposedException) { /* 主入口已退出，Job句柄已经关闭。 */ }
        }

        void ReadInput()
        {
            using var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false, true), false, 1024, leaveOpen: true);
            try
            {
                var line = input.ReadLine();
                if (line is null || line.Length > 16384) throw new InvalidDataException("缺少受限配置");
                var selected = JsonSerializer.Deserialize<PreviewConfiguration>(line, InputOptions) ?? throw new InvalidDataException("配置无效");
                _ = PreviewArguments.Build(selected);
                configuration.TrySetResult(selected);
                while (!lifetime.IsCancellationRequested)
                {
                    timer.Change(TimeSpan.FromSeconds(4), Timeout.InfiniteTimeSpan);
                    var heartbeat = input.ReadLine();
                    if (heartbeat != "heartbeat") break;
                }
            }
            catch (Exception error) when (error is OperationCanceledException or IOException or InvalidDataException or JsonException or DecoderFallbackException or ObjectDisposedException)
            { configuration.TrySetException(new InvalidDataException("预览宿主输入已结束")); }
            finally
            {
                Stop();
            }
        }
    }

    private sealed record InputWatch(Task<PreviewConfiguration> Configuration, Timer Timer) : IDisposable
    {
        /// <summary>:returns: 释放独立看门狗，不等待Windows不可取消的后台读取。</summary>
        public void Dispose() => Timer.Dispose();
    }
}
