/*
 * 两框架纯值 wire 与规范指纹回归：仅内存流，不创建任何原生或 GUI 进程。
 * @Project : SCP-cv
 * @File : PresentationProtocolWireTests.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System.Buffers.Binary;
using System.Text;
using ScpCv.Presentation.Protocol;

namespace ScpCv.Contracts.Tests;

public sealed partial class PresentationProtocolTests
{
    /// <summary>
    /// 规范请求经过有界 JSON 往返，中文路径及所有围栏保持不变。
    /// :returns: 固定小端长度、独立纯值和稳定指纹。
    /// </summary>
    [Fact]
    public void FramedRequestRoundTripPreservesChinesePathsAndCanonicalIdentity()
    {
        var request = CreateRequest();
        var frame = PresentationWireCodec.EncodeRequest(request);
        Assert.Equal(frame.Length - 4, BinaryPrimitives.ReadInt32LittleEndian(frame));
        var restored = PresentationWireCodec.DecodeRequest(frame);
        Assert.NotSame(request, restored);
        Assert.NotSame(request.Scope, restored.Scope);
        Assert.Equal(request.OperationId, restored.OperationId);
        Assert.Equal(request.ParentCommandId, restored.ParentCommandId);
        Assert.Equal(request.ClaimToken, restored.ClaimToken);
        Assert.Equal(request.SourcePath, restored.SourcePath);
        Assert.Equal(request.ParentWindow?.Handle, restored.ParentWindow?.Handle);
        Assert.True(PresentationRequestPolicy.SameScope(request.Scope, restored.Scope));
        Assert.True(restored.ReadOnly);
        Assert.False(restored.AllowMacros);
        Assert.False(restored.UpdateExternalLinks);
        Assert.Equal(PresentationFingerprints.Request(request), PresentationFingerprints.Request(restored));
    }

    /// <summary>
    /// 结果经过单帧往返仍由相同请求和最新授权 scope 匹配。
    /// :returns: 不因序列化获得额外画面更新权限。
    /// </summary>
    [Fact]
    public void FramedResultRoundTripStillRequiresLatestScopeAndFingerprintMatch()
    {
        var request = CreateRequest();
        var response = ResponseFor(request);
        var restored = PresentationWireCodec.DecodeResponse(PresentationWireCodec.EncodeResponse(response));
        Assert.Equal(response.ResultFingerprint, restored.ResultFingerprint);
        Assert.True(PresentationResultPolicy.Match(request, restored, CloneScope(request.Scope), Now).CanApplyObservation);
        var next = CloneScope(request.Scope);
        next.SourceGeneration++;
        Assert.False(PresentationResultPolicy.Match(request, restored, next, Now).CanApplyObservation);
    }

    /// <summary>
    /// 流短读不能截断合法帧；多帧逐个读取，完整 EOF 才返回 null。
    /// :returns: 有序完成两帧，不关闭调用方流。
    /// </summary>
    [Fact]
    public async Task ShortReadsAndConsecutiveFramesAreHandledWithoutOwningTheStream()
    {
        var first = CreateRequest();
        var second = CreateRequest("next_effect");
        second.OperationId = Guid.NewGuid();
        using var output = new MemoryStream();
        await PresentationWireCodec.WriteRequestAsync(output, first);
        await PresentationWireCodec.WriteRequestAsync(output, second);
        Assert.True(output.CanWrite);
        using var input = new FragmentedReadStream(output.ToArray());
        var restoredFirst = await PresentationWireCodec.ReadRequestAsync(input);
        var restoredSecond = await PresentationWireCodec.ReadRequestAsync(input);
        Assert.Equal(first.OperationId, restoredFirst?.OperationId);
        Assert.Equal(second.OperationId, restoredSecond?.OperationId);
        Assert.Null(await PresentationWireCodec.ReadRequestAsync(input));
        Assert.True(input.CanRead);
    }

    /// <summary>
    /// 非法长度必须在分配/读取 payload 前拒绝。
    /// :param length: 声明的非法长度。
    /// :returns: 最多消费四字节，拒绝零/负数/超上限。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65537)]
    [InlineData(int.MaxValue)]
    public async Task InvalidFrameLengthIsRejectedBeforeReadingPayload(int length)
    {
        var frame = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(frame, length);
        using var input = new MemoryStream(frame);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => PresentationWireCodec.ReadRequestAsync(input));
        Assert.Equal("frame_length_invalid", exception.Message);
        Assert.Equal(4, input.Position);
    }

    /// <summary>
    /// 半帧 EOF 不得作为正常无工作返回。
    /// :param bytesToKeep: 从合法帧保留的字节数。
    /// :returns: Header 或 payload 截断明确失败。
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public async Task TruncatedFrameIsNotTreatedAsCleanEof(int bytesToKeep)
    {
        var complete = PresentationWireCodec.EncodeRequest(CreateRequest());
        using var input = new MemoryStream(complete[..bytesToKeep]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => PresentationWireCodec.ReadRequestAsync(input));
    }

    /// <summary>
    /// 调用方取消预算必须传播，不能变成合法 EOF 或空成功。
    /// :returns: 取消令牌的真实取消。
    /// </summary>
    [Fact]
    public async Task CallerCancellationDoesNotBecomeCleanEof()
    {
        using var input = new MemoryStream();
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            PresentationWireCodec.ReadRequestAsync(input, stop.Token));
    }

    /// <summary>
    /// 单帧解码不能默许额外 bytes 或拼接第二个 JSON 根。
    /// :returns: 无尾部数据的单帧语义。
    /// </summary>
    [Fact]
    public void SingleFrameRejectsTrailingBytesAndTrailingJsonRoot()
    {
        var frame = PresentationWireCodec.EncodeRequest(CreateRequest());
        Assert.Throws<InvalidDataException>(() => PresentationWireCodec.DecodeRequest([.. frame, 0]));
        var body = Encoding.UTF8.GetString(frame, 4, frame.Length - 4);
        Assert.Throws<InvalidDataException>(() => PresentationWireCodec.DecodeRequest(Frame(Encoding.UTF8.GetBytes(body + "{}"))));
    }

    /// <summary>
    /// outer-root 定位不能把合法 string 中的括号或 escaped quote 当成 JSON 终点。
    /// :returns: 官方 JSON decoder 保持合法字符串原值。
    /// </summary>
    [Fact]
    public void RootBoundaryScannerPreservesBracketsAndEscapedQuotesInsideStrings()
    {
        var request = CreateRequest();
        var response = ResponseFor(request, "failed");
        response.ErrorCode = "diagnostic_only";
        response.ErrorDetail = "literal {\"child\":[\"quoted\\\\value\"]} and } [ escaped \\\" quote";
        response.ResultFingerprint = PresentationFingerprints.Response(response);
        var restored = PresentationWireCodec.DecodeResponse(PresentationWireCodec.EncodeResponse(response));
        Assert.Equal(response.ErrorDetail, restored.ErrorDetail);
        Assert.True(PresentationResultPolicy.Match(request, restored, CloneScope(request.Scope), Now).Matched);
        Assert.False(PresentationResultPolicy.Match(request, restored, CloneScope(request.Scope), Now).CanApplyObservation);
    }

    /// <summary>
    /// 捕获可变输入时也必须在序列化期间限流，不能先扩容再拒绝超大 payload。
    /// :returns: 超大请求在 executor 之前拒绝。
    /// </summary>
    [Fact]
    public void OversizedMutableInputCaptureIsBoundedBeforeNativeExecution()
    {
        var request = CreateRequest();
        request.SourcePath = @"C:\media\" + new string('x', 100_000) + ".pptx";
        var nativeCalls = 0;
        Assert.Throws<InvalidDataException>(() => PresentationExecutionGate.Execute(request,
            new PresentationCapabilities { CanOpen = true }, CloneScope(request.Scope), Now,
            _ => { nativeCalls++; return new PresentationResponse(); }));
        Assert.Equal(0, nativeCalls);
    }

    /// <summary>
    /// 损坏 UTF-8、深嵌套未知内容及缺失 mandatory 字段必须拒绝。
    /// :returns: 固定 serializer 配额，不调用自制通用 JSON parser。
    /// </summary>
    [Fact]
    public void InvalidUtf8DeepJsonAndMissingRequiredMembersAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => PresentationWireCodec.DecodeRequest(Frame([0xc3, 0x28])));
        var valid = PresentationWireCodec.EncodeRequest(CreateRequest());
        var validBody = Encoding.UTF8.GetString(valid, 4, valid.Length - 4);
        var nested = validBody[..^1] + ",\"unexpected\":" + string.Concat(Enumerable.Repeat("{\"child\":", 30)) + "0" + new string('}', 30) + "}";
        Assert.Throws<InvalidDataException>(() => PresentationWireCodec.DecodeRequest(Frame(Encoding.UTF8.GetBytes(nested))));
        Assert.Throws<InvalidDataException>(() => PresentationWireCodec.DecodeRequest(Frame("{}"u8.ToArray())));
    }

    /// <summary>
    /// 规范指纹覆盖全部副作用输入和身份，不受显式时区等价表示或摘要大小写影响。
    /// :returns: 参数变更有冲突，真正相同语义表示一致。
    /// </summary>
    [Fact]
    public void CanonicalRequestHashIsStableForEquivalentTimeAndSensitiveToChangedInputs()
    {
        var request = CreateRequest();
        var original = PresentationFingerprints.Request(request);
        request.Deadline = Now.AddMinutes(1).ToOffset(TimeSpan.FromHours(8)).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        request.Scope.SourceDigest = request.Scope.SourceDigest.ToUpperInvariant();
        Assert.Equal(original, PresentationFingerprints.Request(request));
        request.SourcePath = @"C:\media\PPT文件\不同原件.pptx";
        Assert.NotEqual(original, PresentationFingerprints.Request(request));
        request = CreateRequest();
        request.ClaimToken = Guid.NewGuid();
        Assert.NotEqual(original, PresentationFingerprints.Request(request));
        request = CreateRequest();
        var parent = request.ParentWindow ?? throw new InvalidOperationException("夹具缺少父窗口");
        parent.ProcessId++;
        Assert.NotEqual(original, PresentationFingerprints.Request(request));
    }

    /// <summary>
    /// 规范结果指纹包含实际观测，不能用同一操作身份掩盖不同页码结果。
    /// :returns: 结果观测变化产生不同指纹。
    /// </summary>
    [Fact]
    public void CanonicalResultHashIncludesActualObservation()
    {
        var response = ResponseFor(CreateRequest());
        var original = response.ResultFingerprint;
        var observation = response.Observation ?? throw new InvalidOperationException("夹具缺少观测");
        observation.CurrentSlide++;
        Assert.NotEqual(original, PresentationFingerprints.Response(response));
    }

    /// <summary>
    /// 独立于生产 codec 手工添加固定小端 header，供恶意输入回归。
    /// :param body: 原始 payload。
    /// :returns: 单帧恶意/合法测试数据。
    /// </summary>
    private static byte[] Frame(byte[] body)
    {
        var frame = new byte[body.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
        body.CopyTo(frame, 4);
        return frame;
    }

    /// <summary>每次最多读两个字节的外部流 seam，不包含原生进程或 GUI。</summary>
    private sealed class FragmentedReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        /// <summary>
        /// 模拟合法的异步短读。
        /// :param buffer: 目标内存。
        /// :param cancellationToken: 调用方取消。
        /// :returns: 本次真实读入字节数。
        /// </summary>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Read(buffer.Span[..Math.Min(2, buffer.Length)]));
        }
    }
}
