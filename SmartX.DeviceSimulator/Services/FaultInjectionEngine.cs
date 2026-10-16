using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed record FaultInjectionResult(
    SimulatedTelemetry Reading,
    string? Description)
{
    public bool HasFault => Description is not null;
}

public sealed class FaultInjectionEngine
{
    private readonly SimulationSettings _settings;

    public FaultInjectionEngine(
        SimulationSettings settings)
    {
        _settings = settings;
    }

    public FaultInjectionResult Apply(
        DeviceKind kind,
        SimulatedTelemetry normalReading,
        int readingNumber)
    {
        // Leave normal readings untouched when
        // fault injection is disabled.
        if (!_settings.FaultsEnabled)
        {
            return new(normalReading, null);
        }

        // Only inject at the configured interval.
        if (readingNumber % _settings.FaultEveryN != 0)
        {
            return new(normalReading, null);
        }

        return kind switch
        {
            DeviceKind.EnvironmentalEsp32 =>
                new FaultInjectionResult(
                    normalReading with
                    {
                        Temperature = 82.0,
                        SoilMoisture = 2.0
                    },
                    "Emergency heat spike and moisture drop"
                ),

            DeviceKind.SmartPlug =>
                new FaultInjectionResult(
                    normalReading with
                    {
                        Wattage = 9500
                    },
                    "Emergency smart plug power surge"
                ),

            DeviceKind.IrrigationValve =>
                new FaultInjectionResult(
                    normalReading with
                    {
                        SoilMoisture = 2.0
                    },
                    "Emergency irrigation moisture drop"
                ),

            DeviceKind.PumpController =>
                new FaultInjectionResult(
                    normalReading with
                    {
                        Wattage = 9500
                    },
                    "Emergency pump power surge"
                ),

            _ => new FaultInjectionResult(
                normalReading,
                null
            )
        };
    }
}
