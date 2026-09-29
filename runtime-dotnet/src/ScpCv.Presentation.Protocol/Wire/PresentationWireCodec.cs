/*
 * 两框架共用的有界纯 JSON framing：4 字节小端长度、64 KiB payload 与固定类型。
 * @Project : SCP-cv
 * @File : PresentationWireCodec.cs
 * @Author : Qintsg
 * @Date : 2026-09-29
 */
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ScpCv.Presentation.Protocol;

public static class PresentationWireCodec
{
    public const int MaximumPayloadBytes = 64 * 1024;
    private const int HeaderBytes = 4;

    /// <summary>
    /// 将结构合法请求编码为单帧；编码不授权执行，也不检查当前 OS 归属。
    /// :param request: 完整纯值请求。
    /// :returns: 4 字节小端长度及 UTF-8 JSON。
    /// :raises InvalidDataException: 结构或长度不合法。
    /// </summary>
    public static byte[] EncodeRequest(PresentationRequest request)
    {
        Require(PresentationRequestPolicy.ValidateStructure(request));
        return Encode(request);
    }

    /// <summary>
    /// 解码恰好一帧请求，拒绝截断、多余字节、深 JSON 与无效版本。
    /// :param frame: 完整单帧字节。
    /// :returns: 独立纯值请求。
    /// :raises InvalidDataException: 数据不合法。
    /// </summary>
    public static PresentationRequest DecodeRequest(byte[] frame)
    {
        var request = Decode<PresentationRequest>(Payload(frame));
        Require(PresentationRequestPolicy.ValidateStructure(request));
        return request;
    }

    /// <summary>
    /// 编码带完整结果指纹的纯值返回，不因此认定画面已经完成。
    /// :param response: 返回值。
    /// :returns: 完整单帧。
    /// :raises InvalidDataException: 结果结构/长度不合法。
    /// </summary>
    public static byte[] EncodeResponse(PresentationResponse response)
    {
        Require(PresentationResultPolicy.ValidateEnvelope(response, requireFingerprint: true));
        return Encode(response);
    }

    /// <summary>
    /// 解码单帧返回；接收方仍须使用结果 matcher 与当前 scope 核对。
    /// :param frame: 单帧字节。
    /// :returns: 独立纯值返回。
    /// :raises InvalidDataException: 数据不合法。
    /// </summary>
    public static PresentationResponse DecodeResponse(byte[] frame)
    {
        var response = Decode<PresentationResponse>(Payload(frame));
        Require(PresentationResultPolicy.ValidateEnvelope(response, requireFingerprint: true));
        return response;
    }

