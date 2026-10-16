using SmartX.DeviceSimulator.Services;

SimulationSettings settings;

try
{
    settings = SimulationSettings.Load();
}
catch (Exception ex)
{
    Console.Error.WriteLine(
        $"Invalid simulator configuration: {ex.Message}"
    );

    return 1;
}

using var cancellation = new CancellationTokenSource();

ConsoleCancelEventHandler cancelHandler = (
    sender,
    eventArgs) =>
{
    // Prevent an abrupt console termination.
    eventArgs.Cancel = true;

    Console.WriteLine();
    Console.WriteLine(
        "Shutdown requested. Stopping simulated devices..."
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
        .Select(
            profile => new SimulatedDevice(profile)
        )
        .ToList();

    Console.WriteLine();
    Console.WriteLine("=========================================");
    Console.WriteLine("       SMART-X IOT DEVICE FLEET");
    Console.WriteLine("=========================================");

    Console.WriteLine(
        $"API: {settings.ApiUrl}"
    );

    Console.WriteLine(
        $"Configured devices: {devices.Count}"
    );

    Console.WriteLine(
        settings.DurationSeconds == 0
            ? "Duration: Continuous (Ctrl+C to stop)"
            : $"Duration: {settings.DurationSeconds} seconds"
    );

    Console.WriteLine(
        settings.MaxReadingsPerDevice == 0
            ? "Readings per device: Unlimited"
            : "Readings per device: " +
              settings.MaxReadingsPerDevice
    );

    Console.WriteLine();

    var registeredDevices =
        new List<(SimulatedDevice Device, int SensorId)>();

    Console.WriteLine("STAGE 1: DEVICE REGISTRATION");
    Console.WriteLine("-----------------------------------------");


    foreach (var device in devices)
    {
        var registrationResult =
            await api.EnsureRegisteredAsync(
                device.Profile,
                cancellation.Token
            );

        registeredDevices.Add((
            Device: device,
            SensorId: registrationResult.SensorId
        ));

        string action = registrationResult.Created
            ? "REGISTERED"
            : "REUSED";

        var interval = settings.GetInterval(
            device.Profile.Kind
        );

        Console.WriteLine(
            $"{action}: {device.Profile.Name}"
        );

        Console.WriteLine(
            $"  Sensor ID: {registrationResult.SensorId}"
        );

        Console.WriteLine(
            $"  MAC: {device.Profile.MacAddress}"
        );

        Console.WriteLine(
            $"  Reporting interval: " +
            $"{interval.TotalMilliseconds:0} ms"
        );
    }


    Console.WriteLine();

    Console.WriteLine("STAGE 2: LIVE TELEMETRY");
    Console.WriteLine("-----------------------------------------");

    var statistics = new SimulationStatistics();

    // Duration starts after registration.
    if (settings.DurationSeconds > 0)
    {
        cancellation.CancelAfter(
            TimeSpan.FromSeconds(
                settings.DurationSeconds
            )
        );
    }

    // Start one independent asynchronous
    // simulation loop for each registered device.
    var runners = registeredDevices
        .Select(registered =>
        {
            return new DeviceSimulationRunner(
                registered.Device,
                registered.SensorId,
                api,
                statistics,

                settings.GetInterval(
                    registered.Device.Profile.Kind
                ),

                settings.MaxReadingsPerDevice
            );
        })
        .ToList();

    // Periodically print fleet-wide statistics.
    var reportingTask = ReportStatisticsAsync(
        statistics,
        cancellation.Token
    );

    // Start every device loop.
    var deviceTasks = runners
        .Select(
            runner => runner.RunAsync(
                cancellation.Token
            )
        )
        .ToArray();

    // Await every device concurrently.
    await Task.WhenAll(deviceTasks);

    // Devices may finish naturally when a
    // maximum reading count is configured.
    cancellation.Cancel();

    await reportingTask;

    var finalStats = statistics.GetSnapshot();

    Console.WriteLine();
    Console.WriteLine("=========================================");
    Console.WriteLine("          SIMULATION SUMMARY");
    Console.WriteLine("=========================================");

    Console.WriteLine(
        $"Devices: {registeredDevices.Count}"
    );

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
        $"Other HTTP rejections: {finalStats.Rejected}"
    );

    Console.WriteLine(
        $"Transmission errors: {finalStats.Errors}"
    );

    Console.WriteLine();
    Console.WriteLine(
        "HTTP 202 confirms queue acceptance, not database persistence."
    );

    Console.WriteLine(
        "Check /api/telemetry/queue/status for processing results."
    );

    Console.WriteLine();

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
        "Simulation stopped during startup."
    );

    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine(
        $"Unable to communicate with Smart-X: {ex.Message}"
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
        while (
            await timer.WaitForNextTickAsync(
                cancellationToken
            )
        )
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
        // Expected when the simulation stops.
    }
}
