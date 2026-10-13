using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Generates and persists simulated IoT readings for the registered sensors.
/// Used for demos/tests, not for production telemetry ingestion.
/// </summary>
public class TelemetrySeeder
{
    private readonly SmartXContext _db;

    public TelemetrySeeder(SmartXContext db)
    {
        _db = db;
    }

    public async Task<TelemetrySimulationResult> GenerateAsync(
        TelemetrySimulationDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.RecordCount <= 0 || request.BatchSize <= 0 ||
            request.AnomalyRate < 0 || request.AnomalyRate > 1)
        {
            throw new InvalidOperationException("Invalid simulation parameters.");
        }

        // Use a projection so clearing the EF tracker after each batch is safe.
        var sensors = await _db.Sensors
            .AsNoTracking()
            .Select(s => new { s.Id, s.Category })
            .ToListAsync(cancellationToken);

        if (sensors.Count == 0)
        {
            throw new InvalidOperationException(
                "Register at least one sensor before simulating telemetry.");
        }

        var stopwatch = Stopwatch.StartNew();
        var anomalies = 0;
        var batches = 0;
        var inserted = 0;
        var now = DateTime.UtcNow;

        while (inserted < request.RecordCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(request.BatchSize, request.RecordCount - inserted);
            var records = new List<TelemetryRecord>(count);

            for (var i = 0; i < count; i++)
            {
                var sensor = sensors[Random.Shared.Next(sensors.Count)];
                var category = (sensor.Category ?? "").Trim().ToLowerInvariant();
                var requestAnomaly = Random.Shared.NextDouble() < request.AnomalyRate;
                var record = new TelemetryRecord
                {
                    SensorId = sensor.Id,
                    Timestamp = request.LiveMode
                        ? now.AddSeconds(-Random.Shared.Next(0, 30))
                        : now.AddMinutes(-Random.Shared.Next(0, 1440))
                };

                if (category.Contains("power") || category.Contains("grid"))
                {
                    record.Wattage = requestAnomaly
                        ? Random.Shared.Next(6500, 10001)
                        : Random.Shared.Next(500, 4001);
                    if (requestAnomaly) anomalies++;
                }
                else if (category.Contains("actuator") || category.Contains("valve"))
                {
                    record.ValveOpen = Random.Shared.Next(2) == 1;
                    // An open valve alone is not a proven anomaly without
                    // a configured expected state, so do not count it as one.
                }
                else
                {
                    record.Temperature = requestAnomaly && Random.Shared.Next(2) == 0
                        ? 55 + Random.Shared.NextDouble() * 35
                        : 18 + Random.Shared.NextDouble() * 14;

                    record.SoilMoisture = requestAnomaly && record.Temperature <= 50
                        ? Random.Shared.NextDouble() * 9
                        : 35 + Random.Shared.NextDouble() * 45;

                    if (requestAnomaly) anomalies++;
                }

                records.Add(record);
            }

            _db.TelemetryRecords.AddRange(records);
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            inserted += count;
            batches++;
        }

        stopwatch.Stop();
        return new TelemetrySimulationResult
        {
            RequestedRecords = request.RecordCount,
            InsertedRecords = inserted,
            AnomaliesGenerated = anomalies,
            SensorsUsed = sensors.Count,
            BatchesProcessed = batches,
            DurationMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }
}
