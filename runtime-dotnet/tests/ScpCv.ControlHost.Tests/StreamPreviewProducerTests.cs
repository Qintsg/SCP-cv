// 文件描述：受控回环编码参数与显式不可用边界；项目：SCP-cv；文件：StreamPreviewProducerTests.cs；作者：Qintsg；日期：2026-10-21。
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewProducerTests
{
    [Fact]
    public void EncoderArgumentsAreSilentBoundedAndOnlyUseOwnedLoopbackInput()
    {
        const string destination = "rtmp://127.0.0.1:19355/scp-preview/0123456789abcdef0123456789abcdef";
        var args = PreviewProducer.BuildArguments("rtsp://127.0.0.1:8554/scp-preview-test", PreviewEncoding.Levels[1], destination);
        Assert.Contains("-an", args); Assert.Contains("-nostdin", args); Assert.Contains("libx264", args);
        Assert.Contains("300k", args); Assert.Contains("no_duration_filesize", args);
        Assert.Equal("2", args[args.ToList().IndexOf("-g") + 1]);
        foreach (var profile in PreviewEncoding.Levels)
        {
            var parameters = PreviewProducer.BuildArguments("rtsp://127.0.0.1:8554/scp-preview-test", profile, destination);
            Assert.Equal(PreviewProducer.KeyframeInterval(profile).ToString(System.Globalization.CultureInfo.InvariantCulture), parameters[parameters.ToList().IndexOf("-g") + 1]);
        }
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://192.0.2.1/scp-preview-test", PreviewEncoding.Levels[0], destination));
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://127.0.0.1/official", PreviewEncoding.Levels[0], destination));
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://127.0.0.1/scp-preview-test", new(9999, 9999, 120, 1000000), destination));
        Assert.Throws<PreviewException>(() => PreviewProducer.BuildArguments("rtsp://127.0.0.1/scp-preview-test", PreviewEncoding.Levels[0], "rtmp://192.0.2.1:1935/scp-preview/0123456789abcdef0123456789abcdef"));
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
