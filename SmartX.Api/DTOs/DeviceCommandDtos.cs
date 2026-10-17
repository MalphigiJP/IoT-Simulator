namespace SmartX.Api.DTOs;

public sealed record CreateDeviceCommandDto
{
    // "Relay", "Valve" or "Pump"
    public string? Target { get; init; }

    public bool? State { get; init; }
}

public sealed record DeviceCommandAcknowledgementDto
{
    public Guid CommandId { get; init; }

    public bool Applied { get; init; }

    // The actual state confirmed by the
    // simulated device.
    public bool? ReportedState { get; init; }

    public string? Error { get; init; }
}

public sealed record DeviceControlReportDto
{
    public string? Target { get; init; }

    public bool? State { get; init; }
}
