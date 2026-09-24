// 验证运行组代次、所有权与受控停机后的命令回收。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;

namespace ScpCv.Infrastructure.Tests;

public sealed class RuntimeAuthorityRepositoryTests
{
    [Fact]
    public async Task ConfirmedStopRetiresInFlightCommandBeforeNextStart()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var authority = new RuntimeAuthorityRepository(factory, writes);
            var commands = new CommandRepository(writes);
            var firstStart = await authority.BeginStartAsync(Guid.NewGuid());
            await authority.ArmAsync(firstStart.ExplicitStartRequestId!.Value, firstStart.GroupEpoch);
            await commands.EnqueueAsync(new EnqueueCommand(
                CommandTargetKind.Display, 2, "OPEN", "{}", SourceGeneration: 1, SourceRevision: 1));
            var inFlight = await commands.ClaimAsync(new ClaimCommand(
                CommandTargetKind.Display, 2, Guid.NewGuid(), 1, firstStart.GroupEpoch, TimeSpan.FromSeconds(30)));
            Assert.NotNull(inFlight);

            var draining = await authority.BeginDrainAsync("system_shutdown");
            await authority.CompleteStopAsync(draining.GroupEpoch);

            await using (var database = factory.CreateDbContext())
            {
                var retired = await database.CommandRecords.SingleAsync();
                Assert.Equal(CommandStatus.Superseded, retired.Status);
                Assert.Equal("runtime_group_stopped", retired.ResultCode);
                Assert.Null(retired.ClaimToken);
            }

            var secondStart = await authority.BeginStartAsync(Guid.NewGuid());
            await authority.ArmAsync(secondStart.ExplicitStartRequestId!.Value, secondStart.GroupEpoch);
            await commands.EnqueueAsync(new EnqueueCommand(
                CommandTargetKind.Display, 2, "OPEN", "{}", SourceGeneration: 2, SourceRevision: 1));
            var next = await commands.ClaimAsync(new ClaimCommand(
                CommandTargetKind.Display, 2, Guid.NewGuid(), 2, secondStart.GroupEpoch, TimeSpan.FromSeconds(30)));
            Assert.NotNull(next);
            Assert.Equal(CommandStatus.Processing, next.Status);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task RepeatedStopRetiresLegacyInFlightCommand()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var authority = new RuntimeAuthorityRepository(factory, writes);
            var commands = new CommandRepository(writes);
            var starting = await authority.BeginStartAsync(Guid.NewGuid());
            await authority.ArmAsync(starting.ExplicitStartRequestId!.Value, starting.GroupEpoch);
            await commands.EnqueueAsync(new EnqueueCommand(
                CommandTargetKind.Display, 2, "OPEN", "{}", SourceGeneration: 1, SourceRevision: 1));
            Assert.NotNull(await commands.ClaimAsync(new ClaimCommand(
                CommandTargetKind.Display, 2, Guid.NewGuid(), 1, starting.GroupEpoch, TimeSpan.FromSeconds(30))));
            var draining = await authority.BeginDrainAsync("system_shutdown");
            await writes.ExecuteAsync(async (database, token) =>
            {
                var group = await database.RuntimeGroupControls.SingleAsync(token);
                group.State = RuntimeGroupState.Stopped;
            });

            await authority.CompleteStopAsync(draining.GroupEpoch);

            await using var verification = factory.CreateDbContext();
            Assert.Equal(CommandStatus.Superseded, (await verification.CommandRecords.SingleAsync()).Status);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task GroupLatchRequiresExplicitStartAndFencesOnDrain()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var repository = new RuntimeAuthorityRepository(factory, writes);
            var requestId = Guid.NewGuid();

            var starting = await repository.BeginStartAsync(requestId);
            Assert.Equal(RuntimeGroupState.Starting, starting.State);
            await Assert.ThrowsAsync<RuntimeAuthorityException>(() =>
                repository.ArmAsync(Guid.NewGuid(), starting.GroupEpoch));

            var armed = await repository.ArmAsync(requestId, starting.GroupEpoch);
            Assert.Equal(RuntimeGroupState.Armed, armed.State);
            var draining = await repository.BeginDrainAsync("player exited");
            Assert.Equal(RuntimeGroupState.Draining, draining.State);
            Assert.Equal(armed.GroupEpoch + 1, draining.GroupEpoch);
            var stopped = await repository.CompleteStopAsync(draining.GroupEpoch);
            Assert.Equal(RuntimeGroupState.Stopped, stopped.State);

            var persisted = await repository.GetGroupAsync();
            Assert.Equal(RuntimeGroupState.Stopped, persisted.State);
            Assert.Null(persisted.ExplicitStartRequestId);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task FailedStartIsPersistedAndFencedByRequestAndGroupEpoch()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var repository = new RuntimeAuthorityRepository(factory, writes);
            var requestId = Guid.NewGuid();
            var starting = await repository.BeginStartAsync(requestId);

