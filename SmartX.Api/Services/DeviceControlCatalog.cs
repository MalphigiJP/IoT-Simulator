using SmartX.Api.Models;

namespace SmartX.Api.Services;

public static class DeviceControlCatalog
{

    public static DeviceControlTarget?
        GetSupportedTarget(string? category)
    {
        return category?.Trim().ToUpperInvariant() switch
        {
            "SMARTPLUG" =>
                DeviceControlTarget.Relay,

            "IRRIGATIONVALVE" =>
                DeviceControlTarget.Valve,

            "PUMPCONTROLLER" =>
                DeviceControlTarget.Pump,

            // Environmental sensors are read-only.
            "ENVIRONMENTAL" => null,

            // Unknown types cannot receive commands.
            _ => null
        };
    }

    public static bool Supports(
        string? category,
        DeviceControlTarget target)
    {
        return GetSupportedTarget(category) == target;
    }

    public static DeviceControlState CreateInitialState(
        Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        if (sensor.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sensor),
                "A registered sensor must have a valid ID."
            );
        }

        return new DeviceControlState
        {
            SensorId = sensor.Id,

            MacAddress = sensor.MacAddress,

            Category = sensor.Category,

            SupportedTarget = GetSupportedTarget(
                sensor.Category
            ),

            DesiredState = null,

            ReportedState = null,

            LastCommandId = null,

            LastDesiredChangeAtUtc = null,

            LastReportedAtUtc = null,

            Revision = 0
        };
    }
}
