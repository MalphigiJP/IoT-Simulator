using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class SimulationSettings
{
    public string ApiUrl { get; init; } =
        "http://localhost:5000";

    // Device reporting intervals
    public int Esp32IntervalMs { get; init; } = 2000;
    public int SmartPlugIntervalMs { get; init; } = 3000;
    public int ValveIntervalMs { get; init; } = 4000;
    public int PumpIntervalMs { get; init; } = 5000;

    // Zero = unlimited
    public int DurationSeconds { get; init; } = 0;
    public int MaxReadingsPerDevice { get; init; } = 0;

    public bool FaultsEnabled { get; init; } = false;

    // Inject a fault on every Nth reading
    // for each device.
    public int FaultEveryN { get; init; } = 8;

    public bool OutageEnabled { get; init; } = false;

    public DeviceKind OutageDevice { get; init; } =
        DeviceKind.IrrigationValve;

    // Start outage after this many attempted
    // telemetry readings.
    public int OutageAfterReadings { get; init; } = 4;

    // Longer than the API's 120-second threshold.
    public int OutageSeconds { get; init; } = 140;

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

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public static SimulationSettings Load()
    {
        string apiUrl =
            Environment.GetEnvironmentVariable(
                "SMARTX_API_URL"
            )?.Trim()
            ?? "http://localhost:5000";

        if (!Uri.TryCreate(
                apiUrl,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "SMARTX_API_URL must be a valid HTTP address."
            );
        }

        return new SimulationSettings
        {
            ApiUrl = apiUrl.TrimEnd('/'),

            Esp32IntervalMs = ReadInt(
                "SMARTX_ESP32_INTERVAL_MS", 2000, 250, 60000),

            SmartPlugIntervalMs = ReadInt(
                "SMARTX_PLUG_INTERVAL_MS", 3000, 250, 60000),

            ValveIntervalMs = ReadInt(
                "SMARTX_VALVE_INTERVAL_MS", 4000, 250, 60000),

            PumpIntervalMs = ReadInt(
                "SMARTX_PUMP_INTERVAL_MS", 5000, 250, 60000),

            DurationSeconds = ReadInt(
                "SMARTX_SIM_DURATION_SECONDS", 0, 0, 86400),

            MaxReadingsPerDevice = ReadInt(
                "SMARTX_SIM_MAX_READINGS_PER_DEVICE",
                0, 0, 1000000),

            FaultsEnabled = ReadBool(
                "SMARTX_FAULTS_ENABLED", false),

            FaultEveryN = ReadInt(
                "SMARTX_FAULT_EVERY_N", 8, 2, 100000),

            OutageEnabled = ReadBool(
                "SMARTX_OUTAGE_ENABLED", false),

            OutageDevice = ReadDeviceKind(
                "SMARTX_OUTAGE_DEVICE",
                DeviceKind.IrrigationValve),

            OutageAfterReadings = ReadInt(
                "SMARTX_OUTAGE_AFTER_READINGS",
                4, 1, 1000000),

            OutageSeconds = ReadInt(
                "SMARTX_OUTAGE_SECONDS",
                140, 135, 3600)
        };
    }

    private static int ReadInt(
        string name,
        int defaultValue,
        int min,
        int max)
    {
        string? raw =
            Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (!int.TryParse(raw, out int value) ||
            value < min || value > max)
        {
            throw new ArgumentException(
                $"{name} must be between {min} and {max}."
            );
        }

        return value;
    }

    private static bool ReadBool(
        string name,
        bool defaultValue)
    {
        string? raw =
            Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (bool.TryParse(raw, out bool value))
            return value;

        throw new ArgumentException(
            $"{name} must be true or false."
        );
    }

    private static DeviceKind ReadDeviceKind(
        string name,
        DeviceKind defaultValue)
    {
        string? raw =
            Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        if (Enum.TryParse<DeviceKind>(
                raw, true, out var kind) &&
            Enum.IsDefined(kind))
        {
            return kind;
        }

        throw new ArgumentException(
            $"Invalid device type for {name}: {raw}"
        );
    }
}
