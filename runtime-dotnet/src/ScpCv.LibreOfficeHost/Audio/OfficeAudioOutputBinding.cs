// 每实例先登记/绑定/验证两个有限零音流；真实声音端点失配只隔离自有会话，不回退系统默认。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using ScpCv.Contracts.Runtime;
using ScpCv.LibreOfficeHost.Runtime;
using ScpCv.LibreOfficeHost.Uno;
using ScpCv.Presentation.Protocol;
using ScpCv.Runtime.Audio;

namespace ScpCv.LibreOfficeHost.Audio;

internal sealed class OfficeAudioOutputBinding(OfficeProcess process, UnoConnection connection, string? endpoint) : IDisposable
{
    private readonly List<AudioPrimePresentation> _primers = new();
    private bool _prepared;
    private bool _retired;

    /// <summary>:param role: 父进程确认的role。:param program: 启动路径。:returns: converter/未配置为null，明确配置须先核唯一端点和私有路径。</summary>
    internal static string? Preflight(string role, string program)
    {
        var configured = Environment.GetEnvironmentVariable(AudioOutputBinding.EnvironmentVariable);
        if (role == "converter" || string.IsNullOrWhiteSpace(configured)) return null;
        NativeApplicationAudioPolicy.EnsureCurrentProcessIsolated();
        var window = role == "impress-1" ? 1 : role == "impress-2" ? 2 : throw new InvalidOperationException("声音绑定role无效。");
        var expected = PrivateAudioEnginePaths.Office(window, configured);
        if (!string.Equals(Path.GetFullPath(program), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("明确声音输出必须使用对应窗口的项目私有Office，未启动原安装。");
        return ResolveEndpoint(configured);
    }

    /// <summary>:param parent: 经OS验证的自有候选容器。:returns: 实际流只在明确端点完成，之后才加载用户文稿。</summary>
    internal void Prepare(PresentationParentWindow parent)
    {
        if (endpoint is null || _prepared) return;
        if (_retired || process.NativeHasExited) throw new InvalidOperationException("Office声音引擎已退出。");
        var registration = Path.Combine(process.Profile, "scp-audio-register.wav");
        var verification = Path.Combine(process.Profile, "scp-audio-verify.wav");
        // 使用独立URI，对齐已验证的暖机/后续媒体流程，不复用旧默认流的媒体对象。
        CreateSilence(registration); CreateSilence(verification);
        var first = OpenPrime(parent, registration);
        Wait(() => ActiveSessions(false).Count > 0, "Office零音未登记实际声音进程。");
        using (var policy = new NativeApplicationAudioPolicy())
            policy.Bind(process.NativeProcessId, Path.Combine(process.ProgramDirectory, "soffice.bin"), PrivateAudioEnginePaths.Root(), endpoint);
        Wait(() => ActiveSessions(false).Count == 0, "第一零音流未自然结束。");
        first.Quiesce();
        var second = OpenPrime(parent, verification);
        Wait(() => { var actual = ActiveSessions(false); return actual.Count > 0 && actual.All(value => AudioOutputBinding.Matches(endpoint, value)); },
            "Office第二零音实际端点不匹配，未加载用户文稿。");
        Wait(() => ActiveSessions(false).Count == 0, "验证零音流未自然结束。");
        second.Quiesce();
        _prepared = true;
        Console.Error.WriteLine("office_audio_prime_confirmed: native=" + process.NativeProcessId);
    }

    /// <summary>:returns: 只读实际会话；端点丢失或错路时静音本原生进程并拒绝，不修改系统主音量。</summary>
    internal void Verify()
    {
        if (endpoint is null || _retired) return;
        if (process.NativeHasExited) throw new InvalidOperationException("Office声音引擎已退出。");
        try
        {
            _ = ResolveEndpoint(endpoint);
            if (ActiveSessions(false).Any(value => !AudioOutputBinding.Matches(endpoint, value)))
                throw new InvalidOperationException("Office实际声音端点失配，已隔离自有实例声音。");
        }
        catch
        {
            _ = ActiveSessions(true);
            throw;
        }
    }

    /// <summary>:returns: 仅独占shutdown时关闭已停止的暖机文稿；放映期间不关闭Frame，失败仍须OS退出核验。</summary>
    internal void ReleaseWarmups()
    {
        foreach (var primer in _primers) primer.Dispose();
        _primers.Clear();
    }

    /// <summary>:param parent: 自有父窗口。:param silence: 自有媒体。:returns: 先登记清理归属，半初始化失败也可清理。</summary>
    private AudioPrimePresentation OpenPrime(PresentationParentWindow parent, string silence)
    {
        var primer = new AudioPrimePresentation(connection); _primers.Add(primer);
        primer.Open(parent, silence); return primer;
    }

    /// <summary>:param mute: 仅故障隔离时写本进程的会话静音。:returns: 原生进程的实际活动端点，不读取其他应用内容。</summary>
    private List<string> ActiveSessions(bool mute)
    {
        var result = new List<string>();
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (var index = 0; index < sessions.Count; index++)
                {
                    using var session = sessions[index];
                    if (session.GetProcessID != process.NativeProcessId) continue;
                    if (mute) session.SimpleAudioVolume.Mute = true;
                    if (session.State == AudioSessionState.AudioSessionStateActive) result.Add(device.ID);
                }
            }
        }
        return result;
    }

    /// <summary>:param configured: 明确RenderID。:returns: 唯一活动端点，缺失不回退。</summary>
    private static string ResolveEndpoint(string configured)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        try { return AudioOutputBinding.Resolve(configured, devices.Select(device => device.ID))!; }
        finally { foreach (var device in devices) device.Dispose(); }
    }

    /// <summary>:param condition: 真实原生条件。:param detail: 固定失败说明。:returns: 五秒内满足，失败不增加预算或重试加载。</summary>
    private static void Wait(Func<bool> condition, string detail)
    {
        var budget = Stopwatch.StartNew();
        while (!condition()) { if (budget.Elapsed.TotalSeconds >= 5) throw new TimeoutException(detail); Thread.Sleep(50); }
    }

    /// <summary>:param path: 本引擎新profile文件。:returns: 三秒双声道PCM全零，CreateNew不覆盖用户文件。</summary>
    private static void CreateSilence(string path)
    {
        using var bytes = new FileStream(path, FileMode.CreateNew); using var writer = new BinaryWriter(bytes, Encoding.ASCII);
        const int rate = 48000, samples = rate * 3;
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 4); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)2); writer.Write(rate); writer.Write(rate * 4); writer.Write((short)4); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples * 4); writer.Write(new byte[samples * 4]);
    }

    /// <summary>:returns: 原生完整正常退出证明后只清本地引用，不向死桥请求关闭。</summary>
    internal void RetireExitedEngine() { _retired = true; foreach (var primer in _primers) primer.RetireExitedEngine(); _primers.Clear(); }
    /// <summary>:returns: 清理尚存暖机资源；异常不得当作正常关闭。</summary>
    public void Dispose() => ReleaseWarmups();
}