    /// <summary>
    /// 读取一帧请求；完全 EOF 返回 null，部分 header/payload EOF 明确失败。
    /// :param stream: 调用方拥有的流。
    /// :param cancellationToken: 调用方施加有限预算；codec 不假定期限内必定完成。
    /// :returns: 请求或流已结束的 null。
    /// :raises EndOfStreamException: 帧截断。
    /// :raises InvalidDataException: 非法长度或 JSON。
    /// </summary>
    public static async Task<PresentationRequest?> ReadRequestAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(stream, cancellationToken).ConfigureAwait(false);
        if (payload == null) return null;
        var request = Decode<PresentationRequest>(payload);
        Require(PresentationRequestPolicy.ValidateStructure(request));
        return request;
    }

    /// <summary>
    /// 读取一帧结果，完整 EOF 和半帧截断分开。
    /// :param stream: 调用方拥有的流。
    /// :param cancellationToken: 读取预算。
    /// :returns: 结果或 null。
    /// :raises EndOfStreamException: 截断。
    /// :raises InvalidDataException: 数据无效。
    /// </summary>
    public static async Task<PresentationResponse?> ReadResponseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var payload = await ReadPayloadAsync(stream, cancellationToken).ConfigureAwait(false);
        if (payload == null) return null;
        var response = Decode<PresentationResponse>(payload);
        Require(PresentationResultPolicy.ValidateEnvelope(response, requireFingerprint: true));
        return response;
    }

    /// <summary>
    /// 写入并 flush 一帧请求，不关闭调用方流。
    /// :param stream: 调用方拥有的流。
    /// :param request: 请求。
    /// :param cancellationToken: 写入预算。
    /// :returns: 写入完成任务，不是原生动作完成证据。
    /// </summary>
    public static Task WriteRequestAsync(Stream stream, PresentationRequest request, CancellationToken cancellationToken = default) =>
        WriteAsync(stream, EncodeRequest(request), cancellationToken);

    /// <summary>
    /// 写入并 flush 一帧返回，不关闭调用方流。
    /// :param stream: 调用方流。
    /// :param response: 返回值。
    /// :param cancellationToken: 写入预算。
    /// :returns: 写入完成任务。
    /// </summary>
    public static Task WriteResponseAsync(Stream stream, PresentationResponse response, CancellationToken cancellationToken = default) =>
        WriteAsync(stream, EncodeResponse(response), cancellationToken);

    /// <summary>
    /// 固定 DataContract 类型序列化，不接受任意运行时类型或原生对象。
    /// :param value: 已校验固定类型。
    /// :returns: 有界帧。
    /// </summary>
    private static byte[] Encode<T>(T value) where T : class
    {
        using var stream = new BoundedPayloadStream();
        Serializer<T>().WriteObject(stream, value);
        var payload = stream.ToArray();
        if (payload.Length == 0 || payload.Length > MaximumPayloadBytes) throw new InvalidDataException("frame_length_invalid");
        var frame = new byte[HeaderBytes + payload.Length];
        var length = payload.Length;
        for (var index = 0; index < HeaderBytes; index++) frame[index] = (byte)(length >> (index * 8));
        Buffer.BlockCopy(payload, 0, frame, HeaderBytes, payload.Length);
        return frame;
    }

    /// <summary>
    /// 用严格 UTF-8、深度和 object graph 配额读取固定 DTO；未知字段不成为执行参数。
    /// :param payload: 已按 frame 长度限制的 JSON。
    /// :returns: 固定类型值。
    /// :raises InvalidDataException: JSON、配额或尾部内容无效。
    /// </summary>
    private static T Decode<T>(byte[] payload) where T : class
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(payload);
            RequireSingleJsonObject(payload);
            var quotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 16, MaxArrayLength = 256, MaxStringContentLength = MaximumPayloadBytes,
                MaxBytesPerRead = MaximumPayloadBytes, MaxNameTableCharCount = MaximumPayloadBytes,
            };
            using var reader = JsonReaderWriterFactory.CreateJsonReader(payload, quotas);
            var result = Serializer<T>().ReadObject(reader) as T ?? throw new InvalidDataException("invalid_json_type");
            if (reader.MoveToContent() != XmlNodeType.None) throw new InvalidDataException("trailing_json_content");
            return result;
        }
        catch (Exception exception) when (exception is SerializationException || exception is XmlException ||
                                          exception is DecoderFallbackException || exception is FormatException)
        {
            throw new InvalidDataException("invalid_json_payload", exception);
        }
    }

    /// <summary>
    /// 创建两个框架共有的有界 DataContractJsonSerializer。
    /// :returns: 固定 DTO serializer，不使用第三方 JSON 包。
    /// </summary>
    private static DataContractJsonSerializer Serializer<T>() where T : class =>
        new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 256 });

    /// <summary>
    /// 在授权前捕获有限纯值快照；结构与能力校验针对返回值，不针对可变原件。
    /// :param request: 调用方可变 DTO，不称为 immutable。
    /// :returns: 已通过结构解码的独立请求。
    /// </summary>
    internal static PresentationRequest CaptureRequest(PresentationRequest request)
    {
        var required = request ?? throw new ArgumentNullException(nameof(request));
        return DecodeRequest(Encode(required));
    }

    /// <summary>
    /// 仅寻找 outer object 的确切字节终点，处理 string/escape，不解析字段或 JSON 值。
    /// DataContract reader 仍负责完整语法、深度和 object graph 配额。
    /// :param payload: 已限制大小和验证 UTF-8 的 JSON bytes。
    /// :returns: 无。
    /// :raises InvalidDataException: 根不是 object、截断或根后有非空白字节。
    /// </summary>
    private static void RequireSingleJsonObject(byte[] payload)
    {
        var start = 0;
        while (start < payload.Length && JsonWhitespace(payload[start])) start++;
        if (start == payload.Length || payload[start] != '{') throw new InvalidDataException("invalid_json_root");
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = start; index < payload.Length; index++)
        {
            var character = payload[index];
            if (inString)
            {
                if (escaped) { escaped = false; continue; }
                if (character == '\\') { escaped = true; continue; }
                if (character == '"') inString = false;
                continue;
            }
            if (character == '"') { inString = true; continue; }
            if (character == '{' || character == '[') depth++;
            else if (character == '}' || character == ']')
            {
                depth--;
                if (depth == 0)
                {
                    if (character != '}') throw new InvalidDataException("invalid_json_root");
                    for (var tail = index + 1; tail < payload.Length; tail++)
                        if (!JsonWhitespace(payload[tail])) throw new InvalidDataException("trailing_json_content");
                    return;
                }
            }
        }
        throw new InvalidDataException("json_root_truncated");
    }

    /// <summary>
    /// 仅识别 JSON 允许的四种空白，不把任意 Unicode 控制字节当成根后空白。
    /// :param value: 原始字节。
    /// :returns: 是否为 JSON 空白。
    /// </summary>
    private static bool JsonWhitespace(byte value) => value == ' ' || value == '\t' || value == '\r' || value == '\n';

    /// <summary>
    /// 先验 header 再分配 payload，并拒绝额外 frame 字节。
    /// :param frame: 单帧字节。
    /// :returns: 有限 JSON 字节。
    /// </summary>
    private static byte[] Payload(byte[] frame)
    {
        var required = frame ?? throw new ArgumentNullException(nameof(frame));
        if (required.Length < HeaderBytes) throw new InvalidDataException("frame_header_truncated");
        var length = Length(required);
        if (required.Length != HeaderBytes + length) throw new InvalidDataException("frame_payload_length_mismatch");
        var payload = new byte[length];
        Buffer.BlockCopy(required, HeaderBytes, payload, 0, length);
        return payload;
    }

    /// <summary>
    /// 解码小端长度，拒绝负数、零及上限之外长度。
    /// :param header: 至少 4 字节 header。
    /// :returns: 有限 payload 长度。
    /// </summary>
    private static int Length(byte[] header)
    {
        var length = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
        if (length <= 0 || length > MaximumPayloadBytes) throw new InvalidDataException("frame_length_invalid");
        return length;
    }

    /// <summary>
    /// 短读可恢复，部分 EOF 不可当成无工作；非法 header 不读取 payload。
    /// :param stream: 调用方流。
    /// :param token: 读取预算。
    /// :returns: payload 或完整 EOF。
    /// </summary>
    private static async Task<byte[]?> ReadPayloadAsync(Stream stream, CancellationToken token)
    {
        var required = stream ?? throw new ArgumentNullException(nameof(stream));
        var header = new byte[HeaderBytes];
        if (!await ReadExactlyAsync(required, header, allowEmpty: true, token).ConfigureAwait(false)) return null;
        var payload = new byte[Length(header)];
        await ReadExactlyAsync(required, payload, allowEmpty: false, token).ConfigureAwait(false);
        return payload;
    }

    /// <summary>
    /// 在固定数组内补足短读；取消由调用方预算决定。
    /// :param stream: 输入流。
    /// :param bytes: 固定大小数组。
    /// :param allowEmpty: 是否允许完整 EOF。
    /// :param token: 读取预算。
    /// :returns: 是否取得完整数据。
    /// </summary>
    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] bytes, bool allowEmpty, CancellationToken token)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            token.ThrowIfCancellationRequested();
