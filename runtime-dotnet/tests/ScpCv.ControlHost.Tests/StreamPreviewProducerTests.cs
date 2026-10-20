// 转换参数只允许回环路径与有界档位，默认模拟实现不能偷偷启动进程。
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewProducerTests
{
    [Fact]
    public void EncoderArgumentsAreSilentBoundedAndOnlyUseOwnedLoopbackInput()
    {
        var args = PreviewProducer.BuildArguments("rtsp://127.0.0.1:8554/scp-preview-test", PreviewEncoding.Levels[1]);
        Assert.Contains("-an", args); Assert.Contains("-nostdin", args); Assert.Contains("libx264", args);
        Assert.Contains("300k", args); Assert.Contains("no_duration_filesize", args);
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://192.0.2.1/scp-preview-test", PreviewEncoding.Levels[0]));
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://127.0.0.1/official", PreviewEncoding.Levels[0]));
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://127.0.0.1/scp-preview-test", new(9999, 9999, 120, 1000000)));
    }

    [Fact]
    public async Task SimulationFailsWithoutPublishingOrSpawningAsync()
    {
        var producer = new UnavailablePreviewProducer();
        var failure = await Assert.ThrowsAsync<PreviewException>(() => producer.RunAsync(new("rtmp://192.0.2.1/live/one", "low"),
            () => PreviewEncoding.Levels[0], _ => Assert.Fail("不能伪造帧"), _ => Assert.Fail("不能伪造运行态"), CancellationToken.None));
        Assert.Equal("preview_dependency_missing", failure.Code);
    }
}
