
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class HistoricalRelationshipEndpoints
{
    public static IEndpointRouteBuilder
        MapHistoricalRelationshipEndpoints(
            this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/insights/relationships",
            async (
                int? days,
                int? windowMinutes,
                int? sensorId,
                SmartXContext db,
                HistoricalRelationshipService service,
                CancellationToken ct) =>
            {
                int requestedDays = days ?? 7;

                int requestedWindow =
                    windowMinutes ?? 30;

                if (
                    requestedDays < 1 ||
                    requestedDays > 30
                )
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Days must be between 1 and 30."
                    });
                }

                if (
                    requestedWindow < 1 ||
                    requestedWindow > 120
                )
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "WindowMinutes must be between 1 and 120."
                    });
                }

                if (
                    sensorId.HasValue &&
                    sensorId.Value <= 0
                )
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "SensorId must be positive."
                    });
                }

                if (sensorId.HasValue)
                {
                    bool exists = await db.Sensors
                        .AsNoTracking()
                        .AnyAsync(
                            s => s.Id == sensorId.Value,
                            ct
                        );

                    if (!exists)
                    {
                        return Results.NotFound(new
                        {
                            error =
                                "Sensor not found."
                        });
                    }
                }

                var options =
                    new HistoricalRelationshipOptions(
                        requestedDays,
                        requestedWindow,
                        sensorId
                    );

                var report = await service.AnalyzeAsync(
                    options,
                    ct
                );

                return Results.Ok(report);
            }
        )
        .WithTags("Operator Insights");

        return app;
    }
}
