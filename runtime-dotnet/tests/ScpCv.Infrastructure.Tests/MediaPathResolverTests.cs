// 页面文件夹到实体媒体目录的安全解析和同名不覆盖规则。
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;

namespace ScpCv.Infrastructure.Tests;

public sealed class MediaPathResolverTests
{
    [Fact]
    public void ResolvesRootAndNestedChineseFolderNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-media-path-test", Guid.NewGuid().ToString("N"));
        var resolver = new MediaPathResolver(root);
        MediaFolder[] folders =
        [
            new() { Id = 1, Name = "PPT文件" },
            new() { Id = 2, Name = "早会", ParentId = 1 },
        ];

        Assert.Equal(Path.GetFullPath(root), resolver.FolderPath(null, folders));
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "PPT文件", "早会"), resolver.FolderPath(2, folders));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("folder.")]
    [InlineData("folder ")]
    [InlineData("a:b")]
    public void RejectsTraversalAndWindowsReservedSegments(string name)
    {
        Assert.Throws<MediaServiceException>(() => MediaPathResolver.ValidateSegment(name));
    }

    [Fact]
    public void CollisionAppendsSuffixWithoutOverwritingEvenWithDifferentCase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"scp-cv-media-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "A.pptx"), "original");
            var resolver = new MediaPathResolver(root);

            Assert.Equal(Path.Combine(root, "a (2).pptx"), resolver.NextAvailableFilePath(root, "a.pptx"));
            Assert.Equal("original", File.ReadAllText(Path.Combine(root, "A.pptx")));
        }
        finally
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (full.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(full).StartsWith("scp-cv-media-collision-", StringComparison.Ordinal) &&
                Directory.Exists(full))
                Directory.Delete(full, recursive: true);
        }
    }
}
