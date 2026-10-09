/*
 * 演示文稿侧车的纯值协议：Net48 与 Net10 同源，不携带原生对象。
 * @Project : SCP-cv
 * @File : PresentationMessages.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;
using System.Runtime.Serialization;

namespace ScpCv.Presentation.Protocol;

/// <summary>单次请求必须绑定的目标、进程实例和源代次。</summary>
[DataContract]
public sealed class PresentationScope
{
    [DataMember(Name = "role", Order = 1, IsRequired = true)]
    public string Role { get; set; } = string.Empty;

    [DataMember(Name = "window_id", Order = 2, IsRequired = true)]
    public int WindowId { get; set; }

    [DataMember(Name = "engine_instance_id", Order = 3, IsRequired = true)]
    public Guid EngineInstanceId { get; set; }

    [DataMember(Name = "group_epoch", Order = 4, IsRequired = true)]
    public long GroupEpoch { get; set; }

    [DataMember(Name = "owner_epoch", Order = 5, IsRequired = true)]
    public long OwnerEpoch { get; set; }

    [DataMember(Name = "engine_epoch", Order = 6, IsRequired = true)]
    public long EngineEpoch { get; set; }

    [DataMember(Name = "document_epoch", Order = 7, IsRequired = true)]
    public long DocumentEpoch { get; set; }

    [DataMember(Name = "source_id", Order = 8, IsRequired = true)]
    public long SourceId { get; set; }

    [DataMember(Name = "source_revision", Order = 9, IsRequired = true)]
    public long SourceRevision { get; set; }

    [DataMember(Name = "source_generation", Order = 10, IsRequired = true)]
    public long SourceGeneration { get; set; }

    [DataMember(Name = "source_digest", Order = 11, IsRequired = true)]
    public string SourceDigest { get; set; } = string.Empty;
}

/// <summary>调用方声明的父窗口归属；实际 OS 归属校验必须由宿主执行。</summary>
[DataContract]
public sealed class PresentationParentWindow
{
    [DataMember(Name = "hwnd", Order = 1, IsRequired = true)]
    public long Handle { get; set; }

    [DataMember(Name = "process_id", Order = 2, IsRequired = true)]
    public int ProcessId { get; set; }

    [DataMember(Name = "process_start_time", Order = 3, IsRequired = true)]
    public string ProcessStartTime { get; set; } = string.Empty;

    [DataMember(Name = "session_id", Order = 4, IsRequired = true)]
    public int SessionId { get; set; }

    [DataMember(Name = "dpi", Order = 5, IsRequired = true)]
    public int Dpi { get; set; }

    [DataMember(Name = "width", Order = 6, IsRequired = true)]
    public int Width { get; set; }

    [DataMember(Name = "height", Order = 7, IsRequired = true)]
    public int Height { get; set; }
}

/// <summary>普通类及显式 JSON 成员，避免 Net48 不支持的 record/原生类型。</summary>
[DataContract]
public sealed class PresentationRequest
{
    [DataMember(Name = "protocol_version", Order = 1, IsRequired = true)]
    public int ProtocolVersion { get; set; } = PresentationRequestPolicy.ProtocolVersion;

    [DataMember(Name = "operation_id", Order = 2, IsRequired = true)]
    public Guid OperationId { get; set; }

    [DataMember(Name = "parent_command_id", Order = 3, IsRequired = true)]
    public Guid? ParentCommandId { get; set; }

    [DataMember(Name = "claim_token", Order = 4, IsRequired = true)]
    public Guid? ClaimToken { get; set; }

    [DataMember(Name = "parent_job_id", Order = 5, IsRequired = true)]
    public Guid? ParentJobId { get; set; }

    [DataMember(Name = "scope", Order = 6, IsRequired = true)]
    public PresentationScope Scope { get; set; } = new PresentationScope();

    [DataMember(Name = "deadline", Order = 7, IsRequired = true)]
    public string Deadline { get; set; } = string.Empty;

    [DataMember(Name = "operation", Order = 8, IsRequired = true)]
    public string Operation { get; set; } = string.Empty;

    [DataMember(Name = "source_path", Order = 9, IsRequired = true)]
    public string SourcePath { get; set; } = string.Empty;

    [DataMember(Name = "output_directory", Order = 10, IsRequired = true)]
    public string OutputDirectory { get; set; } = string.Empty;

    [DataMember(Name = "target_slide", Order = 11, IsRequired = true)]
    public int TargetSlide { get; set; }

    [DataMember(Name = "position_ms", Order = 12, IsRequired = true)]
    public long? PositionMs { get; set; }

    [DataMember(Name = "volume", Order = 13, IsRequired = true)]
    public int? Volume { get; set; }

    [DataMember(Name = "muted", Order = 14, IsRequired = true)]
    public bool? Muted { get; set; }

    [DataMember(Name = "parent_window", Order = 15, IsRequired = true)]
    public PresentationParentWindow? ParentWindow { get; set; }

    [DataMember(Name = "parent_shutdown_id", Order = 16, IsRequired = true)]
    public Guid? ParentShutdownId { get; set; }

    [DataMember(Name = "read_only", Order = 17, IsRequired = true)]
    public bool ReadOnly { get; set; } = true;

    [DataMember(Name = "allow_macros", Order = 18, IsRequired = true)]
    public bool AllowMacros { get; set; }

    [DataMember(Name = "update_external_links", Order = 19, IsRequired = true)]
    public bool UpdateExternalLinks { get; set; }

    [DataMember(Name = "fit_text", Order = 20)]
    public bool FitText { get; set; }
}

/// <summary>未获真实验证的能力保持 false；不是输入请求可自行授予的权限。</summary>
[DataContract]
public sealed class PresentationCapabilities
{
    [DataMember(Name = "open", Order = 1)] public bool CanOpen { get; set; }
    [DataMember(Name = "close", Order = 2)] public bool CanClose { get; set; }
    [DataMember(Name = "navigate_slides", Order = 3)] public bool CanNavigateSlides { get; set; }
    [DataMember(Name = "step_effects", Order = 4)] public bool CanStepEffects { get; set; }
    [DataMember(Name = "observe", Order = 5)] public bool CanObserve { get; set; }
    [DataMember(Name = "export_slides", Order = 6)] public bool CanExportSlides { get; set; }
    [DataMember(Name = "shutdown", Order = 7)] public bool CanShutdown { get; set; }
    [DataMember(Name = "media_pause", Order = 8)] public bool CanPauseMedia { get; set; }
    [DataMember(Name = "media_resume", Order = 9)] public bool CanResumeMedia { get; set; }
    [DataMember(Name = "media_seek", Order = 10)] public bool CanSeekMedia { get; set; }
    [DataMember(Name = "media_volume", Order = 11)] public bool CanSetMediaVolume { get; set; }
    [DataMember(Name = "media_mute", Order = 12)] public bool CanMuteMedia { get; set; }
}

/// <summary>宿主返回的实际文稿观测；媒体位置不可得时保留 null，不制造零进度。</summary>
[DataContract]
public sealed class PresentationObservation
{
    [DataMember(Name = "confirmed", Order = 1)] public bool Confirmed { get; set; }
    [DataMember(Name = "source_id", Order = 2)] public long? SourceId { get; set; }
    [DataMember(Name = "current_slide", Order = 3)] public int CurrentSlide { get; set; }
    [DataMember(Name = "total_slides", Order = 4)] public int TotalSlides { get; set; }
    [DataMember(Name = "document_closed", Order = 5)] public bool DocumentClosed { get; set; }
    [DataMember(Name = "engine_exited", Order = 6)] public bool EngineExited { get; set; }
    [DataMember(Name = "position_ms", Order = 7)] public long? PositionMs { get; set; }
    [DataMember(Name = "duration_ms", Order = 8)] public long? DurationMs { get; set; }
    [DataMember(Name = "artifact_digest", Order = 9)] public string ArtifactDigest { get; set; } = string.Empty;
}

/// <summary>accepted 仅受理；succeeded 仍须匹配请求、当前身份及确认观测才能应用。</summary>
[DataContract]
public sealed class PresentationResponse
{
    [DataMember(Name = "protocol_version", Order = 1, IsRequired = true)]
    public int ProtocolVersion { get; set; } = PresentationRequestPolicy.ProtocolVersion;

    [DataMember(Name = "operation_id", Order = 2, IsRequired = true)]
    public Guid OperationId { get; set; }

    [DataMember(Name = "request_fingerprint", Order = 3, IsRequired = true)]
    public string RequestFingerprint { get; set; } = string.Empty;

    [DataMember(Name = "scope", Order = 4, IsRequired = true)]
    public PresentationScope Scope { get; set; } = new PresentationScope();

    [DataMember(Name = "status", Order = 5, IsRequired = true)]
    public string Status { get; set; } = string.Empty;

    [DataMember(Name = "completed_at", Order = 6, IsRequired = true)]
    public string CompletedAt { get; set; } = string.Empty;

    [DataMember(Name = "error_code", Order = 7, IsRequired = true)]
    public string ErrorCode { get; set; } = string.Empty;

    [DataMember(Name = "error_detail", Order = 8, IsRequired = true)]
    public string ErrorDetail { get; set; } = string.Empty;

    [DataMember(Name = "observation", Order = 9)]
    public PresentationObservation? Observation { get; set; }

    [DataMember(Name = "result_fingerprint", Order = 10, IsRequired = true)]
    public string ResultFingerprint { get; set; } = string.Empty;
}
