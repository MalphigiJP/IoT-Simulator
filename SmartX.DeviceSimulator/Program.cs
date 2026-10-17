using SmartX.DeviceSimulator.Services;

bool loadTestEnabled = string.Equals(
    Environment.GetEnvironmentVariable("SMARTX_LOAD_TEST"),
    "true",
    StringComparison.OrdinalIgnoreCase
);

if (loadTestEnabled)
{
    try
    {
        var loadSettings = LoadTestSettings.Load();

        return await FleetLoadTest.RunAsync(
            loadSettings
        );
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"Invalid load test configuration: {ex.Message}"
        );

        return 1;
    }
}

SimulationSettings settings;

try
{
    settings = SimulationSettings.Load();
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Configuration error: {ex.Message}"
    );

    return 1;
}

using var cancellation = new CancellationTokenSource();

ConsoleCancelEventHandler cancelHandler =
    (sender, eventArgs) =>
    {
        eventArgs.Cancel = true;

        Console.WriteLine();
        Console.WriteLine(
            "Shutdown requested..."
        );

        cancellation.Cancel();
    };

Console.CancelKeyPress += cancelHandler;

try
{

    using var http = new HttpClient
    {
        BaseAddress = new Uri(
            settings.ApiUrl.TrimEnd('/') + "/"
        ),

        Timeout = TimeSpan.FromSeconds(15)
    };

    var api = new SmartXApiClient(http);

    var devices = DeviceProfileCatalog
        .CreateDefaults()
        .Select(profile => new SimulatedDevice(profile))
        .ToList();

    Console.WriteLine();
    Console.WriteLine("=========================================");
    Console.WriteLine("       SMART-X DEVICE SIMULATOR");
    Console.WriteLine("=========================================");

    Console.WriteLine($"API: {settings.ApiUrl}");

    Console.WriteLine($"Devices: {devices.Count}");

    Console.WriteLine(
        $"Fault injection: " +
        $"{(settings.FaultsEnabled ? "ENABLED" : "DISABLED")}"
    );

    Console.WriteLine(
        $"Device outage: " +
        $"{(settings.OutageEnabled ? "ENABLED" : "DISABLED")}"
    );

    if (settings.FaultsEnabled)
    {
        Console.WriteLine(
            $"Fault frequency: every " +
            $"{settings.FaultEveryN} readings per device"
        );
    }

    if (settings.OutageEnabled)
    {
        Console.WriteLine(
            $"Outage target: {settings.OutageDevice}"
        );

        Console.WriteLine(
            $"Outage starts after: " +
            $"{settings.OutageAfterReadings} readings"
        );

        Console.WriteLine(
            $"Outage duration: " +
            $"{settings.OutageSeconds} seconds"
        );
    }

    Console.WriteLine(
        settings.DurationSeconds == 0
            ? "Duration: Unlimited"
            : $"Duration: {settings.DurationSeconds} seconds"
    );

    Console.WriteLine();

    var registeredDevices =
        new List<(SimulatedDevice Device, int SensorId)>();

    foreach (var device in devices)
    {
        var registration =
            await api.EnsureRegisteredAsync(
                device.Profile,
                cancellation.Token
            );

        registeredDevices.Add((
            Device: device,
            SensorId: registration.SensorId
        ));

        Console.WriteLine(
            $"[{(registration.Created ? "REGISTERED" : "REUSED")}] " +
            $"{device.Profile.Name} | " +
            $"Sensor {registration.SensorId}"
        );
    }

    var statistics = new SimulationStatistics();

    if (settings.DurationSeconds > 0)
    {
        cancellation.CancelAfter(
            TimeSpan.FromSeconds(settings.DurationSeconds)
        );
    }

    var runners = registeredDevices
        .Select(registered =>
            new DeviceSimulationRunner(
                registered.Device,
                registered.SensorId,
                api,
                statistics,
                settings
            )
        )
        .ToList();

    Console.WriteLine();
    Console.WriteLine("STARTING LIVE DEVICE TRANSMISSION");
    Console.WriteLine("-----------------------------------------");

    var reportingTask = ReportStatisticsAsync(
        statistics,
        cancellation.Token
    );

    var deviceTasks = runners
        .Select(runner =>
            runner.RunAsync(cancellation.Token)
        )
        .ToArray();

    await Task.WhenAll(deviceTasks);

    // Stop the statistics reporter after the
    // fleet finishes or its duration expires.
    cancellation.Cancel();

    await reportingTask;

    var finalStats = statistics.GetSnapshot();

    Console.WriteLine();
    Console.WriteLine("=========================================");
    Console.WriteLine("          SIMULATION SUMMARY");
    Console.WriteLine("=========================================");

    Console.WriteLine(
        $"Readings attempted: {finalStats.Attempted}"
    );

    Console.WriteLine(
        $"Accepted by API: {finalStats.Accepted}"
    );

    Console.WriteLine(
        $"Queue-full responses: {finalStats.QueueFull}"
    );

    Console.WriteLine(
        $"Other rejections: {finalStats.Rejected}"
    );

    Console.WriteLine(
        $"Transmission errors: {finalStats.Errors}"
    );

    Console.WriteLine(
        $"Injected abnormal readings: {finalStats.InjectedFaults}"
    );

    Console.WriteLine(
        $"Outages started: {finalStats.OutagesStarted}"
    );

    Console.WriteLine(
        $"Outage pauses completed: {finalStats.OutagesRecovered}"
    );

    Console.WriteLine();
    Console.WriteLine(
        "HTTP 202 confirms queue acceptance, " +
        "not database persistence."
    );

    Console.WriteLine(
        "Check /api/alerts/active and " +
        "/api/telemetry/queue/status."
    );

    return (
        finalStats.QueueFull == 0 &&
        finalStats.Rejected == 0 &&
        finalStats.Errors == 0
    ) ? 0 : 1;
}
catch (OperationCanceledException)
    when (cancellation.IsCancellationRequested)
{
    Console.WriteLine(
        "Simulation cancelled during startup."
    );

    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine(
        $"API connection failed: {ex.Message}"
    );

    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Simulator error: {ex.Message}"
    );

    return 1;
}
finally
{
    cancellation.Cancel();

    Console.CancelKeyPress -= cancelHandler;
}

static async Task ReportStatisticsAsync(
    SimulationStatistics statistics,
    CancellationToken cancellationToken)
{
    using var timer = new PeriodicTimer(
        TimeSpan.FromSeconds(10)
    );

    try
    {
        while (await timer.WaitForNextTickAsync(
            cancellationToken))
        {
            var snapshot = statistics.GetSnapshot();

            Console.WriteLine();
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("LIVE FLEET STATISTICS");
            Console.WriteLine("-----------------------------------------");

            Console.WriteLine(
                $"Attempted: {snapshot.Attempted}"
            );

            Console.WriteLine(
                $"Accepted: {snapshot.Accepted}"
            );

            Console.WriteLine(
                $"Faults injected: {snapshot.InjectedFaults}"
            );

            Console.WriteLine(
                $"Outages started: {snapshot.OutagesStarted}"
            );

            Console.WriteLine(
                $"Outage pauses completed: {snapshot.OutagesRecovered}"
            );

            Console.WriteLine(
                $"Queue full: {snapshot.QueueFull}"
            );

            Console.WriteLine(
                $"Rejected: {snapshot.Rejected}"
            );

            Console.WriteLine(
                $"Errors: {snapshot.Errors}"
            );

            Console.WriteLine("-----------------------------------------");
            Console.WriteLine();
        }
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        // Normal shutdown.
    }
}
