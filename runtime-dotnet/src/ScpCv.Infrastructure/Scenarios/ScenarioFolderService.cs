// 预设目录是独立逻辑树，删除非空目录必须先逐项移出，不隐式删除预设。
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Scenarios;

public sealed class ScenarioFolderService(IDbContextFactory<ControlDbContext> contexts, WriteCoordinator writes, ScenarioFileStore? scenarioFiles = null)
{
    public async Task<IReadOnlyList<MediaFolderDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var folders = await db.ScenarioFolders.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        return folders.Select(folder => ToDto(folder, folders)).ToArray();
    }

    public Task<MediaFolderDto> CreateAsync(string name, long? parentId, CancellationToken cancellationToken = default) =>
        WriteAsync(null, name, parentId, cancellationToken);
    public Task<MediaFolderDto> UpdateAsync(long id, string name, long? parentId, CancellationToken cancellationToken = default) =>
        WriteAsync(id, name, parentId, cancellationToken);

    private Task<MediaFolderDto> WriteAsync(long? id, string name, long? parentId, CancellationToken cancellationToken) =>
        writes.ExecuteAsync(async (db, token) =>
        {
            var normalized = Segment(name);
            var folders = await db.ScenarioFolders.ToListAsync(token).ConfigureAwait(false);
            await ValidateParentAsync(db, parentId, token).ConfigureAwait(false);
            if (folders.Any(folder => folder.Id != id && folder.ParentId == parentId && folder.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                throw new ScenarioServiceException("同一目录已有同名文件夹。");
            var folder = id is null ? new ScenarioFolder { CreatedAt = DateTimeOffset.UtcNow } :
                folders.SingleOrDefault(item => item.Id == id) ?? throw new ScenarioServiceException("目录不存在。", true);
            var ancestor = parentId;
            var seen = new HashSet<long>();
            while (ancestor is not null)
            {
                if (ancestor == id || !seen.Add(ancestor.Value)) throw new ScenarioServiceException("不能移动到自身或子目录。");
                ancestor = folders.Single(item => item.Id == ancestor).ParentId;
            }
            folder.Name = normalized;
            folder.ParentId = parentId;
            folder.UpdatedAt = DateTimeOffset.UtcNow;
            if (id is null) { db.ScenarioFolders.Add(folder); folders.Add(folder); }
            await db.SaveChangesAsync(token).ConfigureAwait(false);
            return ToDto(folder, folders);
        }, scenarioFiles, cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken = default) => writes.ExecuteAsync(async (db, token) =>
    {
        var folder = await db.ScenarioFolders.SingleOrDefaultAsync(item => item.Id == id, token).ConfigureAwait(false)
            ?? throw new ScenarioServiceException("目录不存在。", true);
        if (await db.ScenarioFolders.AnyAsync(item => item.ParentId == id, token).ConfigureAwait(false) ||
            await db.Scenarios.AnyAsync(item => item.FolderId == id, token).ConfigureAwait(false))
            throw new ScenarioServiceException("目录非空，请先移动或删除其中的预设和子目录。");
        db.ScenarioFolders.Remove(folder);
    }, scenarioFiles, cancellationToken);

    public static async Task ValidateParentAsync(ControlDbContext db, long? parentId, CancellationToken token)
    {
        if (parentId is not null && !await db.ScenarioFolders.AnyAsync(item => item.Id == parentId, token).ConfigureAwait(false))
            throw new ScenarioServiceException("目标预设目录不存在。");
    }

    public static string PublicPath(long? folderId, IReadOnlyList<ScenarioFolder> folders)
    {
        var names = new List<string>();
        var seen = new HashSet<long>();
        while (folderId is not null)
        {
            if (!seen.Add(folderId.Value)) throw new ScenarioServiceException("预设目录存在循环。");
            var folder = folders.SingleOrDefault(item => item.Id == folderId) ?? throw new ScenarioServiceException("预设目录引用已失效。");
            names.Add(folder.Name);
            folderId = folder.ParentId;
        }
        names.Reverse();
        return "/" + string.Join('/', names);
    }

    public static string Segment(string name)
    {
        try { return MediaPathResolver.ValidateSegment(name); }
        catch (MediaServiceException exception) { throw new ScenarioServiceException(exception.Message); }
    }
    private static MediaFolderDto ToDto(ScenarioFolder folder, IReadOnlyList<ScenarioFolder> folders) => new()
    { Id = folder.Id, Name = folder.Name, ParentId = folder.ParentId, RelativePath = PublicPath(folder.Id, folders),
        CreatedAt = folder.CreatedAt.ToString("O"), UpdatedAt = folder.UpdatedAt.ToString("O") };
}
