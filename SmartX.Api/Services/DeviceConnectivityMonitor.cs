namespace SmartX.Api.Services;

public sealed class DeviceConnectivityMonitor
    : BackgroundService
{
    private readonly DeviceRegistry _registry;

    private readonly ActiveAlertService _alerts;

    private readonly ILogger<DeviceConnectivityMonitor> _logger;

    public DeviceConnectivityMonitor(
        DeviceRegistry registry,
        ActiveAlertService alerts,
        ILogger<DeviceConnectivityMonitor> logger)
    {
        _registry = registry;
        _alerts = alerts;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Smart-X connectivity monitor started."
        );

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(10)
        );

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Snapshot the registered devices.
                    var devices = _registry.GetAll();

                    // Detect newly disconnected devices
                    // and resolve recovered connections.
                    _alerts.CheckConnectivity(
                        devices,
                        DateTime.UtcNow
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unable to check device connectivity."
                    );
                }

                // Sleep until the next check.
                if (!await timer.WaitForNextTickAsync(
                    stoppingToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }

        _logger.LogInformation(
            "Smart-X connectivity monitor stopped."
        );
    }
}
