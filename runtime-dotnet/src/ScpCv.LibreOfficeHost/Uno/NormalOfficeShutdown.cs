// 结束时原生进程可能先关闭URP桥；只有完整正常OS退出证据能替代遗失的UNO回复。
using System;
using ScpCv.LibreOfficeHost.Runtime;
using unoidl.com.sun.star.lang;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class NormalOfficeShutdown
{
    /// <summary>:param process: 仅自有job与原生句柄。:param closeDocument: 当前只读文稿关闭。:param terminate: 当前连接退出请求。:returns: launcher/引擎退出0、job空且监听关闭才返回。</summary>
    internal static void Run(OfficeProcess process, Action closeDocument, Func<bool> terminate)
    {
        try
        {
            closeDocument();
            if (!process.NativeHasExited && !terminate()) throw new InvalidOperationException("Office拒绝退出请求。");
        }
        catch (DisposedException)
        {
            // 不吞掉仍存活、异常退出或归属丢失；独立OS核验保留原五秒预算。
            process.ConfirmStopped();
            Console.Error.WriteLine("office_shutdown_bridge_lost_after_confirmed_normal_exit");
            return;
        }
        process.ConfirmStopped();
    }
}
