// 只有当前认证停机准备才能接受正常成员退出；异常退出与旧代次永远不能被停机掩盖。
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.Supervisor.Runtime;

namespace ScpCv.Windows.Tests;

public sealed class SupervisorStopFenceTests
{
    [Fact]
    public void NormalExitIsUnexpectedUntilCurrentStopPreparationWasAccepted()
    {
        var fence = new SupervisorStopFence(8, 12);
        Assert.True(fence.IsUnexpectedExit(0));
        Assert.True(fence.TryPrepare(Notice(8, 12)));
        Assert.False(fence.IsUnexpectedExit(0));
        Assert.True(fence.IsUnexpectedExit(1));
        Assert.True(fence.IsUnexpectedExit(-1));
    }

    [Theory]
    [InlineData(7, 12)]
    [InlineData(8, 11)]
    [InlineData(8, 13)]
    public void ForeignOwnerOrGroupNeverAuthorizesNormalExit(long owner, long group)
    {
        var fence = new SupervisorStopFence(8, 12);
        Assert.False(fence.TryPrepare(Notice(owner, group)));
        Assert.True(fence.IsUnexpectedExit(0));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"reason\":\"stop\",\"group_epoch\":12,\"shutdown_id\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"reason\":\"stop\",\"group_epoch\":\"12\",\"shutdown_id\":\"abc\"}")]
    public void MalformedPreparationDoesNotChangeExitPolicy(string json)
    {
        var fence = new SupervisorStopFence(8, 12);
        Assert.False(fence.TryPrepare(new IpcFrameDto { MessageType = "shutdown_request", OwnerEpoch = 8, Payload = JsonDocument.Parse(json).RootElement.Clone() }));
        Assert.True(fence.IsUnexpectedExit(0));
    }

    [Fact]
    public void OnlySameNonceCanRepeatPreparation()
    {
        var fence = new SupervisorStopFence(8, 12); var frame = Notice(8, 12);
        Assert.True(fence.TryPrepare(frame)); Assert.True(fence.TryPrepare(frame));
        Assert.False(fence.TryPrepare(Notice(8, 12)));
    }

    [Theory]
    [InlineData(13, "draining", true)]
    [InlineData(13, "stopped", true)]
    [InlineData(12, "draining", false)]
    [InlineData(14, "draining", false)]
    [InlineData(13, "armed", false)]
    public void OnlyImmediateRetirementEpochMayKeepHeartbeatWhileAwaitingPreparation(long epoch, string state, bool allowed)
    {
        var fence = new SupervisorStopFence(8, 12);
        Assert.Equal(allowed, fence.AllowsDrainingHeartbeat(JsonSerializer.SerializeToElement(new { group_epoch = epoch, group_state = state })));
        Assert.True(fence.IsUnexpectedExit(0)); // 心跳状态不代替显式nonce准备。
    }

    private static IpcFrameDto Notice(long owner, long group) => new()
    {
        MessageType = "shutdown_request", OwnerEpoch = owner,
        Payload = JsonSerializer.SerializeToElement(new { reason = "stop", group_epoch = group, shutdown_id = Guid.NewGuid() }),
    };
}
