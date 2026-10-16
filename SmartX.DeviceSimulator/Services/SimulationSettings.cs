using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class SimulationSettings
{

    public string ApiUrl { get; init; } =
        "http://localhost:5000";

    public int Esp32IntervalMs { get; init; } = 2000;

    public int SmartPlugIntervalMs { get; init; } = 3000;

    public int ValveIntervalMs { get; init; } = 4000;

    public int PumpIntervalMs { get; init; } = 5000;

    // Zero means run until Ctrl+C.
    public int DurationSeconds { get; init; } = 0;

    // Zero means unlimited readings per device.
    public int MaxReadingsPerDevice { get; init; } = 0;

    public static SimulationSettings Load()
    {
        string url =
            Environment.GetEnvironmentVariable(
                "SMARTX_API_URL"
            )?.Trim()
            ?? "http://localhost:5000";

        if (
            !Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri
            ) ||
            (
                uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps
            )
        )
        {
            throw new ArgumentException(
                "SMARTX_API_URL must be a valid " +
                "HTTP or HTTPS address."
            );
        }

        return new SimulationSettings
        {
            ApiUrl = url.TrimEnd('/'),

            Esp32IntervalMs = ReadInt(
                "SMARTX_ESP32_INTERVAL_MS",
                2000,
                250,
                60000
            ),

            SmartPlugIntervalMs = ReadInt(
                "SMARTX_PLUG_INTERVAL_MS",
                3000,
                250,
                60000
            ),

            ValveIntervalMs = ReadInt(
                "SMARTX_VALVE_INTERVAL_MS",
                4000,
                250,
                60000
            ),

            PumpIntervalMs = ReadInt(
                "SMARTX_PUMP_INTERVAL_MS",
                5000,
                250,
                60000
            ),

            DurationSeconds = ReadInt(
                "SMARTX_SIM_DURATION_SECONDS",
                0,
                0,
                86400
            ),

            MaxReadingsPerDevice = ReadInt(
                "SMARTX_SIM_MAX_READINGS_PER_DEVICE",
                0,
                0,
                1000000
            )
        };
    }

    public TimeSpan GetInterval(DeviceKind kind)
    {
        int milliseconds = kind switch
        {
            DeviceKind.EnvironmentalEsp32 =>
                Esp32IntervalMs,

            DeviceKind.SmartPlug =>
                SmartPlugIntervalMs,

            DeviceKind.IrrigationValve =>
                ValveIntervalMs,

            DeviceKind.PumpController =>
                PumpIntervalMs,

            _ => throw new NotSupportedException(
                $"Unsupported device kind: {kind}"
            )
        };

        return TimeSpan.FromMilliseconds(
            milliseconds
        );
    }

    private static int ReadInt(
        string name,
        int defaultValue,
        int minimum,
        int maximum)
    {
        string? raw =
            Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (
            !int.TryParse(raw, out int value) ||
            value < minimum ||
            value > maximum
        )
        {
            throw new ArgumentException(
                $"{name} must be an integer " +
                $"between {minimum} and {maximum}."
            );
        }

        return value;
    }
}
