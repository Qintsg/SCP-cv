// 共享别名JSON有界读取：未知长度/分块也不允许先分配任意大数组。
using System.Text;
using Microsoft.AspNetCore.Http;
using ScpCv.ControlHost.StreamPreview;

namespace ScpCv.ControlHost.Tests;

public sealed class StreamPreviewHeartbeatBodyTests
{
    [Fact]
    public async Task NoBodyKeepsOriginalHeartbeatContractAsync()
    {
        var context = new DefaultHttpContext();
        Assert.Null(await PreviewHeartbeatBody.ReadAsync(context.Request, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownAndUnknownLengthOversizeBodiesAreRejectedBeforeJsonAllocationAsync(bool chunked)
    {
        var context = new DefaultHttpContext(); var content = Encoding.UTF8.GetBytes(new string(' ', 8192));
        context.Request.Body = new MemoryStream(content);
        if (chunked) context.Request.Headers.TransferEncoding = "chunked"; else context.Request.ContentLength = content.Length;
        var failure = await Assert.ThrowsAsync<PreviewException>(() => PreviewHeartbeatBody.ReadAsync(context.Request, CancellationToken.None));
        Assert.Equal(400, failure.StatusCode); Assert.Equal("invalid_preview_aliases", failure.Code);
        Assert.True(context.Request.Body.Position <= 4097);
    }

    [Fact]
    public async Task AcceptsBoundedAliasesAndRejectsUnknownPropertiesAsync()
    {
        var id = Guid.NewGuid(); var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes($"{{\"aliases\":[\"{id}\"]}}");
        context.Request.Body = new MemoryStream(bytes); context.Request.ContentLength = bytes.Length;
        Assert.Equal(id, Assert.Single((await PreviewHeartbeatBody.ReadAsync(context.Request, CancellationToken.None))!.Aliases));
        var invalid = Encoding.UTF8.GetBytes("{\"aliases\":[],\"url\":\"rtmp://not-accepted\"}");
        context.Request.Body = new MemoryStream(invalid); context.Request.ContentLength = invalid.Length;
        Assert.Equal(400, (await Assert.ThrowsAsync<PreviewException>(() => PreviewHeartbeatBody.ReadAsync(context.Request, CancellationToken.None))).StatusCode);
    }
}
