// 监听当前认证ControlHost的停机准备；先立围栏，再确认nonce，不能将任意通知视为正常停止。
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;

namespace ScpCv.Supervisor.Runtime;

public static class SupervisorShutdownReceiver
{
    /// <summary>:param client: 原认证连接。:param instanceId: 原Supervisor身份。:param ownerEpoch: Welcome身份。:param fence: 原组围栏。:param token: 监控寿命。:returns: 只在当前准备确认被Host接纳后完成。</summary>
    public static async Task WaitAsync(RuntimePipeClient client, Guid instanceId, long ownerEpoch,
        SupervisorStopFence fence, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fence);
        await foreach (var frame in client.ReadUnsolicitedAsync(token).ConfigureAwait(false))
        {
            if (!fence.TryPrepare(frame)) continue;
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            budget.CancelAfter(TimeSpan.FromSeconds(3));
            var receipt = await client.ExchangeAsync(new IpcFrameDto
            {
                MessageType = "shutdown_prepared", MessageId = Guid.NewGuid(), InstanceId = instanceId, OwnerEpoch = ownerEpoch,
                Payload = JsonSerializer.SerializeToElement(new { shutdown_id = frame.Payload.GetProperty("shutdown_id").GetGuid() }),
            }, budget.Token).ConfigureAwait(false);
            if (receipt.MessageType != "shutdown_prepared_accepted" ||
                !receipt.Payload.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True)
                throw new InvalidOperationException("ControlHost未接纳当前Supervisor停机准备，不能假报正常停止。");
            return;
        }
        token.ThrowIfCancellationRequested();
        throw new IOException("Supervisor停机通知流意外结束。");
    }
}
