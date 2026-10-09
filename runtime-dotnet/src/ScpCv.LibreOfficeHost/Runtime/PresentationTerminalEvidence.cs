// 当前播放轮的纯值耗尽事实；不按请求次数或页码推测，不访问UNO或写文件。
namespace ScpCv.LibreOfficeHost.Runtime;

internal enum PresentationTerminalEvent { Transition, MainBegin, MainEnd, LastBegin, LastEnd, Lost }

internal sealed class PresentationTerminalEvidence
{
    private readonly long _epoch;
    private readonly long _model;
    private bool _mainActive;
    private bool _lastBegun;
    private bool _lost;

    /// <summary>:param epoch: listener不可变播放轮身份。:param model: 预绑定真实模型的纯值编号。</summary>
    internal PresentationTerminalEvidence(long epoch, long model) { _epoch=epoch; _model=model; }
    internal bool Exhausted { get; private set; }

    /// <summary>:param epoch: 新不可变播放轮。:returns: 只继承已观察仍活动的主序，不继承最后组/耗尽或丢失后的推测。</summary>
    internal PresentationTerminalEvidence RenewForRewind(long epoch) => new(epoch, _model)
        { _mainActive = _mainActive && !_lost };

    /// <summary>:param epoch: 事件的不可变listener身份。:param model: 已冻结节点目录的模型编号。:param kind: 原生事件角色。:returns: 只接本轮、本模型的有序事实。</summary>
    internal void Apply(long epoch, long model, PresentationTerminalEvent kind)
    {
        if (epoch != _epoch || _lost ||
            (kind is PresentationTerminalEvent.Transition or PresentationTerminalEvent.Lost ? model != 0 : model != _model)) return;
        switch (kind)
        {
            case PresentationTerminalEvent.Transition:
            case PresentationTerminalEvent.MainEnd:
                _mainActive=false; _lastBegun=false; Exhausted=false; break;
            case PresentationTerminalEvent.MainBegin:
                _mainActive=true; _lastBegun=false; Exhausted=false; break;
            case PresentationTerminalEvent.LastBegin:
                _lastBegun=_mainActive; Exhausted=false; break;
            case PresentationTerminalEvent.LastEnd:
                Exhausted=_mainActive && _lastBegun; break;
            case PresentationTerminalEvent.Lost:
                _lost=true; _mainActive=false; _lastBegun=false; Exhausted=false; break;
        }
    }
}
