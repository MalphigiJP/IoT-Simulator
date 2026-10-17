
namespace SmartX.DeviceSimulator.Services;

public sealed record LoadTestSettings
{
    public string ApiUrl { get; init; } =
        "http://localhost:5000";

    public int DeviceCount { get; init; } = 100;

    public int RequestsPerSecond { get; init; } = 25;

    public int DurationSeconds { get; init; } = 20;

    public int HttpWorkers { get; init; } = 12;

    public int RegistrationWorkers { get; init; } = 6;

    public int DrainSeconds { get; init; } = 30;

    public static LoadTestSettings Load()
    {
        string url =
            Environment.GetEnvironmentVariable(
                "SMARTX_API_URL"
            )?.Trim()
            ?? "http://localhost:5000";

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "SMARTX_API_URL must be a valid HTTP or HTTPS URL."
            );
        }

        return new LoadTestSettings
        {
            ApiUrl = url.TrimEnd('/'),

            DeviceCount = ReadInt(
                "SMARTX_LOAD_DEVICES", 100, 1, 2000),

            RequestsPerSecond = ReadInt(
                "SMARTX_LOAD_RPS", 25, 1, 300),

            DurationSeconds = ReadInt(
                "SMARTX_LOAD_SECONDS", 20, 5, 180),

            HttpWorkers = ReadInt(
                "SMARTX_LOAD_WORKERS", 12, 1, 64),

            RegistrationWorkers = ReadInt(
                "SMARTX_LOAD_REG_WORKERS", 6, 1, 16),

            DrainSeconds = ReadInt(
                "SMARTX_LOAD_DRAIN_SECONDS", 30, 0, 120)
        };
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

        if (!int.TryParse(raw, out int value) ||
            value < minimum ||
            value > maximum)
        {
            throw new ArgumentException(
                $"{name} must be between " +
                $"{minimum} and {maximum}."
            );
        }

        return value;
    }
}
