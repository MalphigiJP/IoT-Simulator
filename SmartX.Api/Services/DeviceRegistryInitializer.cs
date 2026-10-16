
using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;

namespace SmartX.Api.Services;

public sealed class DeviceRegistryInitializer
    : BackgroundService
{
    private readonly DeviceRegistry _registry;

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<DeviceRegistryInitializer> _logger;

    public DeviceRegistryInitializer(
        DeviceRegistry registry,
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceRegistryInitializer> logger)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Retry registration loading if SQL Server
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<SmartXContext>();

                var sensors = await db.Sensors
                    .AsNoTracking()
                    .ToListAsync(stoppingToken);

                foreach (var sensor in sensors)
                {
                    _registry.UpsertSensor(sensor);
                }

                _logger.LogInformation(
                    "Smart-X live registry loaded {Count} devices.",
                    sensors.Count
                );

                // Initial loading completed.
                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to initialise device registry. " +
                    "Retrying in 10 seconds."
                );

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(10),
                        stoppingToken
                    );
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