#if NETFRAMEWORK
            var count = await stream.ReadAsync(bytes, offset, bytes.Length - offset, token).ConfigureAwait(false);
#else
            var count = await stream.ReadAsync(bytes.AsMemory(offset, bytes.Length - offset), token).ConfigureAwait(false);
#endif
            if (count == 0)
            {
                if (offset == 0 && allowEmpty) return false;
                throw new EndOfStreamException("presentation_frame_truncated");
            }
            offset += count;
        }
        return true;
    }

    /// <summary>
    /// 将一帧写入外部流，不取得流的所有权。
    /// :param stream: 外部流。
    /// :param frame: 已验证帧。
    /// :param token: 写入预算。
    /// :returns: 写入任务。
    /// </summary>
    private static async Task WriteAsync(Stream stream, byte[] frame, CancellationToken token)
    {
        var required = stream ?? throw new ArgumentNullException(nameof(stream));
#if NETFRAMEWORK
        await required.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
#else
        await required.WriteAsync(frame.AsMemory(), token).ConfigureAwait(false);
#endif
        await required.FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// 结构失败在任何外部操作前抛可诊断错误。
    /// :param decision: 纯校验结果。
    /// :returns: 无。
    /// </summary>
    private static void Require(PresentationDecision decision)
    {
        if (!decision.Allowed) throw new InvalidDataException(decision.Code);
    }

    /// <summary>序列化期间即限制大小，避免可变 DTO 或未验证捕获先产生超大缓冲。</summary>
    private sealed class BoundedPayloadStream : MemoryStream
    {
        /// <summary>
        /// 有界写入 legacy 字节数组。
        /// :param buffer: 输入数组。
        /// :param offset: 数组起点。
        /// :param count: 字节数。
        /// :returns: 无。
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Guard(count);
            base.Write(buffer, offset, count);
        }

        /// <summary>
        /// 限制单字节写入。
        /// :param value: 输入字节。
        /// :returns: 无。
        /// </summary>
        public override void WriteByte(byte value)
        {
            Guard(1);
            base.WriteByte(value);
        }

#if !NETFRAMEWORK
        /// <summary>
        /// 限制现代框架的 Span 写入路径，不能绕过 legacy override。
        /// :param buffer: 输入字节。
        /// :returns: 无。
        /// </summary>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Guard(buffer.Length);
            base.Write(buffer);
        }
#endif

        /// <summary>
        /// 在扩容前拒绝超过64 KiB，当前位置写入也须遵守上限。
        /// :param count: 待写字节数。
        /// :returns: 无。
        /// </summary>
        private void Guard(int count)
        {
            if (count < 0 || Position > MaximumPayloadBytes - count) throw new InvalidDataException("frame_length_invalid");
        }
    }
}
