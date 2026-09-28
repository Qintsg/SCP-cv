// 只读 COM 诊断公共回归；所有 Office、Win32 与 STA 均替换为外部边界替身。
using System.Text.Json;
using ScpCv.PowerPointHost.Interop;
using ScpCv.PowerPointHost.Sta;

namespace ScpCv.Windows.Tests;

public sealed class PowerPointDispatchDiagnosticTests
{
    /// <summary>raw Invoke 成功也只能作为诊断，不能替代原始窗口证据或升级原生打开结果。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulRawReadDoesNotUpgradeFailedNativeOpen(bool applicationFailed)
    {
        using var fixture = new DiagnosticFixture();
        object? expectedShow = null;
        fixture.Office.Presentations.OnOpen = presentation => expectedShow = presentation.SlideShowSettings.Window;
        if (applicationFailed) fixture.Inner.ApplicationWindow = null;
        else fixture.Inner.ShowWindow = null;

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal(applicationFailed ? "office_process_unavailable" : "slideshow_hwnd_unavailable", opened.Code);
        Assert.Equal(0, opened.SlideShowWindowHandle);
        Assert.Same(applicationFailed ? fixture.Office : expectedShow, fixture.Probe.Source);
        Assert.Equal(new Guid(applicationFailed ? "91493442-5A91-11CF-8700-00AA0060263B" : "91493453-5A91-11CF-8700-00AA0060263B"), fixture.Probe.InterfaceGuid);
        Assert.Equal(applicationFailed ? 2031 : 2010, fixture.Probe.MemberId);
        using var detail = JsonDocument.Parse(opened.Detail);
        Assert.Equal(fixture.Inner.FailureDetail, detail.RootElement.GetProperty("original_detail").GetString());
        var observed = detail.RootElement.GetProperty("dispatch_probe")[0];
        Assert.Equal(0, observed.GetProperty("HResult").GetInt32());
        Assert.Equal(123L, observed.GetProperty("IntegerValue").GetInt64());
        Assert.Empty(fixture.Office.Presentations.Items);
    }

    /// <summary>诊断异常必须保留原始失败码/安全详情，并且不能输出异常消息。</summary>
    [Fact]
    public async Task ProbeExceptionDoesNotReplaceOriginalFailureOrExposeMessage()
    {
        using var fixture = new DiagnosticFixture();
        fixture.Inner.ApplicationWindow = null;
        fixture.Probe.Failure = new InvalidOperationException("不得输出的诊断异常消息。");

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal("office_process_unavailable", opened.Code);
        using var detail = JsonDocument.Parse(opened.Detail);
        Assert.Equal(fixture.Inner.FailureDetail, detail.RootElement.GetProperty("original_detail").GetString());
        Assert.Equal("probe_failed", detail.RootElement.GetProperty("dispatch_probe")[0].GetProperty("Stage").GetString());
        Assert.DoesNotContain(fixture.Probe.Failure.Message, opened.Detail, StringComparison.Ordinal);
    }

    /// <summary>正常窗口取证不额外探测；失败诊断不能残留到后续真实成功结果。</summary>
    [Fact]
    public async Task RecoveryClearsFailedDiagnosticAndKeepsOriginalSuccessEvidence()
    {
        using var fixture = new DiagnosticFixture();
        var applicationWindow = fixture.Inner.ApplicationWindow;
        fixture.Inner.ApplicationWindow = null;
        var failed = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.False(failed.Succeeded);
        fixture.Inner.ApplicationWindow = applicationWindow;
        fixture.Probe.Source = null;

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.True(opened.Succeeded, opened.Code);
        Assert.Equal(fixture.Inner.ShowWindow!.Handle, opened.SlideShowWindowHandle);
        Assert.Equal(string.Empty, opened.Detail);
        Assert.Null(fixture.Probe.Source);
    }

    /// <summary>原生探针对普通托管对象直接拒绝，不获取 COM 指针或创建外部对象。</summary>
    [Fact]
    public void NativeProbeRejectsNonComInputWithoutOfficeActivation()
    {
        var probe = new WindowsPowerPointDispatchProbe();

        var observed = Assert.Single(probe.Read(new object(), Guid.Empty, 2031));

        Assert.Equal("input_not_com", observed.Stage);
        Assert.Equal(unchecked((int)0x80070057), observed.HResult);
    }

    /// <summary>只持有替身对象和一个精确临时源文件的测试装配。</summary>
    private sealed class DiagnosticFixture : IDisposable
    {
        public string Source { get; } = Path.GetTempFileName();
        public PowerPointOwnershipTests.FakeApplication Office { get; } = new();
        public PowerPointOwnershipTests.FakePowerPointInterop Inner { get; }
        public FakeDispatchProbe Probe { get; } = new();
        public PowerPointComAdapter Adapter { get; }

        /// <summary>装配真实 Adapter、诊断层和外部替身；不创建 WPF/STA 线程。</summary>
        public DiagnosticFixture()
        {
            Inner = new(Office)
            {
                FailureDetail = "{\"source\":\"application\",\"stage\":\"com_hwnd\",\"hresult\":\"0x80020003\"}",
            };
            Adapter = new(new InlineDispatcher(), new DiagnosticPowerPointInterop(Inner, Probe));
        }

        /// <summary>协作释放自有替身，再删除精确临时文件。</summary>
        public void Dispose()
        {
            Adapter.Dispose();
            File.Delete(Source);
        }
    }

    /// <summary>只观察外部探针请求；成功返回值故意不能提供播放所有权证据。</summary>
    private sealed class FakeDispatchProbe : IPowerPointDispatchProbe
    {
        public object? Source { get; set; }
        public Guid InterfaceGuid { get; private set; }
        public int MemberId { get; private set; }
        public InvalidOperationException? Failure { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<PowerPointDispatchObservation> Read(object source, Guid interfaceGuid, int memberId)
        {
            Source = source;
            InterfaceGuid = interfaceGuid;
            MemberId = memberId;
            if (Failure is not null) throw Failure;
            return [new("invoke_property_get", 0, 3, 123, interfaceGuid, memberId, 2)];
        }
    }

    /// <summary>不创建线程或窗口的外部 STA 调度替身。</summary>
    private sealed class InlineDispatcher : IOfficeStaDispatcher
    {
        /// <inheritdoc />
        public Task<T> InvokeAsync<T>(Guid operationId, Func<CancellationToken, T> operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(operation(cancellationToken));
        }
    }
}
