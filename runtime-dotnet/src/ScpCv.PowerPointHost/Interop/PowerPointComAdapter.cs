// 独立 STA 中的 PowerPoint COM 放映、导航与归属清理。
using System.Collections.Concurrent;
using System.IO;
using ScpCv.PowerPointHost.Sta;

namespace ScpCv.PowerPointHost.Interop;

public sealed record PowerPointOpenResult(
    bool Succeeded,
    string Code,
    long PresentationIdentity,
    nint SlideShowWindowHandle,
    int SlideCount,
    int ProcessId = 0,
    DateTimeOffset ProcessStart = default,
    int CurrentSlide = 1)
{
    public int ProjectedCurrentSlide => Math.Clamp(CurrentSlide, 1, Math.Max(1, SlideCount));
}

public sealed record PowerPointNavigationResult(bool Succeeded, int CurrentSlide);

public sealed record PowerPointSlideExportResult(bool Succeeded, string Code, int PageCount, string Detail);

/// <summary>所有 COM 对象只在 OfficeStaDispatcher 所在线程创建、访问和释放。</summary>
public sealed class PowerPointComAdapter : IDisposable
{
    private readonly IOfficeStaDispatcher _sta;
    private readonly IPowerPointInterop _interop;
    private readonly ConcurrentDictionary<long, (dynamic Presentation, string Path)> _presentations = new();
    private dynamic? _application;
    private bool _createdApplication;
    private long _nextIdentity;
    private int _disposed;

    /// <summary>
    /// 使用真实 Office STA 与 COM/Win32 互操作，保留原有调用入口。
    /// :param sta: Office 专属 STA 调度器。
    /// </summary>
    public PowerPointComAdapter(OfficeStaDispatcher sta) : this(sta, new WindowsPowerPointInterop()) { }

    /// <summary>
    /// 从外部调度和互操作契约构造 Adapter；所有文稿规则仍经过相同执行路径。
    /// :param sta: 负责外部操作调度与 operation_id 去重的调度器。
    /// :param interop: 真实 COM/Win32 的外部访问实现。
    /// </summary>
    public PowerPointComAdapter(IOfficeStaDispatcher sta, IPowerPointInterop interop)
    {
        ArgumentNullException.ThrowIfNull(sta);
        ArgumentNullException.ThrowIfNull(interop);
        _sta = sta;
        _interop = interop;
    }

    public static bool IsAvailable =>
        OperatingSystem.IsWindows() && Type.GetTypeFromProgID("PowerPoint.Application") is not null;

    public Task<PowerPointOpenResult> OpenAsync(Guid operationId, string path, CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ => OpenCore(path), cancellationToken);

    public Task<PowerPointNavigationResult> NavigateAsync(
        Guid operationId,
        long identity,
        string action,
        int slide,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ =>
        {
            if (!_presentations.TryGetValue(identity, out var item)) return new PowerPointNavigationResult(false, 0);
            var count = (int)item.Presentation.Slides.Count;
            dynamic view = item.Presentation.SlideShowWindow.View;
            switch (action.Trim().ToLowerInvariant())
            {
                case "next":
                    try { view.GotoNextClick(); } catch { if ((int)view.CurrentShowPosition < count) view.Next(); }
                    break;
                case "prev":
                    try { view.GotoPreClick(); } catch { if ((int)view.CurrentShowPosition > 1) view.Previous(); }
                    break;
                case "goto" when slide >= 1 && slide <= count:
                    view.GotoSlide(slide);
                    break;
                default:
                    return new PowerPointNavigationResult(false, (int)view.CurrentShowPosition);
            }
            return new PowerPointNavigationResult(true, (int)view.CurrentShowPosition);
        }, cancellationToken);

