// 在原始窗口取证失败后附加同对象只读诊断，保留原失败结果和所有权门禁。
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ScpCv.PowerPointHost.Interop;

/// <summary>失败诊断附加层；成功结果只来自原窗口取证，诊断结果永不升级为成功。</summary>
public sealed class DiagnosticPowerPointInterop(IPowerPointInterop interop, IPowerPointDispatchProbe probe) : IPowerPointInterop
{
    private static readonly Guid ApplicationInterface = new("91493442-5A91-11CF-8700-00AA0060263B");
    private static readonly Guid SlideShowInterface = new("91493453-5A91-11CF-8700-00AA0060263B");

    /// <summary>原始安全失败详情与当前对象的只读诊断。</summary>
    public string FailureDetail { get; private set; } = string.Empty;

    /// <inheritdoc />
    public object CreateApplication() => interop.CreateApplication();

    /// <inheritdoc />
    public PowerPointWindowEvidence? GetApplicationWindow(object application)
    {
        FailureDetail = string.Empty;
        var evidence = interop.GetApplicationWindow(application);
        if (evidence is null) AddFailureDiagnostic(application, ApplicationInterface, 2031);
        return evidence;
    }

    /// <inheritdoc />
    public PowerPointWindowEvidence? GetSlideShowWindow(object slideShowWindow)
    {
        FailureDetail = string.Empty;
        var evidence = interop.GetSlideShowWindow(slideShowWindow);
        if (evidence is null) AddFailureDiagnostic(slideShowWindow, SlideShowInterface, 2010);
        return evidence;
    }

    /// <summary>
    /// 保留原始失败，追加探测结果；探测本身失败也不能改变主结果。
    /// :param source: 原取证持有的同一对象。
    /// :param interfaceGuid: 原声明接口身份。
    /// :param memberId: 原请求的固定 DispId。
    /// :returns: 无返回值；窗口取证仍然失败。
    /// </summary>
    private void AddFailureDiagnostic(object source, Guid interfaceGuid, int memberId)
    {
        var original = interop.FailureDetail;
        IReadOnlyList<PowerPointDispatchObservation> observations;
        try { observations = probe.Read(source, interfaceGuid, memberId); }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException
            or InvalidCastException or System.ComponentModel.Win32Exception)
        {
            observations = [new("probe_failed", exception.HResult, InterfaceGuid: interfaceGuid, MemberId: memberId)];
        }
        finally { GC.KeepAlive(source); }
        FailureDetail = JsonSerializer.Serialize(new
        {
            component = "powerpoint_window",
            stage = "original_window_evidence_failed",
            original_detail = original,
            dispatch_probe = observations,
        });
    }
}
