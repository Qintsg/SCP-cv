// 原生候选切入成功后才发布实际身份；已确认空输出与清理不确定不得混淆。
using System.Windows.Controls;
using ScpCv.Contracts.Ipc;
using ScpCv.Contracts.Runtime;
using ScpCv.Contracts.Presentations;
using ScpCv.PlayerWorker.Presentations;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    private readonly HashSet<SurfaceResource> _unconfirmedCandidates = [];
    /// <summary>:param lease: 当前OPEN。:param previous: 上一资源。:param candidate: 未发布的候选。:returns: 确认无输出的错误结果，或成功可继续发布；仍存活旧源不被冒替。</summary>
    private async Task<WorkerExecutionResult?> ActivateCandidateAsync(CommandLeaseDto lease, SurfaceResource? previous, SurfaceResource candidate)
    {
        if (candidate.Native is not IPresentationSurface native) return null;
        try { native.Activate(); }
        catch (Exception activation)
        {
            var noPrevious = previous is null || previous.Kind == "libreoffice";
            if (noPrevious) ClearReleasedOutput(lease.SourceGeneration, activation.Message);
            try { await candidate.DisposeAsync(); }
            catch (Exception cleanup)
            {
                _unconfirmedCandidates.Add(candidate);
                throw new PresentationTransportException("候选窗口切入失败且清理未确认；播放资源已隔离，不能安全重试。",
                    new AggregateException(activation, cleanup));
            }
            if (!noPrevious) throw;
            // 两资源均确认不再持有；此时才允许报告明确空输出，不把清理不确定当空窗口。
            return new WorkerExecutionResult("failed", "presentation_activation_failed", Snapshot());
        }
        return null;
    }

    /// <summary>:param generation: 当前OPEN代次。:param message: 切入错误。:returns: 清已确认退役的实际资源，不推断未知候选是否已退出。</summary>
    private void ClearReleasedOutput(long generation, string message)
    {
        _current = null;
        _sourceId = 0; _sourceRevision = 0; _sourceUri = string.Empty;
        _generation = generation;
        _currentSlide = 0; _totalSlides = 0;
        _state = "error"; _errorMessage = message;
        _window.SetSurface(new Grid { Background = System.Windows.Media.Brushes.Black });
    }

    /// <summary>:returns: 只尝试自有候选的关闭；失败仍持有归属，不允许CLOSE假成功或普通控制绕过隔离。</summary>
    private async Task CloseUnconfirmedCandidatesAsync()
    {
        foreach (var candidate in _unconfirmedCandidates.ToArray())
        {
            try { await candidate.DisposeAsync(); }
            catch (Exception cleanup)
            { throw new PresentationTransportException("候选资源关闭仍未确认；归属继续保留。", cleanup); }
            _unconfirmedCandidates.Remove(candidate);
        }
    }
}
