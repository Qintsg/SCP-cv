// 失败取证的只读 COM 诊断契约；任何探测成功都不改变原始放映结果。
namespace ScpCv.PowerPointHost.Interop;

/// <summary>只包含 COM ABI 的受控整数/标志/GUID，不携带文稿或异常消息。</summary>
public sealed record PowerPointDispatchObservation(
    string Stage,
    int HResult,
    int? VariantType = null,
    long? IntegerValue = null,
    Guid? InterfaceGuid = null,
    int? MemberId = null,
    int? InvocationFlags = null,
    int? MemberFlags = null);

/// <summary>对原对象执行精确接口路径的只读诊断，不能创建、关闭或替换 Office 对象。</summary>
public interface IPowerPointDispatchProbe
{
    /// <summary>
    /// 复现声明接口到 IDispatch 的查询，读取指定成员与该对象 TypeInfo。
    /// :param source: 必须是原失败调用所持有的同一 COM 对象。
    /// :param interfaceGuid: 失败调用声明的接口 GUID。
    /// :param memberId: HWND 的固定 DispId。
    /// :returns: 仅供诊断的 ABI 观察；不得作为所有权成功证据。
    /// </summary>
    IReadOnlyList<PowerPointDispatchObservation> Read(object source, Guid interfaceGuid, int memberId);
}
