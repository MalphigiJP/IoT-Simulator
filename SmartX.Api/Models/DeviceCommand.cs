namespace SmartX.Api.Models;

public enum DeviceControlTarget
{
    Relay,
    Valve,
    Pump
}

public enum DeviceCommandStatus
{
    Pending,
    Delivered,
    Applied,
    Failed,
    Cancelled
}

public enum DeviceCommandOrigin
{
    Manual,
    Undo
}

public sealed record DeviceCommand
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int SensorId { get; init; }

    public DeviceControlTarget Target { get; init; }

    public bool RequestedState { get; init; }
    public bool? PreviousDesiredState { get; init; }
    public DeviceCommandOrigin Origin { get; init; } =
        DeviceCommandOrigin.Manual;

    public Guid? UndoOfCommandId { get; init; }

    public DeviceCommandStatus Status { get; init; } =
        DeviceCommandStatus.Pending;

    public DateTime CreatedAtUtc { get; init; } =
        DateTime.UtcNow;

    public DateTime? DeliveredAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string? FailureReason { get; init; }
}
