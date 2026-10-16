
using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class SimulatedDevice
{
    public DeviceProfile Profile { get; }

    private readonly Random _random;

    private int _readingIndex;

    public SimulatedDevice(DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;

        _random = new Random(profile.RandomSeed);

        _readingIndex = 0;
    }

    public SimulatedTelemetry GenerateReading()
    {
        int index = _readingIndex++;

        var reading = Profile.Kind switch
        {
            DeviceKind.EnvironmentalEsp32 =>
                GenerateEnvironmental(index),

            DeviceKind.SmartPlug =>
                GenerateSmartPlug(index),

            DeviceKind.IrrigationValve =>
                GenerateIrrigationValve(index),

            DeviceKind.PumpController =>
                GeneratePump(index),

            _ => throw new NotSupportedException(
                $"Unsupported device type: {Profile.Kind}"
            )
        };

        return reading with
        {
            Timestamp = DateTime.UtcNow
        };
    }

    private SimulatedTelemetry GenerateEnvironmental(
        int index)
    {
        // Gradual changes instead of completely
        // random readings on every sample.
        double temperature =
            24.0 +
            Math.Sin(index / 4.0) * 2.0 +
            Noise(0.6);

        double moisture =
            60.0 +
            Math.Sin(index / 6.0) * 8.0 +
            Noise(2.0);

        return new SimulatedTelemetry
        {
            Temperature = Round(
                Math.Clamp(temperature, 18, 32)
            ),

            SoilMoisture = Round(
                Math.Clamp(moisture, 35, 85)
            )
        };
    }

    private SimulatedTelemetry GenerateSmartPlug(
        int index)
    {
        // Simulate equipment that periodically
        // switches off and resumes operation.
        bool powered = index % 8 < 4;

        int wattage = 0;

        if (powered)
        {
            double consumption =
                1100 +
                Math.Sin(index / 3.0) * 180 +
                Noise(60);

            wattage = Math.Max(
                0,
                (int)Math.Round(consumption)
            );
        }

        return new SimulatedTelemetry
        {
            Wattage = wattage
        };
    }

    // IRRIGATION VALVE CONTROLLER
    private SimulatedTelemetry GenerateIrrigationValve(
        int index)
    {
        // Alternate between three open and
        // three closed reporting cycles.
        bool valveOpen = index % 6 < 3;

        double moisture = valveOpen
            ? 62.0 + Noise(2.0)
            : 51.0 + Noise(2.0);

        return new SimulatedTelemetry
        {
            ValveOpen = valveOpen,

            SoilMoisture = Round(
                Math.Clamp(moisture, 0, 100)
            )
        };
    }

    private SimulatedTelemetry GeneratePump(
        int index)
    {
        // Five reporting cycles running,
        // followed by three cycles stopped.
        bool running = index % 8 < 5;

        int wattage = running
            ? Math.Max(
                0,
                (int)Math.Round(
                    850 + Noise(55)
                )
            )
            : 0;

        // ValveOpen is deliberately omitted:
        // pump state is not the same as valve state.
        return new SimulatedTelemetry
        {
            Wattage = wattage
        };
    }

    // HELPER METHODS
    private double Noise(double amplitude)
    {
        return (
            _random.NextDouble() * 2.0 - 1.0
        ) * amplitude;
    }

    private static double Round(double value)
    {
        return Math.Round(value, 2);
    }
}
