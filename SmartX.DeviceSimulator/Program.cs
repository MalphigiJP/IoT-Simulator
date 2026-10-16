using System.Net.Http;

using SmartX.DeviceSimulator.Services;

// Default to the API port configured earlier.
// Can be overridden without modifying code.
string apiUrl =
    Environment.GetEnvironmentVariable("SMARTX_API_URL")
    ?? "http://localhost:5000";

// Validate the configured address.
if (
    !Uri.TryCreate(
        apiUrl,
        UriKind.Absolute,
        out var parsedUri
    ) ||
    (
        parsedUri.Scheme != Uri.UriSchemeHttp &&
        parsedUri.Scheme != Uri.UriSchemeHttps
    )
)
{
    Console.Error.WriteLine(
        "Invalid SMARTX_API_URL. " +
        "Example: http://localhost:5000"
    );

    return 1;
}

// Ensure relative API paths resolve correctly.
string normalizedAddress =
    apiUrl.TrimEnd('/') + "/";

using var http = new HttpClient
{
    BaseAddress = new Uri(normalizedAddress),

    Timeout = TimeSpan.FromSeconds(15)
};

var api = new SmartXApiClient(http);

// Create our four independent device profiles.
var devices = DeviceProfileCatalog
    .CreateDefaults()
    .Select(profile => new SimulatedDevice(profile))
    .ToList();

// Store the real SensorId assigned by SQL Server.
var registeredDevices =
    new List<(SimulatedDevice Device, int SensorId)>();

const int readingsPerDevice = 6;

int attempted = 0;
int accepted = 0;
int rejected = 0;

Console.WriteLine();
Console.WriteLine("==========================================");
Console.WriteLine("       SMART-X DEVICE SIMULATOR");
Console.WriteLine("==========================================");

Console.WriteLine($"API: {normalizedAddress}");
Console.WriteLine($"Devices: {devices.Count}");
Console.WriteLine("Transmission: HTTP enabled");
Console.WriteLine();

try
{

    Console.WriteLine("STAGE 1: DEVICE REGISTRATION");
    Console.WriteLine("------------------------------------------");

    foreach (var device in devices)
    {
        var profile = device.Profile;

        var result = await api.EnsureRegisteredAsync(
            profile
        );

        registeredDevices.Add((
            Device: device,
            SensorId: result.SensorId
        ));

        string action = result.Created
            ? "REGISTERED"
            : "REUSED";

        Console.WriteLine(
            $"{action}: {profile.Name}"
        );

        Console.WriteLine(
            $"  MAC: {profile.MacAddress}"
        );

        Console.WriteLine(
            $"  Sensor ID: {result.SensorId}"
        );

        Console.WriteLine();
    }

    Console.WriteLine("STAGE 2: TELEMETRY TRANSMISSION");
    Console.WriteLine("------------------------------------------");

    // Six transmission cycles.
    // Every device produces one reading per cycle.
    for (int cycle = 1;
         cycle <= readingsPerDevice;
         cycle++)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"--- TRANSMISSION CYCLE {cycle} ---"
        );

        foreach (var registered in registeredDevices)
        {
            // Generate telemetry using the device's
            // existing Chunk 3.1 profile.
            var reading = registered.Device
                .GenerateReading() with
            {
                // Use the database-assigned ID.
                SensorId = registered.SensorId
            };

            attempted++;

            // Send through actual HTTP POST.
            var result = await api.SendTelemetryAsync(
                reading
            );

            if (result.Accepted)
            {
                accepted++;

                Console.ForegroundColor =
                    ConsoleColor.Green;

                Console.WriteLine(
                    $"[ACCEPTED] " +
                    $"{registered.Device.Profile.Name}"
                );

                Console.ResetColor();

                Console.WriteLine(
                    $"  Sensor: {registered.SensorId}"
                );

                Console.WriteLine(
                    $"  Packet: {result.PacketId}"
                );

                Console.WriteLine(
                    $"  Attempts: {result.Attempts}"
                );
            }
            else
            {
                rejected++;

                Console.ForegroundColor =
                    ConsoleColor.Yellow;

                Console.WriteLine(
                    $"[NOT ACCEPTED] " +
                    $"{registered.Device.Profile.Name}"
                );

                Console.ResetColor();

                Console.WriteLine(
                    $"  Reason: {result.Message}"
                );
            }
        }

        // Short pause between transmission cycles.
        // Continuous simulation comes in Chunk 3.3.
        await Task.Delay(200);
    }

    Console.WriteLine();
    Console.WriteLine("==========================================");
    Console.WriteLine("       TRANSMISSION SUMMARY");
    Console.WriteLine("==========================================");

    Console.WriteLine(
        $"Devices registered/reused: {registeredDevices.Count}"
    );

    Console.WriteLine(
        $"Readings attempted: {attempted}"
    );

    Console.WriteLine(
        $"Accepted into API queue: {accepted}"
    );

    Console.WriteLine(
        $"Not accepted: {rejected}"
    );

    Console.WriteLine(
        "Transport: HTTP"
    );

    Console.WriteLine(
        "Persistence: Async background worker"
    );

    Console.WriteLine();
    Console.WriteLine(
        "Use Smart-X queue, live device and " +
        "telemetry endpoints to verify processing."
    );

    return rejected == 0 ? 0 : 1;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "HTTP communication with Smart-X failed."
    );

    Console.Error.WriteLine(ex.Message);

    Console.Error.WriteLine(
        "Check that the API is running and " +
        "SMARTX_API_URL matches its address."
    );

    return 1;
}
catch (TaskCanceledException)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "The request timed out or was cancelled. " +
        "Check API availability and database health."
    );

    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        $"Simulation failed: {ex.Message}"
    );

    return 1;
}
