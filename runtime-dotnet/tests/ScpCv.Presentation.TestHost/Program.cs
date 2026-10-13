// 合同测试专用stdio侧车：真实启动门禁/管道，固定故障模式，不访问Office、媒体或设备。
using ScpCv.Presentation.Protocol;
using System.Globalization;

namespace ScpCv.Presentation.TestHost;

internal static class Program
{
    /// <summary>:param args: 客户端固定参数，program末段选择测试模式。:returns: 正常EOF退出0；受控故障模式退出2，不作为真实Office退出证明。</summary>
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 2;
        if (args.FirstOrDefault() == "--console-holder") return await ConsoleHolder.RunAsync(args).ConfigureAwait(false);
        if (args.Length == 1 && args[0].StartsWith("--gated-child=", StringComparison.Ordinal))
        {
            using var childGate = EventWaitHandle.OpenExisting(args[0]["--gated-child=".Length..]);
            return childGate.WaitOne(TimeSpan.FromSeconds(8)) ? 0 : 2;
        }
        if (args.Length == 1 && args[0] is "--short-child" or "--long-child")
        {
            await Task.Delay(args[0] == "--short-child" ? 500 : 30000).ConfigureAwait(false);
            return 0;
        }
        var root = Option(args, "profile");
        var mode = Path.GetFileName(Option(args, "office-program"));
        using var gate = EventWaitHandle.OpenExisting(Option(args, "parent-gate"));
        if (!gate.WaitOne(TimeSpan.FromSeconds(10))) return 2;
        Directory.CreateDirectory(root);
        using var input = Console.OpenStandardInput();
        using var output = Console.OpenStandardOutput();
        var childStarted = false;
        while (await PresentationWireCodec.ReadRequestAsync(input).ConfigureAwait(false) is { } request)
        {
            await File.AppendAllTextAsync(Path.Combine(root, "requests.txt"), request.OperationId + "\n").ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(root, "received"), request.OperationId.ToString()).ConfigureAwait(false);
            if (mode is "late-child" or "stuck-child" && !childStarted)
            {
                using var child = RestrictedTestChild.Start(mode == "late-child"
                    ? "--gated-child=Local\\SCP-cv-ShortChild-" + Path.GetFileName(root) : "--long-child");
                await File.WriteAllTextAsync(Path.Combine(root, "child-pid"), child.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                childStarted = true;
            }
            if (mode == "silent") continue;
            if (mode == "queued")
            {
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!File.Exists(Path.Combine(root, "release"))) await Task.Delay(10, budget.Token).ConfigureAwait(false);
            }
            var response = new PresentationResponse
            {
                OperationId = request.OperationId, Scope = request.Scope, Status = "succeeded",
                RequestFingerprint = PresentationFingerprints.Request(request), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            };
            if (mode is "missing-artifact" or "missing-artifact-exit-error")
                response.Observation = new PresentationObservation { Confirmed = true, SourceId = request.Scope.SourceId,
                    TotalSlides = 1, ArtifactDigest = new string('a', 64) };
            if (mode is "open-rejected" or "open-rejected-exit-error")
            {
                response.Status = "failed";
                response.ErrorCode = "controlled_open_rejected";
                response.ErrorDetail = "外部夹具明确拒绝打开";
            }
            if (mode is "export-rejected" or "export-rejected-exit-error")
            {
                response.Status = "failed";
                response.ErrorCode = "controlled_export_rejected";
                response.ErrorDetail = "外部夹具明确拒绝转换";
            }
            if (mode is "shutdown-rejected" or "shutdown-rejected-exit-error" or "close-normal" or "close-normal-exit-error")
            {
                if (request.Operation == "shutdown" && mode.StartsWith("shutdown-rejected", StringComparison.Ordinal))
                {
                    response.Status = "failed";
                    response.ErrorCode = "controlled_shutdown_rejected";
                    response.ErrorDetail = "外部夹具明确拒绝关闭";
                }
                else if (request.Operation != "shutdown") response.Observation = new PresentationObservation { Confirmed = true,
                    SourceId = request.Scope.SourceId, CurrentSlide = 1, TotalSlides = 1 };
            }
            if (mode == "wrong-scope") response.Scope.GroupEpoch++;
            if (mode == "terminal-noop")
            {
                if (request.Operation != "shutdown") response.Observation = new PresentationObservation { Confirmed = true,
                    SourceId = request.Scope.SourceId, CurrentSlide = 1, TotalSlides = 1 };
                if (request.Operation is "next_effect" or "next_slide")
                {
                    response.Status = "noop";
                    response.ErrorCode = "presentation_at_last_slide";
                }
            }
            response.ResultFingerprint = PresentationFingerprints.Response(response);
            if (mode == "half-late")
            {
                var frame = PresentationWireCodec.EncodeResponse(response);
                await output.WriteAsync(frame.AsMemory(0, 2)).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(root, "half-sent"), string.Empty).ConfigureAwait(false);
                using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!File.Exists(Path.Combine(root, "release"))) await Task.Delay(10, budget.Token).ConfigureAwait(false);
                await output.WriteAsync(frame.AsMemory(2)).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(root, "late-sent"), string.Empty).ConfigureAwait(false);
                continue;
            }
            if (mode == "bad-length")
            {
                await output.WriteAsync(new byte[4]).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                continue;
            }
            if (mode == "truncated")
            {
                await output.WriteAsync(PresentationWireCodec.EncodeResponse(response).AsMemory(0, 6)).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
                return 0;
            }
            await PresentationWireCodec.WriteResponseAsync(output, response).ConfigureAwait(false);
        }
        return mode.EndsWith("-exit-error", StringComparison.Ordinal) ? 2 : 0;
    }

    /// <summary>:param args: 参数。:param name: 固定键。:returns: 必填参数。</summary>
    private static string Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, "--" + name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : throw new ArgumentException("缺少测试参数：" + name);
    }
}
