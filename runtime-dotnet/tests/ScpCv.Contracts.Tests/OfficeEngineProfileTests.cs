// 独占profile纯文件语义回归；Compile Link正式模块，不引用/启动UNO或Office程序。
using System.Xml.Linq;
using ScpCv.LibreOfficeHost.Runtime;

namespace ScpCv.Contracts.Tests;

public sealed class OfficeEngineProfileTests
{
    [Fact]
    public void ForcedUpdaterOverrideIsRejectedBeforeCreatingTheOwnedProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-office-profile-" + Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(root, "forced-updater-profile");
        var ownedRoot = Path.GetFullPath(root);
        if (Path.GetDirectoryName(ownedRoot) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("拒绝使用自有profile测试根之外的路径。");
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidOperationException>(() => OfficeEngineProfile.Create(profile, forceUpdater: true));
            Assert.False(Directory.Exists(profile));
            Assert.False(File.Exists(Path.Combine(profile, "user", "registrymodifications.xcu")));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(ownedRoot, recursive: true); }
    }

    [Fact]
    public void FreshOwnedProfileDisablesOfficialMarUpdaterWithoutFakingInitialization()
    {
        var root = Path.Combine(Path.GetTempPath(), "scp-cv-office-profile-" + Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(root, "fresh-owned-profile");
        var ownedRoot = Path.GetFullPath(root);
        if (Path.GetDirectoryName(ownedRoot) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("拒绝使用自有profile测试根之外的路径。");
        Directory.CreateDirectory(root);
        try
        {
            OfficeEngineProfile.Create(profile);

            var configuration = Path.Combine(profile, "user", "registrymodifications.xcu");
            Assert.True(File.Exists(configuration), "新自有profile必须预置官方更新禁用XCU，不能仅建空目录。");
            var bytes = File.ReadAllBytes(configuration);
            Assert.Equal((byte)'<', bytes[0]);
            Assert.DoesNotContain((byte)'\r', bytes);
            Assert.Equal((byte)'\n', bytes[^1]);
            var strictUtf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            Assert.Equal(bytes, strictUtf8.GetBytes(strictUtf8.GetString(bytes)));
            var document = XDocument.Load(configuration);
            XNamespace registry = "http://openoffice.org/2001/registry";
            var rootElement = Assert.IsType<XElement>(document.Root);
            var update = Assert.Single(rootElement.Elements("item"),
                item => (string?)item.Attribute(registry + "path") == "/org.openoffice.Office.Update/Update");
            var enabled = Assert.Single(update.Elements("prop"), property => (string?)property.Attribute(registry + "name") == "Enabled");
            Assert.Equal("false", enabled.Element("value")?.Value);
            Assert.Single(rootElement.Elements("item"));
            Assert.Single(update.Elements("prop"));
            Assert.DoesNotContain("Setup", document.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FirstStart", document.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Migration", document.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Single(Directory.GetFiles(profile, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(ownedRoot, recursive: true);
        }
    }

    [Fact]
    public void ExistingProfileWithSentinelCannotBeModifiedOrReused()
    {
        var root = NewOwnedTestRoot();
        var profile = Path.Combine(root, "existing-profile");
        Directory.CreateDirectory(profile);
        var sentinel = Path.Combine(profile, "user-owned-sentinel.txt");
        File.WriteAllText(sentinel, "保留已有用户内容");
        var original = File.ReadAllBytes(sentinel);
        try
        {
            Assert.Throws<InvalidOperationException>(() => OfficeEngineProfile.Create(profile));
            Assert.Equal(original, File.ReadAllBytes(sentinel));
            Assert.Single(Directory.GetFileSystemEntries(profile));
            Assert.False(Directory.Exists(Path.Combine(profile, "user")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ExistingFileAtProfilePathCannotBeOverwritten()
    {
        var root = NewOwnedTestRoot();
        var profile = Path.Combine(root, "existing-profile-file");
        File.WriteAllText(profile, "已有文件不能充当新profile");
        var original = File.ReadAllBytes(profile);
        try
        {
            Assert.Throws<IOException>(() => OfficeEngineProfile.Create(profile));
            Assert.Equal(original, File.ReadAllBytes(profile));
            Assert.Single(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    /// <summary>:returns: 已核对绝对Temp归属的自有GUID根，不覆盖用户路径。</summary>
    private static string NewOwnedTestRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "scp-cv-office-profile-" + Guid.NewGuid().ToString("N")));
        if (Path.GetDirectoryName(root) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
            throw new InvalidOperationException("拒绝使用自有profile测试根之外的路径。");
        Directory.CreateDirectory(root);
        return root;
    }
}
