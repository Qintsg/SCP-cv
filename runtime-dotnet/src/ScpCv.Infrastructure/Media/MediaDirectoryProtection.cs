// 可信内置标记的服务端门禁；管理员和批量入口不绕过。
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public static class MediaDirectoryProtection
{
    /// <summary>:param source: 受管实体。:raises MediaServiceException: 内置源不可改动。</summary>
    public static void EnsureWritable(MediaSource source)
    {
        if (!string.IsNullOrEmpty(source.SystemKey)) throw new MediaServiceException("内置媒体源不可编辑、移动或删除。");
    }
    /// <summary>:param folderId: 目标目录。:param folders: 完整父链。:raises MediaServiceException: 内置子树不可改动。</summary>
    public static void EnsureWritable(long? folderId, IReadOnlyCollection<MediaFolder> folders)
    {
        var seen = new HashSet<long>();
        while (folderId is { } id && seen.Add(id))
        {
            var folder = folders.SingleOrDefault(item => item.Id == id);
            if (folder is null) return;
            if (!string.IsNullOrEmpty(folder.SystemKey)) throw new MediaServiceException("内置目录不可添加、编辑、移动或删除内容。");
            folderId = folder.ParentId;
        }
    }
}
