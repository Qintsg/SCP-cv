// 内置工作站目录的稳定ID与可达快照同步；不接管用户同名目录，不删除离线源。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Devices;

public sealed class WorkstationCatalog(WriteCoordinator writes)
{
    /// <summary>:param snapshot: 有界探测结果。:param token: 取消寿命。:returns: 仅内置项更新，原媒体/预案引用保持。</summary>
    public Task SynchronizeAsync(IReadOnlyDictionary<string, InputReachability> snapshot, CancellationToken token = default) => writes.ExecuteAsync(async (db, cancellation) =>
    {
        var folders = await db.MediaFolders.ToListAsync(cancellation).ConfigureAwait(false);
        var folder = folders.SingleOrDefault(item => item.SystemKey == "workstation");
        if (folder is null)
        {
            if (folders.Any(item => item.ParentId is null && item.Name.Equals("工作站", StringComparison.OrdinalIgnoreCase)))
                throw new VenueDeviceException("/工作站 已是用户目录，未接管；请先将该目录改名。 ");
            folder = new MediaFolder { Name = "工作站", SystemKey = "workstation", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.MediaFolders.Add(folder); await db.SaveChangesAsync(cancellation).ConfigureAwait(false);
        }
        var sources = await db.MediaSources.Where(item => item.SystemKey.StartsWith("workstation:")).ToListAsync(cancellation).ConfigureAwait(false);
        foreach (var input in SiteInputCatalog.All.Where(item => item.Group == "workstation"))
        {
            var source = sources.SingleOrDefault(item => item.SystemKey == input.SystemKey);
            if (source is null)
            {
                source = new MediaSource { SystemKey = input.SystemKey, SourceType = MediaSourceType.CustomStream, CreatedAt = DateTimeOffset.UtcNow, Uri = input.DefaultPreviewUri };
                db.MediaSources.Add(source);
            }
            snapshot.TryGetValue(input.Id, out var reached);
            var scheme = reached is { Available: true } ? SelectScheme(source.Uri, reached) : null;
            var uri = scheme is null ? source.Uri : $"{scheme}://{input.Address}:{(scheme == "rtsp" ? 554 : 1935)}/live/chn0";
            if (source.Uri != uri) { source.Uri = uri; source.SourceRevision = checked(source.SourceRevision + 1); }
            source.Name = input.Name; source.EntryName = input.Name; source.FolderId = folder.Id;
            source.IsAvailable = reached?.Available == true;
        }
    }, token);

    /// <summary>:param current: 原协议，仍可达时不为偏好变化打断现有流。:param result: 当前探测。:returns: 确实可达的协议。</summary>
    private static string SelectScheme(string current, InputReachability result)
    {
        if (current.StartsWith("rtsp:", StringComparison.Ordinal) && result.Rtsp) return "rtsp";
        if (current.StartsWith("rtmp:", StringComparison.Ordinal) && result.Rtmp) return "rtmp";
        return result.Rtmp ? "rtmp" : "rtsp";
    }
}
