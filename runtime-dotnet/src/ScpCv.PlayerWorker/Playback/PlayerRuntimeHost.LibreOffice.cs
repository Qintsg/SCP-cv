// LibreOffice表面、页码与动画动作适配；统一媒体控制保持明确不支持。
using System.Windows.Controls;
using ScpCv.Contracts.Ipc;
using ScpCv.PlayerWorker.Presentations;
using ScpCv.Runtime.Presentations;

namespace ScpCv.PlayerWorker.Playback;

public sealed partial class PlayerRuntimeHost
{
    /// <summary>:param lease: 相邻整页租约。:param forward: 下一页。:param token: 等待寿命。:returns: 宿主边界结果，非文稿保持原导航语义。</summary>
    private async Task<string> NavigateRelativeAsync(CommandLeaseDto lease, bool forward, CancellationToken token)
    {
        if (_current?.Native is not IPresentationSurface surface)
        {
            await NavigateAsync(lease, forward ? _currentSlide + 1 : Math.Max(1, _currentSlide - 1), token);
            return "ok";
        }
        await surface.ControlAsync(lease, forward ? "next_slide" : "previous_slide", 0, token);
        _currentSlide = surface.CurrentSlide;
        _totalSlides = surface.TotalSlides;
        return surface.LastControlResultCode;
    }

    // 默认路径不变；替换外部引擎adapter仍必须经过同一纯包门禁。
    internal Func<CommandLeaseDto, string, CancellationToken, Task<IPresentationSurface>>? PresentationFactory { get; init; }

    /// <summary>:param lease: 已授权OPEN。:param uri: 原件路径。:param token: 等待寿命。:returns: 已确认文稿表面。</summary>
    private async Task<SurfaceResource> OpenLibreOfficeAsync(CommandLeaseDto lease, string uri, CancellationToken token)
    {
        IPresentationSurface adapter;
        var path = LocalPath(uri);
        if (PresentationFactory is { } factory)
        {
            _ = new PresentationPackageMetadata(path);
            adapter = await factory(lease, path, token);
        }
        else adapter = await LibreOfficeSurface.OpenAsync(_window, _windowId, lease, path, token);
        return new SurfaceResource("libreoffice", new Grid { Background = System.Windows.Media.Brushes.Black }, adapter.DisposeAsync, adapter);
    }

    /// <summary>:param lease: 动画步命令。:param token: 等待寿命。:returns: 实际动画步观测。</summary>
    private async Task<string> StepLibreOfficeAsync(CommandLeaseDto lease, CancellationToken token)
    {
        if (_current?.Native is not IPresentationSurface surface) throw new InvalidOperationException("当前源不支持动画步控制");
        await surface.ControlAsync(lease, lease.Command == "NEXT_EFFECT" ? "next_effect" : "previous_effect", 0, token);
        _currentSlide = surface.CurrentSlide;
        _totalSlides = surface.TotalSlides;
        return surface.LastControlResultCode;
    }
}
