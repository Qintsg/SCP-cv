// 平台/Framework/宿主及协议的纯快照策略；MSIL不是32位原生，运行机无需SDK/targeting pack。
namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationRuntimeDependencyPolicy
{
    /// <summary>:param snapshot: 当前外部只读事实。:returns: 平台与Framework运行条件，不推断UNO或文稿就绪。</summary>
    internal static IEnumerable<PresentationDependencyComponent> Runtime(PresentationDependencySnapshot snapshot)
    {
        var platform = snapshot.Platform;
        var platformReady = platform is { IsWindows: true, IsX64OperatingSystem: true, IsX64Process: true };
        yield return Component("platform", platformReady, "platform_compatible", "platform_unsupported",
            "Windows x64运行平台检查通过。", "文稿运行依赖要求Windows x64操作系统与x64控制主机进程。");
        var framework = snapshot.Framework;
        if (framework is { ReadFailureCode.Length: > 0 })
            yield return new("framework", "unavailable", ".NET Framework运行信息无法只读确认，请检查本机运行时安装。", framework.ReadFailureCode);
        else if (framework?.Release is not int release)
            yield return new("framework", "unavailable", "缺少.NET Framework 4.8或更新Full运行时。", "framework_missing");
        else if (release < 528040)
            yield return new("framework", "unavailable", ".NET Framework Full运行时低于4.8，请安装官方运行时。", "framework_too_old");
        else
            yield return Component("framework", framework.ClrReadable, "framework_compatible", "framework_clr_unreadable",
                ".NET Framework Full及64位CLR文件检查通过。", ".NET Framework 64位CLR无法读取，请检查官方运行时安装。");
    }

    /// <summary>:param file: 已确认可读文件。:param snapshot: 元数据快照。:returns: 映像兼容原因；数据文件不额外要求PE。</summary>
    internal static PresentationDependencyComponent? Image(PresentationDependencyFile file, PresentationDependencySnapshot snapshot)
    {
        if (file.Id is not ("host" or "protocol" or "soffice_exe" or "soffice_bin" or "cli_uno")) return null;
        if (!snapshot.Images.TryGetValue(file.Id, out var image) || !image.IsValidImage)
            return new(file.Id, "unavailable", $"{file.Label}不是可读取的有效PE映像。", "invalid_image");
        var x64Compatible = image.Architecture == PresentationImageArchitecture.X64 ||
            image.Architecture == PresentationImageArchitecture.X86 && image.HasManagedMetadata && image.IlOnly && !image.Requires32Bit && !image.Prefers32Bit;
        if (!x64Compatible || (file.Id is "host" or "soffice_exe" or "soffice_bin" or "cli_uno") && image.Architecture != PresentationImageArchitecture.X64)
            return new(file.Id, "unavailable", $"{file.Label}不兼容当前x64运行平台。", "architecture_incompatible");
        if ((file.Id is "soffice_exe" or "soffice_bin") && image.IsDll)
            return new(file.Id, "unavailable", $"{file.Label}必须是可执行映像，不能用DLL替代。", "executable_kind_invalid");
        if ((file.Id is "soffice_exe" or "soffice_bin") && (!image.IsExecutableImage || !image.HasNativeEntryPoint))
            return new(file.Id, "unavailable", $"{file.Label}缺少原生可执行标志或入口地址。", "native_entrypoint_invalid");
        if (file.Id == "cli_uno" && (!image.IsDll || !image.HasManagedMetadata || image.IlOnly || image.Identity?.Name != "cli_uno"))
            return new(file.Id, "unavailable", "cli_uno.dll必须是匹配名称的x64混合托管DLL。", "cli_uno_kind_invalid");
        if (file.Id is "host" or "protocol")
        {
            var expectedName = file.Id == "host" ? "ScpCv.LibreOfficeHost" : "ScpCv.Presentation.Protocol";
            if (!image.HasManagedMetadata || image.Identity?.Name != expectedName || image.TargetFramework != ".NETFramework,Version=v4.8" ||
                !image.References.Any(reference => reference.Name == "mscorlib" && reference.Version == "4.0.0.0" && reference.PublicKeyToken == "b77a5c561934e089") ||
                image.References.Any(reference => reference.Name == "System.Private.CoreLib"))
                return new(file.Id, "unavailable", $"{file.Label}身份/TargetFramework/基础引用不是匹配的.NET Framework 4.8组件。", "framework_metadata_incompatible");
            if (file.Id == "host" && (image.IsDll || !image.HasManagedEntryPoint))
                return new(file.Id, "unavailable", "Framework文稿宿主没有有效的托管可执行入口。", "host_entrypoint_invalid");
            if (file.Id == "protocol" && !image.IsDll)
                return new(file.Id, "unavailable", "文稿协议文件不是托管DLL。", "protocol_kind_invalid");
            if (file.Id == "protocol" && image.ProtocolVersion != ScpCv.Presentation.Protocol.PresentationRequestPolicy.ProtocolVersion)
                return new(file.Id, "unavailable", "文稿协议的实际wire版本与当前控制主机不一致。", "protocol_version_incompatible");
            if (file.Id == "protocol" && snapshot.Images.TryGetValue("host", out var host) &&
                !host.References.Any(reference => reference == image.Identity))
                return new(file.Id, "unavailable", "文稿协议程序集与宿主实际引用身份不匹配。", "protocol_binding_incompatible");
        }
        return new(file.Id, "installed", $"{file.Label}静态映像与运行元数据检查通过。", "image_compatible");
    }

    /// <summary>:param id: 组件。:param ready: 事实。:param successCode: 成功码。:param failureCode: 失败码。:param success: 成功说明。:param failure: 失败说明。:returns: 同一事实的状态与原因。</summary>
    private static PresentationDependencyComponent Component(string id, bool ready, string successCode, string failureCode, string success, string failure) =>
        new(id, ready ? "installed" : "unavailable", ready ? success : failure, ready ? successCode : failureCode);
}
