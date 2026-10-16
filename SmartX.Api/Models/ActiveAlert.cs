namespace SmartX.Api.Models;

public enum AlertKind
{
    HighTemperature,
    LowSoilMoisture,
    PowerSpike,
    Disconnected
}

// Two different sensors can have the same fault.
// One sensor can also have multiple fault types.
public readonly record struct AlertKey(
    int SensorId,
    AlertKind Kind
);

public sealed record ActiveAlert
{
    public int SensorId { get; init; }

    public string MacAddress { get; init; } = "";

    public string Location { get; init; } = "";

    public string Category { get; init; } = "";

    public AlertKind Kind { get; init; }

    public string Severity { get; init; } = "Critical";

    public string Message { get; init; } = "";

    public double? CurrentValue { get; init; }

    public DateTime ActivatedAtUtc { get; init; }

    public DateTime LastObservedAtUtc { get; init; }

    // How many times this same issue was observed
    // while remaining active.
    public long Observations { get; init; } = 1;
}
