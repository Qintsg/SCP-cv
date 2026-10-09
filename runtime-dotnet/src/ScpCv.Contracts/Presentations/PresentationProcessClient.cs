// .NET10父进程拥有Framework侧车的stdio与生命周期；只有已匹配结果可用于观察。
using System.Diagnostics;
using System.Globalization;
using ScpCv.Presentation.Protocol;
using ScpCv.Runtime.Processes;

namespace ScpCv.Contracts.Presentations;

public sealed class PresentationProcessClient : IAsyncDisposable
{
    private readonly Process _process;
    private readonly PresentationProcessJob _job;
    private readonly EventWaitHandle _launchGate;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Guid _instance;
    private readonly string _role;
    private readonly long _group;
    private readonly long _owner;
    private readonly long _engine;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PresentationCloseCompletion _close = new();
    private int _disposed;
    private int _poisoned;

    /// <summary>:param executable: Framework宿主路径。:param program: Office安装目录。:param profile: 本次全新profile。:param identity: 父进程分配的不可变身份。</summary>
    public PresentationProcessClient(string executable, string program, string profile, PresentationScope identity)
    {
        _instance = identity.EngineInstanceId; _role = identity.Role;
        _group = identity.GroupEpoch; _owner = identity.OwnerEpoch; _engine = identity.EngineEpoch;
        var start = new ProcessStartInfo(Path.GetFullPath(executable))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!,
        };
        using var parent = Process.GetCurrentProcess();
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("LibreOffice侧车只支持Windows");
        var gateName = "Local\\SCP-cv-Presentation-" + Guid.NewGuid().ToString("N");
        _launchGate = new EventWaitHandle(false, EventResetMode.ManualReset, gateName);
        start.ArgumentList.Add("--parent-gate"); start.ArgumentList.Add(gateName);
        foreach (var argument in new[] { "--parent-pid", parent.Id.ToString(CultureInfo.InvariantCulture),
            "--parent-start-ticks", parent.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            "--parent-session", parent.SessionId.ToString(CultureInfo.InvariantCulture) }) start.ArgumentList.Add(argument);
        foreach (var argument in new[] { "--role", _role, "--instance-id", _instance.ToString("D"),
            "--group-epoch", _group.ToString(CultureInfo.InvariantCulture), "--owner-epoch", _owner.ToString(CultureInfo.InvariantCulture),
            "--engine-epoch", _engine.ToString(CultureInfo.InvariantCulture), "--office-program", Path.GetFullPath(program), "--profile", Path.GetFullPath(profile) })
            start.ArgumentList.Add(argument);
        _job = new PresentationProcessJob();
        try
        {
            _process = Process.Start(start) ?? throw new InvalidOperationException("无法启动LibreOffice侧车");
            try { _job.Attach(_process); _launchGate.Set(); }
            catch { if (!_process.HasExited) _process.Kill(); _process.Dispose(); throw; }
        }
        catch { _job.Dispose(); _launchGate.Dispose(); throw; }
        _process.ErrorDataReceived += (_, record) => { if (record.Data is not null) Console.Error.WriteLine(record.Data); };
        _process.BeginErrorReadLine();
    }

    public int ProcessId => _process.Id;
    public DateTimeOffset ProcessStart => new(_process.StartTime.ToUniversalTime());

    /// <summary>:param request: 持有有效父租约的独立请求快照。:param currentScope: 可信父进程当前授权scope。:param cancellationToken: 等待寿命。:returns: 匹配结果；超时不盲重放。</summary>
    public async Task<PresentationResponse> SendAsync(PresentationRequest request, PresentationScope currentScope, CancellationToken cancellationToken = default)
    {
        // wire往返取得独立快照，不保留调用者可修改的请求对象。
        request = PresentationWireCodec.DecodeRequest(PresentationWireCodec.EncodeRequest(request));
        if (request.Scope.EngineInstanceId != _instance || request.Scope.Role != _role || request.Scope.GroupEpoch != _group ||
            request.Scope.OwnerEpoch != _owner || request.Scope.EngineEpoch != _engine || !PresentationRequestPolicy.SameScope(request.Scope, currentScope))
            throw new InvalidDataException("侧车目标身份/授权scope不匹配");
        var valid = PresentationRequestPolicy.Validate(request, DateTimeOffset.UtcNow);
        if (!valid.Allowed) throw new InvalidDataException(valid.Code);
        // deadline包含排队，尚未下发的取消不能污染前一条健康操作或成为不确定原生结果。
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var deadline = DateTimeOffset.Parse(request.Deadline, CultureInfo.InvariantCulture);
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) budget.Cancel();
        else budget.CancelAfter(remaining);
        await _gate.WaitAsync(budget.Token).ConfigureAwait(false);
        var dispatched = false;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (Volatile.Read(ref _poisoned) != 0) throw new IOException("侧车通信结果不确定，禁止复用或重放");
            if (_process.HasExited) throw new IOException("LibreOffice侧车已经退出");
            budget.Token.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline) throw new OperationCanceledException("请求在排队期间已过期，未下发。", budget.Token);
            dispatched = true;
            await PresentationWireCodec.WriteRequestAsync(_process.StandardInput.BaseStream, request, budget.Token).ConfigureAwait(false);
            var response = await PresentationWireCodec.ReadResponseAsync(_process.StandardOutput.BaseStream, budget.Token).ConfigureAwait(false)
                ?? throw new EndOfStreamException("LibreOffice侧车没有返回完成证据");
            var match = PresentationResultPolicy.Match(request, response, currentScope, DateTimeOffset.UtcNow);
            if (!match.Matched) throw new InvalidDataException("侧车结果围栏拒绝：" + match.Code);
            return response;
        }
        catch (Exception error) when (dispatched && error is IOException or OperationCanceledException or InvalidDataException)
        {
            Interlocked.Exchange(ref _poisoned, 1);
            throw new PresentationTransportException("LibreOffice操作结果不确定，通信已隔离且不会盲重放", error);
        }
        finally { _gate.Release(); }
    }

    /// <summary>:returns: 只关闭自有侧车，退出失败抛错；侧车job兜底释放其引擎。</summary>
    public ValueTask DisposeAsync() => _close.RunAsync(DisposeCoreAsync);

    /// <summary>:returns: 单次侧车退出与自有job清理结果，不重放外部关闭。</summary>
    private async Task DisposeCoreAsync()
    {
        Interlocked.Exchange(ref _disposed, 1);
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await _process.StandardInput.DisposeAsync().ConfigureAwait(false);
            await _process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            Console.Error.WriteLine("presentation_job_exit: host=" + _process.Id + ";active=" + _job.ActiveCount() + ";members=" + string.Join(",", _job.Members()));
            // launcher/宿主退出不等于所有子成员同时退出；在同一个15秒预算中核完整job。
            while (_job.HasRunningMembers()) await Task.Delay(25, budget.Token).ConfigureAwait(false);
            if (_process.ExitCode != 0) throw new PresentationTransportException("LibreOffice侧车故障退出，结果不确定",
                new IOException("exit_code=" + _process.ExitCode));
        }
        catch (OperationCanceledException)
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            using var killBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(killBudget.Token).ConfigureAwait(false);
            throw new PresentationTransportException("LibreOffice侧车退出未确认，已执行自有进程兜底清理",
                new TimeoutException("未取得正常退出证明"));
        }
        finally { _job.Dispose(); _launchGate.Dispose(); _process.Dispose(); _lifetime.Dispose(); _gate.Release(); _gate.Dispose(); }
    }
}
