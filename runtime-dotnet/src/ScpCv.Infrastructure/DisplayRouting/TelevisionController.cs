// 电视显示专用单连接顺序发送；不操作电源，不把写出视作出画。
using System.Net;
using System.Net.Sockets;

namespace ScpCv.Infrastructure.DisplayRouting;

public interface ITelevisionController
{
    bool IsHardware { get; }
    Task DispatchAsync(string target, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken);
}

public sealed class SimulationTelevisionController : ITelevisionController
{
    public bool IsHardware => false;
    /// <summary>:param target: 已验证目标。:param frames: 已预检的完整序列。:param cancellationToken: 请求取消。</summary>
    public Task DispatchAsync(string target, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
    {
        _ = TelevisionTargets.Address(target); cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class TcpTelevisionController : ITelevisionController
{
    public bool IsHardware => true;
    /// <summary>:param target: 仅左右电视。:param frames: 6231后610D。:param cancellationToken: 整段取消，不自动重放部分序列。</summary>
    public async Task DispatchAsync(string target, IReadOnlyList<byte[]> frames, CancellationToken cancellationToken)
    {
        var address = TelevisionTargets.Address(target);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(IPAddress.Parse(address), 4830, timeout.Token).ConfigureAwait(false);
            await using var stream = client.GetStream();
            await WriteFramesAsync(stream, frames, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        { throw new DisplayRoutingException("电视画面控制未完整写出。", "display_routing_failed", exception); }
    }

    /// <summary>:param stream: 同一已连接流。:param frames: 已预检序列。:param token: 取消。:returns: 全部写完或抛出，不隐藏中间失败。</summary>
    public static async Task WriteFramesAsync(Stream stream, IReadOnlyList<byte[]> frames, CancellationToken token)
    {
        foreach (var frame in frames) await stream.WriteAsync(frame, token).ConfigureAwait(false);
    }
}

public static class TelevisionTargets
{
    public static IReadOnlyList<string> All { get; } = ["tv_left", "tv_right"];
    /// <summary>:param target: 精确白名单标识。:returns: 固定目标，不接受客户端指定IP。</summary>
    public static string Address(string target) => target switch
    {
        "tv_left" => "192.168.5.161", "tv_right" => "192.168.5.162",
        _ => throw new DisplayRoutingException("未知电视目标。"),
    };
}
