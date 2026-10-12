// 预设独立目录；根目录为空父节点，旧预设不被移动。
namespace ScpCv.Domain.Model;

public sealed class ScenarioFolder
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? ParentId { get; set; }
    public ScenarioFolder? Parent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
