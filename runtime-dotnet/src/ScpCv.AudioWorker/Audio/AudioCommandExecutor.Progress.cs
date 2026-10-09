// 音频Worker同一执行循环内的只读采样；只投影成功命令的源/代次，不触发原生控制。
using System.Text.Json;
using ScpCv.Contracts.Runtime;

namespace ScpCv.AudioWorker.Audio;

public sealed partial class AudioCommandExecutor
{
    /// <summary>:param cancellationToken: Worker循环寿命。:returns: 已完成命令源的实际观测；未知或自然末尾不给假样本。</summary>
    public Task<WorkerStateSample?> SampleProgressAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_observedGeneration <= 0 || _observedSourceId <= 0 || audio.SourceId != _observedSourceId)
            return Task.FromResult<WorkerStateSample?>(null);
        // 原生Ended也表现为stopped；该状态留给带身份/代次的Finished与显式STOP结果，避免先采样stopped挡住列表推进。
        var playbackState = audio.PlaybackState;
        if (playbackState is not ("playing" or "paused" or "error")) return Task.FromResult<WorkerStateSample?>(null);
        return Task.FromResult<WorkerStateSample?>(new WorkerStateSample(_observedGeneration, Snapshot(_observedGeneration, playbackState)));
    }

    /// <summary>:param generation: 已确认命令代次。:param playbackState: 本次先读取的状态，避免二次读取跨自然末尾。:returns: 纯值观测，不授予声音或解码完成。</summary>
    private JsonElement Snapshot(long generation, string? playbackState = null) => JsonSerializer.SerializeToElement(new
    {
        source_generation = generation,
        source_id = audio.SourceId == 0 ? (long?)null : audio.SourceId,
        playback_state = playbackState ?? audio.PlaybackState,
        position_ms = audio.PositionMs,
        duration_ms = audio.DurationMs,
        volume = audio.Volume,
        muted = audio.IsMuted,
        loop_enabled = audio.LoopEnabled,
    });
}
