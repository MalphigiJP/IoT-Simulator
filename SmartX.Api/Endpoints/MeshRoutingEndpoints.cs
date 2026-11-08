
using SmartX.Api.Services.Mesh;

namespace SmartX.Api.Endpoints;

public static class MeshRoutingEndpoints
{
    public static IEndpointRouteBuilder MapMeshRoutingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mesh")
            .WithTags("Mesh Network");

        group.MapGet(
            "/route/{sensorId:int}",
            async (
                int sensorId,
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                if (sensorId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "Sensor ID must be positive."
                    });
                }

                var snapshot = await service.GetSnapshotAsync(ct);
                var result = MeshRoutingQueries.RouteBySensorId(
                    snapshot,
                    sensorId
                );

                if (result is null)
                {
                    return Results.NotFound(new
                    {
                        error = $"Sensor {sensorId} was not found."
                    });
                }

                return Results.Ok(result);
            }
        )
        .WithName("GetMeshRouteBySensorId");

        group.MapGet(
            "/route/by-mac",
            async (
                string? macAddress,
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(macAddress))
                {
                    return Results.BadRequest(new
                    {
                        error = "macAddress is required."
                    });
                }

                var snapshot = await service.GetSnapshotAsync(ct);
                var result = MeshRoutingQueries.RouteByMac(
                    snapshot,
                    macAddress
                );

                if (result is null)
                {
                    return Results.NotFound(new
                    {
                        error = "No registered sensor has that MAC address."
                    });
                }

                return Results.Ok(result);
            }
        )
        .WithName("GetMeshRouteByMacAddress");

        group.MapGet(
            "/nodes/{sensorId:int}",
            async (
                int sensorId,
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                if (sensorId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "Sensor ID must be positive."
                    });
                }

                var snapshot = await service.GetSnapshotAsync(ct);
                var result = MeshRoutingQueries.NodeBySensorId(
                    snapshot,
                    sensorId
                );

                if (result is null)
                {
                    return Results.NotFound(new
                    {
                        error = $"Sensor {sensorId} was not found."
                    });
                }

                return Results.Ok(result);
            }
        )
        .WithName("GetMeshNodeBySensorId");

        group.MapGet(
            "/nodes/by-mac",
            async (
                string? macAddress,
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(macAddress))
                {
                    return Results.BadRequest(new
                    {
                        error = "macAddress is required."
                    });
                }

                var snapshot = await service.GetSnapshotAsync(ct);
                var result = MeshRoutingQueries.NodeByMac(
                    snapshot,
                    macAddress
                );

                if (result is null)
                {
                    return Results.NotFound(new
                    {
                        error = "No registered sensor has that MAC address."
                    });
                }

                return Results.Ok(result);
            }
        )
        .WithName("GetMeshNodeByMacAddress");

        group.MapGet(
            "/mst",
            async (
                MeshNetworkSnapshotService service,
                CancellationToken ct) =>
            {
                var snapshot = await service.GetSnapshotAsync(ct);

                return Results.Ok(
                    MeshRoutingQueries.MinimumSpanningTree(snapshot)
                );
            }
        )
        .WithName("GetMeshMinimumSpanningTree");

        return app;
    }
}
