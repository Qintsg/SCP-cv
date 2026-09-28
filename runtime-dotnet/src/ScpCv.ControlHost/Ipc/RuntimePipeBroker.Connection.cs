// 管道连接只管理传输寿命；原生进程退出观察不随连接断开而取消。
using System.IO.Pipes;
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Domain.Model;

namespace ScpCv.ControlHost.Ipc;

public sealed partial class RuntimePipeBroker
{
    private sealed class RuntimeConnection(
        NamedPipeServerStream stream,
        RegisteredProcessIdentity identity,
        (CommandTargetKind Kind, int Id)? target,
        long ownerEpoch,
        long groupEpoch) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        public RegisteredProcessIdentity Identity { get; } = identity;
        public (CommandTargetKind Kind, int Id)? Target { get; } = target;
        public long OwnerEpoch { get; } = ownerEpoch;
        public long GroupEpoch { get; } = groupEpoch;

        /// <summary>串行发送完整协议帧。</summary>
        public async Task SendAsync(IpcFrameDto frame, CancellationToken cancellationToken)
        {
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await IpcFrameCodec.WritePayloadAsync(stream, JsonSerializer.SerializeToUtf8Bytes(frame), cancellationToken).ConfigureAwait(false);
            }
            finally { _sendGate.Release(); }
        }

        /// <summary>关闭传输资源，不推断受管进程状态。</summary>
        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            _sendGate.Dispose();
        }
    }
}
