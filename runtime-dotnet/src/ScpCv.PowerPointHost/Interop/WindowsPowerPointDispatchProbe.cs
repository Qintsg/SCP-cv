// 仅使用标准 IUnknown/IDispatch/ITypeInfo ABI，读取原 Office 对象，不投影部分 Dual vtable。
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace ScpCv.PowerPointHost.Interop;

/// <summary>同对象的原生只读诊断；不返回播放资源，不关闭对象，不释放共享 RCW。</summary>
public sealed class WindowsPowerPointDispatchProbe : IPowerPointDispatchProbe
{
    private static readonly Guid DispatchInterface = new("00020400-0000-0000-C000-000000000046");
    private const ushort PropertyGet = 2;
    private const uint EnglishUnitedStates = 0x0409;
    private const int InvalidArgument = unchecked((int)0x80070057);
    private const int MemberNotFound = unchecked((int)0x80020003);

    /// <inheritdoc />
    public IReadOnlyList<PowerPointDispatchObservation> Read(object source, Guid interfaceGuid, int memberId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var observations = new List<PowerPointDispatchObservation>();
        if (!Marshal.IsComObject(source))
        {
            observations.Add(new("input_not_com", InvalidArgument, InterfaceGuid: interfaceGuid, MemberId: memberId));
            return observations;
        }

        nint unknown = 0;
        nint declared = 0;
        nint dispatch = 0;
        var stage = "query_declared_interface";
        try
        {
            unknown = Marshal.GetIUnknownForObject(source);
            var result = Marshal.QueryInterface(unknown, in interfaceGuid, out declared);
            observations.Add(new(stage, result, InterfaceGuid: interfaceGuid, MemberId: memberId));
            if (result < 0 || declared == 0) return observations;
            stage = "query_dispatch_interface";
            var dispatchGuid = DispatchInterface;
            result = Marshal.QueryInterface(declared, in dispatchGuid, out dispatch);
            observations.Add(new(stage, result, InterfaceGuid: dispatchGuid, MemberId: memberId));
            if (result < 0 || dispatch == 0) return observations;

            stage = "invoke_property_get";
            observations.Add(ReadProperty(dispatch, memberId));
            stage = "type_info";
            ReadTypeInfo(dispatch, memberId, observations);
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or InvalidOperationException
            or ArgumentException or System.ComponentModel.Win32Exception)
        {
            observations.Add(new(stage, exception.HResult, InterfaceGuid: interfaceGuid, MemberId: memberId));
        }
        finally
        {
            // 仅释放本次 AddRef/QI 取得的指针；原 Application/SlideShowWindow RCW 始终由调用方持有。
            if (dispatch != 0) Marshal.Release(dispatch);
            if (declared != 0) Marshal.Release(declared);
            if (unknown != 0) Marshal.Release(unknown);
            GC.KeepAlive(source);
        }
        return observations;
    }

    /// <summary>
    /// 使用 IDispatch 的标准第 6 槽 Invoke，以 CLR 相同的 IID_NULL/LCID/PROPERTYGET/零参数读成员。
    /// :param dispatch: 已从声明接口 QI 得到的 IDispatch 指针。
    /// :param memberId: 固定 HWND DispId。
    /// :returns: HRESULT、VARIANT 类型和整数值；不把结果作为窗口所有权证据。
    /// </summary>
    private static PowerPointDispatchObservation ReadProperty(nint dispatch, int memberId)
    {
        var invoke = GetFunction<InvokeDelegate>(dispatch, 6);
        var variantBytes = nint.Size == 8 ? 24 : 16;
        var variant = Marshal.AllocCoTaskMem(variantBytes);
        try
        {
            for (var offset = 0; offset < variantBytes; offset += sizeof(long)) Marshal.WriteInt64(variant, offset, 0);
            var reserved = Guid.Empty;
            DISPPARAMS parameters = default;
            var result = invoke(dispatch, memberId, ref reserved, EnglishUnitedStates, PropertyGet,
                ref parameters, variant, 0, 0);
            var variantType = unchecked((ushort)Marshal.ReadInt16(variant));
            // 只读取类型库预期的 VT_I4，绝不解析 BSTR、文稿对象或 EXCEPINFO 中的字符串。
            long? integer = result >= 0 && variantType == 3 ? Marshal.ReadInt32(variant, 8) : null;
            return new("invoke_property_get", result, variantType, integer,
                MemberId: memberId, InvocationFlags: PropertyGet);
        }
        finally
        {
            _ = VariantClear(variant);
            Marshal.FreeCoTaskMem(variant);
        }
    }

