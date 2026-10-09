// 已开始传输后的失败不证明原生未执行，调用方必须保留不确定状态。
namespace ScpCv.Contracts.Presentations;

public sealed class PresentationTransportException(string message, Exception inner) : IOException(message, inner);
