// 编译产物的 COM ABI 回归；仅检查元数据，不激活 Office 或创建 WPF 窗口。
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using ScpCv.PowerPointHost.Interop;

namespace ScpCv.Windows.Tests;

public sealed class PowerPointPiaBindingTests
{
    /// <summary>两个 HWND 入口必须调用既有完整 PIA 的 Dual getter，不能再次成为部分 dispatch-only 投影。</summary>
    [Theory]
    [InlineData("ReadApplication", "91493442-5A91-11CF-8700-00AA0060263B", 2031, 45, 360)]
    [InlineData("ReadSlideShow", "91493453-5A91-11CF-8700-00AA0060263B", 2010, 20, 160)]
    public void WindowGetterUsesCompletePiaDualBinding(string entryPoint, string interfaceGuid, int memberId,
        int expectedSlot, int expectedOffset)
    {
        var entry = typeof(PowerPointWindowHandleReader).GetMethod(entryPoint)!;
        var instructions = entry.GetMethodBody()!.GetILAsByteArray()!;
        // 入口只有 ldarg.0、castclass、callvirt、ret；验证真实调用 token，不读取对象状态。
        Assert.Equal(0x02, instructions[0]);
        Assert.Equal(0x74, instructions[1]);
        Assert.Equal(0x6F, instructions[6]);
        Assert.Equal(0x2A, instructions[11]);
        var getter = (MethodInfo)entry.Module.ResolveMethod(BitConverter.ToInt32(instructions, 7))!;
        var declared = getter.DeclaringType!;

        Assert.Equal(new Guid(interfaceGuid), declared.GUID);
        Assert.Equal(ComInterfaceType.InterfaceIsDual,
            declared.GetCustomAttribute<InterfaceTypeAttribute>()?.Value ?? ComInterfaceType.InterfaceIsDual);
        Assert.StartsWith("Microsoft.Office.Interop.PowerPoint.", declared.FullName!, StringComparison.Ordinal);
        Assert.Same(entry.Module.Assembly, declared.Assembly);
        Assert.NotNull(declared.GetCustomAttribute<TypeIdentifierAttribute>());
        Assert.True(declared.IsImport);
        Assert.Equal("get_HWND", getter.Name);
        Assert.Equal(typeof(int), getter.ReturnType);
        Assert.Empty(getter.GetParameters());
        Assert.Equal(memberId, getter.GetCustomAttribute<DispIdAttribute>()!.Value);
        var slot = ReadEmbeddedGetterSlot(declared);
        Assert.Equal(expectedSlot, slot);
        Assert.Equal(expectedOffset, slot * nint.Size);
        Assert.DoesNotContain(declared.Assembly.GetReferencedAssemblies(), reference =>
            reference.Name is "office" or "Microsoft.Vbe.Interop" or "Microsoft.Office.Interop.PowerPoint");
    }

    /// <summary>
    /// 读取编译器生成的完整 ABI 槽隙，拒绝只把两个成员标 Dual 的不完整投影。
    /// :param declared: 源自既有 PIA 的嵌入接口。
    /// :returns: 包含 IUnknown/IDispatch 七槽的 HWND getter 位置。
    /// </summary>
    private static int ReadEmbeddedGetterSlot(Type declared)
    {
        using var stream = File.OpenRead(declared.Assembly.Location);
        using var image = new PEReader(stream);
        var metadata = image.GetMetadataReader();
        var definition = metadata.GetTypeDefinition((TypeDefinitionHandle)System.Reflection.Metadata.Ecma335.MetadataTokens.Handle(declared.MetadataToken));
        var slot = 7;
        var hasGap = false;
        foreach (var methodHandle in definition.GetMethods())
        {
            var method = metadata.GetMethodDefinition(methodHandle);
            var name = metadata.GetString(method.Name);
            if (name.StartsWith("_VtblGap", StringComparison.Ordinal))
            {
                hasGap = true;
                slot += int.Parse(name[(name.LastIndexOf('_') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (name == "get_HWND")
            {
                Assert.True(hasGap, "没有编译器生成的前置槽隙，不能按完整 Dual ABI 读取 HWND。");
                return slot;
            }
            else slot++;
        }
        throw new InvalidOperationException("生成接口中缺少 HWND getter。");
    }

    /// <summary>普通托管对象只能被拒绝；getter 不能选择全局 Office 对象，也不依赖部署机 GAC。</summary>
    [Fact]
    public void OrdinaryObjectDoesNotBecomeAnotherOfficeWindow()
    {
        Assert.Throws<InvalidCastException>(() => PowerPointWindowHandleReader.ReadApplication(new object()));
        Assert.Throws<InvalidCastException>(() => PowerPointWindowHandleReader.ReadSlideShow(new object()));
    }
}
