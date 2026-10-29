// 文件描述：媒体与窗口维护命令的合并规则。项目：SCP-cv；文件：DisplayCommandPolicy.cs；作者：Qintsg；日期：2026-10-29。
namespace ScpCv.Domain.Commands;

/// <summary>两个大屏显示窗口的意图合并规则。</summary>
public static class DisplayCommandPolicy
{
    private static readonly HashSet<string> ReplacementCommands =
        new HashSet<string>(StringComparer.Ordinal) { "OPEN", "CLOSE", "RESET_PPT" };

    private static readonly HashSet<string> CoalescingCommands =
        new HashSet<string>(StringComparer.Ordinal) { "SEEK", "SET_LOOP", "SET_VOLUME", "SET_MUTE", "SET_WINDOW_FOREGROUND" };

    public static CommandPolicyDecision Evaluate(
        string command,
        IEnumerable<PendingCommand> pending)
    {
        var normalized = Normalize(command);
        ArgumentNullException.ThrowIfNull(pending);

        var superseded = ReplacementCommands.Contains(normalized)
            ? pending.Where(item => Normalize(item.Command) != "SET_WINDOW_FOREGROUND").Select(item => item.CommandId).ToHashSet()
            : CoalescingCommands.Contains(normalized)
                ? pending
                    .Where(item => string.Equals(Normalize(item.Command), normalized, StringComparison.Ordinal))
                    .Select(item => item.CommandId)
                    .ToHashSet()
                : new HashSet<Guid>();

        return new CommandPolicyDecision(normalized, superseded);
    }

    private static string Normalize(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return command.Trim().ToUpperInvariant();
    }
}
