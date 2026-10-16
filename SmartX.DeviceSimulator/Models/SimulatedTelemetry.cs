
namespace SmartX.DeviceSimulator.Models;

public sealed record SimulatedTelemetry
{
    // Assigned after the simulator registers
    // or identifies its sensor through HTTP.
    public int? SensorId { get; init; }

    public double? Temperature { get; init; }

    public double? SoilMoisture { get; init; }

    public int? Wattage { get; init; }

    public bool? ValveOpen { get; init; }

    public DateTime Timestamp { get; init; }
}
