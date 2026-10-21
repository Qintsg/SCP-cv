// 受限现场输入目录与显式服务探测；不是实时出画状态，不接受客户端任意地址。
using Microsoft.EntityFrameworkCore;
using ScpCv.Infrastructure.Devices;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Endpoints;

public static class InputCatalogEndpoints
{
    public sealed record RefreshRequest(bool Force = false);

    public static IEndpointRouteBuilder MapInputCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/inputs").RequireSessionAndCsrf();
        api.MapGet("/", ListAsync);
        api.MapPost("/refresh/", RefreshAsync);
        return endpoints;
    }

    /// <summary>:returns: 只读缓存，GET不触发网络或改变业务数据库。</summary>
    private static async Task<IResult> ListAsync(InputDiscovery discovery, IDbContextFactory<ControlDbContext> contexts, CancellationToken token)
    {
        await using var db = await contexts.CreateDbContextAsync(token).ConfigureAwait(false);
        var ids = await db.MediaSources.AsNoTracking().Where(item => item.SystemKey != string.Empty)
            .ToDictionaryAsync(item => item.SystemKey, item => item.Id, token).ConfigureAwait(false);
        var snapshot = discovery.Snapshot;
        var inputs = SiteInputCatalog.All.Select(item =>
        {
            var reached = snapshot.GetValueOrDefault(item.Id, InputReachability.Unknown);
            return new
            {
                id = item.Id, name = item.Name, group = item.Group, ip_suffix = item.IpSuffix,
                preview_input = new { kind = item.PreviewKind, ip_address = item.PreviewKind == "ip_stream" ? item.Address : string.Empty },
                source_id = ids.TryGetValue(item.SystemKey, out var id) ? (long?)id : null,
                availability = reached.Status, is_available = reached.Available, checked_at = reached.CheckedAt,
                aliases = item.IpSuffix is >= 63 and <= 66 ? new[] { $"窗口 {item.IpSuffix - 62}" } : Array.Empty<string>(),
            };
        });
        return Results.Ok(new { success = true, inputs });
    }

    /// <summary>:param request: 仅显式刷新标志。:returns: 合并探测并同步系统工作站，不能修改用户目录。</summary>
    private static async Task<IResult> RefreshAsync(RefreshRequest request, InputDiscovery discovery, WorkstationCatalog catalog,
        IDbContextFactory<ControlDbContext> contexts, CancellationToken token)
    {
        try
        {
            var snapshot = await discovery.RefreshAsync(request.Force, token).ConfigureAwait(false);
            await catalog.SynchronizeAsync(snapshot, token).ConfigureAwait(false);
            return await ListAsync(discovery, contexts, token).ConfigureAwait(false);
        }
        catch (VenueDeviceException exception) { return ApiEndpointSupport.Error(exception.Message, "input_catalog_unavailable", StatusCodes.Status409Conflict); }
    }
}
