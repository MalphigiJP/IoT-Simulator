namespace SmartX.Api.Models;

public sealed record DeviceControlState
{
    public int SensorId { get; init; }

    public string MacAddress { get; init; } = "";

    public string Category { get; init; } = "";

    // Null indicates a device without a
    // supported manual control target.
    public DeviceControlTarget? SupportedTarget
    {
        get;
        init;
    }

    public bool? DesiredState { get; init; }

    public bool? ReportedState { get; init; }

    public Guid? LastCommandId { get; init; }

    public DateTime? LastDesiredChangeAtUtc
    {
        get;
        init;
    }

    public DateTime? LastReportedAtUtc
    {
        get;
        init;
    }

    // Will increment when control state changes.
    public long Revision { get; init; }
}
