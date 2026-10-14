// 真实回环RTSP外部协议夹具：记录第一条SETUP传输，不发送媒体或访问现场设备。
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScpCv.Windows.Tests;

internal sealed class RecordingRtspTransportServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serve;
    private TcpClient? _client;
    private readonly TaskCompletionSource<string> _transport = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Uri Url { get; }
    internal Task<string> Transport => _transport.Task;
    /// <summary>:returns: 系统动态分配的本机回环地址与独立记录任务。</summary>
    internal RecordingRtspTransportServer()
    {
        _listener.Start(); Url = new Uri($"rtsp://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/quiet/");
        _serve = ServeAsync();
    }
    /// <summary>:returns: 原生客户端的独立SETUP请求；SDP只有无声PCMA声明，不伪造音频/帧完成。</summary>
    private async Task ServeAsync()
    {
        try
        {
            using var client = await _listener.AcceptTcpClientAsync(_stop.Token); _client = client;
            using var stream = client.GetStream(); using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
            while (!_stop.IsCancellationRequested)
            {
                var request = await reader.ReadLineAsync(_stop.Token); if (request is null) break;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadLineAsync(_stop.Token) is { Length: > 0 } line)
                {
                    var separator = line.IndexOf(':'); if (separator > 0) headers[line[..separator]] = line[(separator + 1)..].Trim();
                }
                var method = request.Split(' ')[0]; var body = ""; var extra = "";
                if (method == "DESCRIBE")
                {
                    body = "v=0\r\no=- 1 1 IN IP4 127.0.0.1\r\ns=quiet transport fixture\r\nc=IN IP4 127.0.0.1\r\nt=0 0\r\na=control:*\r\nm=audio 0 RTP/AVP 8\r\na=rtpmap:8 PCMA/8000\r\na=control:trackID=0\r\n";
                    extra = $"Content-Type: application/sdp\r\nContent-Base: {Url}\r\n";
                }
                else if (method == "SETUP")
                {
                    var transport = headers["Transport"]; _transport.TrySetResult(transport);
                    extra = $"Transport: {transport}\r\nSession: fixture\r\n";
                }
                else if (method is "PLAY" or "GET_PARAMETER") extra = "Session: fixture\r\n";
                else if (method == "OPTIONS") extra = "Public: OPTIONS, DESCRIBE, SETUP, PLAY, TEARDOWN\r\n";
                var response = $"RTSP/1.0 200 OK\r\nCSeq: {headers["CSeq"]}\r\n{extra}Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n\r\n{body}";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
                if (method == "TEARDOWN") break;
            }
        }
        catch (Exception error) when (_stop.IsCancellationRequested && error is OperationCanceledException or IOException or SocketException or ObjectDisposedException) { }
    }
    /// <summary>:returns: 仅释放本轮回环监听/连接与任务，不操作外部进程。</summary>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync(); _client?.Dispose(); _listener.Stop();
        try { await _serve; } finally { _stop.Dispose(); }
    }
}
