
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder
        MapRecommendationEndpoints(
            this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/insights/recommendations",
            async (
                int? days,
                int? windowMinutes,
                int? sensorId,
                RecommendationService service,
                SmartXContext db,
                CancellationToken ct) =>
            {
                int requestedDays = days ?? 7;
                int requestedWindow = windowMinutes ?? 30;

                if (requestedDays is < 1 or > 30)
                {
                    return Results.BadRequest(new
                    {
                        error = "Days must be between 1 and 30."
                    });
                }

                if (requestedWindow is < 1 or > 120)
                {
                    return Results.BadRequest(new
                    {
                        error = "WindowMinutes must be between 1 and 120."
                    });
                }

                if (sensorId is <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "SensorId must be positive."
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
                            error = "Sensor not found."
                        });
                    }
                }

                var options =
                    new HistoricalRelationshipOptions(
                        requestedDays,
                        requestedWindow,
                        sensorId
                    );

                var result = await service.GetRecommendationsAsync(
                    options,
                    ct
                );

                return Results.Ok(result);
            }
        )
        .WithTags("Operator Insights");

        return app;
    }
}
