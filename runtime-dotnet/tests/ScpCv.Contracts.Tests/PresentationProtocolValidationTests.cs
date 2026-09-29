/*
 * 纯请求/能力/副作用参数验证回归：不启动 OS、UNO、Office 或显示器。
 * @Project : SCP-cv
 * @File : PresentationProtocolValidationTests.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>
    /// 三项统一媒体控制即便伪造能力为 true，仍不得进入原生 executor。
    /// :param operation: 当前明确不支持的动作。
    /// :returns: 有身份和指纹的 unsupported，零副作用。
    /// </summary>
    [Theory]
    [InlineData("media_resume")]
    [InlineData("media_seek")]
    public void UnsupportedMediaActionsNeverReachExecutor(string operation)
    {
        var request = CreateRequest(operation);
        if (operation == "media_seek") request.PositionMs = 500;
        var nativeCalls = 0;
        var response = PresentationExecutionGate.Execute(request, new PresentationCapabilities
        {
            CanPauseMedia = true, CanResumeMedia = true, CanSeekMedia = true,
        }, CloneScope(request.Scope), Now, _ => { nativeCalls++; return new PresentationResponse(); });
        Assert.Equal("unsupported", response.Status);
        Assert.Equal("unsupported_media_control", response.ErrorCode);
        Assert.Equal(0, nativeCalls);
        Assert.Equal(64, response.RequestFingerprint.Length);
        Assert.Equal(64, response.ResultFingerprint.Length);
        var decision = PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now);
        Assert.True(decision.Matched);
        Assert.False(decision.CanApplyObservation);
    }

    /// <summary>
    /// 默认未验证能力关闭；接受正确结构不能自动授权原生动作。
    /// :returns: 明确缺能力拒绝，不执行默认开启。
    /// </summary>
    [Fact]
    public void DefaultCapabilitiesDoNotAuthorizeOpeningAFile()
    {
        var request = CreateRequest();
        var nativeCalls = 0;
        var response = PresentationExecutionGate.Execute(request, new PresentationCapabilities(),
            CloneScope(request.Scope), Now, _ => { nativeCalls++; return new PresentationResponse(); });
        Assert.Equal("unsupported", response.Status);
        Assert.Equal("capability_unavailable", response.ErrorCode);
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// 正整数代次不等于当前授权，所有 scope 围栏都须与可信宿主独立状态匹配。
    /// :param fence: 被改变的当前授权维度。
    /// :returns: 旧请求在原生操作前失败，零回调。
    /// </summary>
    [Theory]
    [InlineData("role")]
    [InlineData("window")]
    [InlineData("instance")]
    [InlineData("group")]
    [InlineData("owner")]
    [InlineData("engine")]
    [InlineData("document")]
    [InlineData("source")]
    [InlineData("revision")]
    [InlineData("generation")]
    [InlineData("digest")]
    public void StaleRequestsCannotReachExecutorEvenWithPositiveEpochs(string fence)
    {
        var request = CreateRequest();
        var current = CloneScope(request.Scope);
        ChangeFence(current, fence);
        var nativeCalls = 0;
        var response = PresentationExecutionGate.Execute(request, new PresentationCapabilities { CanOpen = true },
            current, Now, _ => { nativeCalls++; return new PresentationResponse(); });
        Assert.Equal("failed", response.Status);
        Assert.Equal("scope_fenced", response.ErrorCode);
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// 无效的身份、期限及副作用参数在任何原生 callback 前拒绝。
    /// :param invalidCase: 具体无效输入。
    /// :param code: 期望诊断码。
    /// :returns: 校验失败和零原生回调，不靠 OS 报错收尾。
    /// </summary>
    [Theory]
    [InlineData("version", "unsupported_protocol_version")]
    [InlineData("operation_id", "invalid_operation_id")]
    [InlineData("unknown_operation", "unknown_operation")]
    [InlineData("retired_window", "invalid_target")]
    [InlineData("wrong_role", "invalid_target")]
    [InlineData("engine_instance", "invalid_engine_identity")]
    [InlineData("group_zero", "invalid_engine_identity")]
    [InlineData("owner_zero", "invalid_engine_identity")]
    [InlineData("engine_zero", "invalid_engine_identity")]
    [InlineData("document_zero", "invalid_document_identity")]
    [InlineData("source_zero", "invalid_document_identity")]
    [InlineData("revision_zero", "invalid_document_identity")]
    [InlineData("generation_zero", "invalid_document_identity")]
    [InlineData("digest", "invalid_document_identity")]
    [InlineData("parent_command", "invalid_parent_identity")]
    [InlineData("claim", "invalid_parent_identity")]
    [InlineData("job_on_display", "invalid_parent_identity")]
    [InlineData("shutdown_on_open", "invalid_parent_identity")]
    [InlineData("parent_handle", "invalid_parent_window")]
    [InlineData("parent_pid", "invalid_parent_window")]
    [InlineData("parent_start", "invalid_parent_window")]
    [InlineData("parent_session", "invalid_parent_window")]
    [InlineData("parent_dpi", "invalid_parent_window")]
    [InlineData("parent_size", "invalid_parent_window")]
    [InlineData("deadline_invalid", "invalid_deadline")]
    [InlineData("deadline_expired", "deadline_expired")]
    [InlineData("deadline_too_far", "deadline_too_far")]
    [InlineData("editable", "unsafe_document_flags")]
    [InlineData("macros", "unsafe_document_flags")]
    [InlineData("external_links", "unsafe_document_flags")]
    [InlineData("relative_path", "invalid_source_path")]
    [InlineData("remote_path", "invalid_source_path")]
    [InlineData("parent_directory", "invalid_source_path")]
    [InlineData("alternate_stream", "invalid_source_path")]
    [InlineData("executable", "invalid_source_path")]
    [InlineData("wildcard", "invalid_source_path")]
    [InlineData("bad_slide", "invalid_slide")]
    [InlineData("unexpected_position", "unexpected_position")]
    [InlineData("unexpected_volume", "unexpected_volume")]
    [InlineData("unexpected_mute", "unexpected_mute")]
    public void InvalidSideEffectInputsAreRejectedBeforeExecution(string invalidCase, string code)
    {
        var request = CreateRequest();
        Invalidate(request, invalidCase);
        var nativeCalls = 0;
        var exception = Assert.Throws<InvalidDataException>(() => PresentationExecutionGate.Execute(request,
            new PresentationCapabilities { CanOpen = true }, CloneScope(request.Scope), Now,
            _ => { nativeCalls++; return new PresentationResponse(); }));
        Assert.Equal(code, exception.Message);
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// 转换目标不能与原件目录等价或包含原件，混用路径分隔符也不能绕过。
    /// :returns: 转换前拒绝覆盖原件的输出目录，零回调。
    /// </summary>
    [Fact]
    public void ConverterCannotUseOriginalDirectoryWithMixedPathSeparators()
    {
        var request = CreateConverterRequest();
        request.SourcePath = "C:/media/source.pptx";
        request.OutputDirectory = @"C:\media";
        var nativeCalls = 0;
        var exception = Assert.Throws<InvalidDataException>(() => PresentationExecutionGate.Execute(request,
            new PresentationCapabilities { CanExportSlides = true }, CloneScope(request.Scope), Now,
            _ => { nativeCalls++; return new PresentationResponse(); }));
        Assert.Equal("invalid_output_directory", exception.Message);
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// Converter 和 shutdown 分别使用作业/生命周期身份，不伪造 display claim。
    /// :returns: 两类合法请求结构可验证；身份互斥错误明确失败。
    /// </summary>
    [Fact]
    public void ConverterAndShutdownUseDistinctParentIdentityKinds()
    {
        var converter = CreateConverterRequest();
        Assert.True(PresentationRequestPolicy.Validate(converter, Now).Allowed);
        converter.ClaimToken = Guid.NewGuid();
        Assert.Equal("invalid_converter_identity", PresentationRequestPolicy.Validate(converter, Now).Code);
        var shutdown = CreateShutdownRequest();
        Assert.True(PresentationRequestPolicy.Validate(shutdown, Now).Allowed);
        shutdown.ParentCommandId = Guid.NewGuid();
        Assert.Equal("invalid_shutdown_identity", PresentationRequestPolicy.Validate(shutdown, Now).Code);
    }

    /// <summary>
    /// 只通过结构和授权 scope 的动作才进入 executor，传递的是独立请求快照。
    /// :returns: 恰一次回调，不把 DTO 可变引用称为不可变/OS 证明。
    /// </summary>
    [Fact]
    public void AuthorizedOpenPassesAnIndependentValueSnapshotToExecutor()
    {
        var request = CreateRequest();
        var nativeCalls = 0;
        var response = PresentationExecutionGate.Execute(request, new PresentationCapabilities { CanOpen = true },
            CloneScope(request.Scope), Now, captured =>
            {
                nativeCalls++;
                Assert.NotSame(request, captured);
                Assert.NotSame(request.Scope, captured.Scope);
                Assert.NotSame(request.ParentWindow, captured.ParentWindow);
                return ResponseFor(captured);
            });
        Assert.Equal(1, nativeCalls);
        Assert.True(PresentationResultPolicy.Match(request, response, CloneScope(request.Scope), Now).CanApplyObservation);
    }

    /// <summary>
    /// executor 期间修改原 DTO 不改变已经校验的独立输入；不是并发或 OS 身份证明。
    /// :returns: 确定性别名隔离，matcher 使用调用方保留的快照。
    /// </summary>
    [Fact]
    public void MutatingOriginalDtoDoesNotChangeTheValidatedExecutorSnapshot()
    {
        var request = CreateRequest();
        var original = PresentationWireCodec.DecodeRequest(PresentationWireCodec.EncodeRequest(request));
        var current = CloneScope(request.Scope);
        var response = PresentationExecutionGate.Execute(request, new PresentationCapabilities { CanOpen = true },
            current, Now, captured =>
            {
                request.Operation = "media_pause";
                request.ReadOnly = false;
                request.Scope.SourceGeneration++;
                Assert.Equal("open", captured.Operation);
                Assert.True(captured.ReadOnly);
                Assert.Equal(original.Scope.SourceGeneration, captured.Scope.SourceGeneration);
                return ResponseFor(captured);
            });
        Assert.True(PresentationResultPolicy.Match(original, response, current, Now).CanApplyObservation);
    }

    /// <summary>
    /// 修改独立当前 scope 夹具中的单一围栏。
    /// :param scope: 当前授权纯值。
    /// :param fence: 要改变的维度。
    /// :returns: 无。
    /// </summary>
    private static void ChangeFence(PresentationScope scope, string fence)
    {
        switch (fence)
        {
            case "role": scope.Role = "impress-2"; break;
            case "window": scope.WindowId = 2; break;
            case "instance": scope.EngineInstanceId = Guid.NewGuid(); break;
            case "group": scope.GroupEpoch++; break;
            case "owner": scope.OwnerEpoch++; break;
            case "engine": scope.EngineEpoch++; break;
            case "document": scope.DocumentEpoch++; break;
            case "source": scope.SourceId++; break;
            case "revision": scope.SourceRevision++; break;
            case "generation": scope.SourceGeneration++; break;
            case "digest": scope.SourceDigest = new string('b', 64); break;
            default: throw new InvalidOperationException("未知测试围栏");
        }
    }

    /// <summary>
    /// 仅修改一个输入维度以定位拒绝原因。
    /// :param request: 合法夹具请求。
    /// :param invalidCase: 无效维度。
    /// :returns: 无。
    /// </summary>
    private static void Invalidate(PresentationRequest request, string invalidCase)
    {
        var parent = request.ParentWindow ?? throw new InvalidOperationException("夹具缺少父窗口");
        switch (invalidCase)
        {
            case "version": request.ProtocolVersion++; break;
            case "operation_id": request.OperationId = Guid.Empty; break;
            case "unknown_operation": request.Operation = "save"; break;
            case "retired_window": request.Scope.WindowId = 3; break;
            case "wrong_role": request.Scope.Role = "impress-2"; break;
            case "engine_instance": request.Scope.EngineInstanceId = Guid.Empty; break;
            case "group_zero": request.Scope.GroupEpoch = 0; break;
            case "owner_zero": request.Scope.OwnerEpoch = 0; break;
            case "engine_zero": request.Scope.EngineEpoch = 0; break;
            case "document_zero": request.Scope.DocumentEpoch = 0; break;
            case "source_zero": request.Scope.SourceId = 0; break;
            case "revision_zero": request.Scope.SourceRevision = 0; break;
            case "generation_zero": request.Scope.SourceGeneration = 0; break;
            case "digest": request.Scope.SourceDigest = new string('g', 64); break;
            case "parent_command": request.ParentCommandId = Guid.Empty; break;
            case "claim": request.ClaimToken = null; break;
            case "job_on_display": request.ParentJobId = Guid.NewGuid(); break;
            case "shutdown_on_open": request.ParentShutdownId = Guid.NewGuid(); break;
            case "parent_handle": parent.Handle = 0; break;
            case "parent_pid": parent.ProcessId = 0; break;
            case "parent_start": parent.ProcessStartTime = "unknown"; break;
            case "parent_session": parent.SessionId = 0; break;
            case "parent_dpi": parent.Dpi = 0; break;
            case "parent_size": parent.Width = 0; break;
            case "deadline_invalid": request.Deadline = "tomorrow"; break;
            case "deadline_expired": request.Deadline = Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture); break;
            case "deadline_too_far": request.Deadline = Now.AddMinutes(6).ToString("O", System.Globalization.CultureInfo.InvariantCulture); break;
            case "editable": request.ReadOnly = false; break;
            case "macros": request.AllowMacros = true; break;
            case "external_links": request.UpdateExternalLinks = true; break;
            case "relative_path": request.SourcePath = "source.pptx"; break;
            case "remote_path": request.SourcePath = @"\\server\media\source.pptx"; break;
            case "parent_directory": request.SourcePath = @"C:\media\..\secret.pptx"; break;
            case "alternate_stream": request.SourcePath = @"C:\media\source:alternate.pptx"; break;
            case "executable": request.SourcePath = @"C:\media\source.exe"; break;
            case "wildcard": request.SourcePath = @"C:\media\source?.pptx"; break;
            case "bad_slide": request.TargetSlide = 501; break;
            case "unexpected_position": request.PositionMs = 1; break;
            case "unexpected_volume": request.Volume = 50; break;
            case "unexpected_mute": request.Muted = true; break;
            default: throw new InvalidOperationException("未知无效测试输入");
        }
    }
}
