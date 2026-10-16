
namespace SmartX.DeviceSimulator.Models;

// The types of IoT devices we can simulate.
public enum DeviceKind
{
    EnvironmentalEsp32,
    SmartPlug,
    IrrigationValve,
    PumpController
}

// Configuration describing one simulated device.
public sealed record DeviceProfile
{
    public string Name { get; init; } = "";

    public string MacAddress { get; init; } = "";

    public string Location { get; init; } = "";

    public string Category { get; init; } = "";

    public DeviceKind Kind { get; init; }

    public string Description { get; init; } = "";

    // Used for repeatable pseudo-random readings.
    public int RandomSeed { get; init; }
}
