// 在所属UI线程按真实WebMessage推进共享资源；预备/后帧与关闭保持不可逆且按资源隔离。
using System.IO;
using System.Text.Json;

namespace ScpCv.PlayerWorker.ScreenSharing;

internal sealed class ScreenSharePlaybackState(ScreenShareReceiverIdentity identity)
{
    private readonly TaskCompletionSource _prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _activated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ready;
    private bool _activationRequested;
    private bool _terminal;
    private long _activationFloor;
    internal ScreenShareReceiverIdentity Identity { get; } = identity;
    internal long Frames { get; private set; }
    internal bool HasAudio { get; private set; }
    internal bool IsPrepared => !_terminal && _ready && Frames > 0;
    internal bool IsPlaying { get; private set; }
    internal string ErrorCode { get; private set; } = string.Empty;

    /// <summary>:param source: 原生文档来源。:param json: 有界纯值消息。:returns: 仅相同身份且正确schema的消息可推进本资源，不接收用户字符串代码。</summary>
    internal bool Accept(string source, string json)
    {
        if (_terminal || json.Length > 2048 || !Identity.IsDocument(source)) return false;
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var message = document.RootElement;
            if (message.ValueKind != JsonValueKind.Object ||
                !Text(message, "share_id", out var id) || !Guid.TryParse(id, out var parsedId) || parsedId != Identity.ShareId ||
                !Number(message, "window_id", out var window) || window != Identity.WindowId ||
                !Number(message, "source_generation", out var generation) || generation != Identity.SourceGeneration ||
                !Text(message, "type", out var type)) return false;
            string[] fields = type switch
            {
                "ready" => ["frames"], "frame" => ["frames", "media_time", "activated", "has_audio"],
                "closed" => ["frames", "has_stream"], "failed" => ["code"], _ => [],
            };
            if (fields.Length == 0 || !ExactFields(message, fields)) return false;
            switch (type)
            {
                case "ready":
                    if (!Number(message, "frames", out var preparedFrames) || preparedFrames < 0) return false;
                    _ready = true; CompletePreparation(); return true;
                case "frame": return AcceptFrame(message);
                case "closed":
                    if (!Number(message, "frames", out var endedFrames) || endedFrames < 0 ||
                        !Boolean(message, "has_stream", out var hasStream) || hasStream) return false;
                    Fail("share_receiver_closed"); return true;
                case "failed":
                    if (!Text(message, "code", out var code) || code is not ("share_receiver_report_failed" or
                        "share_video_play_failed" or "share_remote_track_ended" or "share_peer_disconnected" or
                        "share_authorization_ended" or "share_negotiation_failed")) return false;
                    Fail(code); return true;
                default: return false;
            }
        }
        catch (JsonException) { return false; }
    }

    /// <summary>:param message: 浏览器实际requestVideoFrameCallback。:returns: 严格递增且激活真实后帧才成为playing。</summary>
    private bool AcceptFrame(JsonElement message)
    {
        if (!Number(message, "frames", out var frames) || frames <= Frames ||
            !Boolean(message, "activated", out var activated) || (activated && !_activationRequested) ||
            !Boolean(message, "has_audio", out var hasAudio) ||
            !message.TryGetProperty("media_time", out var time) || time.ValueKind != JsonValueKind.Number ||
            !time.TryGetDouble(out var seconds) || !double.IsFinite(seconds) || seconds < 0) return false;
        Frames = frames; HasAudio = hasAudio;
        CompletePreparation();
        if (activated && _ready && frames > _activationFloor)
        { IsPlaying = true; _activated.TrySetResult(); }
        return true;
    }

    /// <summary>:returns: 协商与真实视频帧均到达才释放预备等待。</summary>
    private void CompletePreparation() { if (IsPrepared) _prepared.TrySetResult(); }

    /// <summary>:param token: OPEN有限寿命。:returns: 不能只等待NavigationCompleted或ready。</summary>
    internal async Task WaitPreparedAsync(CancellationToken token)
    {
        await _prepared.Task.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (!IsPrepared) throw new IOException("屏幕共享预备资源已结束：" + ErrorCode);
    }

    /// <summary>:returns: 在宿主切入后、调用接收页activate前建立后帧围栏。</summary>
    internal void BeginActivation()
    {
        if (!IsPrepared || _activationRequested) throw new InvalidOperationException("共享资源尚未就绪或已切入。");
        _activationFloor = Frames; _activationRequested = true;
    }

    /// <summary>:param token: OPEN有限寿命。:returns: 等待activated=true且超过预备计数的真实视频帧。</summary>
    internal async Task WaitActivatedAsync(CancellationToken token)
    {
        await _activated.Task.WaitAsync(token);
        token.ThrowIfCancellationRequested();
        if (!IsPlaying) throw new IOException("屏幕共享激活资源已结束：" + ErrorCode);
    }

    /// <summary>:param code: 固定错误码。:returns: 原生故障与主动退役均撤销当前资源的后续消息，唤醒等待者。</summary>
    internal void Fail(string code)
    {
        if (_terminal) return;
        _terminal = true; IsPlaying = false; HasAudio = false; ErrorCode = code;
        _prepared.TrySetException(new IOException("屏幕共享接收结束：" + code));
        _activated.TrySetException(new IOException("屏幕共享接收结束：" + code));
        // 未走到激活的失败路径也观察两个任务的终态；以后await仍会得到原异常。
        _ = _prepared.Task.Exception; _ = _activated.Task.Exception;
    }

    /// <summary>:returns: 原生Dispose开始即拒绝所有迟到回调；消息closed不作为Dispose完成证明。</summary>
    internal void Retire() => Fail("share_receiver_retired");

    /// <summary>:param value: 对象。:param fields: 类型专属字段。:returns: 额外/重复键拒绝，身份字段恰好出现一次。</summary>
    private static bool ExactFields(JsonElement value, string[] fields)
    {
        var properties = value.EnumerateObject().ToArray();
        return properties.Length == fields.Length + 4 && properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length &&
            properties.All(p => p.Name is "type" or "share_id" or "window_id" or "source_generation" || fields.Contains(p.Name, StringComparer.Ordinal));
    }

    /// <summary>:param value: JSON对象。:param key: 字段。:param result: 正确类型的值。:returns: 类型检查先于解析，错误schema不抛系统异常。</summary>
    private static bool Number(JsonElement value, string key, out long result)
    { result = 0; return value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out result); }

    /// <summary>:param value: JSON对象。:param key: 字段。:param result: 字符串。:returns: 只接受实际JSON字符串。</summary>
    private static bool Text(JsonElement value, string key, out string? result)
    { result = null; if (!value.TryGetProperty(key, out var field) || field.ValueKind != JsonValueKind.String) return false; result = field.GetString(); return result is not null; }

    /// <summary>:param value: JSON对象。:param key: 字段。:param result: 布尔值。:returns: 不接受字符串或数字替代。</summary>
    private static bool Boolean(JsonElement value, string key, out bool result)
    { result = false; if (!value.TryGetProperty(key, out var field) || field.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false; result = field.GetBoolean(); return true; }
}
