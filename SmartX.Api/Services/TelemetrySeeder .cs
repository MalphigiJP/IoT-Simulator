using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public class TelemetrySeeder
{
    private readonly SmartXContext _context;

    private readonly Random _random = new();

    public TelemetrySeeder(SmartXContext context)
    {
        _context = context;
    }

    public async Task<TelemetrySimulationResult> GenerateAsync(
        TelemetrySimulationDto request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var sensors = _context.Sensors
            .ToList();

        if (sensors.Count == 0)
        {
            throw new InvalidOperationException(
                "No sensors are registered.");
        }

        var totalRecords = request.RecordCount;
        var batchSize = request.BatchSize;

        var anomalyCount = 0;
        var insertedCount = 0;
        var batchesProcessed = 0;

        var records = new List<TelemetryRecord>(
            batchSize);

        for (var i = 0; i < totalRecords; i++)
        {
            var sensor = sensors[
                _random.Next(sensors.Count)
            ];

            var isAnomaly =
                _random.NextDouble() < request.AnomalyRate;

            if (isAnomaly)
            {
                anomalyCount++;
            }

            var telemetry = GenerateTelemetry(
                sensor,
                isAnomaly);

            records.Add(telemetry);

            if (records.Count >= batchSize)
            {
                await InsertBatchAsync(records);

                insertedCount += records.Count;
                batchesProcessed++;

                records.Clear();
            }
        }

        if (records.Count > 0)
        {
            await InsertBatchAsync(records);

            insertedCount += records.Count;
            batchesProcessed++;
        }

        stopwatch.Stop();

        return new TelemetrySimulationResult
        {
            RequestedRecords = totalRecords,
            InsertedRecords = insertedCount,
            AnomaliesGenerated = anomalyCount,
            SensorsUsed = sensors.Count,
            BatchesProcessed = batchesProcessed,
            DurationMilliseconds =
                stopwatch.ElapsedMilliseconds
        };
    }

    private async Task InsertBatchAsync(
        List<TelemetryRecord> records)
    {
        await _context.TelemetryRecords
            .AddRangeAsync(records);

        await _context.SaveChangesAsync();
    }

    private TelemetryRecord GenerateTelemetry(
        Sensor sensor,
        bool anomaly)
    {
        return sensor.Category switch
        {
            "Environmental" =>
                GenerateEnvironmentalTelemetry(
                    sensor.Id,
                    anomaly),

            "Power Consumption" =>
                GeneratePowerTelemetry(
                    sensor.Id,
                    anomaly),

            "Actuator" =>
                GenerateActuatorTelemetry(
                    sensor.Id,
                    anomaly),

            _ =>
                GenerateEnvironmentalTelemetry(
                    sensor.Id,
                    anomaly)
        };
    }

    private TelemetryRecord GenerateEnvironmentalTelemetry(
        int sensorId,
        bool anomaly)
    {
        var temperature = anomaly
            ? RandomDouble(50, 90)
            : RandomDouble(18, 32);

        var soilMoisture = anomaly
            ? RandomDouble(0, 10)
            : RandomDouble(35, 80);

        return new TelemetryRecord
        {
            SensorId = sensorId,

            Temperature = temperature,

            SoilMoisture = soilMoisture,

            Wattage = null,

            ValveOpen = null,

            Timestamp = RandomTimestamp()
        };
    }

    private TelemetryRecord GeneratePowerTelemetry(
        int sensorId,
        bool anomaly)
    {
        var wattage = anomaly
            ? _random.Next(6000, 10000)
            : _random.Next(500, 4000);

        return new TelemetryRecord
        {
            SensorId = sensorId,

            Temperature = null,

            SoilMoisture = null,

            Wattage = wattage,

            ValveOpen = null,

            Timestamp = RandomTimestamp()
        };
    }

    private TelemetryRecord GenerateActuatorTelemetry(
        int sensorId,
        bool anomaly)
    {
        return new TelemetryRecord
        {
            SensorId = sensorId,

            Temperature = null,

            SoilMoisture = null,

            Wattage = null,

            ValveOpen = anomaly
                ? true
                : _random.Next(0, 2) == 1,

            Timestamp = RandomTimestamp()
        };
    }

    private double RandomDouble(
        double minimum,
        double maximum)
    {
        return minimum +
               (_random.NextDouble() *
               (maximum - minimum));
    }

    private DateTime RandomTimestamp()
    {
        return DateTime.UtcNow.AddMinutes(
            -_random.Next(0, 1440));
    }
}