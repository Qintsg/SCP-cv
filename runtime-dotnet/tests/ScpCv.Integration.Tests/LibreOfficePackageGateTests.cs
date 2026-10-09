// 公开上传/转换安全门禁：同一OOXML包换扩展名不能绕过外链拒绝；宿主故意不存在，测试不启动Office。
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ScpCv.ControlHost.Media;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Configuration;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Integration.Tests;

[CollectionDefinition("PresentationPackageEnvironment", DisableParallelization = true)]
public sealed class PresentationPackageEnvironmentGroup;

[Collection("PresentationPackageEnvironment")]
public sealed class LibreOfficePackageGateTests
{
    /// <summary>:returns: .ppsx与.pptx具有同样的外链拒绝行为，不能越过预检去找原生宿主。</summary>
    [Theory]
    [InlineData(".pptx")]
    [InlineData(".PPTX")]
    [InlineData(".pptm")]
    [InlineData(".ppsx")]
    [InlineData(".ppsm")]
    [InlineData(".potx")]
    [InlineData(".potm")]
    public Task SlideshowExtensionCannotBypassExternalPackageRejection(string extension) => AssertExternalPackageRejected(extension);

    /// <summary>:returns: 同一OOXML外链包改名成旧.ppt也不能越过原件内容预检。</summary>
    [Theory]
    [InlineData(".ppt")]
    [InlineData(".pps")]
    [InlineData(".pot")]
    [InlineData(".odp")]
    public Task RenamedOoxmlContentCannotBypassExternalPackageRejection(string extension) => AssertExternalPackageRejected(extension);

    /// <summary>:param extension: 上传时真实保留的源扩展。:returns: 公开转换在外部动作前安全拒绝，原件和输出状态真实。</summary>
    private static async Task AssertExternalPackageRejected(string extension)
    {
        var root = Path.Combine(Path.GetTempPath(), "SCP-cv-package-conversion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = Environment.GetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST");
        try
        {
            Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", Path.Combine(root, "absent-host.exe"));
            var options = new DataRootOptions { RootPath = root };
            var factory = new ControlDbContextFactory(options, root);
            await new DatabaseInitializer(factory).InitializeAsync();
            using var writes = new WriteCoordinator(factory);
            var media = new MediaSourceService(factory, writes, factory, new MediaStorageOptions());
            var preparation = new MediaPreparationService(factory, writes, options);
            using var upload = ExternalPackage();
            var source = await media.AddUploadedAsync(upload, "external" + extension, null, "外链放映包", "ppt", null, false, false);
            var original = SHA256.HashData(await File.ReadAllBytesAsync(source.Uri));
            var job = await preparation.ClaimNextAsync(PreparationJobKind.PptImages);
            Assert.NotNull(job);
            var output = Path.Combine(root, "not-created-artifact");
            var result = await new LibreOfficeSlideConverter(factory).ConvertAsync(job.JobId, job.SourceRevision, source.Uri, output);
            Assert.Equal(OperationStatus.Failed, result.Status);
            Assert.Equal("presentation_package_rejected", result.Code);
            Assert.False(Directory.Exists(output));
            Assert.Equal(original, SHA256.HashData(await File.ReadAllBytesAsync(source.Uri)));
        }
        finally { Environment.SetEnvironmentVariable("SCP_CV_LIBREOFFICE_HOST", previous); }
    }

    /// <summary>:returns: 本次新建含外部页关系的真实ZIP，不包含可执行宏或可访问网络地址。</summary>
    private static MemoryStream ExternalPackage()
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add("ppt/presentation.xml", "<p:presentation xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><p:sldIdLst><p:sldId r:id=\"r1\"/></p:sldIdLst></p:presentation>");
            Add("ppt/_rels/presentation.xml.rels", "<Relationships><Relationship Id=\"r1\" Target=\"slides/slide1.xml\" TargetMode=\"External\"/></Relationships>");
            Add("ppt/slides/slide1.xml", "<sld/>");
            /// <summary>:param name: 此夹具固定条目。:param value: 无可执行内容的元数据。:returns: 写入仅本次内存ZIP。</summary>
            void Add(string name, string value)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(value);
            }
        }
        memory.Position = 0;
        return memory;
    }
}
