// 音频SEEK经公开服务→真实SQLite→公开租约读取，核目标/源代次与合并，不替代实际媒体定位。
using System.Text.Json;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Audio;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Runtime;
using ScpCv.Integration.Tests.Fixtures;

namespace ScpCv.Integration.Tests;

public sealed partial class BackgroundAudioPersistenceTests
{
    /// <summary>:returns: 位置信息完整保留int64，排队SEEK合并不重开/改源代次，未执行位置不伪造。</summary>
    [Fact]
    public async Task SeekLeasePreservesTargetAndGenerationWithoutReopeningSource()
    {
        await using var fixture = await ControlHostFixture.CreateAsync();
        var (first, _) = await SeedAudioSourcesAsync(fixture);
        var service = new BackgroundAudioService(fixture.Database, fixture.Writes,
            new CommandCoordinator(fixture.Commands, new NullCommandWakeNotifier()), fixture.TimeProvider);
        var start = await fixture.RuntimeAuthority.BeginStartAsync(Guid.NewGuid());
        await fixture.RuntimeAuthority.ArmAsync(start.ExplicitStartRequestId!.Value, start.GroupEpoch);
        var worker = Guid.NewGuid();
        var registered = await fixture.RuntimeAuthority.RegisterWorkerAsync(new RegisterWorker(
            CommandTargetKind.Audio, 1, worker, 11, DateTimeOffset.UtcNow, 1, start.GroupEpoch, "[\"audio\"]"));
        var claim = new ClaimCommand(CommandTargetKind.Audio, 1, worker, registered.OwnerEpoch, start.GroupEpoch, TimeSpan.FromSeconds(30));
        await service.PlaySourceAsync(first);
        var opened = Assert.IsType<CommandRecord>(await fixture.Commands.ClaimAsync(claim));
        await fixture.Commands.CompleteAsync(new CompleteCommand(opened.CommandId, opened.ClaimToken!.Value,
            registered.OwnerEpoch, CommandStatus.Completed, "ok", "", "{}"));
        await service.SeekAsync(1000);
        var accepted = await service.SeekAsync(3000000000L);
        Assert.Equal(first, accepted.State.SourceId);
        Assert.Equal(0, accepted.State.PositionMs);
        Assert.Equal("SEEK", accepted.State.PendingCommand);
        var seek = Assert.IsType<CommandRecord>(await fixture.Commands.ClaimAsync(claim));
        Assert.Equal("SEEK", seek.Command);
        Assert.Equal(opened.SourceGeneration, seek.SourceGeneration);
        using var args = JsonDocument.Parse(seek.ArgsJson);
        Assert.Equal(3000000000L, args.RootElement.GetProperty("position_ms").GetInt64());
    }
}
