// 软件公共声音总线的只读就绪边界；未知或缺失不以一个配置布尔值假称已绑定。
namespace ScpCv.Infrastructure.Devices;

public interface IVenueSoftwareAudioBinding
{
    /// <summary>:param token: 查询寿命。:returns: 空字符串代表所有可信启动/实际端点预检通过；否则返回不可用原因。</summary>
    Task<string> UnavailableReasonAsync(CancellationToken token = default);
}

public sealed class UnavailableVenueSoftwareAudioBinding : IVenueSoftwareAudioBinding
{
    public Task<string> UnavailableReasonAsync(CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); return Task.FromResult("软件声音输出未装配，不能选择SCP-cv。"); }
}
