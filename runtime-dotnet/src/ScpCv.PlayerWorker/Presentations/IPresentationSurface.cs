// 文稿外部引擎的窄接缝；只传页码/命令，不向宿主暴露UNO或原生对象。
using ScpCv.Contracts.Ipc;

namespace ScpCv.PlayerWorker.Presentations;

internal interface IPresentationSurface : IAsyncDisposable
{
    int CurrentSlide { get; }
    int TotalSlides { get; }
    /// <summary>本次已确认控制的结果代码；默认普通控制为ok，不能从历史观测猜边界。</summary>
    string LastControlResultCode => "ok";

    /// <summary>:returns: 激活已经完成预备的自有表面。</summary>
    void Activate();

    /// <summary>:param lease: 当前租约。:param operation: 有限动作。:param slide: 目标页。:param token: 等待寿命。:returns: 真实控制结果。</summary>
    Task ControlAsync(CommandLeaseDto lease, string operation, int slide, CancellationToken token);

    /// <summary>:param token: 等待寿命。:returns: 当前文稿的实际页码采样。</summary>
    Task ObserveAsync(CancellationToken token);
}
