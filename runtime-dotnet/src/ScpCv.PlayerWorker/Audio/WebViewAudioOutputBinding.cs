// 网页声音先用零采样自有流登记实际工具进程，绑定并核WASAPI后才允许加载用户内容。
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ScpCv.Contracts.Runtime;
using ScpCv.Runtime.Audio;

namespace ScpCv.PlayerWorker.Audio;

internal static class WebViewAudioOutputBinding
{
    private static readonly string Silence = CreateSilence();

    /// <summary>:param windowId: 唯一大屏窗口。:param endpoint: 可信端点。:returns: 明确配置时必须使用项目专用副本。</summary>
    internal static string? Browser(int windowId, string? endpoint) => PrivateAudioEnginePaths.WebView(windowId, endpoint);

    /// <summary>:param core: 未加载用户内容的自有控件。:param environment: 自有环境。:param browser: 私有浏览器。:param endpoint: 明确输出。:param token: 原OPEN寿命。</summary>
    internal static async Task PrepareAsync(CoreWebView2 core, CoreWebView2Environment environment, string? browser,
        string? endpoint, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        if (browser is null) throw new InvalidOperationException("缺少网页声音专用引擎，未使用默认设备。");
        var expected = ValidateEndpoint(endpoint);
        var originalMuted = core.IsMuted;
        core.IsMuted = false; // 只有零采样自有暖机；IsMuted会抑制声音进程登记，不能用它暖机。
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Loaded(object? _, CoreWebView2DOMContentLoadedEventArgs __) => ready.TrySetResult();
        core.DOMContentLoaded += Loaded;
        try
        {
            core.NavigateToString("<html><body><audio id='scp-silent-prime' loop src='data:audio/wav;base64," + Silence + "'></audio></body></html>");
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            await StartSilenceAsync(core, "document.querySelector('#scp-silent-prime').play()", token);
            uint audioPid = 0;
            var budget = Stopwatch.StartNew();
            while (budget.Elapsed < TimeSpan.FromSeconds(5))
            {
                token.ThrowIfCancellationRequested();
                var owned = environment.GetProcessInfos().Select(info => checked((uint)info.ProcessId)).ToHashSet();
                var active = Sessions(owned).Where(session => session.Active).ToArray();
                if (active.Length > 0)
                {
                    var pids = active.Select(session => session.Pid).Distinct().ToArray();
                    if (pids.Length != 1) throw new InvalidOperationException("网页声音进程归属不唯一，拒绝猜测绑定。");
                    audioPid = pids[0]; break;
                }
                await Task.Delay(50, token);
            }
            if (audioPid == 0) throw new TimeoutException("网页零音流未登记实际声音进程，未加载用户内容。");
            using (var policy = new NativeApplicationAudioPolicy())
                policy.Bind(audioPid, Path.Combine(browser, "msedgewebview2.exe"), PrivateAudioEnginePaths.Root(), expected);
            // 应用策略不保证既有流立即迁移；释放并重新建零音流，再核活动会话。
            await core.ExecuteScriptAsync("document.querySelector('#scp-silent-prime').pause();document.querySelector('#scp-silent-prime').removeAttribute('src');document.querySelector('#scp-silent-prime').load();'stopped'").WaitAsync(token);
            await Task.Delay(100, token);
            await StartSilenceAsync(core, "document.querySelector('#scp-silent-prime').src='data:audio/wav;base64," + Silence + "';document.querySelector('#scp-silent-prime').play()", token);
            budget.Restart();
            while (true)
            {
                var owned = environment.GetProcessInfos().Select(info => checked((uint)info.ProcessId)).ToHashSet();
                var active = Sessions(owned).Where(session => session.Active).ToArray();
                if (active.Length > 0 && active.All(session => AudioOutputBinding.Matches(expected, session.Endpoint))) break;
                if (budget.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("网页实际声音端点未匹配，未加载用户内容或回退默认设备。");
                await Task.Delay(50, token);
            }
            await core.ExecuteScriptAsync("document.querySelector('#scp-silent-prime').pause();document.querySelector('#scp-silent-prime').removeAttribute('src');document.querySelector('#scp-silent-prime').load();'released'").WaitAsync(token);
        }
        finally { core.DOMContentLoaded -= Loaded; core.IsMuted = originalMuted; }
    }

    /// <summary>:param core: 仅零采样自有文档。:param expression: 固定暖机表达式。:param token: OPEN寿命。:returns: 不放宽后续用户网页的自动播放策略。</summary>
    private static async Task StartSilenceAsync(CoreWebView2 core, string expression, CancellationToken token)
    {
        var parameters = JsonSerializer.Serialize(new { expression, userGesture = true, awaitPromise = true, returnByValue = true,
            allowUnsafeEvalBlockedByCSP = false });
        using var response = JsonDocument.Parse(await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", parameters).WaitAsync(TimeSpan.FromSeconds(3), token));
        if (response.RootElement.TryGetProperty("exceptionDetails", out _)) throw new InvalidOperationException("网页零音暖机被拒绝，未加载用户内容。");
    }

    /// <summary>:param endpoint: 明确RenderID。:returns: 当前唯一活动端点；缺失不回退。</summary>
    private static string ValidateEndpoint(string endpoint)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        try { return AudioOutputBinding.Resolve(endpoint, devices.Select(device => device.ID))!; }
        finally { foreach (var device in devices) device.Dispose(); }
    }
    private sealed record Session(uint Pid, string Endpoint, bool Active);
    /// <summary>:param owned: 当前环境实际进程集合。:returns: 只读其声音会话，不采集其他应用音频。</summary>
    private static List<Session> Sessions(HashSet<uint> owned)
    {
        var result = new List<Session>();
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (var index = 0; index < sessions.Count; index++)
                {
                    using var session = sessions[index];
                    if (owned.Contains(session.GetProcessID)) result.Add(new(session.GetProcessID, device.ID,
                        session.State == AudioSessionState.AudioSessionStateActive));
                }
            }
        }
        return result;
    }
    /// <summary>:returns: 一秒PCM全零流，不包含用户音频或外部资源。</summary>
    private static string CreateSilence()
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes, Encoding.ASCII, leaveOpen: true);
        const int rate = 48000, samples = rate;
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 4); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)2); writer.Write(rate); writer.Write(rate * 4); writer.Write((short)4); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 4); writer.Write(new byte[samples * 4]);
        return Convert.ToBase64String(bytes.ToArray());
    }
}
