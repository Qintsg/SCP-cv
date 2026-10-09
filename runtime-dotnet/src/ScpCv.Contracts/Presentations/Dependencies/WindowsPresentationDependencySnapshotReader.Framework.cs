// Windows Framework运行条件只读采集；不检查SDK/targeting pack，不修改Registry。
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace ScpCv.Contracts.Presentations.Dependencies;

public sealed partial class WindowsPresentationDependencySnapshotReader
{
    /// <summary>:returns: Full Release DWORD与Framework64 CLR可读性，不按Version字符串猜发行版。</summary>
    [SupportedOSPlatform("windows")]
    private static PresentationFrameworkRuntime ReadFramework()
    {
        try
        {
            using var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using var full = registry.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", writable: false);
            var release = full?.GetValue("Release") is int installedRelease ? installedRelease : (int?)null;
            var clr = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Microsoft.NET", "Framework64", "v4.0.30319", "clr.dll");
            var readable = ReadFile("framework", ".NET Framework64 CLR", clr).State == PresentationDependencyFileState.Readable;
            return new(release, readable);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        { return new(null, false, "framework_registry_unreadable"); }
    }
}
