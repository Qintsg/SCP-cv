// 官方UNO context仅由唯一MTA线程创建和访问，配置指向自有引擎。
using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using unoidl.com.sun.star.bridge;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.uno;
using unoidl.com.sun.star.beans;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class UnoConnection : IDisposable
{
    private readonly Socket _socket;
    internal XComponentContext Context { get; }
    internal XDesktop2 Desktop { get; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string path);

    /// <summary>:param program: 已验证官方安装目录。:param profile: 自有客户端profile。:param port: 自有端口。</summary>
    internal UnoConnection(string program, string profile, int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        if (!SetDllDirectory(program)) throw new InvalidOperationException("无法设置官方UNO原生依赖目录");
        var parameters = new Hashtable
        {
            ["URE_MORE_TYPES"] = new Uri(Path.Combine(program, "types", "offapi.rdb")).AbsoluteUri,
            ["UserInstallation"] = new Uri(profile).AbsoluteUri,
        };
        var local = uno.util.Bootstrap.defaultBootstrap_InitialComponentContext(
            new Uri(Path.Combine(program, "uno.ini")).AbsoluteUri, parameters.GetEnumerator());
        var resolver = (XUnoUrlResolver)local.getServiceManager().createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", local);
        Context = (XComponentContext)resolver.resolve("uno:socket,host=127.0.0.1,port=" + port + ";urp;StarOffice.ComponentContext");
        Desktop = (XDesktop2)Context.getServiceManager().createInstanceWithContext("com.sun.star.frame.Desktop", Context);
    }

    /// <summary>:param name: 属性名。:param value: 保留CLR类型的值。:returns: UNO属性。</summary>
    internal static PropertyValue Property(string name, object value) =>
        new PropertyValue { Name = name, Value = new uno.Any(value.GetType(), value) };

    /// <summary>:returns: 仅释放本客户端初始化资源。</summary>
    public void Dispose() => _socket.Dispose();
}
