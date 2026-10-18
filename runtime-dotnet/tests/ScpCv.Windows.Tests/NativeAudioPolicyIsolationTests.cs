// 音频策略身份门禁的只读原生回归；不调用Bind、不播放、不改任何输出或音量。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ScpCv.Runtime.Audio;
using ScpCv.PlayerWorker.Audio;
using Xunit.Abstractions;

namespace ScpCv.Windows.Tests;

public sealed class NativeAudioPolicyIsolationTests(ITestOutputHelper output)
{
    /// <summary>:returns: 当前真实包身份必须在策略激活前拒绝，无包独立上下文仍能只读激活。</summary>
    [Fact]
    public void ActualCurrentProcessIdentityMustMatchPolicyIsolationGate()
    {
        using var self = Process.GetCurrentProcess();
        uint length = 0;
        var status = GetPackageFullName(self.Handle, ref length, IntPtr.Zero);
        output.WriteLine("Native current-process package status: {0}", status);
        if (status == 15700)
        {
            using var policy = new NativeApplicationAudioPolicy();
            return;
        }
        if (status is not (0 or 122)) throw new Win32Exception(status);
        var error = Assert.Throws<InvalidOperationException>(() => { using var policy = new NativeApplicationAudioPolicy(); });
        Assert.Contains("包身份", error.Message, StringComparison.Ordinal);
    }

    /// <summary>:returns: 未配置显式输出仍保留原行为，不读取私有引擎配置或激活声音策略。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UnconfiguredWebOutputKeepsOriginalBehavior(string? endpoint) => Assert.Null(WebViewAudioOutputBinding.Browser(1, endpoint));

    /// <summary>:returns: 只有明确无包状态能授权策略，成功或缓冲不足均表示已有包。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(122)]
    public void ExistingPackageIsRejected(int result)
    {
        var error = Assert.Throws<InvalidOperationException>(() => NativeApplicationAudioPolicy.ValidatePackageResult(result));
        Assert.Contains("未写入", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitNoPackageIsAccepted() => NativeApplicationAudioPolicy.ValidatePackageResult(15700);

    /// <summary>:param result: 身份查询原始错误。:returns: 保留原码，不以读取失败冒充无包。</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(87)]
    [InlineData(15703)]
    public void UnknownIdentityCannotAuthorizePolicyWrites(int result)
    {
        var error = Assert.Throws<Win32Exception>(() => NativeApplicationAudioPolicy.ValidatePackageResult(result));
        Assert.Equal(result, error.NativeErrorCode);
    }

    /// <summary>:returns: 实际目标进程与当前进程使用同一只读原生门禁，不能只核宿主而漏掉实际声音进程。</summary>
    [Fact]
    public void ActualTargetIdentityIsIndependentlyChecked()
    {
        using var self = Process.GetCurrentProcess();
        uint length = 0;
        var status = GetPackageFullName(self.Handle, ref length, IntPtr.Zero);
        output.WriteLine("Native target-process package status: {0}", status);
        if (status == 15700) { NativeApplicationAudioPolicy.EnsureProcessIsolated(self); return; }
        if (status is not (0 or 122)) throw new Win32Exception(status);
        Assert.Throws<InvalidOperationException>(() => NativeApplicationAudioPolicy.EnsureProcessIsolated(self));
        var error = Assert.Throws<InvalidOperationException>(() => WebViewAudioOutputBinding.Browser(1, "explicit-test-endpoint"));
        Assert.Contains("包身份", error.Message, StringComparison.Ordinal);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32), DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetPackageFullName(IntPtr process, ref uint length, IntPtr value);
}
