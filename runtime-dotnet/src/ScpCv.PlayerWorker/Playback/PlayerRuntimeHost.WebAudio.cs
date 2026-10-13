// 普通网页保温只保留页面，不保留可听声音；切换失败恢复旧声音并清候选归属。
using ScpCv.Contracts.Presentations;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param previous: 当前声音。:param next: 已准备候选。:returns: 新当前页面出声，旧保温页静音，失败不留双路声音。</summary>
    private async Task ActivateWebAudioAsync(SurfaceResource? previous, SurfaceResource next)
    {
        if (ReferenceEquals(previous, next)) { next.SetAudible(true); return; }
        try
        {
            previous?.SetAudible(false);
            next.SetAudible(true);
        }
        catch (Exception activation)
        {
            Exception? rollback = null;
            try { previous?.SetAudible(true); }
            catch (Exception failure) { rollback = failure; }
            foreach (var key in _warmWebResources.Where(pair => ReferenceEquals(pair.Value, next)).Select(pair => pair.Key).ToArray())
                _warmWebResources.Remove(key);
            if (previous is not null && previous.Kind is not ("libreoffice" or "screen_share")) _window.SetSurface(previous.Surface);
            await DiscardCandidateAfterRetirementFailureAsync(next, activation);
            if (rollback is not null) throw new PresentationTransportException("网页切入失败且旧声音恢复未确认。", new AggregateException(activation, rollback));
            throw;
        }
    }
}
