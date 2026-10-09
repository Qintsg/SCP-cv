// 发布专用CLI拒绝任意目录；真实自有子进程有界结束，不播放媒体或改系统设置。
using System.Diagnostics;

namespace ScpCv.Windows.Tests;

[Collection("PresentationCreationEnvironment")]
public sealed class VlcPluginCacheCommandTests
{
    /// <summary>:returns: 额外插件路径在加载引擎/写索引前被明确拒绝，不打印该路径值。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task CacheCommandRejectsExtraPluginSearchPath()
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ScpCv.AudioWorker.exe"))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true } };
        const string outside = "C:/not-this-package-private-plugin-path";
        process.StartInfo.Environment["VLC_PLUGIN_PATH"] = outside;
        process.StartInfo.ArgumentList.Add("--generate-plugin-cache"); process.StartInfo.ArgumentList.Add(AppContext.BaseDirectory);
        process.Start(); var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { process.Kill(); await process.WaitForExitAsync(); throw; }
        Assert.Equal(2, process.ExitCode); var error = await errors;
        Assert.Contains("额外插件搜索路径", error, StringComparison.Ordinal);
        Assert.DoesNotContain(outside, error, StringComparison.Ordinal); Assert.DoesNotContain("AudioWorker ready", await output, StringComparison.Ordinal);
    }

    /// <summary>:returns: 已存在索引不能被发布入口覆盖，即使它内容无效。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task CacheCommandNeverOverwritesExistingIndex()
    {
        var cache = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64", "plugins", "plugins.dat");
        var created = !File.Exists(cache);
        if (created) await File.WriteAllBytesAsync(cache, [1, 2, 3]);
        var before = await File.ReadAllBytesAsync(cache);
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "ScpCv.AudioWorker.exe"))
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true } };
            process.StartInfo.Environment.Remove("VLC_PLUGIN_PATH");
            process.StartInfo.ArgumentList.Add("--generate-plugin-cache"); process.StartInfo.ArgumentList.Add(AppContext.BaseDirectory);
            process.Start(); var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { process.Kill(); await process.WaitForExitAsync(); throw; }
            Assert.Equal(2, process.ExitCode); Assert.Contains("拒绝覆盖", await errors, StringComparison.Ordinal);
            Assert.Equal(before, await File.ReadAllBytesAsync(cache)); Assert.DoesNotContain("AudioWorker ready", await output, StringComparison.Ordinal);
        }
        finally { if (created) File.Delete(cache); }
    }

    /// <summary>:returns: 错误目录必须在创建播放适配器前明确退出，不误入常驻AudioWorker。</summary>
    [Fact]
    [Trait("Category", "Physical")]
    public async Task CacheCommandRejectsDirectoryOutsideCurrentPackageWithoutStartingWorker()
    {
        var worker = Path.Combine(AppContext.BaseDirectory, "ScpCv.AudioWorker.exe");
        Assert.True(File.Exists(worker), "测试输出须具备实际AudioWorker入口");
        using var process = new Process { StartInfo = new ProcessStartInfo(worker)
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true } };
        process.StartInfo.ArgumentList.Add("--generate-plugin-cache");
        process.StartInfo.ArgumentList.Add(Path.Combine(Path.GetTempPath(), "not-this-runtime-package"));
        process.Start(); var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        var timedOut = false;
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (TimeoutException) { timedOut = true; process.Kill(); await process.WaitForExitAsync(); }
        Assert.False(timedOut, "发布命令不能被当作常驻播放服务启动");
        Assert.Equal(2, process.ExitCode);
        Assert.Contains("当前发布包", await errors, StringComparison.Ordinal);
        Assert.DoesNotContain("AudioWorker ready", await output, StringComparison.Ordinal);
    }
}
