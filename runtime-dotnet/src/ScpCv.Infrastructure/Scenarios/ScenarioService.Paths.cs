// 预设公开路径冲突门禁；历史同名记录保留，但新建/改名/移动不再制造歧义。
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.Infrastructure.Scenarios;

public sealed partial class ScenarioService
{
    /// <summary>:param name: 合法单段名称。:param folderId: 目标目录。:param exceptId: 排除自身。:returns: 同目录路径唯一。</summary>
    private static async Task EnsureUniquePathAsync(ControlDbContext db, string name, long? folderId, long? exceptId,
        CancellationToken token)
    {
        var names = await db.Scenarios.Where(item => item.FolderId == folderId && item.Id != exceptId)
            .Select(item => item.Name).ToListAsync(token).ConfigureAwait(false);
        if (names.Contains(name, StringComparer.OrdinalIgnoreCase))
            throw new ScenarioServiceException("目标目录已有同名预设，请改名或选择其他目录。", code: "scenario_path_conflict");
    }
}
