// 共享Job启动原语需要的固定错误；不引用服务器运行时或业务数据库。
namespace ScpCv.ControlHost.StreamPreview;

public sealed class PreviewException(string message, string code, int statusCode = 503) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
