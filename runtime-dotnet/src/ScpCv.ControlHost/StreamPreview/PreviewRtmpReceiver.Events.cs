// 文件描述：自有发布身份、成熟媒体回调及完整元数据；项目：SCP-cv；文件：PreviewRtmpReceiver.Events.cs；作者：Qintsg；日期：2026-10-21。
using System.Net;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Networking.Server.Contracts;
using LiveStreamingServerNet.Rtmp;
using LiveStreamingServerNet.Rtmp.Server.Auth;
using LiveStreamingServerNet.Rtmp.Server.Auth.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Contracts;
using LiveStreamingServerNet.Utilities.Buffers.Contracts;
using LiveStreamingServerNet.Utilities.Contracts;
using mtanksl.ActionMessageFormat;

namespace ScpCv.ControlHost.StreamPreview;

public sealed partial class PreviewRtmpReceiver : IRtmpMediaMessageInterceptor, IAuthorizationHandler, IServerEventHandler, IRtmpServerStreamEventHandler
{
    /// <summary>:param client: 实际会话。:param streamPath: 随机路径。:param streamArguments: 发布参数。:param publishingType: 类型。:returns: 仅首个有效回环发布身份可以进入本轮。</summary>
    public Task<AuthorizationResult> AuthorizePublishingAsync(ISessionInfo client, string streamPath,
        IReadOnlyDictionary<string, string> streamArguments, string publishingType)
    {
        lock (_sync)
        {
            if (_lifetime.IsCancellationRequested || streamPath != _path || streamArguments.Count != 0 || publishingType != "live" ||
                client.RemoteEndPoint is not IPEndPoint address || !IPAddress.IsLoopback(address.Address) || _publisher is not null)
                return Task.FromResult(AuthorizationResult.Unauthorized("预览发布身份无效"));
            _publisher = client.Id;
            return Task.FromResult(AuthorizationResult.Authorized());
        }
    }

    /// <summary>:param client: 订阅身份。:param streamPath: 路径。:param streamArguments: 参数。:returns: 该内部入口拒绝全部网络订阅。</summary>
    public Task<AuthorizationResult> AuthorizeSubscribingAsync(ISessionInfo client, string streamPath, IReadOnlyDictionary<string, string> streamArguments) =>
        Task.FromResult(AuthorizationResult.Unauthorized("内部预览入口不提供订阅"));

    /// <summary>:param clientId: 库认证身份。:param streamPath: 自有路径。:param mediaType: 种类。:param rentedBuffer: 本次有效载荷。:param timestamp: 库解析毫秒。:param isSkippable: 库分类。:returns: 在当前身份及尺寸通过后复制为有界媒体对象。</summary>
    public ValueTask OnReceiveMediaMessageAsync(uint clientId, string streamPath, MediaType mediaType,
        IRentedBuffer rentedBuffer, uint timestamp, bool isSkippable)
    {
        if (clientId != _publisher || streamPath != _path || mediaType != MediaType.Video)
            throw new InvalidDataException("预览媒体身份或种类无效");
        var tag = new FlvTag(9, timestamp, rentedBuffer.AsSpan());
        return _media.Writer.WriteAsync(tag, _lifetime.Token);
    }

    /// <summary>:param context: 库事件。:param clientId: 已认证发布身份。:param streamPath: 路径。:param metaData: 库解析的字段。:returns: 使用成熟AMF组件封装有限标量元数据。</summary>
    public ValueTask OnRtmpStreamMetaDataReceivedAsync(IEventContext context, uint clientId, string streamPath, IReadOnlyDictionary<string, object> metaData)
    {
        if (clientId != _publisher || streamPath != _path || metaData.Count > 64 ||
            metaData.Any(item => item.Key.Length > 128 || item.Value is string text && text.Length > 1024 ||
                item.Value is not (null or string or double or float or int or long or uint or ulong or short or ushort or byte or bool)))
            throw new InvalidDataException("预览元数据超出支持范围");
        var amf = new AmfWriter(); amf.WriteAmf0("onMetaData");
        amf.WriteAmf0(new Amf0Object { ClassName = string.Empty,
            DynamicMembersAndValues = metaData.ToDictionary(item => item.Key, item => (object?)item.Value) });
        return _media.Writer.WriteAsync(new FlvTag(18, 0, amf.Data), _lifetime.Token);
    }

    /// <summary>:param context: 库事件。:param tcpListener: 自有监听。:returns: 保留实际对象供绑定后读取。</summary>
    public Task OnListenerCreatedAsync(IEventContext context, ITcpListener tcpListener) { _listener = tcpListener; return Task.CompletedTask; }
    /// <summary>:param context: 库事件。:returns: 通知已绑定端口。</summary>
    public Task OnServerStartedAsync(IEventContext context) { _ready.TrySetResult(((IPEndPoint)_listener!.LocalEndpoint).Port); return Task.CompletedTask; }
    /// <summary>:param context: 库事件。:param tcpClient: 实际连接。:returns: 连接随后受限额与发布授权检查。</summary>
    public Task OnClientAcceptedAsync(IEventContext context, ITcpClient tcpClient) => Task.CompletedTask;
    /// <summary>:param context: 库事件。:param client: 实际会话。:returns: 最多两个临时握手，超限立即关闭精确会话。</summary>
    public Task OnClientConnectedAsync(IEventContext context, ISessionHandle client)
    { if (Interlocked.Increment(ref _connections) > 2) client.Disconnect(); return Task.CompletedTask; }
    /// <summary>:param context: 库事件。:param client: 关闭会话。:returns: 发布身份退出使本轮明确结束。</summary>
    public Task OnClientDisconnectedAsync(IEventContext context, ISessionInfo client)
    { Interlocked.Decrement(ref _connections); if (client.Id == _publisher) EncoderExited(); return Task.CompletedTask; }
    /// <summary>:param context: 库事件。:returns: 监听结束关闭有界媒体队列。</summary>
    public Task OnServerStoppedAsync(IEventContext context) { _media.Writer.TryComplete(); return Task.CompletedTask; }
    /// <summary>:param context: 库事件。:param clientId: 发布身份。:param streamPath: 路径。:param streamArguments: 参数。:returns: 媒体仅经拦截器进入。</summary>
    public ValueTask OnRtmpStreamPublishedAsync(IEventContext context, uint clientId, string streamPath, IReadOnlyDictionary<string, string> streamArguments) => ValueTask.CompletedTask;
    /// <summary>:param context: 库事件。:param clientId: 原发布身份。:param streamPath: 原路径。:returns: 本轮发布结束，身份不重新授予。</summary>
    public ValueTask OnRtmpStreamUnpublishedAsync(IEventContext context, uint clientId, string streamPath)
    { if (clientId == _publisher) EncoderExited(); return ValueTask.CompletedTask; }
    /// <summary>:param context: 库事件。:param clientId: 订阅者。:param streamPath: 路径。:param streamArguments: 参数。:returns: 订阅已经在授权阶段拒绝。</summary>
    public ValueTask OnRtmpStreamSubscribedAsync(IEventContext context, uint clientId, string streamPath, IReadOnlyDictionary<string, string> streamArguments) => ValueTask.CompletedTask;
    /// <summary>:param context: 库事件。:param clientId: 订阅者。:param streamPath: 路径。:returns: 不创建观看需求。</summary>
    public ValueTask OnRtmpStreamUnsubscribedAsync(IEventContext context, uint clientId, string streamPath) => ValueTask.CompletedTask;
}
