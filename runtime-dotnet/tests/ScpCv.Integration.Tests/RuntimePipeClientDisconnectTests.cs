// Named Pipe对端在完整请求后立即断开：写入/reader退出竞态必须保留可重连传输错误。
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.Integration.Tests;

public sealed class RuntimePipeClientDisconnectTests
{
    [Fact]
    public async Task ImmediatePeerDisconnectDuringExchangeReportsIOException()
    {
        for (var round = 0; round < 4096; round++)
        {
            var pipeName = $"scp-cv-client-disconnect-{Guid.NewGuid():N}";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = Task.Run(async () =>
            {
                await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(timeout.Token);
                _ = await IpcFrameCodec.ReadPayloadAsync(server, TimeSpan.FromSeconds(5), timeout.Token);
            }, timeout.Token);
            await using var client = new RuntimePipeClient(pipeName);
            await client.ConnectAsync(timeout.Token);
            var error = await Record.ExceptionAsync(async () => await client.ExchangeAsync(new IpcFrameDto
            {
                MessageType = "audio_finished", MessageId = Guid.NewGuid(), InstanceId = Guid.NewGuid(),
                Payload = JsonSerializer.SerializeToElement(new { event_id = Guid.NewGuid() }),
            }, timeout.Token));
            await serverTask;
            Assert.IsAssignableFrom<IOException>(error);
        }
    }
}
