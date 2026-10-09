// 公开Player的危险包预检：改名源在创建原生候选前拒绝，隐藏自有STA窗口不启动Office。
using System.IO.Compression;
using System.Text;
using ScpCv.PlayerWorker;
using ScpCv.PlayerWorker.Playback;

namespace ScpCv.Windows.Tests;

public sealed partial class PlayerRuntimeHostLifecycleTests
{
    /// <summary>:param extension: 声明/改名扩展。:returns: 与转换一样拒绝外链包，不进入原生/发布新画面。</summary>
    [Theory]
    [InlineData(".ppsx")]
    [InlineData(".pptm")]
    [InlineData(".ppt")]
    public async Task DynamicOpenRejectsExternalPackageRegardlessOfAcceptedExtension(string extension)
    {
        await OnHiddenDispatcherAsync(async () =>
        {
            var package = CreatePackage();
            var source = Path.ChangeExtension(package, extension);
            File.Copy(package, source);
            using (var zip = ZipFile.Open(source, ZipArchiveMode.Update))
            using (var writer = new StreamWriter(zip.CreateEntry("ppt/slides/_rels/slide1.xml.rels").Open(), new UTF8Encoding(false)))
                writer.Write("<Relationships><Relationship Id=\"remote\" Target=\"../media/a.png\" TargetMode=\"External\"/></Relationships>");
            var window = new PlayerWindow();
            await using var host = new PlayerRuntimeHost(window, 1);
            try
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => host.ExecuteAsync(OpenLease(source), CancellationToken.None));
                // 未发布任何资源时不应伪造实时采样；公开NEXT也不能宣称成功。
                Assert.Null(await host.SampleProgressAsync(CancellationToken.None));
                await Assert.ThrowsAsync<InvalidOperationException>(() => host.ExecuteAsync(OpenLease(source) with { Command = "NEXT" }, CancellationToken.None));
                Assert.Equal(0, window.NativeHandle);
            }
            finally { window.Close(); }
        });
    }
}
