// 公开媒体路径从/开始；受管原件取真实叶名，URL和外部文件仅有逻辑路径。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed partial class MediaSourceService
{
    /// <summary>:param source: 源实体。:param folders: 完整目录树。:returns: 不泄漏磁盘根的媒体路径。</summary>
    private MediaSourceDto ProjectPath(MediaSource source, IReadOnlyCollection<MediaFolder> folders) =>
        ToSourceDto(source, _artifactRoot) with { Path = PublicPath(source, folders, _mediaRoot) };

    /// <summary>:param source: 媒体实体。:param folders: 目录树。:param mediaRoot: 受管媒体根。:returns: 公开逻辑路径。</summary>
    private static string PublicPath(MediaSource source, IReadOnlyCollection<MediaFolder> folders, string mediaRoot)
    {
        if (source.UploadedFile.Length > 0 && IsWithinRoot(Path.GetFullPath(source.UploadedFile), mediaRoot))
            return "/" + Path.GetRelativePath(mediaRoot, source.UploadedFile).Replace('\\', '/');
        var resolver = new MediaPathResolver(mediaRoot);
        var folder = Path.GetRelativePath(mediaRoot, resolver.FolderPath(source.FolderId, folders)).Replace('\\', '/');
        var leaf = source.EntryName;
        if (leaf.Length == 0)
        {
            try { leaf = MediaPathResolver.ValidateSegment(source.OriginalFilename.Length > 0 ? source.OriginalFilename : source.Name); }
            catch (MediaServiceException) { leaf = $"source-{source.Id}"; }
        }
        return (folder == "." ? "/" : "/" + folder + "/") + leaf;
    }
}
