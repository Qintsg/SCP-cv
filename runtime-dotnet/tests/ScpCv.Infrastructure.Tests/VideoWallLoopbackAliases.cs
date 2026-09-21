using System.Net;
using System.Net.Sockets;
using ScpCv.Infrastructure.VideoWall;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 回环节点的别名就绪情况：192.168.5.101~150 里哪些地址能在本机绑上 4830。
/// 能绑上说明本机挂了这个别名（见 <c>runtime-dotnet/scripts/videowall-loopback.ps1</c>），
/// 真实 TCP 下发就会把控制包送到测试自己的假节点上，而不是发到真实网段。
/// </summary>
internal static class VideoWallLoopbackAliases
{
    /// <summary>准备命令，写进跳过原因里，让人知道怎么把这批用例跑起来。</summary>
    public const string SetupCommand = "runtime-dotnet/scripts/videowall-loopback.ps1 -Apply";

    private static readonly Lazy<(string[] Present, string[] Missing)> Aliases = new(Probe);

    /// <summary>能绑上端口的节点地址（列优先顺序）。</summary>
    public static string[] Present => Aliases.Value.Present;

    /// <summary>绑不上端口的节点地址：别名没挂，或端口被别的进程占着。</summary>
    public static string[] Missing => Aliases.Value.Missing;

    private static (string[] Present, string[] Missing) Probe()
    {
        var present = new List<string>();
        var missing = new List<string>();
        foreach (var ip in VideoWallSequenceBuilder.AllTargetIps())
        {
            (CanBind(ip) ? present : missing).Add(ip);
        }

        return ([.. present], [.. missing]);
    }

    private static bool CanBind(string ip)
    {
        var listener = new TcpListener(IPAddress.Parse(ip), VideoWallSequenceBuilder.TcpPort);
        try
        {
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            // 别名没挂（WSAEADDRNOTAVAIL）或端口被别人占着（WSAEADDRINUSE）：这个节点现在都当不了回环节点。
            return false;
        }
        finally
        {
            listener.Stop();
        }
    }
}

/// <summary>
/// 只在回环节点别名就绪时运行的用例。缺别名时按各自的前置条件跳过并写明准备命令：
/// 没做过准备的机器上默认 <c>dotnet test</c> 不会红，也不会假装这些用例跑过了。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresVideoWallAliasesAttribute : FactAttribute
{
    /// <summary>要求「没有」别名的节点：节点掉线/无响应场景靠它制造连接超时。</summary>
    public string? Absent { get; init; }

    public override string? Skip
    {
        get => Reason();
        set { }
    }

    private string? Reason()
    {
        var present = VideoWallLoopbackAliases.Present;
        var missing = VideoWallLoopbackAliases.Missing;
        if (Absent is { } absent)
        {
            if (present.Contains(absent, StringComparer.Ordinal))
            {
                return $"{absent} 现在挂着别名，造不出连接超时。先运行："
                    + $"{VideoWallLoopbackAliases.SetupCommand} -Except {absent}";
            }

            missing = [.. missing.Where(ip => !string.Equals(ip, absent, StringComparison.Ordinal))];
        }

        return missing.Length == 0
            ? null
            : $"需要 50 个节点地址都能在本机绑定 {VideoWallSequenceBuilder.TcpPort}，当前缺 {missing.Length} 个"
                + $"（{Preview(missing)}）。先以管理员运行：{VideoWallLoopbackAliases.SetupCommand}";
    }

    private static string Preview(string[] ips) =>
        string.Join('、', ips.Take(4)) + (ips.Length > 4 ? " 等" : string.Empty);
}
