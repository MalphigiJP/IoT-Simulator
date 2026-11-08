
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;

namespace SmartX.Api.Services.Mesh;

public sealed class MeshNetworkSnapshotService
{
    private readonly SmartXContext _db;

    public MeshNetworkSnapshotService(
        SmartXContext db)
    {
        _db = db;
    }

    public async Task<MeshNetworkSnapshot> GetSnapshotAsync(
        CancellationToken ct = default)
    {
        var sensors = await _db.Sensors
            .AsNoTracking()
            .OrderBy(sensor => sensor.Id)
            .Select(sensor => new
            {
                sensor.Id,
                sensor.MacAddress,
                sensor.Location,
                sensor.Category
            })
            .ToArrayAsync(ct);

        var latestTelemetry = await _db.TelemetryRecords
            .AsNoTracking()
            .GroupBy(record => record.SensorId)
            .Select(group => new
            {
                SensorId = group.Key,
                LastTelemetryAtUtc = group.Max(
                    record => record.Timestamp
                )
            })
            .ToArrayAsync(ct);

        var latestBySensorId = latestTelemetry
            .ToDictionary(
                item => item.SensorId,
                item => (DateTime?)item.LastTelemetryAtUtc
            );

        var inputs = sensors
            .Select(sensor => new MeshSensorInput(
                sensor.Id,
                sensor.MacAddress ?? "",
                sensor.Location ?? "",
                sensor.Category ?? "",
                latestBySensorId.GetValueOrDefault(
                    sensor.Id
                )
            ))
            .ToArray();

        return MeshNetworkBuilder.Build(
            inputs,
            DateTime.UtcNow
        );
    }
}
