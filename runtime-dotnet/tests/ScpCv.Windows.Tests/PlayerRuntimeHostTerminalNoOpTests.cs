// 公开Player与正式LibreOffice adapter的末页noop桥接回归；外部stdio夹具、不启动Office或显示窗口。
using ScpCv.Contracts.Ipc;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:returns: 原生已确认的noop是completed信息边界，实际源/页/playing保持，不误降播放错误。</summary>
    [Theory]
    [InlineData("NEXT_EFFECT")]
    [InlineData("NEXT")]
    public Task TerminalNoOpKeepsPlayerStateAndReturnsBoundaryCode(string command) => RunCloseDiagnosticAsync("terminal-noop", async host =>
    {
        var result = await host.ExecuteAsync(new CommandLeaseDto
        {
            CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = command, TargetSequence = 2,
            GroupEpoch = 1, OwnerEpoch = 1, SourceRevision = 1, SourceGeneration = 1,
        }, CancellationToken.None);
        Assert.Equal("completed", result.Status);
        Assert.Equal("presentation_at_last_slide", result.ResultCode);
        Assert.Equal(101, result.ActualState.GetProperty("source_id").GetInt64());
        Assert.Equal("playing", result.ActualState.GetProperty("playback_state").GetString());
        Assert.Equal(1, result.ActualState.GetProperty("current_slide").GetInt32());
        Assert.Equal(1, result.ActualState.GetProperty("total_slides").GetInt32());
        Assert.Equal(string.Empty, result.ActualState.GetProperty("error_message").GetString());
        var previous = await host.ExecuteAsync(new CommandLeaseDto
        {
            CommandId = Guid.NewGuid(), ClaimToken = Guid.NewGuid(), Command = "PREVIOUS_EFFECT", TargetSequence = 3,
            GroupEpoch = 1, OwnerEpoch = 1, SourceRevision = 1, SourceGeneration = 1,
        }, CancellationToken.None);
        Assert.Equal("completed", previous.Status);
        Assert.Equal("ok", previous.ResultCode);
        Assert.Equal("playing", previous.ActualState.GetProperty("playback_state").GetString());
    });
}
