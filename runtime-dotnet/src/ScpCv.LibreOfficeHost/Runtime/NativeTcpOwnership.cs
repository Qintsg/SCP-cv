// 从Windows TCP表核监听PID，禁止把端口连通误当自有Office就绪。
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ScpCv.LibreOfficeHost.Runtime;

internal static class NativeTcpOwnership
{
    /// <summary>:param port: 已分配回环端口。:returns: 监听进程PID，无监听为0。</summary>
    internal static long ListenerOwner(int port)
    {
        var size = 0;
        var initial = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
        if (initial != 122 && initial != 0) throw new Win32Exception((int)initial);
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var status = GetExtendedTcpTable(buffer, ref size, false, 2, 3, 0);
            if (status != 0) throw new Win32Exception((int)status);
            var count = Marshal.ReadInt32(buffer);
            for (var index = 0; index < count; index++)
            {
                var row = 4 + index * 24;
                var localPort = (Marshal.ReadByte(buffer, row + 8) << 8) | Marshal.ReadByte(buffer, row + 9);
                if (localPort == port) return (uint)Marshal.ReadInt32(buffer, row + 20);
            }
            return 0;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sorted, int family, int tableClass, uint reserved);
}
