/*
 * 固定字段顺序的 UTF-8/小端纯值 SHA256 指纹；不依赖两个框架的 JSON 排版。
 * @Project : SCP-cv
 * @File : PresentationFingerprints.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ScpCv.Presentation.Protocol;

public static class PresentationFingerprints
{
    /// <summary>
    /// 为结构合法的请求生成固定字段指纹；过期请求可审计但不能因此执行。
    /// :param request: 完整请求。
    /// :returns: 大写 SHA256 十六进制。
    /// :raises InvalidDataException: 结构不合法。
    /// </summary>
    public static string Request(PresentationRequest request)
    {
        var valid = PresentationRequestPolicy.ValidateStructure(request);
        if (!valid.Allowed) throw new InvalidDataException(valid.Code);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
        {
            writer.Write("SCP-CV.Presentation.Request/v3");
            writer.Write(request.ProtocolVersion);
            writer.Write(request.OperationId.ToString("D"));
            WriteGuid(writer, request.ParentCommandId);
            WriteGuid(writer, request.ClaimToken);
            WriteGuid(writer, request.ParentJobId);
            WriteGuid(writer, request.ParentShutdownId);
            WriteScope(writer, request.Scope);
            WriteTime(writer, request.Deadline);
            writer.Write(request.Operation);
            writer.Write(request.SourcePath);
            writer.Write(request.OutputDirectory);
            writer.Write(request.TargetSlide);
            WriteLong(writer, request.PositionMs);
            writer.Write(request.Volume.HasValue);
            if (request.Volume.HasValue) writer.Write(request.Volume.Value);
            writer.Write(request.Muted.HasValue);
            if (request.Muted.HasValue) writer.Write(request.Muted.Value);
            writer.Write(request.ReadOnly);
            writer.Write(request.AllowMacros);
            writer.Write(request.UpdateExternalLinks);
            writer.Write(request.FitText);
            writer.Write(request.ParentWindow != null);
            if (request.ParentWindow != null)
            {
                var parent = request.ParentWindow;
                writer.Write(parent.Handle);
                writer.Write(parent.ProcessId);
                WriteTime(writer, parent.ProcessStartTime);
                writer.Write(parent.SessionId);
                writer.Write(parent.Dpi);
                writer.Write(parent.Width);
                writer.Write(parent.Height);
            }
        }
        return Digest(bytes.ToArray());
    }

    /// <summary>
    /// 计算结果指纹，覆盖受理/终态、错误和全部实际观测；排除自身结果指纹字段。
    /// :param response: 待盖章或核对的完整结果。
    /// :returns: 大写 SHA256 十六进制。
    /// :raises InvalidDataException: 结果结构不合法。
    /// </summary>
    public static string Response(PresentationResponse response)
    {
        var valid = PresentationResultPolicy.ValidateEnvelope(response, requireFingerprint: false);
        if (!valid.Allowed) throw new InvalidDataException(valid.Code);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
        {
            writer.Write("SCP-CV.Presentation.Response/v3");
            writer.Write(response.ProtocolVersion);
            writer.Write(response.OperationId.ToString("D"));
            writer.Write(response.RequestFingerprint.ToUpperInvariant());
            WriteScope(writer, response.Scope);
            writer.Write(response.Status);
            WriteTime(writer, response.CompletedAt);
            writer.Write(response.ErrorCode);
            writer.Write(response.ErrorDetail);
            writer.Write(response.Observation != null);
            if (response.Observation != null)
            {
                var observed = response.Observation;
                writer.Write(observed.Confirmed);
                WriteLong(writer, observed.SourceId);
                writer.Write(observed.CurrentSlide);
                writer.Write(observed.TotalSlides);
                writer.Write(observed.DocumentClosed);
                writer.Write(observed.EngineExited);
                WriteLong(writer, observed.PositionMs);
                WriteLong(writer, observed.DurationMs);
                writer.Write(observed.ArtifactDigest);
            }
        }
        return Digest(bytes.ToArray());
    }

    /// <summary>
    /// 复制纯值 scope，避免结果复用请求的可变对象引用；复制不是 OS 归属证明。
    /// :param scope: 已验证 scope。
    /// :returns: 独立纯值副本。
    /// </summary>
    internal static PresentationScope CopyScope(PresentationScope scope) => new PresentationScope
    {
        Role = scope.Role, WindowId = scope.WindowId, EngineInstanceId = scope.EngineInstanceId,
        GroupEpoch = scope.GroupEpoch, OwnerEpoch = scope.OwnerEpoch, EngineEpoch = scope.EngineEpoch,
        DocumentEpoch = scope.DocumentEpoch, SourceId = scope.SourceId, SourceRevision = scope.SourceRevision,
        SourceGeneration = scope.SourceGeneration, SourceDigest = scope.SourceDigest,
    };

    /// <summary>
    /// 写出固定 scope 顺序；摘要仅统一十六进制大小写，不正规化文件路径。
    /// :param writer: 规范字节写入器。
    /// :param scope: 已验证纯值 scope。
    /// :returns: 无。
    /// </summary>
    private static void WriteScope(BinaryWriter writer, PresentationScope scope)
    {
        writer.Write(scope.Role);
        writer.Write(scope.WindowId);
        writer.Write(scope.EngineInstanceId.ToString("D"));
        writer.Write(scope.GroupEpoch);
        writer.Write(scope.OwnerEpoch);
        writer.Write(scope.EngineEpoch);
        writer.Write(scope.DocumentEpoch);
        writer.Write(scope.SourceId);
        writer.Write(scope.SourceRevision);
        writer.Write(scope.SourceGeneration);
        writer.Write(scope.SourceDigest.ToUpperInvariant());
    }

    /// <summary>
    /// 写出可空 UUID 的存在位与固定 D 格式。
    /// :param writer: 规范字节写入器。
    /// :param value: 可空 UUID。
    /// :returns: 无。
    /// </summary>
    private static void WriteGuid(BinaryWriter writer, Guid? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue) writer.Write(value.Value.ToString("D"));
    }

    /// <summary>
    /// 写出可空整数，不将 null 当成零。
    /// :param writer: 规范字节写入器。
    /// :param value: 可空整数。
    /// :returns: 无。
    /// </summary>
    private static void WriteLong(BinaryWriter writer, long? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue) writer.Write(value.Value);
    }

    /// <summary>
    /// 将显式时区时间写成 UTC ticks，避免 JSON 时间格式或本机文化差异。
    /// :param writer: 规范字节写入器。
    /// :param value: 已验证时间。
    /// :returns: 无。
    /// </summary>
    private static void WriteTime(BinaryWriter writer, string value)
    {
        PresentationRequestPolicy.TryTimestamp(value, out var timestamp);
        writer.Write(timestamp.UtcDateTime.Ticks);
    }

    /// <summary>
    /// 使用两个框架共有的 SHA256 实现，不依赖 HashData 或平台原生对象。
    /// :param bytes: 规范字节。
    /// :returns: 大写 SHA256。
    /// </summary>
    private static string Digest(byte[] bytes)
    {
#if NETFRAMEWORK
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty);
#else
        return Convert.ToHexString(SHA256.HashData(bytes));
#endif
    }
}
