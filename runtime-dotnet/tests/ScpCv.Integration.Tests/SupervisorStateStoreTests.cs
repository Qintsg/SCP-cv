// Supervisor 状态文件的跨进程归属和原子发布回归。
using ScpCv.Supervisor.Runtime;

namespace ScpCv.Integration.Tests;

public sealed class SupervisorStateStoreTests
{
    [Fact]
    public void OldSupervisorCannotDeleteNewGroupsState()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scp-cv-state-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "runtime-processes.json");
        var oldGroup = new[] { new SupervisorProcessState("player-1", 111, DateTimeOffset.UtcNow, 1) };
        var newGroup = new[] { new SupervisorProcessState("player-1", 222, oldGroup[0].StartTime.AddSeconds(1), 1) };
        try
        {
            SupervisorStateStore.Write(path, oldGroup);
            SupervisorStateStore.Write(path, newGroup);

            Assert.False(SupervisorStateStore.DeleteIfMatches(path, oldGroup));
            Assert.Equal(newGroup, SupervisorStateStore.Read(path));
            Assert.True(SupervisorStateStore.DeleteIfMatches(path, newGroup));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProcessStartTimeIsPartOfDeletionIdentity()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scp-cv-state-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "runtime-processes.json");
        var oldGroup = new[] { new SupervisorProcessState("audio", 123, DateTimeOffset.UtcNow, 1) };
        var reusedPid = new[] { oldGroup[0] with { StartTime = oldGroup[0].StartTime.AddSeconds(1) } };
        try
        {
            SupervisorStateStore.Write(path, reusedPid);

            Assert.False(SupervisorStateStore.DeleteIfMatches(path, oldGroup));
            Assert.Equal(reusedPid, SupervisorStateStore.Read(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
