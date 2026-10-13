// Framework侧车以有界纯值stdin/stdout与父进程协作，UNO只在MTA主线程访问。
using System;
using System.Globalization;
using System.IO;
using ScpCv.LibreOfficeHost.Runtime;
using ScpCv.LibreOfficeHost.Audio;
using ScpCv.LibreOfficeHost.Uno;
using ScpCv.Presentation.Protocol;

namespace ScpCv.LibreOfficeHost;

internal static class Program
{
    /// <summary>:param args: 父进程分配的不可变组/实例/role/profile。:returns: 正常退出0，故障2。</summary>
    [MTAThread]
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Console.SetError(new StreamWriter(Console.OpenStandardError(), new System.Text.UTF8Encoding(false)) { AutoFlush = true });
            using var parentLifetime = new ParentProcessLifetime(int.Parse(Option(args, "parent-pid"), CultureInfo.InvariantCulture),
                long.Parse(Option(args, "parent-start-ticks"), CultureInfo.InvariantCulture), int.Parse(Option(args, "parent-session"), CultureInfo.InvariantCulture));
            var gateName = Option(args, "parent-gate");
            if (!gateName.StartsWith("Local\\SCP-cv-Presentation-", StringComparison.Ordinal)) throw new InvalidDataException("启动门禁名称无效");
            using (var launchGate = System.Threading.EventWaitHandle.OpenExisting(gateName))
                if (!launchGate.WaitOne(10000)) throw new TimeoutException("父进程未确认侧车job归属");
            var role = Option(args, "role");
            var instance = Guid.Parse(Option(args, "instance-id"));
            var group = long.Parse(Option(args, "group-epoch"), CultureInfo.InvariantCulture);
            var owner = long.Parse(Option(args, "owner-epoch"), CultureInfo.InvariantCulture);
            var epoch = long.Parse(Option(args, "engine-epoch"), CultureInfo.InvariantCulture);
            if (role != "impress-1" && role != "impress-2" && role != "converter") throw new InvalidDataException("宿主role无效");
            if (instance == Guid.Empty || group <= 0 || owner <= 0 || epoch <= 0) throw new InvalidDataException("宿主身份无效");
            var officeProgram = Option(args, "office-program");
            var audioEndpoint = OfficeAudioOutputBinding.Preflight(role, officeProgram);
            using var process = new OfficeProcess(officeProgram, Option(args, "profile"), role == "converter");
            using var connection = new UnoConnection(process.ProgramDirectory, process.Profile + "-client", process.Port);
            using var audio = new OfficeAudioOutputBinding(process, connection, audioEndpoint);
            using var document = new PresentationDocument(connection, audio);
            var capabilities = new PresentationCapabilities { CanOpen = role != "converter", CanClose = role != "converter", CanNavigateSlides = role != "converter", CanStepEffects = role != "converter", CanObserve = role != "converter", CanShutdown = true, CanExportSlides = role == "converter" };
            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            PresentationScope? current = null;
            while (true)
            {
                var request = PresentationWireCodec.ReadRequestAsync(input).GetAwaiter().GetResult();
                if (request == null) break;
                if (request.Scope.Role != role || request.Scope.EngineInstanceId != instance || request.Scope.GroupEpoch != group ||
                    request.Scope.OwnerEpoch != owner || request.Scope.EngineEpoch != epoch) throw new InvalidDataException("宿主身份围栏拒绝");
                var validated = PresentationRequestPolicy.Validate(request, DateTimeOffset.UtcNow);
                if (!validated.Allowed) throw new InvalidDataException(validated.Code);
                // wire解码创建独立DTO；串行线程且不向外暴露，先校验后预留。
                if ((request.Operation == "open" || request.Operation == "export_slides") && (current == null || request.Scope.DocumentEpoch > current.DocumentEpoch)) current = request.Scope;
                var authorized = request.Operation == "shutdown" ? request.Scope : current;
                if (authorized == null) throw new InvalidDataException("尚无授权文稿");
                var response = PresentationExecutionGate.Execute(request, capabilities, authorized, DateTimeOffset.UtcNow, accepted =>
                {
                    try
                    {
                    PresentationObservation? observation;
                    var status = "succeeded";
                    var errorCode = string.Empty;
                    if (accepted.Operation == "shutdown")
                    {
                        Console.Error.WriteLine("presentation_shutdown_close_enter: instance=" + instance);
                        NormalOfficeShutdown.Run(process, () => { document.Close(); audio.ReleaseWarmups(); }, connection.Desktop.terminate);
                        document.RetireExitedEngine(); audio.RetireExitedEngine();
                        Console.Error.WriteLine("presentation_shutdown_native_exit_confirmed: instance=" + instance);
                        observation = null;
                    }
                    else if (accepted.Operation == "export_slides")
                    {
                        VerifyOriginal(accepted);
                        observation = SlideExporter.Export(connection, accepted);
                        VerifyOriginal(accepted);
                    }
                    else
                    {
                        VerifyOriginal(accepted);
                        ParentWindowGuard.Verify(accepted.ParentWindow ?? throw new InvalidDataException("缺少父窗口"));
                        var boundary = document.ObserveTerminalBoundary(accepted);
                        if (boundary is not null)
                        {
                            observation = boundary;
                            status = "noop";
                            errorCode = "presentation_at_last_slide";
                        }
                        else observation = accepted.Operation == "open" ? document.Open(accepted) : document.Control(accepted);
                    }
                    var result = new PresentationResponse
                    {
                        OperationId = accepted.OperationId, Scope = accepted.Scope, RequestFingerprint = PresentationFingerprints.Request(accepted),
                        Status = status, ErrorCode = errorCode,
                        CompletedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture), Observation = observation,
                    };
                    result.ResultFingerprint = PresentationFingerprints.Response(result);
                    return result;
                    }
                    catch (PresentationLoadRejectedException error) when (accepted.Operation == "export_slides")
                    {
                        // 只捕获加载阶段的实际交互终止；导出错误仍走原生不确定分支。
                        PresentationLoadRejectedException.VerifyEmptyOutput(accepted.OutputDirectory);
                        VerifyOriginal(accepted);
                        var rejected = new PresentationResponse
                        {
                            OperationId = accepted.OperationId, Scope = accepted.Scope,
                            RequestFingerprint = PresentationFingerprints.Request(accepted), Status = "failed",
                            ErrorCode = "presentation_load_rejected", ErrorDetail = error.Message,
                            CompletedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        };
                        rejected.ResultFingerprint = PresentationFingerprints.Response(rejected);
                        return rejected;
                    }
                    catch (Exception error) when (accepted.Operation == "open")
                    {
                        Console.Error.WriteLine("presentation_open_primary_failure: " + error);
                        process.TraceExitState("open_failure_before_cleanup");
                        // 候选OPEN失败：正常关闭其文稿后才返回failed，父进程保留旧容器。
                        // 关闭自身失败则继续向外抛，按transport uncertain处理并清理独占job。
                        document.Close();
                        var failed = new PresentationResponse
                        {
                            OperationId = accepted.OperationId, Scope = accepted.Scope,
                            RequestFingerprint = PresentationFingerprints.Request(accepted), Status = "failed",
                            ErrorCode = "presentation_open_failed", ErrorDetail = error.Message.Substring(0, Math.Min(1500, error.Message.Length)),
                            CompletedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        };
                        failed.ResultFingerprint = PresentationFingerprints.Response(failed);
                        return failed;
                    }
                });
                PresentationWireCodec.WriteResponseAsync(output, response).GetAwaiter().GetResult();
                if (request.Operation == "shutdown" && response.Status == "succeeded") return 0;
            }
            NormalOfficeShutdown.Run(process, () => { document.Close(); audio.ReleaseWarmups(); }, connection.Desktop.terminate);
            document.RetireExitedEngine(); audio.RetireExitedEngine();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("presentation_host_failure: " + error); return 2; }
    }

    /// <summary>:param request: 已结构校验请求。:returns: OPEN前核磁盘摘要；OS父窗口在承载模块另核。</summary>
    private static void VerifyOriginal(PresentationRequest request)
    {
        if (request.Operation != "open" && request.Operation != "export_slides") return;
        using var stream = File.OpenRead(request.SourcePath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var digest = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        if (!string.Equals(digest, request.Scope.SourceDigest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("原件摘要与授权请求不一致");
    }

    /// <summary>:param args: 命令行。:param name: 参数名。:returns: 必填值，不允许空值。</summary>
    private static string Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, "--" + name);
        if (index < 0 || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1])) throw new ArgumentException("缺少参数：" + name);
        return args[index + 1];
    }
}
