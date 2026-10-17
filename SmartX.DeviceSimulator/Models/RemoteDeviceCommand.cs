
namespace SmartX.DeviceSimulator.Models;

public sealed record RemoteDeviceCommand
{
    public Guid CommandId { get; init; }

    public int SensorId { get; init; }

    public string Target { get; init; } = "";

    public bool RequestedState { get; init; }

    public string Status { get; init; } = "";
}
