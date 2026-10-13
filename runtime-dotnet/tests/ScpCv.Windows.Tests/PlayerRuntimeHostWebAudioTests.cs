// 公开OPEN/CLOSE的网页保温声音生命周期；仅缓存夹具，不启动WebView、Office或网络。
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Windows.Controls;
using ScpCv.Contracts.Runtime;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    [Fact]
    public async Task CachedWebSwitchAndCloseMuteRetiredPageWithoutDiscardingCache()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); var host = new PlayerRuntimeHost(window, 1);
            var first = new List<bool>(); var second = new List<bool>();
            var one = WebLease(101, "https://example.invalid/one"); var two = WebLease(202, "https://example.invalid/two");
            SeedWeb(host, one, first.Add); SeedWeb(host, two, second.Add);
            try
            {
                await host.ExecuteAsync(one, CancellationToken.None);
                await host.ExecuteAsync(two, CancellationToken.None);
                Assert.Equal([true, false], first); Assert.Equal([true], second);
                await host.ExecuteAsync(two with { Command = "CLOSE" }, CancellationToken.None);
                Assert.Equal([true, false], second);
                await host.ExecuteAsync(one with { SourceGeneration = 3 }, CancellationToken.None);
                Assert.Equal([true, false, true], first);
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    [Fact]
    public async Task FailedWebUnmuteRestoresPriorSoundAndNeverPublishesCandidateIdentity()
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var window = new PlayerWindow(); var host = new PlayerRuntimeHost(window, 1);
            var prior = new List<bool>(); var disposed = 0;
            var one = WebLease(101, "https://example.invalid/one"); var two = WebLease(202, "https://example.invalid/two");
            SeedWeb(host, one, prior.Add); SeedWeb(host, two, _ => throw new IOException("有界声音激活故障"), () => disposed++);
            try
            {
                await host.ExecuteAsync(one, CancellationToken.None);
                await Assert.ThrowsAsync<IOException>(() => host.ExecuteAsync(two, CancellationToken.None));
                Assert.Equal([true, false, true], prior); Assert.Equal(1, disposed);
                Assert.Equal(101L, typeof(PlayerRuntimeHost).GetField("_sourceId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host));
            }
            finally { await host.DisposeAsync(); window.Close(); }
        });
    }

    private static ScpCv.Contracts.Ipc.CommandLeaseDto WebLease(long source, string uri)
    {
        var lease = OpenLease(CreatePackage());
        lease.Args["source_id"] = JsonSerializer.SerializeToElement(source);
        lease.Args["source_type"] = JsonSerializer.SerializeToElement("web");
        lease.Args["uri"] = JsonSerializer.SerializeToElement(uri);
        return lease;
    }
    private static void SeedWeb(PlayerRuntimeHost host, ScpCv.Contracts.Ipc.CommandLeaseDto lease, Action<bool> audible, Action? closed = null)
    {
        var type = typeof(PlayerRuntimeHost).GetNestedType("SurfaceResource", BindingFlags.NonPublic)!;
        Func<ValueTask> dispose = () => { closed?.Invoke(); return ValueTask.CompletedTask; };
        var resource = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: ["web", new Grid(), dispose, null, null, audible], culture: null)!;
        var cache = (IDictionary)typeof(PlayerRuntimeHost).GetField("_warmWebResources", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var key = $"{lease.Args["source_id"].GetInt64()}:{lease.SourceRevision}:{lease.Args["uri"].GetString()}";
        cache.Add(key, resource);
    }
}
