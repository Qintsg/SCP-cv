// 一窗一个独立Framework侧车；保留纯值文稿身份并正常释放其自有引擎。
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Presentations;
using ScpCv.Contracts.Runtime;
using ScpCv.Presentation.Protocol;
using ScpCv.Runtime.Presentations;
using ScpCv.Runtime.Audio;

namespace ScpCv.PlayerWorker.Presentations;

internal sealed class LibreOfficeSurface : IPresentationSurface
{
    private readonly PresentationProcessClient _client;
    internal PresentationScope Scope { get; }
    private readonly PresentationParentWindow _parent;
    private readonly PresentationViewport _viewport;
    private CommandLeaseDto _lastLease;
    public int CurrentSlide { get; private set; }
    public int TotalSlides { get; private set; }
    public string LastControlResultCode { get; private set; } = "ok";
    private readonly PresentationCloseCompletion _close = new();

    /// <summary>:param window: 唯一输出窗口。:param windowId: 1/2。:param lease: 已授权原件命令。:param sourcePath: 实体原件。:param token: 等待寿命。:returns: 实际出画资源。</summary>
    internal static async Task<LibreOfficeSurface> OpenAsync(PlayerWindow window, int windowId, CommandLeaseDto lease, string sourcePath, CancellationToken token)
    {
        // 无引擎副作用的包检查在候选窗口/侧车创建前完成；失败保留旧画面。
        _ = new PresentationPackageMetadata(sourcePath);
        var officeProgram = PrivateAudioEnginePaths.Office(windowId, Environment.GetEnvironmentVariable(AudioOutputBinding.EnvironmentVariable))
            ?? PresentationRuntimePaths.OfficeProgram;
        using var process = Process.GetCurrentProcess();
        var viewport = new PresentationViewport(window);
        var scope = new PresentationScope
        {
            Role = "impress-" + windowId, WindowId = windowId, EngineInstanceId = Guid.NewGuid(), GroupEpoch = lease.GroupEpoch,
            OwnerEpoch = lease.OwnerEpoch, EngineEpoch = 1, DocumentEpoch = 1,
            SourceId = lease.Args["source_id"].GetInt64(), SourceRevision = lease.SourceRevision,
            SourceGeneration = lease.SourceGeneration, SourceDigest = lease.Args["content_digest"].GetString() ?? string.Empty,
        };
        var parent = new PresentationParentWindow
        {
            Handle = viewport.Handle.ToInt64(), ProcessId = process.Id,
            ProcessStartTime = new DateTimeOffset(process.StartTime.ToUniversalTime()).ToString("O"), SessionId = process.SessionId,
            Dpi = window.NativeDpi, Width = viewport.Width, Height = viewport.Height,
        };
        PresentationProcessClient client;
        try
        {
            client = new PresentationProcessClient(PresentationRuntimePaths.HostExecutable(), officeProgram,
                PresentationRuntimePaths.EngineProfile(scope.EngineInstanceId), scope);
        }
        catch { viewport.Dispose(); throw; }
        var surface = new LibreOfficeSurface(client, scope, parent, viewport, lease);
        try
        {
            var target = lease.Args.TryGetValue("target_slide", out var page) ? page.GetInt32() : 1;
            var result = await surface.SendAsync(lease, "open", Math.Max(1, target), sourcePath, token);
            surface.Update(result);
            return surface;
        }
        catch (Exception operationFailure)
        {
            await DiscardUnpublishedAsync(client, viewport, operationFailure);
            throw;
        }
    }

    /// <summary>:param client: 本次尚未发布实例。:param viewport: 本次自有候选窗。:param operationFailure: 原始OPEN错误。:returns: 所有清理确认时保持原异常；双/多失败保留原因且仍uncertain。</summary>
    private static async Task DiscardUnpublishedAsync(PresentationProcessClient client, PresentationViewport viewport, Exception operationFailure)
    {
        var causes = new List<Exception> { operationFailure };
        try { await client.DisposeAsync(); }
        catch (Exception cleanup) { causes.Add(cleanup); }
        try { viewport.Dispose(); }
        catch (Exception cleanup) { causes.Add(cleanup); }
        if (causes.Count > 1)
            throw new PresentationTransportException("文稿候选打开失败，且自有实例清理也未确认；保留全部原因，不能盲重试。",
                new AggregateException(causes));
    }

    /// <summary>:param client: 自有侧车。:param scope: 当前文稿身份。:param parent: 真实窗口声明。</summary>
    private LibreOfficeSurface(PresentationProcessClient client, PresentationScope scope, PresentationParentWindow parent, PresentationViewport viewport, CommandLeaseDto lease)
    { _client = client; Scope = scope; _parent = parent; _viewport = viewport; _lastLease = lease; }

