
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class TelemetryQueueWorker : BackgroundService
{
    private readonly TelemetryQueue _queue;

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<TelemetryQueueWorker> _logger;

    public TelemetryQueueWorker(
        TelemetryQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryQueueWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Smart-X FIFO telemetry worker started."
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            QueuedTelemetry packet;

            try
            {
                packet = await _queue.DequeueAsync(
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<SmartXContext>();

                // Confirm the sensor is registered.
                var sensorExists = await db.Sensors
                    .AsNoTracking()
                    .AnyAsync(
                        s => s.Id == packet.SensorId,
                        stoppingToken
                    );

                if (!sensorExists)
                {
                    _logger.LogWarning(
                        "Rejected queued packet {PacketId}: " +
                        "Sensor {SensorId} does not exist.",
                        packet.PacketId,
                        packet.SensorId
                    );

                    _queue.MarkFailed();
                    continue;
                }

                // Convert the queued packet into
                // the existing database entity.
                var record = new TelemetryRecord
                {
                    SensorId = packet.SensorId,

                    SoilMoisture = packet.SoilMoisture,
                    Temperature = packet.Temperature,

                    Wattage = packet.Wattage,
                    ValveOpen = packet.ValveOpen,

                    Timestamp = packet.Timestamp
                };

                db.TelemetryRecords.Add(record);

                await db.SaveChangesAsync(
                    stoppingToken
                );

                _queue.MarkProcessed();

                _logger.LogDebug(
                    "Processed packet {PacketId} " +
                    "for Sensor {SensorId}.",
                    packet.PacketId,
                    packet.SensorId
                );
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _queue.MarkFailed();
                break;
            }
            catch (Exception ex)
            {
                _queue.MarkFailed();

                _logger.LogError(
                    ex,
                    "Unable to process telemetry packet {PacketId}.",
                    packet.PacketId
                );
            }
        }

        _logger.LogInformation(
            "Smart-X FIFO telemetry worker stopped."
        );
    }
}
