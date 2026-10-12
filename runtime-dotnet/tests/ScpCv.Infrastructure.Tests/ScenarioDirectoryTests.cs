// 预设目录同名、循环、非空删除及公开路径回归。
using Microsoft.Data.Sqlite;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Scenarios;

namespace ScpCv.Infrastructure.Tests;

public sealed class ScenarioDirectoryTests
{
    [Fact]
    public async Task FoldersRejectCyclesDuplicatesAndNonemptyDeletion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-scenario-dir-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var folders = new ScenarioFolderService(factory, writes);
            var parent = await folders.CreateAsync("演讲", null);
            var child = await folders.CreateAsync("开场", parent.Id);
            Assert.Equal("/演讲/开场", child.RelativePath);
            await Assert.ThrowsAsync<ScenarioServiceException>(() => folders.CreateAsync("演讲", null));
            await Assert.ThrowsAsync<ScenarioServiceException>(() => folders.UpdateAsync(parent.Id, "演讲", child.Id));
            await Assert.ThrowsAsync<ScenarioServiceException>(() => folders.DeleteAsync(parent.Id));
            await folders.DeleteAsync(child.Id);
            await folders.UpdateAsync(parent.Id, "结束", null);
            Assert.Equal("/结束", Assert.Single(await folders.ListAsync()).RelativePath);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
