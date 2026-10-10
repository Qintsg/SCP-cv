// 屏幕共享的接收授权、真实后帧和终态围栏；不启动浏览器或采集用户桌面。
using System.Text.Json;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.Windows.Tests;

public sealed class ScreenSharePlaybackTests
{
    private static readonly Guid ShareId = Guid.Parse("403f10e1-ff66-4814-959f-fb419b20a941");
    private static readonly string Address = $"http://127.0.0.1:5207/api/screen-share/receiver/{ShareId:D}/?window_id=1&source_generation=7#{new string('a', 64)}";

    /// <summary>:returns: 私有地址核对目标/代次，默认字符串不泄漏cap，脚本移除fragment后仍识别原文档。</summary>
    [Fact]
    public void ReceiverIdentityIsBoundAndDoesNotPrintAuthorization()
    {
        var identity = ScreenShareReceiverIdentity.Parse(Address, ShareId, 1, 7);
        Assert.Equal(ShareId, identity.ShareId);
        Assert.DoesNotContain(new string('a', 64), identity.ToString(), StringComparison.Ordinal);
        Assert.True(identity.IsDocument(Address));
        Assert.True(identity.IsDocument(Address.Split('#')[0]));
        Assert.False(identity.IsDocument(Address.Replace("/receiver/", "/receiver/other/", StringComparison.Ordinal)));
        Assert.False(identity.IsDocument(Address.Replace("127.0.0.1", "192.168.5.194", StringComparison.Ordinal)));
    }

