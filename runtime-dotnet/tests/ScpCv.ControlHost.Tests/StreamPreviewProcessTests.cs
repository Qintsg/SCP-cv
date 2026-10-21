// 文件描述：真实编码器、成熟协议接收与精确退出验证；项目：SCP-cv；文件：StreamPreviewProcessTests.cs；作者：Qintsg；日期：2026-10-21。
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
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var receiver = await PreviewRtmpReceiver.StartAsync(timeout.Token);
        await using var producer = PreviewProcess.Start(executable,
        ["-nostdin", "-hide_banner", "-loglevel", "error", "-re", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=8",
            "-an", "-c:v", "libx264", "-preset", "ultrafast", "-tune", "zerolatency", "-pix_fmt", "yuv420p",
            "-threads", "1", "-g", "8", "-sc_threshold", "0", "-f", "flv", "-flvflags", "no_duration_filesize", receiver.PublishUri]);
        using var process = Process.GetProcessById(producer.ProcessId); _ = process.Handle;
        var buffer = new FlvGopBuffer(); var groups = 0;
        await foreach (var tag in receiver.Reader.ReadAllAsync(timeout.Token))
        {
            if (buffer.Push(tag) is not null) groups++;
            if (groups == 2) break;
        }
        Assert.Equal(2, groups); Assert.Equal(ProcessPriorityClass.Idle, process.PriorityClass);
        await producer.DisposeAsync(); Assert.True(process.HasExited);
    }
}
