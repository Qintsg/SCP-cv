// 本机回环 RTSP 协议夹具：服务器在线，但 DESCRIBE 路径不存在；永不发送音视频数据。
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScpCv.Windows.Tests;

internal sealed class RejectingRtspServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentBag<Task> _clients = [];
    private readonly Task _accepting;

    public RejectingRtspServer()
    {
        _listener.Start();
        Uri = $"rtsp://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/missing-{Guid.NewGuid():N}";
        _accepting = AcceptAsync();
    }

    public string Uri { get; }
    public TaskCompletionSource DescribeRejected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>端口由 OS 临时分配，仅接受回环请求；所有 handler 纳入收尾。</summary>
    private async Task AcceptAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                _clients.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (SocketException) when (_stopping.IsCancellationRequested) { }
    }

    /// <summary>OPTIONS 明确可达；DESCRIBE 明确 404，区分错误路径与服务器未监听。</summary>
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                while (!_stopping.IsCancellationRequested)
                {
                    var request = await reader.ReadLineAsync(_stopping.Token);
                    if (request is null) return;
                    var cseq = "1";
                    while (await reader.ReadLineAsync(_stopping.Token) is { Length: > 0 } header)
                    {
                        if (header.StartsWith("CSeq:", StringComparison.OrdinalIgnoreCase)) cseq = header[5..].Trim();
                    }
                    var describe = request.StartsWith("DESCRIBE ", StringComparison.Ordinal);
                    var response = describe
                        ? $"RTSP/1.0 404 Not Found\r\nCSeq: {cseq}\r\nContent-Length: 0\r\n\r\n"
                        : $"RTSP/1.0 200 OK\r\nCSeq: {cseq}\r\nPublic: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN\r\nContent-Length: 0\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stopping.Token);
                    await stream.FlushAsync(_stopping.Token);
                    if (describe) DescribeRejected.TrySetResult();
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
        }
    }

    /// <summary>先取消 socket 读和 accept，再等待所有自有任务结束并释放端口。</summary>
    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        await _accepting;
        await Task.WhenAll(_clients);
        _stopping.Dispose();
    }
}
