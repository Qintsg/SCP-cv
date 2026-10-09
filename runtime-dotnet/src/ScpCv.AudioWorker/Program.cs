// AudioWorker真实播放、结束通知及同执行循环的周期观测入口，不直接访问业务数据库。
using ScpCv.AudioWorker.Audio;
using ScpCv.AudioWorker.Publishing;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("AudioWorker 仅支持 Windows x64 播放主机。");
    return 2;
}

// 显式发布命令在播放适配器/运行组入口前退出，不创建音频播放器或连接业务管道。
if (args.FirstOrDefault() == "--generate-plugin-cache")
{
    try
    {
        if (args.Length != 2) throw new ArgumentException("插件索引生成需要当前发布包目录。");
        var result = VlcPluginCacheGenerator.Generate(args[1]);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
        return 0;
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
        or LibVLCSharp.Shared.VLCException or DllNotFoundException or BadImageFormatException)
    {
        Console.Error.WriteLine($"插件索引生成失败：{error.Message}");
        return 2;
    }
}

var pipeName = Option(args, "pipe-name");
var startGate = Option(args, "start-gate");
var instanceId = Guid.TryParse(Option(args, "instance-id"), out var parsedInstance)
    ? parsedInstance
    : Guid.NewGuid();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
await using var audio = new VlcAudioAdapter();
var executor = new AudioCommandExecutor(audio);

if (string.IsNullOrWhiteSpace(pipeName))
{
    Console.WriteLine("AudioWorker ready (standalone diagnostics)");
    try { await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token); } catch (OperationCanceledException) { }
    return 0;
}

await using var session = new RuntimeWorkerSession(pipeName, new RuntimeWorkerIdentity(
    "audio",
    instanceId,
    new IpcTargetDto { Kind = "audio", Id = 1 },
    ["libvlc", "audio_finished"]));
audio.Finished += (_, finished) => _ = NotifyFinishedAsync(session, finished, stop.Token);
try
{
    await RuntimeStartGate.WaitAsync(startGate, cancellationToken: stop.Token);
    await session.RunAsync(executor.ExecuteAsync, executor.SampleProgressAsync, stop.Token);
    return 0;
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    return 0;
}

static async Task NotifyFinishedAsync(
    RuntimeWorkerSession session,
    AudioFinishedEventArgs finished,
    CancellationToken cancellationToken)
{
    try
    {
        await session.SendAudioFinishedAsync(new AudioFinishedDto
        {
            EventId = finished.EventId,
            SourceId = finished.SourceId,
            SourceGeneration = finished.SourceGeneration,
        }, cancellationToken);
    }
    catch (Exception exception) when (exception is IOException or InvalidOperationException)
    {
        Console.Error.WriteLine($"AudioFinished 上报失败：{exception.Message}");
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // Worker 退出时取消尚未确认的自然结束事件，不让后台任务形成未观察异常。
    }
}

static string? Option(string[] values, string name)
{
    var prefix = $"--{name}=";
    var inline = values.FirstOrDefault(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (inline is not null) return inline[prefix.Length..];
    var index = Array.FindIndex(values, value => string.Equals(value, $"--{name}", StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}
