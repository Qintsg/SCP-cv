// 失联清理和尽力网络压力采样；不探测媒体库，不变更系统QoS或正式播放。
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.StreamPreview;

public sealed partial class PreviewMaintenance(StreamPreviewRegistry registry, MediaMtxPreviewClient relay,
    IDbContextFactory<ControlDbContext> contexts, PreviewOptions options, TimeProvider clock,
    ILogger<PreviewMaintenance> logger) : BackgroundService
{
    private readonly Dictionary<string, (long In, long Out, long Stamp)> _network = new(StringComparer.Ordinal);
    private readonly List<Task> _cleanups = [];
    private Task? _relayCleanup;
    private DateTimeOffset _lastReport;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                for (var index = _cleanups.Count - 1; index >= 0; index--)
                {
                    var task = _cleanups[index]; if (!task.IsCompleted) continue;
                    if (task.Exception is not null) Report("preview_cleanup_pending"); _cleanups.RemoveAt(index);
                }
                var playing = false; var busy = false;
                if (registry.LeaseCount > 0 || registry.FeedCount > 0)
                {
                    try
                    {
                        await using var database = await contexts.CreateDbContextAsync(stoppingToken).ConfigureAwait(false);
                        playing = await database.PlaybackSessions.AsNoTracking().AnyAsync(session =>
                            (session.PlaybackState == PlaybackState.Playing || session.PlaybackState == PlaybackState.Loading) &&
                            session.MediaSource != null && (session.MediaSource.SourceType == MediaSourceType.RtspStream ||
                                session.MediaSource.SourceType == MediaSourceType.SrtStream || session.MediaSource.SourceType == MediaSourceType.CustomStream), stoppingToken).ConfigureAwait(false);
                        busy = HostBusy();
                    }
                    catch (Exception error) when (error is Microsoft.Data.Sqlite.SqliteException or NetworkInformationException or NotSupportedException or InvalidOperationException or System.Security.SecurityException)
                    { busy = true; Report("preview_resource_observation_unavailable"); }
                }
                // 退出等待不堵住下一轮租约到期。每流只进入一次关闭，工作集合受上游数约束。
                _cleanups.Add(registry.SweepAsync(playing, busy));
                if (_relayCleanup is null || _relayCleanup.IsCompleted)
                {
                    if (_relayCleanup?.Exception is not null) Report("preview_relay_cleanup_pending");
                    _relayCleanup = relay.CleanupPendingAsync();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            try { await Task.WhenAll(_cleanups).ConfigureAwait(false); }
            catch (Exception error) when (error is PreviewException or IOException or System.ComponentModel.Win32Exception) { Report("preview_cleanup_pending"); }
            await registry.DisposeAsync().ConfigureAwait(false);
            if (_relayCleanup is not null) await _relayCleanup.ConfigureAwait(false);
            await relay.CleanupPendingAsync().ConfigureAwait(false);
        }
    }

    private bool HostBusy()
    {
        var busy = false;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up || adapter.Speed <= 0 ||
                adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            var stats = adapter.GetIPv4Statistics(); var stamp = clock.GetTimestamp();
            if (_network.TryGetValue(adapter.Id, out var previous))
            {
                var seconds = clock.GetElapsedTime(previous.Stamp, stamp).TotalSeconds;
                var bytes = Math.Max(stats.BytesReceived - previous.In, stats.BytesSent - previous.Out);
                if (seconds > 0 && bytes >= 0 && bytes * 8d / seconds > adapter.Speed * (options.NetworkUtilizationPercent / 100d)) busy = true;
            }
            _network[adapter.Id] = (stats.BytesReceived, stats.BytesSent, stamp);
        }
        return busy;
    }

    private void Report(string code)
    {
        if (clock.GetUtcNow() - _lastReport < TimeSpan.FromSeconds(30)) return;
        _lastReport = clock.GetUtcNow(); MaintenancePending(logger, code);
    }

    [LoggerMessage(EventId = 1180, Level = LogLevel.Warning, Message = "直播预览维护未完成：{Code}")]
    private static partial void MaintenancePending(ILogger logger, string code);
}
