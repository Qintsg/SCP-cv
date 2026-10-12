// 六路内置摄像机是受信目录；固定监控76已核实，不接管同名用户目录或猜测云台端点。
using Microsoft.EntityFrameworkCore;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Devices;

public sealed record CameraDefinition(string Id, string Name, string Uri, bool Movable);

public sealed class CameraCatalog(IDbContextFactory<ControlDbContext> contexts, WriteCoordinator writes, VenueOptions options)
{
    public IReadOnlyList<CameraDefinition> Definitions =>
    [
        new("rear", "后摄像机", Stream(84), true),
        new("center1", "中摄像机1", Stream(85), true),
        new("center2", "中摄像机2", Stream(86), true),
        new("left", "左摄像机", Stream(87), true),
        new("right", "右摄像机", Stream(88), true),
        new("fixed", "固定监控", options.FixedCameraRtsp, false),
    ];

    /// <summary>:param cancellationToken: 取消寿命。:returns: 幂等同步系统所有项，用户项不覆盖。</summary>
    public Task SynchronizeAsync(CancellationToken cancellationToken = default) => writes.ExecuteAsync(async (db, token) =>
    {
        if (options.FixedCameraRtsp.Length > 0 &&
            (!Uri.TryCreate(options.FixedCameraRtsp, UriKind.Absolute, out var fixedUri) || fixedUri.Scheme != "rtsp"))
            throw new VenueDeviceException("固定监控必须配置已核实的RTSP地址。");
        var folders = await db.MediaFolders.ToListAsync(token).ConfigureAwait(false);
        var folder = folders.SingleOrDefault(item => item.SystemKey == "camera");
        if (folder is null)
        {
            if (folders.Any(item => item.ParentId is null && item.Name.Equals("Camera", StringComparison.OrdinalIgnoreCase)))
                throw new VenueDeviceException("/Camera 已是用户目录，未覆盖；请先将该用户目录改名后重启服务。");
            folder = new MediaFolder { Name = "Camera", SystemKey = "camera", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.MediaFolders.Add(folder);
            await db.SaveChangesAsync(token).ConfigureAwait(false);
        }
        var sources = await db.MediaSources.Where(item => item.SystemKey.StartsWith("camera:")).ToListAsync(token).ConfigureAwait(false);
        foreach (var camera in Definitions)
        {
            var source = sources.SingleOrDefault(item => item.SystemKey == "camera:" + camera.Id);
            if (source is null)
            {
                source = new MediaSource { SystemKey = "camera:" + camera.Id, SourceType = MediaSourceType.RtspStream, CreatedAt = DateTimeOffset.UtcNow };
                db.MediaSources.Add(source);
            }
            source.FolderId = folder.Id;
            source.Name = camera.Name;
            source.EntryName = camera.Name;
            source.Uri = camera.Uri;
            // 有播放地址不代表摄像机当前在线；播放器仍须真实确认出画。
            source.IsAvailable = camera.Uri.Length > 0;
        }
    }, cancellationToken);

    /// <summary>:returns: 已同步的公开ID，供设备页打开来源使用。</summary>
    public async Task<IReadOnlyDictionary<string, long>> SourceIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.MediaSources.AsNoTracking().Where(item => item.SystemKey.StartsWith("camera:"))
            .ToDictionaryAsync(item => item.SystemKey[7..], item => item.Id, cancellationToken).ConfigureAwait(false);
    }

    private static string Stream(int number) => $"rtsp://192.168.5.{number}:554/live/chn0";
}
