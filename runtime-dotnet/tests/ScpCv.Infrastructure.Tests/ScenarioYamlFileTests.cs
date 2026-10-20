// YAML完整往返、目录移动、文件冲突和数据库提交之前的准备失败回归；独立临时目录。
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Scenarios;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Tests;

public sealed class ScenarioYamlFileTests
{
    [Fact]
    public async Task SaveReadEditMoveAndDeleteKeepYamlAndIndexConsistent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-yaml-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync(); using var writes = new WriteCoordinator(factory);
            var files = new ScenarioFileStore(root);
            var service = new ScenarioService(factory, writes, new CommandCoordinator(new CommandRepository(writes), new NullCommandWakeNotifier()), scenarioFiles: files);
            var folders = new ScenarioFolderService(factory, writes, files);
            var item = await service.CreateAsync(new("中文预案", "第一版", "unset", "single", "set", 0, [],
                Advanced: new() { CameraPositions = new Dictionary<string, int?> { ["rear"] = 0 } }));
            var path = Path.Combine(root, "scenarios", "中文预案.yaml");
            Assert.True(File.Exists(path));
            var document = ScenarioFileStore.ParseDocument(await File.ReadAllTextAsync(path));
            Assert.Equal(item.Id, document.Scenario.Id); Assert.Equal(0, document.Scenario.VolumeLevel);
            files.Verify(item); Assert.Null(item.Advanced.CameraPositions["rear"]);
            var folder = await folders.CreateAsync("活动", null);
            item = await service.UpdateAsync(item.Id, new(null, "第二版", null, null, null, null, null, folder.Id, true));
            files.Verify(item); Assert.False(File.Exists(path)); path = Path.Combine(root, "scenarios", "活动", "中文预案.yaml"); Assert.True(File.Exists(path));
            await folders.UpdateAsync(folder.Id, "新活动", null); item = await service.GetAsync(item.Id); files.Verify(item);
            await service.DeleteAsync(item.Id); Assert.False(File.Exists(Path.Combine(root, "scenarios", "新活动", "中文预案.yaml")));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExistingUnownedYamlRejectsBeforeDatabaseCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-yaml-conflict-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root); await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory); var files = new ScenarioFileStore(root);
            var service = new ScenarioService(factory, writes, new CommandCoordinator(new CommandRepository(writes), new NullCommandWakeNotifier()), scenarioFiles: files);
            Directory.CreateDirectory(Path.Combine(root, "scenarios")); var path = Path.Combine(root, "scenarios", "不可覆盖.yaml");
            await File.WriteAllTextAsync(path, "用户原文件");
            var exception = await Assert.ThrowsAsync<ScenarioServiceException>(() => service.CreateAsync(new("不可覆盖", "", "unset", "single", "unset", 100, [])));
            Assert.Equal("scenario_file_prepare_failed", exception.Code); Assert.Equal("用户原文件", await File.ReadAllTextAsync(path));
            await using var db = factory.CreateDbContext(); Assert.Empty(await db.Scenarios.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PublishFailureReportsCommittedIndexAndPreservesNewExternalConflict()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-yaml-publish-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root); await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory); var files = new ScenarioFileStore(root);
            var folder = await new ScenarioFolderService(factory, writes, files).CreateAsync("目录", null);
            var collision = Path.Combine(root, "scenarios", "目录");
            var projection = new AfterPrepare(files, () => File.WriteAllText(collision, "外部新文件"));
            var failure = await Assert.ThrowsAsync<ScenarioServiceException>(() => writes.ExecuteAsync(async (db, token) =>
            {
                db.Scenarios.Add(new Scenario { Name = "发布测试", FolderId = folder.Id }); await db.SaveChangesAsync(token);
            }, projection));
            Assert.Equal("scenario_file_publish_failed", failure.Code); Assert.Contains("索引已提交", failure.Message);
            Assert.Equal("外部新文件", await File.ReadAllTextAsync(collision));
            await using var database = factory.CreateDbContext(); Assert.Single(await database.Scenarios.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class AfterPrepare(ScenarioFileStore files, Action action) : IWriteProjection
    {
        public async Task<IPreparedWriteProjection> PrepareAsync(ControlDbContext database, CancellationToken cancellationToken)
        {
            var prepared = await files.PrepareAsync(database, cancellationToken); action(); return prepared;
        }
    }
}
