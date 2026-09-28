// 通过公共 Adapter 验证 Office 共享文稿保护；替换全部 COM、Win32 与 STA 外部访问。
using System.Text.Json;
using ScpCv.Contracts.Ipc;
using ScpCv.PowerPointHost.Interop;
using ScpCv.PowerPointHost.Sta;

namespace ScpCv.Windows.Tests;

public sealed class PowerPointOwnershipTests
{
    /// <summary>复用 Application 前必须重新检查后来加入的用户文稿。</summary>
    [Fact]
    public async Task ReopeningAfterUserJoinsPreservesUserAndRejectsNativeShow()
    {
        using var fixture = new OfficeFixture();
        var first = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.True(first.Succeeded, first.Code);
        Assert.True(await fixture.Adapter.CloseAsync(Guid.NewGuid(), first.PresentationIdentity));
        var user = fixture.Office.Presentations.AddUserPresentation();

        var reopened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(reopened.Succeeded);
        Assert.Equal("office_not_exclusive", reopened.Code);
        fixture.Adapter.Dispose();
        Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
        Assert.False(user.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>相同进程名或相近创建时间不能替代 Application 的精确 PID/启动时间。</summary>
    [Theory]
    [InlineData(99, false)]
    [InlineData(42, true)]
    public async Task ForeignSlideShowEvidenceIsRejectedAndOnlyOpeningPresentationIsClosed(
        int processId, bool reusedProcessId)
    {
        using var fixture = new OfficeFixture();
        var original = fixture.Interop.ShowWindow!;
        fixture.Interop.ShowWindow = original with
        {
            ProcessId = processId,
            ProcessStart = reusedProcessId ? original.ProcessStart.AddSeconds(1) : original.ProcessStart,
        };

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal("slideshow_owner_mismatch", opened.Code);
        Assert.Empty(fixture.Office.Presentations.Items);
    }

    /// <summary>Office 可重入调用期间加入用户文稿时，本次打开必须失败并仅补偿自有文稿。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserJoiningDuringOfficeCallIsPreservedAndNativeOpenFails(bool duringRun)
    {
        using var fixture = new OfficeFixture();
        FakePresentation? user = null;
        fixture.Office.Presentations.OnOpen = presentation =>
        {
            if (duringRun)
                presentation.SlideShowSettings.OnRun = () => user = fixture.Office.Presentations.AddUserPresentation();
            else
                user = fixture.Office.Presentations.AddUserPresentation();
        };

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal("office_not_exclusive", opened.Code);
        fixture.Adapter.Dispose();
        Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
        Assert.False(user!.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>已有用户文稿时初次打开与转换均拒绝，未知身份 Close 也不触碰用户对象。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingUserPresentationSurvivesRejectedOpenOrConversion(bool conversion)
    {
        using var fixture = new OfficeFixture();
        var user = fixture.Office.Presentations.AddUserPresentation();
        if (conversion)
        {
            var exported = await fixture.Adapter.ExportSlidesAsync(Guid.NewGuid(), fixture.Source,
                fixture.Source + "-unused-output");
            Assert.False(exported.Succeeded);
            Assert.Equal("office_not_exclusive", exported.Code);
        }
        else
        {
            var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
            Assert.False(opened.Succeeded);
            Assert.Equal("office_not_exclusive", opened.Code);
        }
        Assert.True(await fixture.Adapter.CloseAsync(Guid.NewGuid(), long.MaxValue));

        fixture.Adapter.Dispose();

        Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
        Assert.False(user.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>停机只关闭登记文稿；用户后来加入时保留 Application，否则协作退出空实例。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposeClosesOwnedPresentationAndQuitsOnlyEmptyApplication(bool shared)
    {
        using var fixture = new OfficeFixture();
        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.True(opened.Succeeded, opened.Code);
        var owned = Assert.Single(fixture.Office.Presentations.Items);
        var user = shared ? fixture.Office.Presentations.AddUserPresentation() : null;

        fixture.Adapter.Dispose();

        Assert.True(owned.Closed);
        Assert.Equal(!shared, fixture.Office.QuitRequested);
        if (shared)
        {
            Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
            Assert.False(user!.Closed);
        }
        else Assert.Empty(fixture.Office.Presentations.Items);
    }

    /// <summary>无法证明 Application 或 Run 窗口归属时，不返回可附着的成功句柄。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingWindowEvidenceFailsAndLeavesNoOwnedPresentation(bool applicationEvidenceMissing)
    {
        using var fixture = new OfficeFixture();
        if (applicationEvidenceMissing) fixture.Interop.ApplicationWindow = null;
        else fixture.Interop.ShowWindow = null;

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal(applicationEvidenceMissing ? "office_process_unavailable" : "slideshow_hwnd_unavailable", opened.Code);
        Assert.Equal(0, opened.SlideShowWindowHandle);
        Assert.Empty(fixture.Office.Presentations.Items);
    }

    /// <summary>窗口取证失败必须通过结果携带受控诊断，不能只写不可见的控制台。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingWindowEvidenceIncludesExternalFailureDetail(bool applicationEvidenceMissing)
    {
        using var fixture = new OfficeFixture();
        const string diagnostic = "{\"source\":\"application\",\"stage\":\"com_hwnd\",\"hresult\":\"0x80004005\"}";
        fixture.Interop.FailureDetail = diagnostic;
        if (applicationEvidenceMissing) fixture.Interop.ApplicationWindow = null;
        else fixture.Interop.ShowWindow = null;

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal(diagnostic, opened.Detail);
        Assert.Equal(applicationEvidenceMissing ? "office_process_unavailable" : "slideshow_hwnd_unavailable", opened.Code);
    }

    /// <summary>取证失败经过真实 Office 执行器传回诊断；没有详细证据时仍兼容错误码。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOfficeRequestReportsEvidenceDetailOrExistingCode(bool hasDetail)
    {
        using var fixture = new OfficeFixture();
        fixture.Interop.ApplicationWindow = null;
        const string diagnostic = "{\"source\":\"application\",\"stage\":\"is_window\",\"raw_hwnd\":0,\"is_window\":false}";
        fixture.Interop.FailureDetail = hasDetail ? diagnostic : string.Empty;
        var executor = new PowerPointOfficeRequestExecutor(fixture.Adapter, 1, 2);
        var request = new OfficeRequestDto
        {
            OfficeOperationId = Guid.NewGuid(),
            GroupEpoch = 1,
            HostEpoch = 2,
            SlotEpoch = 1,
            Operation = "open",
            Parameters = new() { ["path"] = JsonSerializer.SerializeToElement(fixture.Source) },
        };

        var result = await executor.ExecuteAsync(request);

        Assert.Equal("failed", result.Status);
        Assert.Equal("office_process_unavailable", result.ErrorCode);
        Assert.Equal(hasDetail ? diagnostic : "office_process_unavailable", result.ErrorDetail);
    }

    /// <summary>放映调用抛异常时，补偿只影响该次打开取得的对象。</summary>
    [Fact]
    public async Task RunFailureClosesOwnedPresentationAndKeepsConcurrentUserPresentation()
    {
        using var fixture = new OfficeFixture();
        FakePresentation? user = null;
        fixture.Office.Presentations.OnOpen = presentation => presentation.SlideShowSettings.OnRun = () =>
        {
            user = fixture.Office.Presentations.AddUserPresentation();
            throw new InvalidOperationException("模拟 Office 放映失败。");
        };

        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(opened.Succeeded);
        Assert.Equal("com_open_failed:InvalidOperationException", opened.Code);
        fixture.Adapter.Dispose();
        Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
        Assert.False(user!.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>自有 Close 失败时不能退出含未关闭文稿的 Application，也不能关闭用户文稿。</summary>
    [Fact]
    public async Task CloseFailureKeepsAllPresentationsAndDoesNotQuitApplication()
    {
        using var fixture = new OfficeFixture();
        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.True(opened.Succeeded, opened.Code);
        var owned = Assert.Single(fixture.Office.Presentations.Items);
        owned.CloseFails = true;
        var user = fixture.Office.Presentations.AddUserPresentation();

        Assert.False(await fixture.Adapter.CloseAsync(Guid.NewGuid(), opened.PresentationIdentity));
        fixture.Adapter.Dispose();

        Assert.Equal(2, fixture.Office.Presentations.Items.Count);
        Assert.False(owned.Closed);
        Assert.False(user.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>停机无法读文稿集合时，不以历史所有权证据退出用户可能仍在使用的 Application。</summary>
    [Fact]
    public async Task UnreadablePresentationCollectionPreventsApplicationQuit()
    {
        using var fixture = new OfficeFixture();
        var opened = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.True(opened.Succeeded, opened.Code);
        var user = fixture.Office.Presentations.AddUserPresentation();
        fixture.Office.Presentations.CountFails = true;

        fixture.Adapter.Dispose();

        Assert.Same(user, Assert.Single(fixture.Office.Presentations.Items));
        Assert.False(user.Closed);
        Assert.False(fixture.Office.QuitRequested);
    }

    /// <summary>打开失败且自有文稿无法补偿时，保留该资源登记并阻止再次打开。</summary>
    [Fact]
    public async Task FailedOpeningCompensationRetainsOwnedSlotUntilCleanup()
    {
        using var fixture = new OfficeFixture();
        fixture.Office.Presentations.OnOpen = presentation =>
        {
            presentation.CloseFails = true;
            presentation.SlideShowSettings.OnRun = () => throw new InvalidOperationException("模拟放映失败。");
        };
        var failed = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);
        Assert.False(failed.Succeeded);

        var retry = await fixture.Adapter.OpenAsync(Guid.NewGuid(), fixture.Source);

        Assert.False(retry.Succeeded);
        Assert.Equal("office_slot_busy", retry.Code);
        var owned = Assert.Single(fixture.Office.Presentations.Items);
        owned.CloseFails = false;
        fixture.Adapter.Dispose();
        Assert.True(owned.Closed);
        Assert.Empty(fixture.Office.Presentations.Items);
        Assert.True(fixture.Office.QuitRequested);
    }

    /// <summary>测试用外部系统集合；源文件只是存在性输入，不包含或打开真实 Office 文稿。</summary>
    private sealed class OfficeFixture : IDisposable
    {
        public string Source { get; } = Path.GetTempFileName();
        public FakeApplication Office { get; } = new();
        public FakePowerPointInterop Interop { get; }
        public PowerPointComAdapter Adapter { get; }

        /// <summary>装配公共 Adapter 和不创建线程/窗口的外部替身。</summary>
        public OfficeFixture()
        {
            Interop = new(Office);
            Adapter = new(new InlineOfficeDispatcher(), Interop);
        }

        /// <summary>经过真实 Dispose 逻辑关闭自有替身文稿，再移除精确临时文件。</summary>
        public void Dispose()
        {
            Adapter.Dispose();
            File.Delete(Source);
        }
    }

    /// <summary>同步外部调度替身；不启动 WPF Dispatcher 或原生进程。</summary>
    private sealed class InlineOfficeDispatcher : IOfficeStaDispatcher
    {
        /// <summary>
        /// 执行外部替身操作；测试使用唯一 operation_id。
        /// :param operationId: 操作身份。
        /// :param operation: Adapter 的真实操作。
        /// :param cancellationToken: 取消令牌。
        /// :returns: 真实操作的返回值。
        /// </summary>
        public Task<T> InvokeAsync<T>(Guid operationId, Func<CancellationToken, T> operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(operation(cancellationToken));
        }
    }

    /// <summary>替代外部 COM 创建和 Win32 查询，仍由 Adapter 执行文稿/进程归属决策。</summary>
    public sealed class FakePowerPointInterop(FakeApplication application) : IPowerPointInterop
    {
        private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-28T01:00:00Z",
            System.Globalization.CultureInfo.InvariantCulture);
        public PowerPointWindowEvidence? ApplicationWindow { get; set; } = new((nint)10, 42, Start);
        public PowerPointWindowEvidence? ShowWindow { get; set; } = new((nint)20, 42, Start);
        public string FailureDetail { get; set; } = string.Empty;

        /// <summary>:returns: 不连接真实 Office 的外部 Application 替身。</summary>
        public object CreateApplication() => application;

        /// <summary>
        /// :param application: 当前 Application 对象。
        /// :returns: 可控制的当前 Application 身份证据。
        /// </summary>
        public PowerPointWindowEvidence? GetApplicationWindow(object application) => ApplicationWindow;

        /// <summary>
        /// :param slideShowWindow: Run 返回的外部放映对象。
        /// :returns: 可控制的窗口与进程身份证据。
        /// </summary>
        public PowerPointWindowEvidence? GetSlideShowWindow(object slideShowWindow) => ShowWindow;
    }

    /// <summary>可由 dynamic 访问的外部 Application 替身。</summary>
    public sealed class FakeApplication
    {
        public FakePresentations Presentations { get; } = new();
        public bool Visible { get; set; }
        public bool QuitRequested { get; private set; }

        /// <summary>记录 Application 退出结果，不启动或结束任何进程。</summary>
        public void Quit() => QuitRequested = true;
    }

    /// <summary>可加入用户文稿的外部 COM 集合替身。</summary>
    public sealed class FakePresentations
    {
        public List<FakePresentation> Items { get; } = [];
        public int Count => CountFails ? throw new InvalidOperationException("模拟文稿集合不可读。") : Items.Count;
        public bool CountFails { get; set; }
        public Action<FakePresentation>? OnOpen { get; set; }

        /// <summary>
        /// :param path: 文稿原件路径。
        /// :param readOnly: Office 只读参数。
        /// :param untitled: Office 无标题参数。
        /// :param withWindow: Office 编辑窗口参数。
        /// :returns: 由本次 Open 取得的外部文稿替身。
        /// </summary>
        public FakePresentation Open(string path, int readOnly, int untitled, int withWindow)
        {
            var presentation = new FakePresentation(this);
            Items.Add(presentation);
            OnOpen?.Invoke(presentation);
            return presentation;
        }

        /// <summary>:returns: 与本系统文稿共享 Application 的用户文稿替身。</summary>
        public FakePresentation AddUserPresentation()
        {
            var presentation = new FakePresentation(this);
            Items.Add(presentation);
            return presentation;
        }
    }

    /// <summary>替代一份特定外部 Presentation；Close 只影响该对象。</summary>
    public sealed class FakePresentation(FakePresentations owner)
    {
        public FakeSlides Slides { get; } = new();
        public FakeSlideShowSettings SlideShowSettings { get; } = new();
        public bool Closed { get; private set; }
        public bool CloseFails { get; set; }

        /// <summary>移除本份文稿，保留同集合的其他文稿。</summary>
        public void Close()
        {
            if (CloseFails) throw new InvalidOperationException("模拟文稿关闭失败。");
            Closed = true;
            owner.Items.Remove(this);
        }
    }

    /// <summary>最小外部 Slides 结果。</summary>
    public sealed class FakeSlides
    {
        public int Count { get; } = 9;
    }

    /// <summary>本次 Presentation 的外部放映入口替身。</summary>
    public sealed class FakeSlideShowSettings
    {
        public FakeSlideShowWindow Window { get; } = new();
        public Action? OnRun { get; set; }

        /// <summary>:returns: 本次 Run 返回的具体外部窗口对象。</summary>
        public FakeSlideShowWindow Run()
        {
            OnRun?.Invoke();
            return Window;
        }
    }

    /// <summary>最小外部放映窗口结果。</summary>
    public sealed class FakeSlideShowWindow
    {
        public FakeView View { get; } = new();
    }

    /// <summary>最小外部放映画面状态。</summary>
    public sealed class FakeView
    {
        public int CurrentShowPosition { get; } = 1;
    }
}
