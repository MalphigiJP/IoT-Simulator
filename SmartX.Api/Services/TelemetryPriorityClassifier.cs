
using SmartX.Api.Models;

namespace SmartX.Api.Services;

// Lower numeric value = higher priority.
public enum TelemetryPriority
{
    Emergency = 0,
    Critical = 1,
    Normal = 2
}

public static class TelemetryPriorityClassifier
{
    public static TelemetryPriority Classify(
        QueuedTelemetry packet)
    {

        bool emergencyTemperature =
            packet.Temperature is >= 70;

        bool emergencyMoisture =
            packet.SoilMoisture is <= 5;

        bool emergencyPower =
            packet.Wattage is >= 9000;

        if (
            emergencyTemperature ||
            emergencyMoisture ||
            emergencyPower
        )
        {
            return TelemetryPriority.Emergency;
        }

        bool criticalTemperature =
            packet.Temperature is > 50;

        bool criticalMoisture =
            packet.SoilMoisture is < 10;

        bool criticalPower =
            packet.Wattage is > 6000;

        if (
            criticalTemperature ||
            criticalMoisture ||
            criticalPower
        )
        {
            return TelemetryPriority.Critical;
        }

        return TelemetryPriority.Normal;
    }
}