    /// <summary>
    /// 读取同一 IDispatch 返回的实际 TypeInfo，并查询目标 HWND 的 FUNCDESC。
    /// :param dispatch: 原对象的精确 IDispatch 指针。
    /// :param memberId: 当前 HWND DispId。
    /// :param observations: 仅记录受控 ABI 元数据的结果集合。
    /// :returns: 无返回值；任何失败仅记录，不改变原始播放结果。
    /// </summary>
    private static void ReadTypeInfo(nint dispatch, int memberId, List<PowerPointDispatchObservation> observations)
    {
        var getTypeInfo = GetFunction<GetTypeInfoDelegate>(dispatch, 4);
        nint typeInfo = 0;
        nint attributePointer = 0;
        try
        {
            var result = getTypeInfo(dispatch, 0, EnglishUnitedStates, out typeInfo);
            observations.Add(new("type_info", result, MemberId: memberId));
            if (result < 0 || typeInfo == 0) return;
            result = GetFunction<GetTypeAttrDelegate>(typeInfo, 3)(typeInfo, out attributePointer);
            observations.Add(new("type_info_attributes", result, MemberId: memberId));
            if (result < 0 || attributePointer == 0) return;
            var attributes = Marshal.PtrToStructure<TYPEATTR>(attributePointer);
            observations.Add(new("type_info_identity", 0, InterfaceGuid: attributes.guid, MemberId: memberId,
                MemberFlags: (int)attributes.wTypeFlags));
            var getFunction = GetFunction<GetFuncDescDelegate>(typeInfo, 5);
            var releaseFunction = GetFunction<ReleaseDescriptorDelegate>(typeInfo, 20);
            // TypeInfo 是外部输入；诊断最多扫描 256 项，防止异常元数据拖住 Office 命令。
            for (var index = 0; index < Math.Min((int)attributes.cFuncs, 256); index++)
            {
                nint functionPointer = 0;
                try
                {
                    result = getFunction(typeInfo, checked((uint)index), out functionPointer);
                    if (result < 0 || functionPointer == 0)
                    {
                        observations.Add(new("type_info_function", result, InterfaceGuid: attributes.guid, MemberId: memberId));
                        return;
                    }
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    if (function.memid != memberId) continue;
                    observations.Add(new("type_info_member", 0, InterfaceGuid: attributes.guid,
                        MemberId: function.memid, InvocationFlags: (int)function.invkind, MemberFlags: function.wFuncFlags));
                    return;
                }
                finally { if (functionPointer != 0) releaseFunction(typeInfo, functionPointer); }
            }
            observations.Add(new("type_info_member", MemberNotFound, InterfaceGuid: attributes.guid, MemberId: memberId));
        }
        finally
        {
            if (attributePointer != 0) GetFunction<ReleaseDescriptorDelegate>(typeInfo, 19)(typeInfo, attributePointer);
            if (typeInfo != 0) Marshal.Release(typeInfo);
        }
    }

    /// <summary>
    /// 仅从已 QI 的标准 COM 接口读取预先定义的 ABI 槽位，不推算 Office Dual 布局。
    /// :param pointer: 已持有引用的标准 COM 接口指针。
    /// :param slot: IDispatch/ITypeInfo 标准槽位。
    /// :returns: 仅在持有该接口期间调用的标准函数委托。
    /// </summary>
    private static T GetFunction<T>(nint pointer, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer), slot * nint.Size));

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InvokeDelegate(nint instance, int memberId, ref Guid reserved, uint lcid, ushort flags,
        ref DISPPARAMS parameters, nint variant, nint exceptionInfo, nint argumentError);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetTypeInfoDelegate(nint instance, uint index, uint lcid, out nint typeInfo);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetTypeAttrDelegate(nint instance, out nint attributes);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetFuncDescDelegate(nint instance, uint index, out nint function);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ReleaseDescriptorDelegate(nint instance, nint descriptor);

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(nint variant);
}
