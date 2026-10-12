// 有界内存共享登记：所有权、窗口预约、短期cap与源代次围栏。SCP-cv 2026-10-10。
using System.Security.Cryptography;
using System.Text;
using ScpCv.Infrastructure.Playback;

namespace ScpCv.ControlHost.ScreenSharing;

public sealed partial class ScreenShareRegistry(TimeProvider timeProvider)
{
    private const int MaximumSdpBytes = 65536;
    private const int MaximumTerminalEntries = 32;
    private static readonly TimeSpan ReservationLifetime = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PublisherLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TerminalLifetime = TimeSpan.FromMinutes(1);
    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, ScreenShareEntry> _entries = [];

    public int ActiveCount
    {
        get { lock (_sync) { ExpireLocked(); return _entries.Values.Count(entry => !entry.IsTerminal); } }
    }

    public int RetainedCount
    {
        get { lock (_sync) { ExpireLocked(); return _entries.Count; } }
    }

    /// <summary>预约在线检查后的目标；此步没有任何播放器副作用。</summary>
    public ScreenShareSnapshot Create(string ownerId, ScreenShareTargetIdentity target, bool hasAudio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(target);
        var windowId = target.WindowId;
        if (windowId is not (1 or 2)) throw Error("分享目标只能是窗口1或窗口2。", "share_window_invalid");
        lock (_sync)
        {
            ExpireLocked();
            if (_entries.Values.Any(entry => entry.WindowId == windowId && !entry.IsTerminal))
                throw Error("该窗口已有分享或仍在等待停止确认。", "share_window_busy", 409);
            var now = timeProvider.GetUtcNow();
            var entry = new ScreenShareEntry
            {
                Id = Guid.NewGuid(), OwnerId = ownerId, Target = target, HasAudio = hasAudio,
                CreatedAt = now, LastPublisherSeen = now, ExpiresAt = now + ReservationLifetime,
            };
            _entries.Add(entry.Id, entry);
            return entry.Snapshot();
        }
    }

    /// <summary>只返回所属发布者的快照；不存在和越权使用同一错误。</summary>
    public ScreenShareSnapshot GetOwned(string ownerId, Guid id)
    {
        lock (_sync) { return OwnedLocked(ownerId, id).Snapshot(); }
    }

    /// <summary>仅供服务事务重核原预约身份；该身份不进入发布者DTO。</summary>
    public ScreenShareTargetIdentity GetTargetOwned(string ownerId, Guid id)
    {
        lock (_sync) { return OwnedLocked(ownerId, id).Target; }
    }

    /// <summary>只接受一次有界offer，等待OPEN事务分配代次。</summary>
    public ScreenShareSnapshot SetOffer(string ownerId, Guid id, string type, string sdp)
    {
        lock (_sync)
        {
            var entry = OwnedLocked(ownerId, id);
            ValidateSdp(type, sdp, "offer");
            if (entry.State != ScreenShareState.Allocated)
                throw Error("该会话不能重复协商，请重新发起分享。", "share_negotiation_conflict", 409);
            entry.Offer = new(type, sdp);
            entry.State = ScreenShareState.Negotiating;
            return entry.Snapshot();
        }
    }

    /// <summary>绑定OPEN分配的源代次；token绝不进入持久命令。</summary>
    public ScreenShareSnapshot BindReceiver(string ownerId, Guid id, long sourceGeneration)
    {
        lock (_sync)
        {
            var entry = OwnedLocked(ownerId, id);
            if (sourceGeneration <= 0 || entry.State != ScreenShareState.Negotiating ||
                (entry.SourceGeneration.HasValue && entry.SourceGeneration != sourceGeneration))
                throw Error("分享OPEN身份不匹配。", "share_generation_conflict", 409);
            entry.SourceGeneration = sourceGeneration;
            entry.ReceiverToken ??= Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            return entry.Snapshot();
        }
    }

