
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class OperatorInteractionEndpoints
{
    public static IEndpointRouteBuilder
        MapOperatorInteractionEndpoints(
            this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/insights")
            .WithTags("Operator Insights");

        group.MapPost(
            "/searches",
            async (
                RecordOperatorSearchDto request,
                SmartXContext db,
                CancellationToken ct) =>
            {
                var validation =
                    OperatorSearchValidator.Validate(
                        request,
                        DateTime.UtcNow
                    );

                if (!validation.IsValid)
                {
                    return Results.BadRequest(new
                    {
                        error = validation.Error
                    });
                }

                var entry = validation.Entry!;

                if (entry.SensorId.HasValue)
                {
                    bool exists = await db.Sensors
                        .AnyAsync(
                            s => s.Id == entry.SensorId.Value,
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

                db.OperatorSearchEvents.Add(entry);

                await db.SaveChangesAsync(ct);

                return Results.Created(
                    $"/api/insights/searches/{entry.Id}",
                    new
                    {
                        searchId = entry.Id,
                        query = entry.SearchText,
                        scope = entry.Scope,
                        sensorId = entry.SensorId,
                        resultCount = entry.ResultCount,
                        recordedAtUtc = entry.RecordedAtUtc
                    }
                );
            }
        );

        group.MapGet(
            "/searches/{searchId:long}",
            async (
                long searchId,
                SmartXContext db,
                CancellationToken ct) =>
            {
                var entry = await db.OperatorSearchEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        e => e.Id == searchId,
                        ct
                    );

                if (entry is null)
                {
                    return Results.NotFound(new
                    {
                        error = "Search event not found."
                    });
                }

                return Results.Ok(new
                {
                    searchId = entry.Id,
                    query = entry.SearchText,
                    scope = entry.Scope,
                    sensorId = entry.SensorId,
                    resultCount = entry.ResultCount,
                    recordedAtUtc = entry.RecordedAtUtc
                });
            }
        );

        group.MapGet(
            "/searches",
            async (
                int? sensorId,
                string? scope,
                int? limit,
                SmartXContext db,
                CancellationToken ct) =>
            {
                int pageSize = Math.Clamp(
                    limit ?? 50,
                    1,
                    100
                );

                var query = db.OperatorSearchEvents
                    .AsNoTracking()
                    .AsQueryable();

                if (sensorId.HasValue)
                {
                    query = query.Where(
                        e => e.SensorId == sensorId.Value
                    );
                }

                if (!string.IsNullOrWhiteSpace(scope))
                {
                    string normalizedScope =
                        scope.Trim().ToLowerInvariant();

                    query = query.Where(
                        e => e.Scope == normalizedScope
                    );
                }

                var entries = await query
                    .OrderByDescending(e => e.RecordedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        searchId = e.Id,
                        query = e.SearchText,
                        scope = e.Scope,
                        sensorId = e.SensorId,
                        resultCount = e.ResultCount,
                        recordedAtUtc = e.RecordedAtUtc
                    })
                    .ToListAsync(ct);

                return Results.Ok(entries);
            }
        );

        group.MapGet(
            "/interactions",
            async (
                int? sensorId,
                int? limit,
                SmartXContext db,
                CancellationToken ct) =>
            {
                int pageSize = Math.Clamp(
                    limit ?? 50,
                    1,
                    100
                );

                var searchQuery = db.OperatorSearchEvents
                    .AsNoTracking()
                    .AsQueryable();

                var commandQuery = db.DeviceCommands
                    .AsNoTracking()
                    .AsQueryable();

                if (sensorId.HasValue)
                {
                    searchQuery = searchQuery.Where(
                        e => e.SensorId == sensorId.Value
                    );

                    commandQuery = commandQuery.Where(
                        c => c.SensorId == sensorId.Value
                    );
                }

                var searches = await searchQuery
                    .OrderByDescending(e => e.RecordedAtUtc)
                    .ThenByDescending(e => e.Id)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var commands = await commandQuery
                    .OrderByDescending(c => c.CreatedAtUtc)
                    .ThenByDescending(c => c.Id)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var searchInteractions = searches.Select(
                    entry => new OperatorInteractionDto(
                        Type: "Search",
                        ReferenceId: entry.Id.ToString(),
                        OccurredAtUtc: entry.RecordedAtUtc,
                        SensorId: entry.SensorId,
                        Scope: entry.Scope,
                        Query: entry.SearchText,
                        CommandId: null,
                        Target: null,
                        RequestedState: null,
                        CommandStatus: null,
                        Origin: null
                    )
                );

                var commandInteractions = commands.Select(
                    command => new OperatorInteractionDto(
                        Type: "DeviceCommand",
                        ReferenceId: command.Id.ToString(),
                        OccurredAtUtc: command.CreatedAtUtc,
                        SensorId: command.SensorId,
                        Scope: "commands",
                        Query: null,
                        CommandId: command.Id,
                        Target: command.Target.ToString(),
                        RequestedState: command.RequestedState,
                        CommandStatus: command.Status.ToString(),
                        Origin: command.Origin.ToString()
                    )
                );

                var interactions = searchInteractions
                    .Concat(commandInteractions)
                    .OrderByDescending(
                        item => item.OccurredAtUtc
                    )
                    .ThenByDescending(
                        item => item.ReferenceId
                    )
                    .Take(pageSize)
                    .ToArray();

                return Results.Ok(interactions);
            }
        );

        return app;
    }
}
