// 音频回执严格schema与资源寿命回归；仅纯值测试，不把轨道/音量字段当实际可听证明。
using System.Text.Json;
using ScpCv.PlayerWorker.ScreenSharing;

namespace ScpCv.Windows.Tests;

public sealed class ScreenShareAudioReceiptTests
{
    /// <summary>:param level: 实际音量。:param requestedMute: 明确请求。:param hasAudio: 实际音轨。:returns: 正确接收属性与无音轨强制静音，浮点尾差不是失败。</summary>
    [Theory]
    [InlineData(0, false, true)]
    [InlineData(29, false, true)]
    [InlineData(100, true, true)]
    [InlineData(50, false, false)]
    public void ExactNativeReadbackIsAccepted(int level, bool requestedMute, bool hasAudio)
    {
        var json = JsonSerializer.Serialize(new { accepted = true, volume = level / 100d * 100,
            muted = requestedMute || !hasAudio, has_audio = hasAudio });
        var receipt = ScreenShareAudioReceipt.Parse(json, level, requestedMute);
        Assert.Equal(level, receipt.Volume);
        Assert.Equal(requestedMute || !hasAudio, receipt.Muted);
        Assert.Equal(hasAudio, receipt.HasAudio);
    }

    /// <summary>:param json: 拒绝、错误类型、重复/额外字段和不一致实际结果。:returns: 固定失败不能回显原始内容或变成成功。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"not-an-object\"")]
    [InlineData("{\"accepted\":false}")]
    [InlineData("{\"accepted\":true,\"volume\":50,\"muted\":false,\"has_audio\":false}")]
    [InlineData("{\"accepted\":true,\"volume\":49,\"muted\":false,\"has_audio\":true}")]
    [InlineData("{\"accepted\":true,\"volume\":\"50\",\"muted\":false,\"has_audio\":true}")]
    [InlineData("{\"accepted\":true,\"volume\":50,\"muted\":\"false\",\"has_audio\":true}")]
    [InlineData("{\"accepted\":true,\"volume\":50,\"muted\":false,\"has_audio\":true,\"volume\":50}")]
    [InlineData("{\"accepted\":true,\"volume\":50,\"muted\":false,\"has_audio\":true,\"secret\":\"never-echo\"}")]
    [InlineData("{\"accepted\":true,\"volume\":50,\"muted\":false,\"has_audio\":true,\"frames\":1}")]
    [InlineData("{")]
    public void InvalidNativeReadbackCannotClaimSuccess(string json)
    {
        var error = Assert.Throws<IOException>(() => ScreenShareAudioReceipt.Parse(json, 50, false));
        Assert.Equal("屏幕共享接收端未提供有效音频回执。", error.Message);
    }

    /// <summary>:returns: activate专属帧数必须合法，不能由accepted=true放行错误激活schema。</summary>
    [Fact]
    public void ActivationHasItsOwnStrictReceipt()
    {
        var accepted = "{\"accepted\":true,\"volume\":100,\"muted\":true,\"has_audio\":true,\"frames\":1}";
        Assert.True(ScreenShareAudioReceipt.Parse(accepted, 100, true, activation: true).Muted);
        Assert.Throws<IOException>(() => ScreenShareAudioReceipt.Parse(accepted.Replace("\"frames\":1", "\"frames\":-1", StringComparison.Ordinal), 100, true, activation: true));
        Assert.Throws<IOException>(() => ScreenShareAudioReceipt.Parse(new string(' ', 2049), 100, true));
    }

    /// <summary>:returns: 音频回执不增加帧、不激活预备画面；退役后清空实际属性且不能复活。</summary>
    [Fact]
    public void AudioReadbackCannotPromotePreparedOrRetiredResources()
    {
        var id = Guid.NewGuid();
        var address = $"http://127.0.0.1:5207/api/screen-share/receiver/{id:D}/?window_id=1&source_generation=7#{new string('a', 64)}";
        var state = new ScreenSharePlaybackState(ScreenShareReceiverIdentity.Parse(address, id, 1, 7));
        var receipt = new ScreenShareAudioReceipt(50, false, true);
        Assert.Throws<IOException>(() => state.RecordAudio(receipt));
        state.Accept(address, JsonSerializer.Serialize(new { type = "ready", share_id = id, window_id = 1, source_generation = 7, frames = 0 }));
        state.Accept(address, JsonSerializer.Serialize(new { type = "frame", share_id = id, window_id = 1, source_generation = 7, frames = 1, media_time = 1, activated = false, has_audio = true }));
        state.BeginActivation(); state.RecordAudio(receipt);
        Assert.False(state.IsPlaying);
        Assert.Equal(1, state.Frames);
        Assert.Equal(receipt, state.Audio);
        state.Retire();
        Assert.Null(state.Audio);
        Assert.Throws<IOException>(() => state.RecordAudio(receipt));
    }
}
