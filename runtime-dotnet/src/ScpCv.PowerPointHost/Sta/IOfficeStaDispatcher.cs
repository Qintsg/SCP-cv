// Office 外部 STA 调度契约；测试替身不创建线程或窗口。
namespace ScpCv.PowerPointHost.Sta;

/// <summary>将 Office 操作调度到其专属 STA，保持 operation_id 去重和取消语义。</summary>
public interface IOfficeStaDispatcher
{
    /// <summary>
    /// 在 Office STA 上执行同步外部操作；已开始的 COM 调用不能安全撤销。
    /// :param operationId: 幂等操作身份。
    /// :param operation: 仅在 Office STA 上访问外部对象的操作。
    /// :param cancellationToken: 进入执行前和等待结果时的取消令牌。
    /// :returns: 外部操作的完成结果。
    /// </summary>
    Task<T> InvokeAsync<T>(
        Guid operationId,
        Func<CancellationToken, T> operation,
        CancellationToken cancellationToken = default);
}
