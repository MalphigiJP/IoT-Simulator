
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class HistoricalRelationshipService
{
    private const int TelemetryLimit = 5000;
    private const int CommandLimit = 1000;
    private const int SearchLimit = 1500;

    private readonly SmartXContext _db;

    public HistoricalRelationshipService(
        SmartXContext db)
    {
        _db = db;
    }

    public async Task<HistoricalRelationshipReport> AnalyzeAsync(
        HistoricalRelationshipOptions options,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;

        var fromUtc = nowUtc.AddDays(
            -options.Days
        );

        var sensors = await _db.Sensors
            .AsNoTracking()
            .Select(s => new RelationshipSensor(
                s.Id,
                s.Location
            ))
            .ToArrayAsync(ct);

        var telemetryQuery = _db.TelemetryRecords
            .AsNoTracking()
            .Where(t =>
                t.Timestamp >= fromUtc &&
                t.Timestamp <= nowUtc
            );

        if (options.SensorId.HasValue)
        {
            int sourceSensorId = options.SensorId.Value;

            telemetryQuery = telemetryQuery.Where(
                t => t.SensorId == sourceSensorId
            );
        }

        var telemetryRows = await telemetryQuery
            .OrderByDescending(t => t.Timestamp)
            .ThenByDescending(t => t.Id)
            .Take(TelemetryLimit + 1)
            .Select(t => new RelationshipReading(
                t.Id,
                t.SensorId,
                t.Timestamp,
                t.Temperature,
                t.SoilMoisture,
                t.Wattage
            ))
            .ToArrayAsync(ct);

        bool telemetryTruncated =
            telemetryRows.Length > TelemetryLimit;

        var telemetry = telemetryRows
            .Take(TelemetryLimit)
            .ToArray();

        var commandRows = await _db.DeviceCommands
            .AsNoTracking()
            .Where(c =>
                c.CreatedAtUtc >= fromUtc &&
                c.CreatedAtUtc <= nowUtc &&
                c.Status == DeviceCommandStatus.Applied &&
                c.Origin == DeviceCommandOrigin.Manual
            )
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenByDescending(c => c.Id)
            .Take(CommandLimit + 1)
            .Select(c => new RelationshipAction(
                c.Id,
                c.SensorId,
                c.Target,
                c.RequestedState,
                c.CreatedAtUtc,
                c.Status,
                c.Origin
            ))
            .ToArrayAsync(ct);

        bool commandsTruncated =
            commandRows.Length > CommandLimit;

        var commands = commandRows
            .Take(CommandLimit)
            .ToArray();

        var searchRows = await _db.OperatorSearchEvents
            .AsNoTracking()
            .Where(s =>
                s.RecordedAtUtc >= fromUtc &&
                s.RecordedAtUtc <= nowUtc
            )
            .OrderByDescending(s => s.RecordedAtUtc)
            .ThenByDescending(s => s.Id)
            .Take(SearchLimit + 1)
            .Select(s => new RelationshipSearch(
                s.Id,
                s.SensorId,
                s.RecordedAtUtc,
                s.Scope
            ))
            .ToArrayAsync(ct);

        bool searchesTruncated =
            searchRows.Length > SearchLimit;

        var searches = searchRows
            .Take(SearchLimit)
            .ToArray();

        var data = new HistoricalRelationshipData(
            sensors,
            telemetry,
            commands,
            searches,
            telemetryTruncated,
            commandsTruncated,
            searchesTruncated
        );

        return HistoricalRelationshipAnalyzer.Analyze(
            data,
            options,
            nowUtc
        );
    }
}