    public void Activate() => _viewport.Activate();

    /// <summary>:param lease: 控制租约。:param operation: 已验证有限动作。:param slide: 1起始页。:param token: 等待寿命。:returns: 实际更新。</summary>
    public async Task ControlAsync(CommandLeaseDto lease, string operation, int slide, CancellationToken token)
    {
        if (lease.GroupEpoch != Scope.GroupEpoch || lease.OwnerEpoch != Scope.OwnerEpoch ||
            lease.SourceRevision != Scope.SourceRevision || lease.SourceGeneration != Scope.SourceGeneration)
            throw new InvalidOperationException("放映组/文稿授权身份已变化");
        _parent.Width = _viewport.Width; _parent.Height = _viewport.Height;
        var result = await SendAsync(lease, operation, slide, string.Empty, token);
        Update(result);
        LastControlResultCode = result.Status == "noop" ? result.ErrorCode : "ok";
        _lastLease = lease;
    }

    /// <summary>:param token: 当前Worker会话寿命。:returns: 只读实际页码采样，关联当前文稿最后租约而不发放新控制权。</summary>
    public async Task ObserveAsync(CancellationToken token) => Update(await SendAsync(_lastLease, "observe", 0, string.Empty, token, 3));

    /// <summary>:param lease: 父命令。:param operation: 动作。:param slide: 跳页。:param path: OPEN原件。:param token: 预算。:returns: 匹配的实际结果。</summary>
    private Task<PresentationResponse> SendAsync(CommandLeaseDto lease, string operation, int slide, string path, CancellationToken token, int seconds = 30) =>
        _client.SendAsync(new PresentationRequest
        {
            OperationId = Guid.NewGuid(), ParentCommandId = lease.CommandId, ClaimToken = lease.ClaimToken, Scope = Scope,
            ParentWindow = _parent, Operation = operation, TargetSlide = slide, SourcePath = path,
            FitText = operation == "open" && lease.Args.TryGetValue("fit_text", out var fit) && fit.GetBoolean(),
            Deadline = DateTimeOffset.UtcNow.AddSeconds(seconds).ToString("O"),
        }, Scope, token);

    /// <summary>:param response: 确认结果。:returns: 更新真实页码，不生成媒体时钟。</summary>
    private void Update(PresentationResponse response)
    {
        if (response.Status is not ("succeeded" or "noop") || response.Observation is not { Confirmed: true } actual)
            throw new InvalidOperationException("LibreOffice动作失败：" + response.ErrorCode +
                (string.IsNullOrWhiteSpace(response.ErrorDetail) ? string.Empty : "；" + response.ErrorDetail));
        CurrentSlide = actual.CurrentSlide;
        TotalSlides = actual.TotalSlides;
    }

    /// <summary>:returns: 明确关闭/退出自有引擎，失败不伪造完成。</summary>
    public ValueTask DisposeAsync() => _close.RunAsync(DisposeCoreAsync);

    /// <summary>:returns: 仅一次shutdown与退出证明，清理失败也不能转成正常完成。</summary>
    private async Task DisposeCoreAsync()
    {
        var shutdown = new PresentationScope
        {
            Role = Scope.Role, WindowId = Scope.WindowId, EngineInstanceId = Scope.EngineInstanceId,
            GroupEpoch = Scope.GroupEpoch, OwnerEpoch = Scope.OwnerEpoch, EngineEpoch = Scope.EngineEpoch,
        };
        var failures = new List<Exception>();
        try
        {
            var result = await _client.SendAsync(new PresentationRequest
            {
                Scope = shutdown, Operation = "shutdown", OperationId = Guid.NewGuid(), ParentShutdownId = Guid.NewGuid(),
                Deadline = DateTimeOffset.UtcNow.AddSeconds(20).ToString("O"),
            }, shutdown);
            if (result.Status != "succeeded")
                throw new InvalidOperationException("LibreOffice关闭未确认：" + result.ErrorCode + "；" + result.ErrorDetail);
        }
        catch (Exception error) { failures.Add(error); }
        // 按原顺序尝试所有自有清理，不能让后阶段错误覆盖已经发生的外部关闭失败。
        try { await _client.DisposeAsync(); }
        catch (Exception error) { failures.Add(error); }
        try { _viewport.Dispose(); }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Throw(failures[0]);
        if (failures.Count > 1)
            throw new PresentationTransportException("文稿关闭多个阶段未确认；保留全部原因，不能盲重试。",
                new AggregateException(failures));
    }
}
