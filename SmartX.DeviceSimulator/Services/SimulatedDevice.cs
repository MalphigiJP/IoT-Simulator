
using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class SimulatedDevice
{
    public DeviceProfile Profile { get; }

    private readonly Random _random;

    private int _readingIndex;


    private bool? _controlOverride;

    public SimulatedDevice(DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = profile;

        _random = new Random(profile.RandomSeed);
        _readingIndex = 0;
    }

    public string? GetControlTarget()
    {
        return Profile.Kind switch
        {
            DeviceKind.SmartPlug => "Relay",

            DeviceKind.IrrigationValve => "Valve",

            DeviceKind.PumpController => "Pump",

            _ => null
        };
    }

    public bool? GetCurrentControlState()
    {
        if (GetControlTarget() is null)
        {
            return null;
        }

        if (_controlOverride.HasValue)
        {
            return _controlOverride.Value;
        }

        // Before the first reading, describe the
        // state that the first reading will use.
        int index = Math.Max(0, _readingIndex - 1);

        return Profile.Kind switch
        {
            DeviceKind.SmartPlug =>
                index % 8 < 4,

            DeviceKind.IrrigationValve =>
                index % 6 < 3,

            DeviceKind.PumpController =>
                index % 8 < 5,

            _ => null
        };
    }

    public bool TryApplyControl(
        string target,
        bool state,
        out string? error)
    {
        string? supported = GetControlTarget();

        if (supported is null)
        {
            error = "Device is read-only.";
            return false;
        }

        if (!string.Equals(
                supported,
                target,
                StringComparison.OrdinalIgnoreCase))
        {
            error =
                $"Device does not support {target}. " +
                $"Supported target: {supported}.";

            return false;
        }

        // This is the actual simulated actuator
        // state change. It persists across readings.
        _controlOverride = state;

        error = null;
        return true;
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
                $"Unsupported device kind: {Profile.Kind}"
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
        bool powered =
            _controlOverride ?? (index % 8 < 4);

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


    private SimulatedTelemetry GenerateIrrigationValve(
        int index)
    {
        bool open =
            _controlOverride ?? (index % 6 < 3);

        double moisture = open
            ? 62.0 + Noise(2.0)
            : 51.0 + Noise(2.0);

        return new SimulatedTelemetry
        {
            ValveOpen = open,

            SoilMoisture = Round(
                Math.Clamp(moisture, 0, 100)
            )
        };
    }

    private SimulatedTelemetry GeneratePump(
        int index)
    {
        bool running =
            _controlOverride ?? (index % 8 < 5);

        int wattage = running
            ? Math.Max(
                0,
                (int)Math.Round(
                    850 + Noise(55)
                )
            )
            : 0;

        return new SimulatedTelemetry
        {
            Wattage = wattage
        };
    }

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
