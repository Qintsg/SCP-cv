// SCP-cv总线63的只读运行预检；不改声音策略/端点/音量，不以设置字符串替代实际活动端点与引擎。
using NAudio.CoreAudioApi;
using ScpCv.Contracts.Runtime;
using ScpCv.Infrastructure.Devices;
using ScpCv.Runtime.Audio;

namespace ScpCv.ControlHost.Runtime;

public sealed class WindowsVenueSoftwareAudioBinding(VenueOptions options) : IVenueSoftwareAudioBinding
{
    /// <summary>:param token: 查询寿命。:returns: 窄范围本机配置与实际端点/私有映像/ABI检查，异常明确不可用。</summary>
    public Task<string> UnavailableReasonAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            if (!OperatingSystem.IsWindows()) return Task.FromResult("软件声音绑定只支持Windows。");
            var endpoint = Environment.GetEnvironmentVariable(AudioOutputBinding.EnvironmentVariable);
            if (string.IsNullOrWhiteSpace(options.SoftwareAudioOutputDeviceId) ||
                !AudioOutputBinding.Matches(options.SoftwareAudioOutputDeviceId, endpoint ?? string.Empty))
                return Task.FromResult("启动声音输出未显式绑定已确认的63号总线。");
            using var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            try { _ = AudioOutputBinding.Resolve(endpoint, devices.Select(device => device.ID)); }
            finally { foreach (var device in devices) device.Dispose(); }
            for (var window = 1; window <= 2; window++)
            { _ = PrivateAudioEnginePaths.WebView(window, endpoint); _ = PrivateAudioEnginePaths.Office(window, endpoint); }
            using var policy = new NativeApplicationAudioPolicy(); // 仅验证已知ABI与接口可用，不执行Bind。
            return Task.FromResult(string.Empty);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or
            NotSupportedException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        { return Task.FromResult("软件声音绑定预检失败：" + error.GetType().Name + "。请检查活动端点和项目专用引擎。"); }
    }
}
