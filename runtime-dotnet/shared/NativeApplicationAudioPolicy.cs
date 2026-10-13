// 项目私有引擎的Windows应用输出策略；不设置系统默认/总音量，不允许作用于原安装。
// 接口ABI依据EarTrumpet公开互操作定义；仅已验证Win10/Win11变体，失配明确拒绝。
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScpCv.Runtime.Audio;

internal sealed class NativeApplicationAudioPolicy : IDisposable
{
    private IntPtr _factory;
    private bool _uninitialize;
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly SetEndpoint _set;
    private readonly GetEndpoint _get;

    /// <summary>:returns: 已知ABI的当前线程策略接口；不猜测槽位或回退到注册表。</summary>
    internal NativeApplicationAudioPolicy()
    {
        var version = new NativeVersion { Size = (uint)Marshal.SizeOf<NativeVersion>(), ServicePack = string.Empty };
        if (RtlGetVersion(ref version) != 0 || version.Major != 10 || version.Build < 17763)
            throw new NotSupportedException("当前Windows版本的应用声音绑定尚未验证。");
        var initialized = RoInitialize(Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA ? 1u : 0u);
        if (initialized < 0) Marshal.ThrowExceptionForHR(initialized);
        _uninitialize = true;
        var name = IntPtr.Zero;
        try
        {
            name = String("Windows.Media.Internal.AudioPolicyConfig");
            var iid = new Guid(version.Build >= 22000 ? "ab3d4648-e242-459f-b02f-541c70306324" : "2a59116d-6c4f-45e0-a74f-707e3fef9258");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref iid, out _factory));
            var table = Marshal.ReadIntPtr(_factory);
            _set = Marshal.GetDelegateForFunctionPointer<SetEndpoint>(Marshal.ReadIntPtr(table, 25 * IntPtr.Size));
            _get = Marshal.GetDelegateForFunctionPointer<GetEndpoint>(Marshal.ReadIntPtr(table, 26 * IntPtr.Size));
        }
        catch { Dispose(); throw; }
        finally { Marshal.ThrowExceptionForHR(WindowsDeleteString(name)); }
    }

    /// <summary>:param pid: 实际发声、已归属当前引擎的进程。:param image: 专用副本。:param root: 可信标记根。:param endpoint: 完整RenderID。:returns: 配置读回；实际声音由调用方另验。</summary>
    internal void Bind(uint pid, string image, string root, string endpoint)
    {
        PrivateAudioEnginePaths.ValidateImage(image, root);
        using var process = Process.GetProcessById(checked((int)pid));
        using var owner = Process.GetCurrentProcess();
        // 持有原生进程句柄，避免验证与PID接口调用间对象退出后PID被重用。
        _ = process.Handle;
        if (process.HasExited || process.SessionId != owner.SessionId ||
            !string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), Path.GetFullPath(image), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("实际声音进程不属于已验证的私有引擎。");
        var before = new[] { Read(pid, 0), Read(pid, 1) };
        var target = @"\\?\SWD#MMDEVAPI#" + endpoint + "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        try
        {
            Write(pid, 0, target); Write(pid, 1, target);
            if (Read(pid, 0) != target || Read(pid, 1) != target)
                throw new InvalidOperationException("应用声音策略读回不匹配。");
        }
        catch (Exception failure)
        {
            try { Write(pid, 0, before[0]); Write(pid, 1, before[1]); }
            catch (Exception rollback) { throw new InvalidOperationException("应用声音绑定失败且原策略恢复未确认。", new AggregateException(failure, rollback)); }
            throw;
        }
    }

    /// <summary>:param pid: 自有实际声音进程。:param role: console/multimedia。:returns: 配置读回，不是WASAPI活动端点。</summary>
    private string Read(uint pid, int role)
    {
        Marshal.ThrowExceptionForHR(_get(_factory, pid, 0, role, out var value));
        try { return Marshal.PtrToStringUni(WindowsGetStringRawBuffer(value, out var length), checked((int)length)) ?? string.Empty; }
        finally { Marshal.ThrowExceptionForHR(WindowsDeleteString(value)); }
    }
    /// <summary>:param pid: 已持句柄的自有进程。:param role: 端点角色。:param value: Render设备接口或恢复空值。</summary>
    private void Write(uint pid, int role, string value)
    {
        var text = value.Length == 0 ? IntPtr.Zero : String(value);
        try { Marshal.ThrowExceptionForHR(_set(_factory, pid, 0, role, text)); }
        finally { Marshal.ThrowExceptionForHR(WindowsDeleteString(text)); }
    }
    /// <summary>:param value: 固定系统名称/设备接口。:returns: 有效HSTRING，调用方负责释放。</summary>
    private static IntPtr String(string value)
    {
        Marshal.ThrowExceptionForHR(WindowsCreateString(value, (uint)value.Length, out var result));
        return result;
    }
    /// <summary>:returns: 仅释放当前线程的接口，不清理其他应用或系统策略。</summary>
    public void Dispose()
    {
        if (Environment.CurrentManagedThreadId != _thread) throw new InvalidOperationException("声音策略接口必须在原创建线程释放。");
        if (_factory != IntPtr.Zero) { Marshal.Release(_factory); _factory = IntPtr.Zero; }
        if (_uninitialize) { _uninitialize = false; RoUninitialize(); }
        GC.SuppressFinalize(this);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeVersion
    {
        internal uint Size, Major, Minor, Build, Platform;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string ServicePack;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetEndpoint(IntPtr self, uint pid, int flow, int role, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetEndpoint(IntPtr self, uint pid, int flow, int role, out IntPtr value);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("ntdll.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int RtlGetVersion(ref NativeVersion info);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll")] private static extern int RoInitialize(uint kind);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll")] private static extern void RoUninitialize();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr classId, ref Guid iid, out IntPtr factory);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int WindowsCreateString(string value, uint length, out IntPtr result);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr value);
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("combase.dll")] private static extern IntPtr WindowsGetStringRawBuffer(IntPtr value, out uint length);
}
