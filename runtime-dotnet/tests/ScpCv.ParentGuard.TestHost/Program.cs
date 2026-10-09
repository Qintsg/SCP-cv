// 正式Framework父归属监测的三角色QA；仅操作自有进程/证据目录，不创建job或访问Office。
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using ScpCv.LibreOfficeHost.Runtime;

namespace ScpCv.ParentGuard.TestHost;

internal static class Program
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false);
    private const int ExitBudgetMilliseconds = 5000;

    /// <summary>:param args: observer、parent或guard及本次新证据根。:returns: observer全通过0，失败1；guard拒绝/父死亡2。</summary>
    private static int Main(string[] args)
    {
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) throw new PlatformNotSupportedException("QA仅支持Windows。");
            if (args.Length < 2) throw new ArgumentException("用法：ScpCv.ParentGuard.TestHost.exe observer <全新证据根>");
            var root = Path.GetFullPath(args[1]);
            if (args[0] == "observer") return Observe(root);
            if (args[0] == "parent") return Parent(root);
            if (args[0] == "guard") return Guard(root, args);
            throw new ArgumentException("未知QA角色。");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    /// <summary>:param root: 必须不存在的新根；所有证据均保留。:returns: 正向timer及三个归属拒绝全通过0。</summary>
    private static int Observe(string root)
    {
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("证据根必须全新，不覆盖既有证据。");
        Directory.CreateDirectory(root);
        Evidence(root, "scope", "net48;x64;linked-production-parent-guard;no-owned-job;no-Office");
        ObserveParentLoss(Path.Combine(root, "parent-loss"));
        ObserveRejection(Path.Combine(root, "wrong-pid"), "wrongPID");
        ObserveRejection(Path.Combine(root, "wrong-start"), "wrongStart");
        ObserveRejection(Path.Combine(root, "wrong-session"), "wrongSession");
        Evidence(root, "result", "passed=4;timer-parent-loss=1;identity-rejection=3");
        Console.WriteLine("ParentProcessLifetime QA通过4/4，证据：" + root);
        return 0;
    }

    /// <summary>:param root: 本轮timer证据目录。:returns: 父存活时ready，父结束后guard五秒内以2退出且正式timer日志存在。</summary>
    private static void ObserveParentLoss(string root)
    {
        Directory.CreateDirectory(root);
        using var parent = Start("parent", root);
        Process? guard = null;
        try
        {
            WaitForFile(Path.Combine(root, "guard.identity"), parent);
            guard = OpenIdentity(Path.Combine(root, "guard.identity"));
            Evidence(root, "retained-parent", Identity(parent));
            Evidence(root, "retained-guard", Identity(guard));
            WaitForFile(Path.Combine(root, "guard.ready"), guard);
            Require(!parent.HasExited && !guard.HasExited, "ready时父和guard必须仍存活。");
            Require(!File.Exists(Path.Combine(root, "guard.rejected")), "正向真实父归属不能被拒绝。");
            Evidence(root, "alive-ready-confirmed", "true");

            var loss = Stopwatch.StartNew();
            parent.Kill();
            Require(parent.WaitForExit(ExitBudgetMilliseconds), "精确自有parent未在五秒内退出。");
            Require(guard.WaitForExit(ExitBudgetMilliseconds), "正式timer未在父死亡后五秒内结束guard。");
            loss.Stop();
            Require(loss.ElapsedMilliseconds <= ExitBudgetMilliseconds, "guard父死亡总预算超过五秒。");
            Require(guard.ExitCode == 2, "guard父死亡退出码不是2。");
            var diagnostic = File.ReadAllText(Path.Combine(root, "guard.stderr.log"), Utf8);
            Require(diagnostic.Contains("libreoffice_parent_lost: owned_job_cleanup"), "缺少正式timer父死亡诊断，不能当timer通过。");
            Evidence(root, "result", "passed;guard_exit=2;parent_loss_ms=" + loss.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            // 精确句柄兜底只用于失败收尾；它不计为timer成功，也不全局按进程名清理。
            try { if (guard != null) Cleanup(guard, root, "guard"); }
            finally { Cleanup(parent, root, "parent"); guard?.Dispose(); }
        }
    }

    /// <summary>:param root: 本次拒绝证据根。:param mode: 一项错误父属性。:returns: 正式构造拒绝、无ready、准确错误和退出2。</summary>
    private static void ObserveRejection(string root, string mode)
    {
        Directory.CreateDirectory(root);
        using var self = Process.GetCurrentProcess();
        var pid = self.Id;
        var ticks = self.StartTime.ToUniversalTime().Ticks;
        var session = self.SessionId;
        if (mode == "wrongPID") pid = 0;
        else if (mode == "wrongStart") ticks = checked(ticks + 1);
        else if (mode == "wrongSession") session = checked(session + 1);
        else throw new ArgumentException("未知拒绝模式。");
        using var guard = StartGuard(root, pid, ticks, session);
        try
        {
            Evidence(root, "retained-guard", Identity(guard));
            Require(guard.WaitForExit(ExitBudgetMilliseconds), "错误归属guard未在五秒内拒绝退出。");
            Require(guard.ExitCode == 2, "错误归属guard退出码不是2。");
            Require(!File.Exists(Path.Combine(root, "guard.ready")), "错误归属产生ready，围栏失效。");
            var rejection = File.ReadAllText(Path.Combine(root, "guard.rejected"), Utf8);
            var expected = mode == "wrongPID" ? "OS实际父进程不匹配" : "父进程启动时间/会话不匹配";
            Require(rejection.Contains(typeof(InvalidDataException).FullName!) && rejection.Contains(expected), "拒绝类型或对应错误文案不匹配。");
            Require(!File.ReadAllText(Path.Combine(root, "guard.stderr.log"), Utf8).Contains("libreoffice_parent_lost"), "错误初始归属不能冒充timer父死亡。");
            Evidence(root, "result", "passed;exit=2;ready=false;exception=InvalidDataException;mode=" + mode);
        }
        finally { Cleanup(guard, root, "guard"); }
    }

    /// <summary>:param root: 自有目录。:returns: 创建真实子guard、保存其身份，随后主线程睡眠无IO。</summary>
    private static int Parent(string root)
    {
        using var self = Process.GetCurrentProcess();
        using var guard = StartGuard(root, self.Id, self.StartTime.ToUniversalTime().Ticks, self.SessionId);
        try
        {
            Evidence(root, "parent.identity", Identity(self));
            Evidence(root, "guard.identity", Identity(guard));
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        catch { Cleanup(guard, root, "guard"); throw; }
    }

    /// <summary>:param root: 本次日志根。:param args: 声明父PID/UTC ticks/session。:returns: 只由正式构造拒绝或timer父死亡退出2。</summary>
    private static int Guard(string root, string[] args)
    {
        Directory.CreateDirectory(root);
        using var log = new StreamWriter(new FileStream(Path.Combine(root, "guard.stderr.log"), FileMode.CreateNew,
            FileAccess.Write, FileShare.Read), Utf8) { AutoFlush = true };
        Console.SetError(log);
        try
        {
            if (args.Length != 5) throw new ArgumentException("guard父属性必须齐全。");
            var pid = int.Parse(args[2], CultureInfo.InvariantCulture);
            var ticks = long.Parse(args[3], CultureInfo.InvariantCulture);
            var session = int.Parse(args[4], CultureInfo.InvariantCulture);
            using var lifetime = new ParentProcessLifetime(pid, ticks, session);
            using var self = Process.GetCurrentProcess();
            Evidence(root, "guard.ready", Identity(self));
            // 不读stdin、不等待父EOF：父死亡只能由正式Timer逻辑检测；没有自有job兜底。
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        catch (Exception error)
        {
            var record = error.GetType().FullName + "\n" + error.Message;
            Evidence(root, "guard.rejected", record);
            Console.Error.WriteLine(record);
            return 2;
        }
    }

    /// <summary>:param root: 自有证据根。:param pid: 声明父PID。:param ticks: UTC启动ticks。:param session: 会话。:returns: 已缓存精确句柄的自有guard。</summary>
    private static Process StartGuard(string root, int pid, long ticks, int session) => Start("guard", root,
        pid.ToString(CultureInfo.InvariantCulture), ticks.ToString(CultureInfo.InvariantCulture), session.ToString(CultureInfo.InvariantCulture));

    /// <summary>:param role: 固定角色。:param root: 证据目录。:param rest: 固定纯数字参数。:returns: 新子进程，启动后立即缓存精确OS句柄。</summary>
    private static Process Start(string role, string root, params string[] rest)
    {
        var executable = Assembly.GetExecutingAssembly().Location;
        var arguments = role + " " + Quote(root);
        foreach (var argument in rest) arguments += " " + Quote(argument);
        var start = new ProcessStartInfo(executable, arguments)
        { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)! };
        var process = Process.Start(start) ?? throw new InvalidOperationException("无法启动本次自有QA子进程。");
        try { _ = process.Handle; return process; }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    Require(process.WaitForExit(ExitBudgetMilliseconds), "缓存句柄失败后的自有进程未在五秒内退出。");
                }
            }
            finally { process.Dispose(); }
            throw;
        }
    }

    /// <summary>:param value: Windows argv值。:returns: 符合反斜杠/引号规则的单参数，不经过shell。</summary>
    private static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '"') result.Append('\\', slashes * 2 + 1);
            else result.Append('\\', slashes);
            result.Append(character);
            slashes = 0;
        }
        result.Append('\\', slashes * 2).Append('"');
        return result.ToString();
    }

    /// <summary>:param file: parent输出的guard身份。:returns: 核对启动时间/会话并缓存精确句柄，拒绝PID复用。</summary>
    private static Process OpenIdentity(string file)
    {
        var fields = File.ReadAllText(file, Utf8).Trim().Split('|');
        if (fields.Length != 3) throw new InvalidDataException("guard身份证据字段数错误。");
        var process = Process.GetProcessById(int.Parse(fields[0], CultureInfo.InvariantCulture));
        try
        {
            _ = process.Handle;
            Require(process.StartTime.ToUniversalTime().Ticks == long.Parse(fields[1], CultureInfo.InvariantCulture) &&
                process.SessionId == int.Parse(fields[2], CultureInfo.InvariantCulture), "guard身份不一致，禁止操作该进程。");
            return process;
        }
        catch { process.Dispose(); throw; }
    }

    /// <summary>:param process: 已保留精确句柄的对象。:returns: PID|UTC启动ticks|session。</summary>
    private static string Identity(Process process) => process.Id.ToString(CultureInfo.InvariantCulture) + "|" +
        process.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + "|" + process.SessionId.ToString(CultureInfo.InvariantCulture);

    /// <summary>:param path: 本次外部事件文件。:param owned: 精确自有发出方。:returns: 五秒内取得完成事件；死亡或超时即失败。</summary>
    private static void WaitForFile(string path, Process owned)
    {
        var budget = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            Require(!owned.HasExited, "自有QA进程在事件前退出：" + Path.GetFileName(path));
            Require(budget.ElapsedMilliseconds < ExitBudgetMilliseconds, "等待QA事件超过五秒：" + Path.GetFileName(path));
            Thread.Sleep(10);
        }
    }

    /// <summary>:param process: 唯一精确自有进程。:param root: 清理证据根。:param role: 清理对象。:returns: 最多五秒收尾；强清明确记录且不计成功。</summary>
    private static void Cleanup(Process process, string root, string role)
    {
        if (process.HasExited) return;
        try { Evidence(root, "cleanup-" + role, "forced;pid=" + process.Id.ToString(CultureInfo.InvariantCulture)); }
        finally
        {
            // 即使证据文件写入失败，也必须尝试结束已保留精确句柄的自有对象。
            process.Kill();
            Require(process.WaitForExit(ExitBudgetMilliseconds), "精确自有" + role + "强清后仍未在五秒内退出。");
        }
    }

    /// <summary>:param root: 已创建自有目录。:param name: 固定证据文件名。:param value: 原始证据。:returns: 写完再原子发布，UTF8/LF；既有证据不覆盖。</summary>
    private static void Evidence(string root, string name, string value)
    {
        var temporary = Path.Combine(root, name + ".tmp-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(temporary, value + "\n", Utf8);
        File.Move(temporary, Path.Combine(root, name));
    }

    /// <summary>:param condition: 必须成立的实证断言。:param reason: 失败原因。:returns: 无；失败非零退出。</summary>
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