    public Task<bool> ControlPlaybackAsync(
        Guid operationId,
        long identity,
        string action,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ =>
        {
            if (!_presentations.TryGetValue(identity, out var item)) return false;
            try
            {
                dynamic view = item.Presentation.SlideShowWindow.View;
                switch (action.Trim().ToLowerInvariant())
                {
                    case "play": view.State = 1 /* ppSlideShowRunning */; break;
                    case "pause": view.State = 2 /* ppSlideShowPaused */; break;
                    case "stop": view.Exit(); break;
                    default: return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);

    public Task<bool> ControlMediaAsync(
        Guid operationId,
        long identity,
        string action,
        string? mediaId = null,
        int? mediaIndex = null,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ =>
        {
            if (!_presentations.TryGetValue(identity, out var item)) return false;
            try
            {
                dynamic view = item.Presentation.SlideShowWindow.View;
                dynamic? player = ResolveMediaPlayer(item.Presentation, view, mediaId, mediaIndex);

                if (player is null) return false;
                switch (action.Trim().ToLowerInvariant())
                {
                    case "play": player.Play(); break;
                    case "pause": player.Pause(); break;
                    case "stop": player.Stop(); break;
                    case "toggle":
                        try { if ((int)player.State == 1) player.Pause(); else player.Play(); }
                        catch { player.Play(); }
                        break;
                    default: return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);

    public Task<bool> CloseAsync(Guid operationId, long identity, CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ => CloseCore(identity), cancellationToken);

    public Task<bool> ExportPdfAsync(Guid operationId, long identity, string outputPath, CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ =>
        {
            if (!_presentations.TryGetValue(identity, out var item)) return false;
            item.Presentation.SaveAs(outputPath, 32 /* ppSaveAsPDF */);
            return File.Exists(outputPath);
        }, cancellationToken);

    /// <summary>上传阶段在独立 STA 导出 PNG，绝不创建 SlideShow 放映窗口。</summary>
    public Task<PowerPointSlideExportResult> ExportSlidesAsync(
        Guid operationId,
        string path,
        string outputDirectory,
        CancellationToken cancellationToken = default) =>
        _sta.InvokeAsync(operationId, _ => ExportSlidesCore(path, outputDirectory), cancellationToken);

    private PowerPointSlideExportResult ExportSlidesCore(string path, string outputDirectory)
    {
        if (!File.Exists(path)) return new(false, "source_missing", 0, "文稿原件不存在。");
        if (!_presentations.IsEmpty) return new(false, "office_slot_busy", 0, "当前有原生放映，暂不转换上传文稿。");
        var fullOutput = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(fullOutput) && Directory.EnumerateFileSystemEntries(fullOutput).Any())
            return new(false, "output_not_empty", 0, "转换暂存目录不是空目录。");

        dynamic? presentation = null;
        dynamic? slides = null;
        dynamic? setup = null;
        try
        {
            if (_application is null)
            {
                _application = _interop.CreateApplication();
                _createdApplication = true;
            }

            dynamic presentations = _application.Presentations;
            try
            {
                if ((int)presentations.Count != 0)
                    return new(false, "office_not_exclusive", 0, "现有 PowerPoint 实例含其他文稿，未接管用户内容。");
                // Open(FileName, ReadOnly, Untitled, WithWindow)：只读且不创建编辑窗口。
                presentation = presentations.Open(path, -1, 0, 0);
            }
            finally { MarshalFinalRelease(presentations); }

            slides = presentation.Slides;
            var pageCount = (int)slides.Count;
            if (pageCount is < 1 or > 500)
                return new(false, "invalid_page_count", 0, "文稿页数必须为 1 到 500。" );
            setup = presentation.PageSetup;
            var slideWidth = Convert.ToDouble(setup.SlideWidth, System.Globalization.CultureInfo.InvariantCulture);
            var slideHeight = Convert.ToDouble(setup.SlideHeight, System.Globalization.CultureInfo.InvariantCulture);
            if (slideWidth <= 0 || slideHeight <= 0)
                return new(false, "invalid_slide_size", 0, "文稿页面尺寸无效。");
            const int imageWidth = 1920;
            var imageHeight = Math.Clamp((int)Math.Round(imageWidth * slideHeight / slideWidth), 1, 2160);
            Directory.CreateDirectory(fullOutput);
            for (var index = 1; index <= pageCount; index++)
            {
                dynamic slide = slides[index];
                try
                {
                    var file = Path.Combine(fullOutput, $"page-{index:0000}.png");
                    slide.Export(file, "PNG", imageWidth, imageHeight);
                    if (!File.Exists(file) || new FileInfo(file).Length == 0)
                        throw new IOException($"第 {index} 页 PNG 未生成。");
                }
                finally { MarshalFinalRelease(slide); }
            }
            return new(true, "ok", pageCount, "逐页 PNG 已生成。");
        }
        catch (Exception exception)
        {
            return new(false, $"com_export_failed:{exception.GetType().Name}", 0, exception.Message);
        }
        finally
        {
            if (setup is not null) MarshalFinalRelease(setup);
            if (slides is not null) MarshalFinalRelease(slides);
            if (presentation is not null)
            {
                try { presentation.Close(); } catch { }
                MarshalFinalRelease(presentation);
            }
            ReleaseIdleApplication();
        }
    }

    /// <summary>
    /// 只有本 Host 创建、无登记文稿且当前集合为空时退出；集合不可读则只释放引用。
    /// :returns: 无返回值；无法证明独占时保留外部 Office 进程。
    /// </summary>
    private void ReleaseIdleApplication()
    {
        if (!_createdApplication || _application is null || !_presentations.IsEmpty) return;
        dynamic? presentations = null;
        try
        {
            presentations = _application.Presentations;
            if ((int)presentations.Count == 0) _application.Quit();
        }
        catch { }
        finally
        {
            if (presentations is not null) MarshalFinalRelease(presentations);
            MarshalFinalRelease(_application);
            _application = null;
            _createdApplication = false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                _sta.InvokeAsync(Guid.NewGuid(), _ =>
                {
                    foreach (var identity in _presentations.Keys.ToArray()) CloseCore(identity);
                    // 与转换路径共用集合检查/释放；不遗漏临时 Presentations 的 COM 引用。
                    ReleaseIdleApplication();
                    if (_application is not null) MarshalFinalRelease(_application);
                    _application = null;
                    _createdApplication = false;
                    return true;
                }).GetAwaiter().GetResult();
            }
            catch { /* STA/COM 失控时由 OwnershipGuard 保留证据，不强杀用户 Office。 */ }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 在 STA 打开并登记本次文稿；失败补偿只关闭刚取得的文稿对象。
    /// :param path: 已存在的文稿原件路径。
    /// :returns: 放映结果与精确窗口/进程证据。
    /// </summary>
    private PowerPointOpenResult OpenCore(string path)
    {
        if (!File.Exists(path)) return new(false, "source_missing", 0, 0, 0);
        if (!_presentations.IsEmpty) return new(false, "office_slot_busy", 0, 0, 0);
        dynamic? openingPresentation = null;
        var openingIdentity = 0L;
        dynamic? presentations = null;
        dynamic? settings = null;
        dynamic? showWindow = null;
        try
        {
            var createdNow = _application is null;
            if (_application is null)
            {
                _application = _interop.CreateApplication();
            }
            presentations = _application.Presentations;
            // 即使复用同一 Application，用户也可能在上一次 Close 后加入文稿。
            if ((int)presentations.Count != 0)
            {
                if (createdNow)
                {
                    MarshalFinalRelease(_application);
                    _application = null;
                }
                return new(false, "office_not_exclusive", 0, 0, 0);
            }
            if (createdNow) _createdApplication = true;
            _application.Visible = true;
            var applicationWindow = _interop.GetApplicationWindow((object)_application);
            if (applicationWindow is null || applicationWindow.Handle == 0 ||
                applicationWindow.ProcessId <= 0 || applicationWindow.ProcessStart == default)
                return new(false, "office_process_unavailable", 0, 0, 0);
            // IDispatch 后期绑定不支持命名参数：PowerPoint 不通过 GetIDsOfNames 暴露
            // 参数名，`Open(path, WithWindow: -1)` 会抛 MissingMemberException。
            // 按签名位置传参：Open(FileName, ReadOnly, Untitled, WithWindow)，
            // WithWindow 取 msoTrue(-1) 以便后续附着放映窗口 HWND。
            dynamic presentation = presentations.Open(path, 0, 0, -1);
            openingPresentation = presentation;
            openingIdentity = Interlocked.Increment(ref _nextIdentity);
            if ((int)presentations.Count != 1)
                return new(false, "office_not_exclusive", 0, 0, 0);
            settings = presentation.SlideShowSettings;
            showWindow = settings.Run();
            var window = _interop.GetSlideShowWindow((object)showWindow);
            if (window is null || window.Handle == 0 || window.ProcessId <= 0 || window.ProcessStart == default)
                return new(false, "slideshow_hwnd_unavailable", 0, 0, 0);
            if (window.ProcessId != applicationWindow.ProcessId || window.ProcessStart != applicationWindow.ProcessStart)
                return new(false, "slideshow_owner_mismatch", 0, 0, 0);
            var slides = (int)presentation.Slides.Count;
            var currentSlide = 1;
            try { currentSlide = (int)showWindow.View.CurrentShowPosition; }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"PowerPoint 初始页码读取失败，按第 1 页上报：{exception.Message}");
            }
            // COM 调用可泵消息并让用户同时加入文稿；登记成功前再次复核独占状态。
            if ((int)presentations.Count != 1)
                return new(false, "office_not_exclusive", 0, 0, 0);
            _presentations[openingIdentity] = (presentation, path);
            openingPresentation = null;
            return new(true, "ok", openingIdentity, window.Handle, slides, window.ProcessId, window.ProcessStart, currentSlide);
        }
        catch (Exception exception)
        {
            return new(false, $"com_open_failed:{exception.GetType().Name}", 0, 0, 0);
        }
        finally
        {
            if (showWindow is not null) MarshalFinalRelease(showWindow);
            if (settings is not null) MarshalFinalRelease(settings);
            if (openingPresentation is not null)
            {
                var retained = false;
                try { openingPresentation.Close(); }
                catch (Exception exception)
                {
                    // 补偿失败不能丢失自有对象；保留槽位，由协作 Dispose 重试，绝不强退 Office。
                    _presentations[openingIdentity] = (openingPresentation, path);
                    retained = true;
                    Console.Error.WriteLine($"PowerPoint 打开失败后的文稿关闭失败，已保留自有槽位：{exception.Message}");
                }
                if (!retained) MarshalFinalRelease(openingPresentation);
            }
            if (presentations is not null) MarshalFinalRelease(presentations);
        }
    }

    private bool CloseCore(long identity)
    {
        if (!_presentations.TryGetValue(identity, out var item)) return true;
        try
        {
            item.Presentation.Close();
            _presentations.TryRemove(identity, out _);
            MarshalFinalRelease(item.Presentation);
            return true;
        }
        catch { return false; }
    }

    private static void MarshalFinalRelease(object value)
    {
        try
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(value))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
        }
        catch { }
    }

    private static dynamic? ResolveMediaPlayer(
        dynamic presentation,
        dynamic view,
        string? mediaId,
        int? mediaIndex)
    {
        var candidates = new List<int>();
        if (int.TryParse(mediaId, out var parsed) && parsed > 0) candidates.Add(parsed);
        try
        {
            dynamic slide = presentation.Slides(view.CurrentShowPosition);
            for (var index = 1; index <= (int)slide.Shapes.Count; index++)
            {
                dynamic shape = slide.Shapes(index);
                try
                {
                    _ = shape.MediaFormat;
                    var shapeId = (int)shape.Id;
                    if (!candidates.Contains(shapeId)) candidates.Add(shapeId);
                }
                catch { }
            }
        }
        catch { }

        if (mediaIndex is > 0 && mediaIndex <= candidates.Count)
        {
            var preferred = candidates[mediaIndex.Value - 1];
            candidates.Remove(preferred);
            candidates.Insert(0, preferred);
        }
        foreach (var candidate in candidates)
        {
            try { return view.Player(candidate); } catch { }
        }
        return null;
    }

}
