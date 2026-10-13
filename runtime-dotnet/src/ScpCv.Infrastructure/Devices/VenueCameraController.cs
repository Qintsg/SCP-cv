// 摄像机按住短租约与迟到请求围栏；失败释放独立于浏览器取消，绝不盲目重发运动。
using ScpCv.Contracts.Http;

namespace ScpCv.Infrastructure.Devices;

public sealed class VenueCameraController(VenueJoinDispatcher dispatcher, TimeProvider clock) : IAsyncDisposable, IDisposable
{
    private int _disposed;
    private static readonly string[] CameraIds = ["rear", "center1", "center2", "left", "right"];
    private readonly Dictionary<string, CameraSlot> _slots = CameraIds.ToDictionary(id => id, _ => new CameraSlot(), StringComparer.Ordinal);
    private sealed record Motion(Guid Id, int Join, DateTimeOffset ExpiresAt, bool StopPending = false);
    private sealed record PendingPress(Guid Id, CancellationTokenSource Cancellation);
    private sealed class CameraSlot : IDisposable
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal readonly Lock Signal = new();
        internal readonly Dictionary<Guid, DateTimeOffset> Retired = [];
        internal Motion? Active;
        internal PendingPress? Pending;
        public void Dispose() => Gate.Dispose();
    }

    public static int PositionCount => 6;

    /// <summary>:param id: 内置摄像机。:returns: 仅本服务运动意图，不声称物理反馈。</summary>
    public string Intent(string id) => _slots.TryGetValue(id, out var slot) && Volatile.Read(ref slot.Active) is { } active
        ? active.StopPending ? "stop_pending" : "held" : "idle";

    /// <summary>:param request: 带新鲜时间和运动令牌的开始/续租/停止。:returns: 发送状态，不返回虚构的机械位置。</summary>
    public async Task<VenueCameraResult> MotionAsync(string id, CameraControlRequest request, CancellationToken token = default)
    {
        _ = VenueJoinDispatcher.CameraHost(id);
        if (request.MotionId == Guid.Empty || request.Phase is not ("start" or "hold" or "stop"))
            throw new VenueDeviceException("运动必须携带motion_id与start/hold/stop阶段。");
        var join = Direction(request.Direction);
        var slot = _slots[id];
        if (request.Phase == "stop")
        {
            // 不等待正在连网的开始请求释放Gate；先取消它，防止松手后迟到的按下。
            lock (slot.Signal)
            {
                Retire(slot, request.MotionId, clock.GetUtcNow());
                if (slot.Pending?.Id == request.MotionId) slot.Pending.Cancellation.Cancel();
            }
        }
        // stop不继承浏览器取消：松手后客户端退出仍需要发释放。
        await slot.Gate.WaitAsync(request.Phase == "stop" ? CancellationToken.None : token).ConfigureAwait(false);
        try
        {
            var now = clock.GetUtcNow();
            Prune(slot, now);
            if (request.Phase == "stop")
            {
                Retire(slot, request.MotionId, now);
                if (slot.Active?.Id == request.MotionId) await ReleaseAsync(id, slot).ConfigureAwait(false);
                return Result(request.MotionId, "该运动已释放或已失效；未影响其他运动令牌。");
            }
            lock (slot.Signal)
                if (slot.Retired.ContainsKey(request.MotionId)) throw new VenueDeviceException("该运动已停止，迟到请求未执行。");
            if (request.Phase == "hold")
            {
                if (slot.Active is not { } active || active.Id != request.MotionId || active.Join != join ||
                    active.StopPending || active.ExpiresAt <= now)
                    throw new VenueDeviceException("运动租约已失效，续租不能重新启动。");
                slot.Active = active with { ExpiresAt = now.AddMilliseconds(900) };
                return Result(request.MotionId, "运动租约已续期，没有重复发送按下帧。");
            }
            var age = now.ToUnixTimeMilliseconds() - request.IssuedAtMilliseconds;
            if (age is < -250 or > 2000) throw new VenueDeviceException("开始请求已过期或时钟偏差过大，未启动运动。");
            lock (slot.Signal)
                if (slot.Retired.Count >= 256) throw new VenueDeviceException("短时间内运动请求过多，暂不接受新动作。");
            if (slot.Active is { } existing)
            {
                if (existing.Id == request.MotionId && existing.Join == join && !existing.StopPending && existing.ExpiresAt > now)
                    return Result(request.MotionId, "同一开始请求已接受，没有重复按下。");
                Retire(slot, existing.Id, now);
                await ReleaseAsync(id, slot).ConfigureAwait(false);
            }
            // 先登记，写出或取消结果不确定时仍能发0并由看门狗继续安全释放。
            using var pendingToken = CancellationTokenSource.CreateLinkedTokenSource(token);
            lock (slot.Signal)
            {
                if (slot.Retired.ContainsKey(request.MotionId)) throw new VenueDeviceException("开始前已收到停止，未发送按下帧。");
                slot.Active = new(request.MotionId, join, now.AddMilliseconds(900));
                slot.Pending = new(request.MotionId, pendingToken);
            }
            try
            {
                try { await dispatcher.DigitalAsync(id, join, true, pendingToken.Token).ConfigureAwait(false); }
                catch (Exception exception)
                {
                    Retire(slot, request.MotionId, clock.GetUtcNow());
                    try { await ReleaseAsync(id, slot).ConfigureAwait(false); }
                    catch (VenueDeviceException) { /* 保留stop_pending，由看门狗继续发送安全释放。 */ }
                    if (exception is OperationCanceledException && !token.IsCancellationRequested)
                        throw new VenueDeviceException("停止已取消仍在路上的开始请求，未重新启动运动。");
                    throw;
                }
            }
            finally { lock (slot.Signal) slot.Pending = null; }
            return Result(request.MotionId, "方向按下帧已发送；机械动作未读回。松手或900ms无续租后释放。");
        }
        finally { slot.Gate.Release(); }
    }

    /// <summary>:param id: 摄像机。:param position: 1–6。:param action: 存储或调用。:returns: 原工程模式按钮后位置按钮的发送状态。</summary>
    public async Task<VenueCameraResult> PositionAsync(string id, int position, string action, CancellationToken token = default)
    {
        ValidatePosition(id, position, action);
        var slot = _slots[id];
        await slot.Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (slot.Active is { } active) { Retire(slot, active.Id, clock.GetUtcNow()); await ReleaseAsync(id, slot).ConfigureAwait(false); }
            await TapAsync(id, action == "save" ? 38 : 39, token).ConfigureAwait(false);
            await TapAsync(id, 44 + position, token).ConfigureAwait(false);
            return Result(null, $"位置{position}{(action == "save" ? "存储" : "调用")}指令已发送，未读取实际位置；未自动重试。");
        }
        finally { slot.Gate.Release(); }
    }

    public static void ValidatePosition(string id, int position, string action)
    {
        _ = VenueJoinDispatcher.CameraHost(id);
        if (position is < 1 or > 6 || action is not ("call" or "save")) throw new VenueDeviceException("工程只提供位置1–6和save/call动作。");
    }

    /// <summary>:returns: 独立看门狗释放到期/失败动作；失败保持意图，不假称停止。</summary>
    public Task SweepAsync() => Task.WhenAll(_slots.Select(pair => SweepOneAsync(pair.Key, pair.Value, false)));
    public Task StopAllAsync() => Task.WhenAll(_slots.Select(pair => SweepOneAsync(pair.Key, pair.Value, true)));

    private async Task SweepOneAsync(string id, CameraSlot slot, bool force)
    {
        await slot.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (slot.Active is not { } active || (!force && !active.StopPending && active.ExpiresAt > clock.GetUtcNow())) return;
            Retire(slot, active.Id, clock.GetUtcNow());
            await ReleaseAsync(id, slot).ConfigureAwait(false);
        }
        finally { slot.Gate.Release(); }
    }

    private async Task ReleaseAsync(string id, CameraSlot slot)
    {
        if (slot.Active is not { } active) return;
        slot.Active = active with { StopPending = true };
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try { await dispatcher.DigitalAsync(id, active.Join, false, budget.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw new VenueDeviceException("停止帧发送超时，仍保留stop_pending，请检查设备连接。"); }
        slot.Active = null;
    }

    private async Task TapAsync(string id, int join, CancellationToken token)
    {
        // 模式/位置不是运动，也必须按下后释放；取消不跳过松开。
        try
        {
            await dispatcher.DigitalAsync(id, join, true, token).ConfigureAwait(false);
            await Task.Delay(100, token).ConfigureAwait(false);
        }
        finally
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await dispatcher.DigitalAsync(id, join, false, budget.Token).ConfigureAwait(false);
        }
    }

    private static int Direction(string direction) => direction switch
    {
        "up" => 31, "down" => 32, "left" => 33, "right" => 34, "zoom_in" => 35, "zoom_out" => 36,
        _ => throw new VenueDeviceException("只支持上/下/左/右及拉近/拉远。"),
    };
    private static void Prune(CameraSlot slot, DateTimeOffset now)
    {
        lock (slot.Signal)
            foreach (var key in slot.Retired.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray()) slot.Retired.Remove(key);
    }
    private static void Retire(CameraSlot slot, Guid id, DateTimeOffset now)
    {
        // 单摄像机的短窗口内超过限制不接新动作，但安全释放不被容量门禁阻止。
        lock (slot.Signal)
            if (slot.Retired.Count < 256 || slot.Retired.ContainsKey(id)) slot.Retired[id] = now.AddSeconds(5);
    }
    private VenueCameraResult Result(Guid? id, string detail) => new()
    { MotionId = id, Status = dispatcher.IsSimulation ? "simulated" : "sent_unconfirmed", Detail = dispatcher.IsSimulation ? "仿真结果，未发送实体控制。" : detail };

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAllAsync().ConfigureAwait(false);
        foreach (var slot in _slots.Values) slot.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}
