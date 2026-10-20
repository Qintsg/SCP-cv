// 可选文件投影：数据库提交前只准备临时文件，提交后发布，失败不得假报完整保存。
namespace ScpCv.Infrastructure.Persistence;

public interface IWriteProjection
{
    Task<IPreparedWriteProjection> PrepareAsync(ControlDbContext database, CancellationToken cancellationToken);
}

public interface IPreparedWriteProjection : IDisposable
{
    Task PublishAsync();
}
