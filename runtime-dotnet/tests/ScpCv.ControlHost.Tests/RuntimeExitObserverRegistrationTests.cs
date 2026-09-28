// 真 Host 的 Hardware 装配必须提供原生只读观察器，不能靠 Simulation/构造默认值掩盖漏接线。
using Microsoft.Extensions.DependencyInjection;
using ScpCv.ControlHost.Ipc;

namespace ScpCv.ControlHost.Tests;

public sealed class RuntimeExitObserverRegistrationTests
{
    /// <summary>解析生产注册的 observer/broker；不启动 Supervisor、Office 或实体设备。</summary>
    [Fact]
    public void HardwareHostRegistersNativeProcessObserverWithRuntimeBroker()
    {
        using var factory = new ControlHostApplicationFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("SafetyMode", "Hardware"));
        Assert.IsType<WindowsRuntimeProcessExitObserver>(factory.Services.GetRequiredService<IRuntimeProcessExitObserver>());
        Assert.NotNull(factory.Services.GetRequiredService<RuntimePipeBroker>());
    }
}
