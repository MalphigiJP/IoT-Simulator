
namespace SmartX.Api.Models;

public sealed record DeviceState
{

    public int SensorId { get; init; }

    public string MacAddress { get; init; } = "";

    public string Location { get; init; } = "";

    public string Category { get; init; } = "";

    public DateTime RegisteredAtUtc { get; init; }

    public double? Temperature { get; init; }

    public double? SoilMoisture { get; init; }

    public int? Wattage { get; init; }

    public bool? ValveOpen { get; init; }

    // Timestamp supplied by the device.
    public DateTime? LastSampleTimestampUtc { get; init; }

    // Time the API received the packet.
    public DateTime? LastReceivedAtUtc { get; init; }

    public long PacketsProcessed { get; init; }
}
