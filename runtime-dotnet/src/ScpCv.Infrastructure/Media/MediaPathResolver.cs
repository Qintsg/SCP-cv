// 用户文件夹映射为受管理目录，拒绝路径越界和 Windows 保留名称。
using ScpCv.Domain.Model;

namespace ScpCv.Infrastructure.Media;

public sealed class MediaPathResolver(string mediaRoot)
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", ".staging", "uploads",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public string RootPath { get; } = Path.GetFullPath(mediaRoot);

    public static string ValidateSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120 || value != value.Trim() ||
            value is "." or ".." || value.EndsWith('.') ||
            value.Any(character => character < 32 || "<>:\"/\\|?*".Contains(character, StringComparison.Ordinal)) ||
            (Reserved.Contains(value) || Reserved.Contains(value.Split('.')[0])))
            throw new MediaServiceException($"文件或文件夹名称不符合 Windows 路径规则：{value}");
        return value;
    }

    public string FolderPath(long? folderId, IReadOnlyCollection<MediaFolder> folders)
    {
        if (folderId is null) return RootPath;
        var byId = folders.ToDictionary(folder => folder.Id);
        var segments = new Stack<string>();
        var visited = new HashSet<long>();
        var current = folderId;
        while (current is not null)
        {
            if (!visited.Add(current.Value)) throw new MediaServiceException("文件夹层级存在循环引用。");
            if (!byId.TryGetValue(current.Value, out var folder))
                throw new MediaServiceException($"文件夹 id={current.Value} 不存在", isNotFound: true);
            segments.Push(ValidateSegment(folder.Name));
            current = folder.ParentId;
        }

        var path = RootPath;
        foreach (var segment in segments) path = Path.Combine(path, segment);
        EnsureWithinRoot(path);
        return path;
    }

    public string NextAvailableFilePath(string folderPath, string fileName)
    {
        EnsureWithinRoot(folderPath);
        EnsureNoReparsePoint(folderPath);
        var safeName = ValidateSegment(fileName);
        var extension = Path.GetExtension(safeName);
        var stem = Path.GetFileNameWithoutExtension(safeName);
        for (var suffix = 1; suffix <= 9999; suffix++)
        {
            var candidateName = suffix == 1 ? safeName : $"{stem} ({suffix}){extension}";
            if (candidateName.Length > 120) throw new MediaServiceException("同名文件过多，目标文件名过长。");
            var candidate = Path.Combine(folderPath, candidateName);
            EnsureWithinRoot(candidate);
            if (!Directory.Exists(folderPath) || !Directory.EnumerateFileSystemEntries(folderPath)
                    .Any(entry => string.Equals(Path.GetFileName(entry), candidateName, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
        throw new MediaServiceException("目标文件夹中的同名文件超过上限。");
    }

    public void EnsureWithinRoot(string path)
    {
        var relative = Path.GetRelativePath(RootPath, Path.GetFullPath(path));
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            throw new MediaServiceException("媒体路径超出受管理目录。");
        if (Path.GetFullPath(path).Length > 240)
            throw new MediaServiceException("媒体路径过长，请缩短文件夹或文件名。");
    }

    public void EnsureNoReparsePoint(string path)
    {
        EnsureWithinRoot(path);
        var relative = Path.GetRelativePath(RootPath, Path.GetFullPath(path));
        var current = RootPath;
        if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new MediaServiceException("媒体根目录不能是重解析点。");
        if (relative == ".") return;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new MediaServiceException("媒体路径中包含符号链接或重解析点，已拒绝写入。");
        }
    }
}
