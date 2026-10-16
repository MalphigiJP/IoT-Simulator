using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class TelemetryQueueWorker : BackgroundService
{
    private readonly TelemetryQueue _queue;
    private readonly DeviceRegistry _registry;
    private readonly TelemetryHistoryStore _history;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryQueueWorker> _logger;

    public TelemetryQueueWorker(
        TelemetryQueue queue,
        DeviceRegistry registry,
        TelemetryHistoryStore history,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryQueueWorker> logger)
    {
        _queue = queue;
        _registry = registry;
        _history = history;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Smart-X telemetry history worker started."
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

            bool processingCompleted = false;

            var priority =
                TelemetryPriorityClassifier.Classify(packet);

            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<SmartXContext>();

                if (!_registry.TryGetById(
                    packet.SensorId,
                    out _))
                {
                    var sensor = await db.Sensors
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.Id == packet.SensorId,
                            stoppingToken
                        );

                    if (sensor is null)
                    {
                        _queue.MarkFailed(packet);
                        processingCompleted = true;

                        _logger.LogWarning(
                            "Unknown sensor {SensorId}.",
                            packet.SensorId
                        );

                        continue;
                    }

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

                await db.SaveChangesAsync(stoppingToken);

                _queue.MarkProcessed(packet);
                processingCompleted = true;

                try
                {
                    _history.AddProcessed(record);

                    bool registryUpdated =
                        _registry.RecordTelemetry(packet);

                    if (!registryUpdated)
                    {
                        _logger.LogWarning(
                            "Sensor {SensorId} is missing " +
                            "from the live device registry.",
                            packet.SensorId
                        );
                    }
                }
                catch (Exception cacheException)
                {
                    _logger.LogError(
                        cacheException,
                        "Saved record {RecordId}, but " +
                        "a live cache update failed.",
                        record.Id
                    );
                }

                if (priority != TelemetryPriority.Normal)
                {
                    _logger.LogWarning(
                        "Processed {Priority} telemetry " +
                        "for Sensor {SensorId}.",
                        priority,
                        packet.SensorId
                    );
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                if (!processingCompleted)
                {
                    _queue.MarkFailed(packet);
                }

                break;
            }
            catch (Exception ex)
            {
                if (!processingCompleted)
                {
                    _queue.MarkFailed(packet);
                }

                _logger.LogError(
                    ex,
                    "Unable to process telemetry packet {PacketId}.",
                    packet.PacketId
                );
            }
        }

        _logger.LogInformation(
            "Smart-X telemetry history worker stopped."
        );
    }
}