            var failed = await repository.FailStartAsync(requestId, starting.GroupEpoch, "worker_ready_timeout");
            Assert.Equal(RuntimeGroupState.Faulted, failed.State);
            Assert.Equal("worker_ready_timeout", failed.StopReason);
            Assert.Equal(requestId, failed.ExplicitStartRequestId);

            var duplicate = await repository.FailStartAsync(requestId, starting.GroupEpoch, "worker_ready_timeout");
            Assert.Equal(RuntimeGroupState.Faulted, duplicate.State);
            await Assert.ThrowsAsync<RuntimeAuthorityException>(() =>
                repository.FailStartAsync(Guid.NewGuid(), starting.GroupEpoch, "stale_request"));
            await Assert.ThrowsAsync<RuntimeAuthorityException>(() =>
                repository.FailStartAsync(requestId, starting.GroupEpoch + 1, "stale_epoch"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task WorkerOwnershipOnlyTransfersAfterConfirmedExit()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var repository = new RuntimeAuthorityRepository(factory, writes);
            var startId = Guid.NewGuid();
            var starting = await repository.BeginStartAsync(startId);
            await repository.ArmAsync(startId, starting.GroupEpoch);
            var processStart = DateTimeOffset.UtcNow;
            var firstRequest = new RegisterWorker(
                CommandTargetKind.Display,
                1,
                Guid.NewGuid(),
                ProcessId: 101,
                processStart,
                LogonSessionId: 3,
                starting.GroupEpoch,
                "[\"video\"]");

            var first = await repository.RegisterWorkerAsync(firstRequest);
            var same = await repository.RegisterWorkerAsync(firstRequest);
            Assert.Equal(first.OwnerEpoch, same.OwnerEpoch);
            var replacementRequest = firstRequest with
            {
                WorkerInstanceId = Guid.NewGuid(),
                ProcessId = 202,
                ProcessStartTime = processStart.AddSeconds(1),
            };
            await Assert.ThrowsAsync<RuntimeAuthorityException>(() =>
                repository.RegisterWorkerAsync(replacementRequest));

            var replacement = await repository.RegisterWorkerAsync(
                replacementRequest with { PreviousOwnerExitConfirmed = true });

            Assert.Equal(first.OwnerEpoch + 1, replacement.OwnerEpoch);
            Assert.False(await repository.RecordHeartbeatAsync(
                CommandTargetKind.Display,
                1,
                first.WorkerInstanceId,
                first.OwnerEpoch,
                uiProgress: true));
            Assert.True(await repository.RecordHeartbeatAsync(
                CommandTargetKind.Display,
                1,
                replacement.WorkerInstanceId,
                replacement.OwnerEpoch,
                uiProgress: true));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task OfficeOperationKeepsStableIdentityAndDeduplicatesResult()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var factory = await CreateInitializedFactoryAsync(root);
            using var writes = new WriteCoordinator(factory);
            var repository = new RuntimeAuthorityRepository(factory, writes);
            var startId = Guid.NewGuid();
            var starting = await repository.BeginStartAsync(startId);
            await repository.ArmAsync(startId, starting.GroupEpoch);
            var request = new RegisterOfficeOperation(
                Guid.NewGuid(),
                ParentCommandId: Guid.NewGuid(),
                ParentJobId: null,
                ClaimToken: Guid.NewGuid(),
                SourceGeneration: 9,
                starting.GroupEpoch,
                HostEpoch: 4,
                SlotEpoch: 6,
                DateTimeOffset.UtcNow.AddMinutes(1),
                "{\"operation\":\"next\"}");

            var registered = await repository.RegisterOfficeOperationAsync(request);
            var duplicateRegistration = await repository.RegisterOfficeOperationAsync(request);
            Assert.Equal(registered.Id, duplicateRegistration.Id);
            await Assert.ThrowsAsync<OfficeOperationConflictException>(() =>
                repository.RegisterOfficeOperationAsync(request with { RequestJson = "{\"operation\":\"previous\"}" }));

            var completion = new CompleteOfficeOperation(
                request.OperationId,
                request.HostEpoch,
                request.SlotEpoch,
                OperationStatus.Succeeded,
                "sha256:result",
                string.Empty);
            Assert.False((await repository.CompleteOfficeOperationAsync(completion)).Duplicate);
            Assert.True((await repository.CompleteOfficeOperationAsync(completion)).Duplicate);
            await Assert.ThrowsAsync<OfficeOperationConflictException>(() =>
                repository.CompleteOfficeOperationAsync(completion with { ResultFingerprint = "sha256:different" }));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static async Task<ControlDbContextFactory> CreateInitializedFactoryAsync(string root)
    {
        var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
        await new DatabaseInitializer(factory).InitializeAsync();
        return factory;
    }

    private static string CreateTemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), "scp-cv-tests", Guid.NewGuid().ToString("N"));

    private static void DeleteTemporaryRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
