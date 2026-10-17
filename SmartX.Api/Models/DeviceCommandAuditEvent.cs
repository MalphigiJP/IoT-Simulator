
namespace SmartX.Api.Models;

public sealed class DeviceCommandAuditEvent
{
    public long Id { get; set; }

    public int SensorId { get; set; }

    public Guid? CommandId { get; set; }

    public string Action { get; set; } = "";

    public bool? DesiredState { get; set; }

    public bool? ReportedState { get; set; }

    public DateTime OccurredAtUtc { get; set; } =
        DateTime.UtcNow;
}
