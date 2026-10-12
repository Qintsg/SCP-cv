// 预设目录REST入口；完整更新明确给出父目录，根目录使用null。
using System.Text.Json.Serialization;
using ScpCv.Infrastructure.Scenarios;

namespace ScpCv.ControlHost.Endpoints;

public static class ScenarioFolderEndpoints
{
    public static IEndpointRouteBuilder MapScenarioFolderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/scenario-folders").RequireSessionAndCsrf();
        api.MapGet("/", async (ScenarioFolderService service, CancellationToken token) =>
            Results.Ok(new { success = true, folders = await service.ListAsync(token).ConfigureAwait(false) }));
        api.MapPost("/", async (FolderRequest request, ScenarioFolderService service, CancellationToken token) =>
        {
            try { return Results.Json(new { success = true, folder = await service.CreateAsync(request.Name, request.ParentId, token).ConfigureAwait(false) }, statusCode: 201); }
            catch (ScenarioServiceException exception) { return Error(exception); }
        });
        api.MapPut("/{id:long}/", async (long id, FolderRequest request, ScenarioFolderService service, CancellationToken token) =>
        {
            try { return Results.Ok(new { success = true, folder = await service.UpdateAsync(id, request.Name, request.ParentId, token).ConfigureAwait(false) }); }
            catch (ScenarioServiceException exception) { return Error(exception); }
        });
        api.MapDelete("/{id:long}/", async (long id, ScenarioFolderService service, CancellationToken token) =>
        {
            try { await service.DeleteAsync(id, token).ConfigureAwait(false); return Results.Ok(new { success = true }); }
            catch (ScenarioServiceException exception) { return Error(exception); }
        });
        return endpoints;
    }
    private static IResult Error(ScenarioServiceException exception) => ApiEndpointSupport.Error(exception.Message,
        exception.Code, exception.IsNotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
    public sealed record FolderRequest
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("parent_id")] public long? ParentId { get; init; }
    }
}
