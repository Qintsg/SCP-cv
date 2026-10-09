// 加载拒绝的纯文件输出证明；与真实Physical转换分层，不引用UNO或假造正常退出。
using ScpCv.LibreOfficeHost.Uno;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationLoadRejectionGuardTests
{
    /// <summary>:returns: 缺失stage不能当已验证空输出，仍须拒绝明确失败分类。</summary>
    [Fact]
    public void MissingOutputHasNoEmptyProof() => Assert.Throws<InvalidOperationException>(() =>
        PresentationLoadRejectedException.VerifyEmptyOutput(Path.Combine(Path.GetTempPath(), "missing-load-stage-" + Guid.NewGuid().ToString("N"))));

    /// <summary>:returns: 空自有stage可提供无输出证明，检查不写任何文件。</summary>
    [Fact]
    public void EmptyOutputIsVerifiedWithoutMutation()
    {
        var root = NewStage();
        PresentationLoadRejectedException.VerifyEmptyOutput(root);
        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    /// <summary>:param directory: 存在子目录还是文件。:returns: 任意已有内容都不能假装空输出，证据保持。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingEvidenceIsNeverTreatedAsEmpty(bool directory)
    {
        var root = NewStage();
        var entry = Path.Combine(root, "existing-evidence");
        if (directory) Directory.CreateDirectory(entry);
        else File.WriteAllText(entry, "保留本轮证据");
        Assert.Throws<InvalidOperationException>(() => PresentationLoadRejectedException.VerifyEmptyOutput(root));
        Assert.Single(Directory.GetFileSystemEntries(root));
        if (!directory) Assert.Equal("保留本轮证据", File.ReadAllText(entry));
    }

    /// <summary>:returns: 本次新空GUID目录，保留用于回归诊断，不删除其它数据。</summary>
    private static string NewStage()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-load-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
