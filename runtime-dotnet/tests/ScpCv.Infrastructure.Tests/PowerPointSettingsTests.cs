// PowerPoint 原生放映默认关闭且实验开关跨控制端持久化。
using Microsoft.Data.Sqlite;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.Infrastructure.Tests;

public sealed class PowerPointSettingsTests
{
    [Fact]
    public async Task NativeSlideShowRemainsOffUntilExplicitlyEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-ppt-settings-{Guid.NewGuid():N}");
        try
        {
            var factory = new ControlDbContextFactory(new DataRootOptions { RootPath = root }, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var settings = new PowerPointSettingsService(factory, writes);

            Assert.False(await settings.GetAsync());
            await settings.SetAsync(true);
            Assert.True(await new PowerPointSettingsService(factory, writes).GetAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-ppt-settings-", StringComparison.Ordinal) &&
                Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }
}
