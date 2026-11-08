
using SmartX.Api.Services.Mesh;

namespace SmartX.Api.Endpoints;

public static class MeshPreviewEndpoints
{
    public static IEndpointRouteBuilder
        MapMeshPreviewEndpoints(
            this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/mesh/preview",
            async (
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                var snapshot =
                    await service.GetSnapshotAsync(ct);

                return Results.Ok(snapshot);
            }
        )
        .WithTags("Mesh Network");

        return app;
    }
}
