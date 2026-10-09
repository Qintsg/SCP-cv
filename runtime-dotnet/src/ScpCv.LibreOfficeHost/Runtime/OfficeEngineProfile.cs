// 独占Office profile创建；仅预置官方MAR禁用，不伪造初始化或改用户默认profile。
using System;
using System.IO;
using System.Text;

namespace ScpCv.LibreOfficeHost.Runtime;

internal static class OfficeEngineProfile
{
    /// <summary>:param profile: 本次全新自有profile路径。:param forceUpdater: 已继承强制更新覆盖是否存在。:returns: 覆盖存在时在任何文件副作用前拒绝，否则仅创建独占最小配置。</summary>
    internal static void Create(string profile, bool forceUpdater = false)
    {
        if (forceUpdater) throw new InvalidOperationException("检测到LIBO_UPDATER_TEST_ENABLE强制更新覆盖，拒绝创建或启动自有Office实例。");
        if (Directory.Exists(profile)) throw new InvalidOperationException("引擎profile必须是本次新目录");
        Directory.CreateDirectory(profile);
        var user = Path.Combine(profile, "user");
        Directory.CreateDirectory(user);
        const string configuration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<oor:items xmlns:oor=\"http://openoffice.org/2001/registry\">\n" +
            "  <item oor:path=\"/org.openoffice.Office.Update/Update\">\n" +
            "    <prop oor:name=\"Enabled\" oor:op=\"fuse\"><value>false</value></prop>\n" +
            "  </item>\n</oor:items>\n";
        using var file = new FileStream(Path.Combine(user, "registrymodifications.xcu"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(file, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(configuration);
    }
}
