using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 回环假节点集合：在每个视频墙节点地址的真实端口上监听，把收到的控制包原样记下来。
/// 配合 192.168.5.101~150 的本机别名，真实 TCP 下发路径（<see cref="TcpVideoWallTransport"/>
/// 加控制器）就能在开发机上跑完整序列，把超时、重试、中止这些行为测出实测数字。
/// </summary>
internal sealed class FakeVideoWallNodes : IAsyncDisposable
{
    private readonly FakeVideoWallNode[] _nodes;

    private FakeVideoWallNodes(FakeVideoWallNode[] nodes) => _nodes = nodes;

    /// <summary>在给定地址上起监听。绑不上直接抛：静默少一个节点会让「每节点都收齐」的断言变成假证据。</summary>
    public static async Task<FakeVideoWallNodes> StartAsync(IEnumerable<string> ips)
    {
        var nodes = new List<FakeVideoWallNode>();
        try
        {
            foreach (var ip in ips)
            {
                var node = new FakeVideoWallNode(ip, VideoWallSequenceBuilder.TcpPort);
                node.Start();
                nodes.Add(node);
            }
        }
        catch (SocketException exception)
        {
            foreach (var node in nodes) await node.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException(
                $"回环节点监听失败，先运行 {VideoWallLoopbackAliases.SetupCommand} 挂好别名。",
                exception);
        }

        return new FakeVideoWallNodes([.. nodes]);
    }

    public FakeVideoWallNode[] Nodes => _nodes;

    /// <summary>等到每个节点都收满 <paramref name="packetsPerNode"/> 个包，或超时；返回还没收满的节点描述。</summary>
    public async Task<string[]> WaitForAsync(int packetsPerNode, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var shortOf = _nodes
                .Where(node => node.Received.Length < packetsPerNode)
                .Select(node => $"{node.Ip}({node.Received.Length})")
                .ToArray();
            if (shortOf.Length == 0 || stopwatch.Elapsed >= timeout) return shortOf;
            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 静默窗：取消之后的用例没有「应该收到几个」可等，只能给在途连接一点时间落库再断言。
    /// </summary>
    public static Task SettleAsync() => Task.Delay(200);

    public async ValueTask DisposeAsync()
    {
        foreach (var node in _nodes) await node.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// 单个假节点。每个控制包一条连接（客户端发完即关），所以读到 0 就表示这一包结束；
/// 连接之间不能互相等，否则阶段内的 25 并发会被这里串行化。
/// </summary>
internal sealed class FakeVideoWallNode : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentQueue<byte[]> _received = new();
    private readonly ConcurrentBag<Task> _reads = [];
    private Task? _acceptLoop;

    public FakeVideoWallNode(string ip, int port)
    {
        Ip = ip;
        _listener = new TcpListener(IPAddress.Parse(ip), port);
    }

    public string Ip { get; }

    /// <summary>按到达顺序记下的控制包字节。</summary>
    public byte[][] Received => [.. _received];

    public void Start()
    {
        _listener.Start();
        _acceptLoop = AcceptAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        if (_acceptLoop is { } loop) await loop.ConfigureAwait(false);
        await Task.WhenAll(_reads).ConfigureAwait(false);
        _cancellation.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _reads.Add(ReadAsync(client));
        }
    }

    private async Task ReadAsync(TcpClient client)
    {
        using (client)
        {
            var buffer = new byte[4096];
            var total = 0;
            try
            {
                var stream = client.GetStream();
                while (total < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(total), _cancellation.Token).ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                }
            }
            catch (Exception exception)
                when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
            {
                // 对端被取消或重置：已经读到的部分照记。半包由断言去发现，不在这里吞掉。
            }

            if (total > 0) _received.Enqueue(buffer[..total]);
        }
    }
}
