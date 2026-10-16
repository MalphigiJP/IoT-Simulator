
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartX.DeviceSimulator.Models;
using SmartX.DeviceSimulator.Services;

Console.WriteLine();
Console.WriteLine("======================================");
Console.WriteLine("   SMART-X IoT DEVICE SIMULATOR");
Console.WriteLine("======================================");
Console.WriteLine();

// Create the configured simulated devices.
var profiles = DeviceProfileCatalog.CreateDefaults();

var devices = profiles
    .Select(profile => new SimulatedDevice(profile))
    .ToList();

// Configure JSON to match ASP.NET Core
// API field names and omit unused metrics.
var jsonOptions = new JsonSerializerOptions(
    JsonSerializerDefaults.Web)
{
    WriteIndented = false,

    DefaultIgnoreCondition =
        JsonIgnoreCondition.WhenWritingNull
};

int totalGenerated = 0;

foreach (var device in devices)
{
    DeviceProfile profile = device.Profile;

    Console.WriteLine("--------------------------------------");
    Console.WriteLine($"DEVICE: {profile.Name}");
    Console.WriteLine($"MAC: {profile.MacAddress}");
    Console.WriteLine($"LOCATION: {profile.Location}");
    Console.WriteLine($"CATEGORY: {profile.Category}");
    Console.WriteLine($"TYPE: {profile.Kind}");
    Console.WriteLine("--------------------------------------");

    // Generate six readings per device.
    for (int i = 0; i < 6; i++)
    {
        SimulatedTelemetry reading =
            device.GenerateReading();

        bool hasMeasurement =
            reading.Temperature.HasValue ||
            reading.SoilMoisture.HasValue ||
            reading.Wattage.HasValue ||
            reading.ValveOpen.HasValue;

        if (!hasMeasurement)
        {
            throw new InvalidOperationException(
                $"Device {profile.Name} generated " +
                "an empty telemetry packet."
            );
        }

        string json = JsonSerializer.Serialize(
            reading,
            jsonOptions
        );

        Console.WriteLine(
            $"Reading {i + 1}: {json}"
        );

        totalGenerated++;
    }

    Console.WriteLine();
}

Console.WriteLine("======================================");
Console.WriteLine(" SIMULATION PREVIEW COMPLETE");
Console.WriteLine("======================================");

Console.WriteLine(
    $"Simulated devices: {devices.Count}"
);

Console.WriteLine(
    $"Readings generated: {totalGenerated}"
);

Console.WriteLine(
    "Transmission: Not enabled (Chunk 3.2)"
);

Console.WriteLine(
    "Database writes: None"
);

Console.WriteLine();
