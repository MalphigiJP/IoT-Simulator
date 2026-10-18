
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class RecommendationService
{
    private readonly SmartXContext _db;
    private readonly HistoricalRelationshipService _relationships;

    public RecommendationService(
        SmartXContext db,
        HistoricalRelationshipService relationships)
    {
        _db = db;
        _relationships = relationships;
    }

    public async Task<RecommendationReport> GetRecommendationsAsync(
        HistoricalRelationshipOptions options,
        CancellationToken ct = default)
    {
        var history = await _relationships.AnalyzeAsync(
            options,
            ct
        );

        var sensorIds = history.Patterns
            .SelectMany(pattern => new[]
            {
                pattern.SourceSensorId,
                pattern.ActionSensorId
            })
            .Distinct()
            .ToArray();

        if (sensorIds.Length == 0)
        {
            return RecommendationRanker.Rank(
                history,
                new Dictionary<int, RecommendationDeviceSnapshot>(),
                DateTime.UtcNow
            );
        }

        var sensors = await _db.Sensors
            .AsNoTracking()
            .Where(s => sensorIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.Category,
                s.Location
            })
            .ToArrayAsync(ct);

        var latestTelemetry = await _db.TelemetryRecords
            .AsNoTracking()
            .Where(t => sensorIds.Contains(t.SensorId))
            .GroupBy(t => t.SensorId)
            .Select(group => group
                .OrderByDescending(t => t.Timestamp)
                .ThenByDescending(t => t.Id)
                .Select(t => new
                {
                    t.SensorId,
                    t.Timestamp,
                    t.Temperature,
                    t.SoilMoisture,
                    t.Wattage
                })
                .First()
            )
            .ToArrayAsync(ct);

        var controlStates = await _db.DeviceControlStates
            .AsNoTracking()
            .Where(s => sensorIds.Contains(s.SensorId))
            .Select(s => new
            {
                s.SensorId,
                s.DesiredState,
                s.ReportedState
            })
            .ToArrayAsync(ct);

        var outstandingSensors = await _db.DeviceCommands
            .AsNoTracking()
            .Where(command =>
                sensorIds.Contains(command.SensorId) &&
                (
                    command.Status == DeviceCommandStatus.Pending ||
                    command.Status == DeviceCommandStatus.Delivered
                )
            )
            .Select(command => command.SensorId)
            .Distinct()
            .ToArrayAsync(ct);

        var telemetryBySensor = latestTelemetry
            .ToDictionary(t => t.SensorId);

        var controlsBySensor = controlStates
            .ToDictionary(s => s.SensorId);

        var outstanding = new HashSet<int>(
            outstandingSensors
        );

        var devices = new Dictionary<
            int,
            RecommendationDeviceSnapshot
        >();

        foreach (var sensor in sensors)
        {
            telemetryBySensor.TryGetValue(
                sensor.Id,
                out var telemetry
            );

            controlsBySensor.TryGetValue(
                sensor.Id,
                out var control
            );

            devices[sensor.Id] =
                new RecommendationDeviceSnapshot(
                    sensor.Id,
                    sensor.Category,
                    sensor.Location,
                    telemetry?.Timestamp,
                    telemetry?.Temperature,
                    telemetry?.SoilMoisture,
                    telemetry?.Wattage,
                    control?.DesiredState,
                    control?.ReportedState,
                    outstanding.Contains(sensor.Id)
                );
        }

        return RecommendationRanker.Rank(
            history,
            devices,
            DateTime.UtcNow
        );
    }
}
