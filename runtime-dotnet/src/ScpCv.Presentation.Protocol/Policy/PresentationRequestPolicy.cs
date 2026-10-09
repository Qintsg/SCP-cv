/*
 * 文稿协议的纯结构、有限参数与期限策略，不证明 PID/HWND 或父租约的真实归属。
 * @Project : SCP-cv
 * @File : PresentationRequestPolicy.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ScpCv.Presentation.Protocol;

/// <summary>纯策略判断，不携带原生执行授权或实际完成证据。</summary>
public sealed class PresentationDecision
{
    /// <summary>
    /// 创建只读策略结果。
    /// :param allowed: 本策略是否允许继续。
    /// :param code: 可诊断原因。
    /// </summary>
    public PresentationDecision(bool allowed, string code) { Allowed = allowed; Code = code; }
    public bool Allowed { get; }
    public string Code { get; }
}

public static class PresentationRequestPolicy
{
    public const int ProtocolVersion = 3;
    public const int MaximumSlides = 500;
    private const string TimestampPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$";

    /// <summary>
    /// 校验请求结构与当前有限执行期限；成功也不证明 OS 归属或命令租约有效。
    /// :param request: 纯值请求。
    /// :param now: 可信调用方时钟。
    /// :returns: 有限策略结果，过期/过远请求不能进入副作用执行。
    /// </summary>
    public static PresentationDecision Validate(PresentationRequest? request, DateTimeOffset now)
    {
        var structure = ValidateStructure(request);
        if (!structure.Allowed || request == null) return structure;
        TryTimestamp(request.Deadline, out var deadline);
        if (deadline <= now) return Reject("deadline_expired");
        return deadline - now > TimeSpan.FromMinutes(5) ? Reject("deadline_too_far") : Accept();
    }

    /// <summary>
    /// 比较可信宿主持有的当前授权操作 scope；调用方不能以请求自身代替 currentScope。
    /// :param expected: 请求声明的 scope。
    /// :param current: 可信宿主预留/持有的当前操作 scope，不是最后实际画面。
    /// :returns: 所有身份、代次和摘要都一致时为 true。
    /// </summary>
    public static bool SameScope(PresentationScope? expected, PresentationScope? current) =>
        expected != null && current != null && expected.Role == current.Role && expected.WindowId == current.WindowId &&
        expected.EngineInstanceId == current.EngineInstanceId && expected.GroupEpoch == current.GroupEpoch &&
        expected.OwnerEpoch == current.OwnerEpoch && expected.EngineEpoch == current.EngineEpoch &&
        expected.DocumentEpoch == current.DocumentEpoch && expected.SourceId == current.SourceId &&
        expected.SourceRevision == current.SourceRevision && expected.SourceGeneration == current.SourceGeneration &&
        string.Equals(expected.SourceDigest, current.SourceDigest, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 校验固定结构，允许为已过期操作计算审计指纹，但不允许据此执行。
    /// :param request: 待校验纯值请求。
    /// :returns: 结构合法性。
    /// </summary>
    internal static PresentationDecision ValidateStructure(PresentationRequest? request)
    {
        if (request == null || request.Scope == null) return Reject("invalid_request");
        if (request.ProtocolVersion != ProtocolVersion) return Reject("unsupported_protocol_version");
        if (request.OperationId == Guid.Empty) return Reject("invalid_operation_id");
        if (!KnownOperation(request.Operation)) return Reject("unknown_operation");
        var scope = request.Scope;
        var converter = scope.Role == "converter";
        if (!converter && (scope.WindowId < 1 || scope.WindowId > 2 ||
            scope.Role != "impress-" + scope.WindowId.ToString(CultureInfo.InvariantCulture))) return Reject("invalid_target");
        if (converter && scope.WindowId != 0) return Reject("invalid_target");
        if (scope.EngineInstanceId == Guid.Empty || scope.GroupEpoch <= 0 || scope.OwnerEpoch <= 0 || scope.EngineEpoch <= 0)
            return Reject("invalid_engine_identity");
        if (request.Operation == "shutdown")
        {
            if (!Nonempty(request.ParentShutdownId) || request.ParentCommandId != null || request.ClaimToken != null ||
                request.ParentJobId != null || request.ParentWindow != null) return Reject("invalid_shutdown_identity");
            if (scope.DocumentEpoch != 0 || scope.SourceId != 0 || scope.SourceRevision != 0 || scope.SourceGeneration != 0 ||
                scope.SourceDigest != string.Empty) return Reject("invalid_shutdown_scope");
        }
        else
        {
            if (request.ParentShutdownId != null) return Reject("invalid_parent_identity");
            if (scope.DocumentEpoch <= 0 || scope.SourceId <= 0 || scope.SourceRevision <= 0 || scope.SourceGeneration <= 0 ||
                !IsDigest(scope.SourceDigest)) return Reject("invalid_document_identity");
            if (converter)
            {
                if (request.Operation != "export_slides" || !Nonempty(request.ParentJobId) || request.ParentCommandId != null ||
                    request.ClaimToken != null || request.ParentWindow != null) return Reject("invalid_converter_identity");
            }
            else
            {
                if (request.Operation == "export_slides" || !Nonempty(request.ParentCommandId) || !Nonempty(request.ClaimToken) ||
                    request.ParentJobId != null) return Reject("invalid_parent_identity");
                if (!ValidParent(request.ParentWindow)) return Reject("invalid_parent_window");
            }
        }
        if (!TryTimestamp(request.Deadline, out _)) return Reject("invalid_deadline");
        if (!request.ReadOnly || request.AllowMacros || request.UpdateExternalLinks) return Reject("unsafe_document_flags");
        return ValidateArguments(request);
    }

    /// <summary>
    /// 校验固定动作的参数，不允许无关副作用字段搭车。
    /// :param request: 结构已校验请求。
    /// :returns: 参数合法性。
    /// </summary>
    private static PresentationDecision ValidateArguments(PresentationRequest request)
    {
        var opensFile = request.Operation == "open" || request.Operation == "export_slides";
        if (request.FitText && !opensFile) return Reject("unexpected_text_layout");
        if (opensFile && (!LocalPath(request.SourcePath) || !PresentationSource(request.SourcePath))) return Reject("invalid_source_path");
        if (!opensFile && request.SourcePath != string.Empty) return Reject("unexpected_source_path");
        if (request.Operation == "export_slides")
        {
            if (!LocalPath(request.OutputDirectory)) return Reject("invalid_output_directory");
            var output = request.OutputDirectory.Replace('/', '\\').TrimEnd('\\');
            var original = request.SourcePath.Replace('/', '\\');
            if (string.Equals(original, output, StringComparison.OrdinalIgnoreCase) ||
                original.StartsWith(output + "\\", StringComparison.OrdinalIgnoreCase))
                return Reject("invalid_output_directory");
        }
        else if (request.OutputDirectory != string.Empty) return Reject("unexpected_output_directory");
        if (request.Operation == "open" || request.Operation == "goto")
        {
            if (request.TargetSlide < 1 || request.TargetSlide > MaximumSlides) return Reject("invalid_slide");
        }
        else if (request.TargetSlide != 0) return Reject("unexpected_slide");
        if (request.Operation == "media_seek")
        {
            if (request.PositionMs == null || request.PositionMs < 0) return Reject("invalid_position");
        }
        else if (request.PositionMs != null) return Reject("unexpected_position");
        if (request.Operation == "media_volume")
        {
            if (request.Volume == null || request.Volume < 0 || request.Volume > 100) return Reject("invalid_volume");
        }
        else if (request.Volume != null) return Reject("unexpected_volume");
        if (request.Operation == "media_mute")
        {
            if (request.Muted == null) return Reject("invalid_mute");
        }
        else if (request.Muted != null) return Reject("unexpected_mute");
        return Accept();
    }

    /// <summary>:param path: 已验证本地路径。:returns: 与既有非PDF文稿登记格式对齐；不授予宏/密码/外链能力。</summary>
    private static bool PresentationSource(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".ppt" or ".pps" or ".pot" or ".pptx" or ".pptm" or ".ppsx" or ".ppsm" or ".potx" or ".potm" or ".odp";

    /// <summary>
    /// 检查本机绝对 Windows 路径，根目录白名单与实际文件摘要仍由宿主验证。
    /// :param path: 输入路径。
    /// :returns: 不含控制字符、UNC、ADS 或相对父目录时为 true。
    /// </summary>
    private static bool LocalPath(string? path)
    {
        if (path == null || path.Length > 2048 || path.Length < 4 || path[1] != ':' ||
            !((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) ||
            (path[2] != '\\' && path[2] != '/')) return false;
        for (var index = 2; index < path.Length; index++)
            if (char.IsControl(path[index]) || path[index] == ':' || path[index] == '"' || path[index] == '*' || path[index] == '?') return false;
        foreach (var segment in path.Split('\\', '/')) if (segment == "." || segment == "..") return false;
        return true;
    }

    /// <summary>
    /// 校验父窗口声明的有限值，不调用 OS 或据此宣称句柄属于该进程。
    /// :param parent: 父窗口纯值声明。
    /// :returns: 声明结构完整。
    /// </summary>
    private static bool ValidParent(PresentationParentWindow? parent) => parent != null && parent.Handle > 0 &&
        parent.ProcessId > 0 && parent.SessionId > 0 && parent.Dpi >= 72 && parent.Dpi <= 768 &&
        parent.Width > 0 && parent.Width <= 32768 && parent.Height > 0 && parent.Height <= 32768 &&
        TryTimestamp(parent.ProcessStartTime, out _);

    /// <summary>
    /// 读取带显式时区的 ISO 时间，统一转 UTC 供两个框架使用。
    /// :param value: 时间文本。
    /// :param timestamp: 成功时的 UTC 时间。
    /// :returns: 可解析且格式有限。
    /// </summary>
    internal static bool TryTimestamp(string? value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return value != null && value.Length <= 40 && Regex.IsMatch(value, TimestampPattern,
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)) && DateTimeOffset.TryParse(value,
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out timestamp);
    }

    /// <summary>
    /// 验证 SHA256 文本，不把它当作磁盘原件摘要已被验证。
    /// :param value: 摘要文本。
    /// :returns: 恰为 64 位十六进制。
    /// </summary>
    internal static bool IsDigest(string? value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (var character in value)
            if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F'))) return false;
        return true;
    }

    /// <summary>
    /// 检查有限动作集合。
    /// :param operation: 动作名。
    /// :returns: 当前协议版本是否识别。
    /// </summary>
    private static bool KnownOperation(string operation) => operation == "open" || operation == "close" ||
        operation == "next_slide" || operation == "previous_slide" || operation == "goto" ||
        operation == "next_effect" || operation == "previous_effect" || operation == "observe" ||
        operation == "export_slides" || operation == "shutdown" || operation == "media_pause" ||
        operation == "media_resume" || operation == "media_seek" || operation == "media_volume" || operation == "media_mute";

    /// <summary>
    /// 检查可空 UUID 是否有非零值。
    /// :param value: UUID。
    /// :returns: 存在且非空。
    /// </summary>
    private static bool Nonempty(Guid? value) => value.HasValue && value.Value != Guid.Empty;

    /// <summary>
    /// 创建允许的纯策略结果。
    /// :returns: 不代表原生完成的允许判断。
    /// </summary>
    private static PresentationDecision Accept() => new PresentationDecision(true, "ok");

    /// <summary>
    /// 创建拒绝的纯策略结果。
    /// :param code: 拒绝原因。
    /// :returns: 不授权外部执行。
    /// </summary>
    private static PresentationDecision Reject(string code) => new PresentationDecision(false, code);
}
