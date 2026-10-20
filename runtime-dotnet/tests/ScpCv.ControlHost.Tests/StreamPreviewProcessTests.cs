// 显式本机原生验证：只产生lavfi合成画面，无设备连接、窗口或音频。
using System.Diagnostics;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

[Trait("Category", "Physical")]
public sealed class StreamPreviewProcessTests
{
    [Fact]
    public async Task OwnedFfmpegProducesDecodableStructureAndStopsExactProcessAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("此显式验证只支持Windows");
        var executable = Environment.GetEnvironmentVariable("SCP_PREVIEW_NATIVE_FFMPEG")
            ?? throw new InvalidOperationException("请显式指定本机FFmpeg绝对路径，默认测试不会运行原生验证");
        await using var producer = PreviewProcess.Start(executable,
        ["-nostdin", "-hide_banner", "-loglevel", "error", "-re", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=8",
            "-an", "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-pix_fmt", "yuv420p",
            "-threads", "1", "-g", "8", "-sc_threshold", "0", "-f", "flv", "pipe:1"]);
        using var process = Process.GetProcessById(producer.ProcessId); _ = process.Handle;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var buffer = new FlvGopBuffer(); var groups = 0;
        await foreach (var tag in FlvReader.ReadAsync(producer.Output, timeout.Token))
        {
            if (buffer.Push(tag) is not null) groups++;
            if (groups == 2) break;
        }
        Assert.Equal(2, groups); Assert.Equal(ProcessPriorityClass.Idle, process.PriorityClass);
        await producer.DisposeAsync(); Assert.True(process.HasExited);
    }
}
