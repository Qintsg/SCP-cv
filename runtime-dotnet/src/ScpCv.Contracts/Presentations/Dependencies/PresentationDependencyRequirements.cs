// 运行文件必需事实白名单；固定公开标签，不信任外部快照携带的任意名称或路径。
namespace ScpCv.Contracts.Presentations.Dependencies;

internal static class PresentationDependencyRequirements
{
    internal static readonly (string Id, string Label)[] Files =
    [
        ("host", "Framework文稿宿主"), ("protocol", "Framework文稿协议"),
        ("soffice_exe", "soffice.exe"), ("soffice_bin", "soffice.bin"), ("uno_ini", "uno.ini"),
        ("cli_uno", "cli_uno.dll"), ("types_rdb", "types.rdb"), ("services_rdb", "services.rdb"),
        ("offapi_rdb", "types/offapi.rdb"),
    ];
}
