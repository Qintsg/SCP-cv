// 引擎profile使用独立短路径，不能随深层媒体/制品目录增长或复用其它实例。
using ScpCv.Contracts.Presentations;

namespace ScpCv.Contracts.Tests;

public sealed class PresentationRuntimePathsTests
{
    /// <summary>:returns: 播放与转换共用短的独占实例目录，不在此方法创建任何文件。</summary>
    [Fact]
    public void EngineProfileUsesExistingShortTempConvention()
    {
        var instance = Guid.NewGuid();
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SCP-cv", "presentations", instance.ToString("N"))),
            PresentationRuntimePaths.EngineProfile(instance));
    }

    /// <summary>:returns: 无身份不得生成可被复用的profile目录。</summary>
    [Fact]
    public void EngineProfileRejectsEmptyIdentity() =>
        Assert.Throws<ArgumentException>(() => PresentationRuntimePaths.EngineProfile(Guid.Empty));

    /// <summary>:returns: 不触碰磁盘即可拒绝异常深的OS临时目录。</summary>
    [Fact]
    public void EngineProfileRejectsDeepTempRootBeforeCreatingFiles()
    {
        var deepRoot = Path.Combine(Path.GetTempPath(), new string('a', 100));
        Assert.Throws<InvalidOperationException>(() => PresentationRuntimePaths.EngineProfile(deepRoot, Guid.NewGuid()));
        Assert.False(Directory.Exists(deepRoot));
    }

    /// <summary>:returns: 两实例必须得到不同的profile，不能共享进程/扩展缓存。</summary>
    [Fact]
    public void EngineProfileDoesNotReuseAnotherInstance() =>
        Assert.NotEqual(PresentationRuntimePaths.EngineProfile(Guid.NewGuid()), PresentationRuntimePaths.EngineProfile(Guid.NewGuid()));
}
