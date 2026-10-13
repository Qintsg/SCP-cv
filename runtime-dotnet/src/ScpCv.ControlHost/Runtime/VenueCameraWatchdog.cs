// 云台看门狗不依赖HTTP寿命；正常停机仍以独立预算释放自有运动。
using ScpCv.Infrastructure.Devices;

namespace ScpCv.ControlHost.Runtime;

public sealed class VenueCameraWatchdog(VenueCameraController cameras, ILogger<VenueCameraWatchdog> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> StopWarning = LoggerMessage.Define(LogLevel.Warning,
        new EventId(7301, "CameraStopPending"), "云台停止未确认；继续安全释放，不重发运动。");
    private static readonly Action<ILogger, Exception?> ShutdownError = LoggerMessage.Define(LogLevel.Error,
        new EventId(7302, "CameraShutdownStopPending"), "停机云台停止帧未确认，需现场检查。");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                try { await cameras.SweepAsync().ConfigureAwait(false); }
                catch (VenueDeviceException exception) { StopWarning(logger, exception); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            try { await cameras.StopAllAsync().ConfigureAwait(false); }
            catch (VenueDeviceException exception) { ShutdownError(logger, exception); }
        }
    }
}
