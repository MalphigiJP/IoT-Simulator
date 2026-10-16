using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;


// Only the fields needed from the existing
// GET /api/sensors and POST /api/sensors responses.
public sealed record RegisteredSensor
{
    public int Id { get; init; }

    public string MacAddress { get; init; } = "";
}

// Result of ensuring a sensor is registered.
public sealed record DeviceRegistrationResult(
    int SensorId,
    bool Created
);

// Response from POST /api/telemetry.
public sealed record TelemetryAcceptance
{
    public Guid PacketId { get; init; }

    public int SensorId { get; init; }

    public string Status { get; init; } = "";
}

// Outcome of an HTTP transmission attempt.
public sealed record TelemetrySendResult(
    bool Accepted,
    bool QueueFull,
    int Attempts,
    Guid? PacketId,
    string Message
);

public sealed class SmartXApiClient
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _http;

    private readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull
        };

    public SmartXApiClient(HttpClient http)
    {
        _http = http;
    }

    private async Task<List<RegisteredSensor>>
        GetRegisteredSensorsAsync(
            CancellationToken cancellationToken)
    {
        return await _http.GetFromJsonAsync<
            List<RegisteredSensor>>(
                "api/sensors",
                _jsonOptions,
                cancellationToken
            ) ?? new List<RegisteredSensor>();
    }

    private static RegisteredSensor? FindByMac(
        IEnumerable<RegisteredSensor> sensors,
        string macAddress)
    {
        return sensors.FirstOrDefault(sensor =>
            string.Equals(
                sensor.MacAddress.Trim(),
                macAddress.Trim(),
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    public async Task<DeviceRegistrationResult>
        EnsureRegisteredAsync(
            DeviceProfile profile,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // First check whether SQL Server already
        // contains this MAC address.
        var sensors = await GetRegisteredSensorsAsync(
            cancellationToken
        );

        var existing = FindByMac(
            sensors,
            profile.MacAddress
        );

        if (existing is not null)
        {
            return new DeviceRegistrationResult(
                existing.Id,
                Created: false
            );
        }

        // Not registered yet:
        // create the device through the real API.
        var registration = new
        {
            macAddress = profile.MacAddress,
            location = profile.Location,
            category = profile.Category
        };

        using var response = await _http.PostAsJsonAsync(
            "api/sensors",
            registration,
            _jsonOptions,
            cancellationToken
        );

        // Another process might register the MAC
        // between our GET and POST requests.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var refreshed = await GetRegisteredSensorsAsync(
                cancellationToken
            );

            var matching = FindByMac(
                refreshed,
                profile.MacAddress
            );

            if (matching is not null)
            {
                return new DeviceRegistrationResult(
                    matching.Id,
                    Created: false
                );
            }

            throw new InvalidOperationException(
                "Sensor registration returned HTTP 409, " +
                "but no matching MAC address was found."
            );
        }

        if (response.StatusCode != HttpStatusCode.Created)
        {
            string details = await response.Content
                .ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Registration failed for {profile.Name}. " +
                $"HTTP {(int)response.StatusCode}: {details}"
            );
        }

        var created = await response.Content
            .ReadFromJsonAsync<RegisteredSensor>(
                _jsonOptions,
                cancellationToken
            );

        if (created is null || created.Id <= 0)
        {
            throw new InvalidOperationException(
                "The API returned HTTP 201, but did not " +
                "provide a valid registered sensor ID."
            );
        }

        return new DeviceRegistrationResult(
            created.Id,
            Created: true
        );
    }

    public async Task<TelemetrySendResult>
        SendTelemetryAsync(
            SimulatedTelemetry reading,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        if (reading.SensorId is null or <= 0)
        {
            throw new ArgumentException(
                "Telemetry requires a registered SensorId."
            );
        }

        for (int attempt = 1;
             attempt <= MaxAttempts;
             attempt++)
        {
            using var response = await _http.PostAsJsonAsync(
                "api/telemetry",
                reading,
                _jsonOptions,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                var receipt = await response.Content
                    .ReadFromJsonAsync<TelemetryAcceptance>(
                        _jsonOptions,
                        cancellationToken
                    );

                if (
                    receipt is null ||
                    receipt.PacketId == Guid.Empty ||
                    receipt.SensorId != reading.SensorId ||
                    receipt.Status != "Queued"
                )
                {
                    throw new InvalidOperationException(
                        "The API returned HTTP 202 with " +
                        "an unexpected telemetry receipt."
                    );
                }

                return new TelemetrySendResult(
                    Accepted: true,
                    QueueFull: false,
                    Attempts: attempt,
                    PacketId: receipt.PacketId,
                    Message: "HTTP 202 Accepted (Queued)"
                );
            }

            if (
                response.StatusCode ==
                HttpStatusCode.TooManyRequests
            )
            {
                if (attempt < MaxAttempts)
                {
                    // Bounded exponential backoff.
                    int delayMilliseconds =
                        250 * (1 << (attempt - 1));

                    await Task.Delay(
                        delayMilliseconds,
                        cancellationToken
                    );

                    continue;
                }

                return new TelemetrySendResult(
                    Accepted: false,
                    QueueFull: true,
                    Attempts: attempt,
                    PacketId: null,
                    Message:
                        "HTTP 429 - queue remained full"
                );
            }

            string errorBody = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return new TelemetrySendResult(
                Accepted: false,
                QueueFull: false,
                Attempts: attempt,
                PacketId: null,
                Message:
                    $"HTTP {(int)response.StatusCode}: " +
                    errorBody
            );
        }

        throw new InvalidOperationException(
            "Unexpected telemetry retry state."
        );
    }
}
