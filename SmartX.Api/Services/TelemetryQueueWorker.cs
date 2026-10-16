using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class TelemetryQueueWorker : BackgroundService
{
    private readonly TelemetryQueue _queue;

    private readonly DeviceRegistry _registry;

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<TelemetryQueueWorker> _logger;

    public TelemetryQueueWorker(
        TelemetryQueue queue,
        DeviceRegistry registry,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryQueueWorker> logger)
    {
        _queue = queue;
        _registry = registry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Smart-X telemetry processing worker started."
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

            var priority = TelemetryPriorityClassifier.Classify(packet);

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<SmartXContext>();

                bool registeredInMemory =
                    _registry.TryGetById(
                        packet.SensorId,
                        out _
                    );

                if (!registeredInMemory)
                {
                    // Cache miss: validate against
                    // the persistent sensor registry.
                    var sensor = await db.Sensors
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.Id == packet.SensorId,
                            stoppingToken
                        );

                    if (sensor is null)
                    {
                        _logger.LogWarning(
                            "Packet {PacketId} references " +
                            "unregistered Sensor {SensorId}.",
                            packet.PacketId,
                            packet.SensorId
                        );

                        _queue.MarkFailed(packet);
                        continue;
                    }

                    // Add device to live memory.
                    _registry.UpsertSensor(sensor);
                }

                var record = new TelemetryRecord
                {
                    SensorId = packet.SensorId,

                    Temperature = packet.Temperature,
                    SoilMoisture = packet.SoilMoisture,

                    Wattage = packet.Wattage,
                    ValveOpen = packet.ValveOpen,

                    Timestamp = packet.Timestamp
                };

                db.TelemetryRecords.Add(record);

                await db.SaveChangesAsync(
                    stoppingToken
                );

                bool updated =
                    _registry.RecordTelemetry(packet);

                if (!updated)
                {
                    _logger.LogWarning(
                        "Saved packet {PacketId}, but Sensor " +
                        "{SensorId} is no longer in memory.",
                        packet.PacketId,
                        packet.SensorId
                    );
                }

                _queue.MarkProcessed(packet);

                if (priority != TelemetryPriority.Normal)
                {
                    _logger.LogWarning(
                        "Processed {Priority} telemetry " +
                        "for Sensor {SensorId}. " +
                        "Temperature={Temperature}, " +
                        "Moisture={Moisture}, Wattage={Wattage}",
                        priority,
                        packet.SensorId,
                        packet.Temperature,
                        packet.SoilMoisture,
                        packet.Wattage
                    );
                }
                else
                {
                    _logger.LogDebug(
                        "Processed normal telemetry " +
                        "for Sensor {SensorId}.",
                        packet.SensorId
                    );
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _queue.MarkFailed(packet);
                break;
            }
            catch (Exception ex)
            {
                _queue.MarkFailed(packet);

                _logger.LogError(
                    ex,
                    "Unable to process {Priority} packet {PacketId}.",
                    priority,
                    packet.PacketId
                );
            }
        }

        _logger.LogInformation(
            "Smart-X telemetry processing worker stopped."
        );
    }
}
