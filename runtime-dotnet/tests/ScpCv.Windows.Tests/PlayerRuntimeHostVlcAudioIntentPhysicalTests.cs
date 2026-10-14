// 真实VLC窗口音频意图回归：自有全零PCM、隐藏STA、不写系统音量/默认输出、不访问D4。
using System.Text.Json;
using LibVLCSharp.Shared;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 仅准备媒体再显式播放、暂停恢复均沿用窗口声音，不因尚无原生输出丢失配置。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task PreparedVlcThenPlayAndResumeKeepAudioIntent()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["autoplay"] = JsonSerializer.SerializeToElement(false);
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "PLAY" }, CancellationToken.None);
                Assert.NotNull(player);
                // PLAY可返回loading；音量-1表示原生输出尚未创建，先核真实开始而不等待目标音量掩盖缺陷。
                var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
                while (player.State != VLCState.Playing && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
                Assert.Equal(VLCState.Playing, player.State);
                Assert.Equal(0, player.Volume);
                Assert.True(player.Mute);
                await host.ExecuteAsync(lease with { Command = "PAUSE" }, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "PLAY" }, CancellationToken.None);
                Assert.Equal(0, player.Volume);
                Assert.True(player.Mute);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 三次真实自然结束后自动循环仍保留零音量/静音，不以loop布尔代替重播。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task NaturalVlcLoopsKeepAudioIntent()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null; var ended = 0;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => { player = value; value.EndReached += (_, _) => Interlocked.Increment(ref ended); });
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture(1));
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                lease.Args["loop"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
                while (Volatile.Read(ref ended) < 3 && DateTimeOffset.UtcNow < deadline)
                {
                    Assert.NotNull(player);
                    // 原生停止/重建输出之间音量为-1（不可用），不能把该瞬间冒称已播放或配置丢失。
                    if (player.State == VLCState.Playing && player.Volume >= 0)
                    {
                        Assert.Equal(0, player.Volume);
                        Assert.True(player.Mute);
                    }
                    await Task.Delay(25);
                }
                Assert.True(Volatile.Read(ref ended) >= 3, "没有三次真实自然结束/重播证据。");
                while ((player!.State != VLCState.Playing || player.Volume < 0) && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
                Assert.Equal(VLCState.Playing, player!.State);
                Assert.Equal(0, player!.Volume);
                Assert.True(player.Mute);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 候选创建失败保留旧原生声音；后续无字段切源仍沿用旧成功配置。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task FailedVlcCandidateKeepsOldAudioAndSuccessfulDefaults()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null; var created = 0;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value =>
                {
                    if (++created == 2) throw new IOException("受控外部VLC候选创建失败。");
                    player = value;
                });
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(35);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                var old = player;
                var candidate = new Dictionary<string, JsonElement>(lease.Args)
                {
                    ["uri"] = JsonSerializer.SerializeToElement(CreateSilentAudioFixture()),
                    ["volume"] = JsonSerializer.SerializeToElement(0),
                    ["muted"] = JsonSerializer.SerializeToElement(false),
                };
                await Assert.ThrowsAsync<IOException>(() => host.ExecuteAsync(lease with { SourceGeneration = 2, Args = candidate }, CancellationToken.None));
                Assert.Same(old, player);
                Assert.Equal(35, player!.Volume);
                Assert.True(player.Mute);
                candidate.Remove("volume"); candidate.Remove("muted");
                await host.ExecuteAsync(lease with { SourceGeneration = 3, Args = candidate }, CancellationToken.None);
                Assert.NotSame(old, player);
                Assert.Equal(35, player!.Volume);
                Assert.True(player.Mute);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 畸形音量不能回退为默认100；创建原生候选前明确拒绝。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task MalformedVlcAudioIntentIsRejectedBeforeNativeCreation()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(101);
                await Assert.ThrowsAsync<InvalidDataException>(() => host.ExecuteAsync(lease, CancellationToken.None));
                Assert.Null(player);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: STOP后真实重播保留原零音量/静音，不能重新回到默认有声状态。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task VlcStopThenPlayKeepsAudioIntent()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                await host.ExecuteAsync(lease, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "STOP" }, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "PLAY" }, CancellationToken.None);
                Assert.NotNull(player);
                var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
                while ((player.Volume != 0 || !player.Mute) && DateTimeOffset.UtcNow < deadline) await Task.Delay(20);
                Assert.Equal(0, player.Volume);
                Assert.True(player.Mute);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 健康同源重开也应用新租约音频值，不只在新建MediaPlayer时生效。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task HealthyVlcReopenAppliesCurrentAudioIntent()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(35);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(false);
                await host.ExecuteAsync(lease, CancellationToken.None);
                for (var generation = 2; generation <= 21; generation++)
                {
                    var volume = generation % 2 == 0 ? 0 : 35;
                    var muted = generation % 2 == 0;
                    var args = new Dictionary<string, JsonElement>(lease.Args)
                    { ["volume"] = JsonSerializer.SerializeToElement(volume), ["muted"] = JsonSerializer.SerializeToElement(muted) };
                    await host.ExecuteAsync(lease with { SourceGeneration = generation, Args = args }, CancellationToken.None);
                    Assert.NotNull(player);
                    Assert.Equal(volume, player.Volume);
                    Assert.Equal(muted, player.Mute);
                }
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 空闲设置并非对空资源的假成功；旧OPEN无音频字段时也沿用已设置值。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task VlcOpenKeepsAudioConfiguredWhileIdle()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(CreateSilentAudioFixture());
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                await host.ExecuteAsync(lease with { Command = "SET_VOLUME", Args = new() { ["volume"] = JsonSerializer.SerializeToElement(0) } }, CancellationToken.None);
                await host.ExecuteAsync(lease with { Command = "SET_MUTE", Args = new() { ["muted"] = JsonSerializer.SerializeToElement(true) } }, CancellationToken.None);
                await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.NotNull(player);
                Assert.Equal(0, player.Volume);
                Assert.True(player.Mute);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: OPEN零音量/静音必须兑现于真实VLC，不能只存入HTTP配置。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task VlcOpenAppliesMutedZeroVolumeBeforePublishingResource()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var path = CreateSilentAudioFixture();
            var window = new PlayerWindow(); MediaPlayer? player = null;
            await using var host = new PlayerRuntimeHost(window, 1,
                libVlcFactory: () => new LibVLC("--no-video", "--vout=dummy"),
                configureVlcPlayer: value => player = value);
            try
            {
                var lease = OpenLease(path);
                lease.Args["source_type"] = JsonSerializer.SerializeToElement("audio");
                lease.Args["volume"] = JsonSerializer.SerializeToElement(0);
                lease.Args["muted"] = JsonSerializer.SerializeToElement(true);
                var result = await host.ExecuteAsync(lease, CancellationToken.None);
                Assert.Equal("completed", result.Status);
                Assert.NotNull(player);
                Assert.Equal(0, player.Volume);
                Assert.True(player.Mute);
                Assert.False(window.IsVisible);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    /// <summary>:returns: 自有有限全零PCM，默认音量也不产生可听内容，不使用用户媒体。</summary>
    private static string CreateSilentAudioFixture(int seconds = 8)
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-vlc-audio-intent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var path = Path.Combine(root, "silent.wav");
        using var writer = new BinaryWriter(File.Create(path)); const int rate = 48000; var count = rate * seconds;
        writer.Write("RIFF"u8); writer.Write(36 + count * 4); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((short)1); writer.Write((short)2); writer.Write(rate); writer.Write(rate * 4);
        writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write(count * 4);
        for (var index = 0; index < count; index++) writer.Write(0);
        return path;
    }
}