    /// <summary>:param address: 越界或不完整接收地址。:returns: 在创建任何原生资源前拒绝，不回显凭据。</summary>
    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("http://127.0.0.1:5207/")]
    [InlineData("http://user:secret@127.0.0.1:5207/api/screen-share/receiver/403f10e1-ff66-4814-959f-fb419b20a941/?window_id=1&source_generation=7")]
    public void UnsafeReceiverAddressIsRejected(string address) =>
        Assert.Throws<InvalidDataException>(() => ScreenShareReceiverIdentity.Parse(address, ShareId, 1, 7));

    /// <summary>:param replacement: 原参数。:param value: 错误参数。:returns: 不能靠额外、重复参数或另一目标获取接收资源。</summary>
    [Theory]
    [InlineData("window_id=1", "window_id=2")]
    [InlineData("window_id=1", "window_id=1&window_id=1")]
    [InlineData("source_generation=7", "source_generation=8")]
    [InlineData("source_generation=7", "source_generation=7&token=bad")]
    [InlineData("#", "#x")]
    public void MismatchedReceiverIdentityIsRejected(string replacement, string value) =>
        Assert.Throws<InvalidDataException>(() => ScreenShareReceiverIdentity.Parse(Address.Replace(replacement, value, StringComparison.Ordinal), ShareId, 1, 7));

    /// <summary>:returns: ready只是协商完成；预备帧也不能授予实际playing，须激活后的下一真实帧。</summary>
    [Fact]
    public async Task ReadyAndPreparedFramesDoNotMeanActivatedOutput()
    {
        var state = State();
        Assert.True(state.Accept(Address, Message("ready", new { frames = 0 })));
        Assert.False(state.IsPrepared);
        Assert.False(state.IsPlaying);
        Assert.True(state.Accept(Address, Frame(1, false)));
        await state.WaitPreparedAsync(CancellationToken.None);
        Assert.True(state.IsPrepared);
        Assert.False(state.IsPlaying);
        state.BeginActivation();
        Assert.True(state.Accept(Address, Frame(2, false)));
        Assert.False(state.IsPlaying);
        Assert.True(state.Accept(Address, Frame(3, true)));
        await state.WaitActivatedAsync(CancellationToken.None);
        Assert.True(state.IsPlaying);
        Assert.Equal(3, state.Frames);
    }

    /// <summary>:param frameFirst: 两类消息可能乱序。:returns: 两个预备条件均成立才允许切入。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationRequiresBothNegotiationAndActualVideo(bool frameFirst)
    {
        var state = State();
        state.Accept(Address, frameFirst ? Frame(1, false) : Message("ready", new { frames = 0 }));
        Assert.False(state.IsPrepared);
        state.Accept(Address, frameFirst ? Message("ready", new { frames = 1 }) : Frame(1, false));
        Assert.True(state.IsPrepared);
    }

    /// <summary>:returns: 重复帧、没有激活的伪后帧，以及另一个资源的迟到消息均不推进状态。</summary>
    [Fact]
    public void ReplayedAndCrossResourceFramesCannotActivate()
    {
        var state = State();
        Assert.False(state.Accept(Address, Frame(1, true)));
        state.Accept(Address, Message("ready", new { frames = 0 }));
        state.Accept(Address, Frame(1, false));
        state.BeginActivation();
        Assert.False(state.Accept(Address, Frame(1, true)));
        Assert.False(state.Accept(Address, Frame(2, true).Replace(ShareId.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal)));
        Assert.False(state.Accept(Address.Replace("5207", "5208", StringComparison.Ordinal), Frame(2, true)));
        Assert.False(state.IsPlaying);
        Assert.Equal(1, state.Frames);
    }

    /// <summary>:param json: 错误schema，不应抛出JSON类型异常或变成实际帧。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"frame\"")]
    [InlineData("{\"type\":\"frame\",\"frames\":\"1\"}")]
    [InlineData("{")]
    public void InvalidMessagesAreIgnored(string json)
    {
        var state = State();
        Assert.False(state.Accept(Address, json));
        Assert.Equal(0, state.Frames);
        Assert.False(state.IsPlaying);
    }

    /// <summary>:param close: 关闭或故障均为吸收终态。:returns: 首帧等待明确失败，旧回调不能复活该资源。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndedResourceFailsWaitersAndCannotRevive(bool close)
    {
        var state = State();
        state.Accept(Address, Message(close ? "closed" : "failed", close ? (object)new { frames = 0, has_stream = false } : new { code = "share_peer_disconnected" }));
        await Assert.ThrowsAsync<IOException>(() => state.WaitPreparedAsync(CancellationToken.None));
        Assert.False(state.Accept(Address, Frame(1, false)));
        Assert.False(state.IsPlaying);
        Assert.False(state.IsPrepared);
    }

    /// <summary>:returns: 主动退役后排队的frame/failed不能污染新实例；每个实例拥有自己的计数与任务。</summary>
    [Fact]
    public void NativeRetirementIsIndependentOfTheNextResource()
    {
        var old = State(); var current = State();
        old.Retire();
        Assert.False(old.Accept(Address, Frame(5, true)));
        current.Accept(Address, Message("ready", new { frames = 0 }));
        current.Accept(Address, Frame(1, false));
        current.BeginActivation(); current.Accept(Address, Frame(2, true));
        Assert.True(current.IsPlaying);
        Assert.False(old.IsPlaying);
    }

    /// <summary>:returns: 以前已完成的首帧Task不能掩盖后来的终止；公开等待仍须核当前资源存活。</summary>
    [Fact]
    public async Task CompletedFrameTasksDoNotMaskALaterEnd()
    {
        var state = State();
        state.Accept(Address, Message("ready", new { frames = 0 }));
        state.Accept(Address, Frame(1, false));
        await state.WaitPreparedAsync(CancellationToken.None);
        state.BeginActivation(); state.Accept(Address, Frame(2, true));
        await state.WaitActivatedAsync(CancellationToken.None);
        state.Fail("share_peer_disconnected");
        await Assert.ThrowsAsync<IOException>(() => state.WaitPreparedAsync(CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(() => state.WaitActivatedAsync(CancellationToken.None));
    }

    /// <summary>:returns: 独立消息门禁，不包含真实浏览器或模拟视频。</summary>
    private static ScreenSharePlaybackState State() => new(ScreenShareReceiverIdentity.Parse(Address, ShareId, 1, 7));

    /// <summary>:param frames: 递增计数。:param activated: 原生明确切入标记。:returns: 与正式接收页相同的消息schema。</summary>
    private static string Frame(long frames, bool activated) => Message("frame", new { frames, activated, media_time = frames / 30d, has_audio = false });

    /// <summary>:param type: 消息。:param fields: 实际字段。:returns: 不包含接收cap的值对象。</summary>
    private static string Message(string type, object fields)
    {
        var values = JsonSerializer.SerializeToElement(fields).EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        values.Add("type", JsonSerializer.SerializeToElement(type));
        values.Add("share_id", JsonSerializer.SerializeToElement(ShareId));
        values.Add("window_id", JsonSerializer.SerializeToElement(1));
        values.Add("source_generation", JsonSerializer.SerializeToElement(7));
        return JsonSerializer.Serialize(values);
    }
}
