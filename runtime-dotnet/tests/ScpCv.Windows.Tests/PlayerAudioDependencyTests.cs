// 核编译后的真实Player依赖图，避免多TFM同名NAudio外壳在完整合包时冲突；不启动原生引擎。
using System.Text.Json;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed class PlayerAudioDependencyTests
{
    /// <summary>:returns: 播放器只要求WASAPI子包，不再要求多TFM外壳、ASIO/MIDI或WinForms音频控制。</summary>
    [Fact]
    public void PlayerUsesWasapiModuleWithoutFrameworkSpecificFacade()
    {
        var deps = Path.ChangeExtension(typeof(PlayerRuntimeHost).Assembly.Location, ".deps.json");
        using var document = JsonDocument.Parse(File.ReadAllText(deps));
        var names = document.RootElement.GetProperty("libraries").EnumerateObject().Select(item => item.Name).ToArray();
        Assert.Contains("NAudio.Wasapi/2.2.1", names);
        Assert.Contains("NAudio.Core/2.2.1", names);
        Assert.DoesNotContain(names, name => name.StartsWith("NAudio/", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("NAudio.WinForms/", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("NAudio.Asio/", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("NAudio.Midi/", StringComparison.Ordinal));
    }
}