    /// <summary>调用方必须先核对Worker管道身份；本方法再次核对分享/窗口/代次。</summary>
    public ScreenShareReceiverGrant GetReceiverGrant(Guid id, ScreenShareTargetIdentity target, long sourceGeneration)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (_sync)
        {
            var entry = ReceiverLocked(id, target.WindowId, sourceGeneration);
            if (entry.Target != target)
                throw Error("分享接收授权已失效。", "share_receiver_denied", 404);
            return new(id, target.WindowId, sourceGeneration, entry.ReceiverToken!);
        }
    }

    /// <summary>接收者故障只能终止自己的资源，不能指定任意分享或窗口。</summary>
    public ScreenShareSnapshot StopReceiver(Guid id, int windowId, long generation, string token)
    {
        lock (_sync)
        {
            var entry = AuthorizedReceiverLocked(id, windowId, generation, token);
            StopLocked(entry, "receiver_ended");
            return entry.Snapshot();
        }
    }

    /// <summary>接收页仅可用匹配且仍有效的能力读取一次协商的offer。</summary>
    public ScreenShareSdp ReadOffer(Guid id, int windowId, long sourceGeneration, string token)
    {
        lock (_sync) { return AuthorizedReceiverLocked(id, windowId, sourceGeneration, token).Offer!; }
    }

    /// <summary>有界answer不可被替换；生成answer或连通并不证明视频出画。</summary>
    public void SetAnswer(Guid id, int windowId, long sourceGeneration, string token, string type, string sdp)
    {
        lock (_sync)
        {
            var entry = AuthorizedReceiverLocked(id, windowId, sourceGeneration, token);
            ValidateSdp(type, sdp, "answer");
            if (entry.Answer is not null)
                throw Error("接收者应答不可重复或替换。", "share_negotiation_conflict", 409);
            entry.Answer = new(type, sdp);
        }
    }

    /// <summary>发布者续租不能延长20秒协商上限，也不能授予实际帧状态。</summary>
    public ScreenShareSnapshot Heartbeat(string ownerId, Guid id)
    {
        lock (_sync)
        {
            var entry = OwnedLocked(ownerId, id);
            if (entry.IsTerminal || entry.State == ScreenShareState.Stopping)
                throw Error("分享已结束或正在停止。", "share_ended", 409);
            entry.LastPublisherSeen = timeProvider.GetUtcNow();
            if (entry.State == ScreenShareState.Receiving) entry.ExpiresAt = entry.LastPublisherSeen + PublisherLifetime;
            return entry.Snapshot();
        }
    }

    /// <summary>只信任授权接收资源的实际视频帧，接收活动不延长发布者寿命。</summary>
    public void ReportFrame(Guid id, int windowId, long sourceGeneration, string token, long frames, bool hasAudio, ReceiverQuality? quality = null)
    {
        lock (_sync)
        {
            var entry = AuthorizedReceiverLocked(id, windowId, sourceGeneration, token);
            quality?.Validate();
            if (entry.Answer is null || frames <= entry.ActualFrames || (hasAudio && !entry.HasAudio))
                throw Error("接收帧证据无效。", "share_frame_invalid");
            entry.ActualFrames = frames;
            entry.Quality = quality;
            entry.LastFrameAt = timeProvider.GetUtcNow();
            entry.ReceiverHasAudio = hasAudio;
            entry.State = ScreenShareState.Receiving;
            entry.ExpiresAt = entry.LastPublisherSeen + PublisherLifetime;
        }
    }

    /// <summary>先撤销所有媒体授权；已OPEN的目标仍占用至实际释放确认。</summary>
    public ScreenShareSnapshot BeginStop(string ownerId, Guid id, string reason)
    {
        lock (_sync)
        {
            var entry = OwnedLocked(ownerId, id);
            StopLocked(entry, reason);
            PruneLocked();
            return entry.Snapshot();
        }
    }

    /// <summary>由清理服务读取需条件CLOSE的目标；读取不是关闭成功。</summary>
    public IReadOnlyList<ScreenShareSnapshot> GetPendingStops()
    {
        lock (_sync)
        {
            ExpireLocked();
            return _entries.Values.Where(entry => entry.State == ScreenShareState.Stopping)
                .Select(entry => entry.Snapshot()).ToArray();
        }
    }

    /// <summary>只有同分享/窗口/代次停止证据才能释放预约，旧会话不能影响新分享。</summary>
    public bool ConfirmClosed(Guid id, int windowId, long sourceGeneration)
    {
        lock (_sync)
        {
            ExpireLocked();
            if (!_entries.TryGetValue(id, out var entry) || entry.WindowId != windowId || entry.SourceGeneration != sourceGeneration)
                return false;
            if (entry.State == ScreenShareState.Closed) return true;
            if (entry.State != ScreenShareState.Stopping) return false;
            entry.State = ScreenShareState.Closed;
            entry.TerminalAt = timeProvider.GetUtcNow();
            PruneLocked();
            return true;
        }
    }

    private ScreenShareEntry OwnedLocked(string ownerId, Guid id)
    {
        ExpireLocked();
        if (!_entries.TryGetValue(id, out var entry) || !string.Equals(entry.OwnerId, ownerId, StringComparison.Ordinal))
            throw Error("分享不存在或无权访问。", "share_not_found", 404);
        return entry;
    }

    private ScreenShareEntry ReceiverLocked(Guid id, int windowId, long generation)
    {
        ExpireLocked();
        if (!_entries.TryGetValue(id, out var entry) || entry.WindowId != windowId || generation <= 0 ||
            entry.SourceGeneration != generation || entry.ReceiverToken is null || entry.Offer is null ||
            entry.State is not (ScreenShareState.Negotiating or ScreenShareState.Receiving))
            throw Error("分享接收授权已失效。", "share_receiver_denied", 404);
        return entry;
    }

    private ScreenShareEntry AuthorizedReceiverLocked(Guid id, int windowId, long generation, string token)
    {
        var entry = ReceiverLocked(id, windowId, generation);
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(entry.ReceiverToken!)))
            throw Error("分享接收授权已失效。", "share_receiver_denied", 404);
        return entry;
    }

    private void ExpireLocked()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in _entries.Values)
            if (!entry.IsTerminal && entry.State != ScreenShareState.Stopping && entry.ExpiresAt <= now)
                StopLocked(entry, "publisher_timeout");
        PruneLocked();
    }

    private void StopLocked(ScreenShareEntry entry, string reason)
    {
        if (entry.IsTerminal || entry.State == ScreenShareState.Stopping) return;
        entry.ReceiverToken = null;
        entry.Offer = null;
        entry.Answer = null;
        entry.Quality = null;
        entry.StopReason = reason;
        entry.State = entry.SourceGeneration.HasValue ? ScreenShareState.Stopping : ScreenShareState.Closed;
        if (entry.IsTerminal) entry.TerminalAt = timeProvider.GetUtcNow();
    }

    private void PruneLocked()
    {
        var now = timeProvider.GetUtcNow();
        var terminal = _entries.Values.Where(entry => entry.IsTerminal)
            .OrderBy(entry => entry.TerminalAt).ThenBy(entry => entry.CreatedAt).ToArray();
        for (var index = 0; index < terminal.Length; index++)
            if (index < terminal.Length - MaximumTerminalEntries || terminal[index].TerminalAt + TerminalLifetime <= now)
                _entries.Remove(terminal[index].Id);
    }

    private static void ValidateSdp(string type, string sdp, string expectedType)
    {
        if (!string.Equals(type, expectedType, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(sdp) ||
            !sdp.StartsWith("v=0\r\n", StringComparison.Ordinal))
            throw Error("媒体协商内容或类型无效。", "share_sdp_invalid");
        if (Encoding.UTF8.GetByteCount(sdp) > MaximumSdpBytes)
            throw Error("媒体协商内容超过64KiB限制。", "share_sdp_too_large", 413);
    }

    private static ScreenShareException Error(string message, string code, int statusCode = 400) => new(message, code, statusCode);
}
