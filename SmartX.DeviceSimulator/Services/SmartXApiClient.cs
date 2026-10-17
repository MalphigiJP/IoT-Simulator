
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed record RegisteredSensor
{
    public int Id { get; init; }

    public string MacAddress { get; init; } = "";
}

public sealed record DeviceRegistrationResult(
    int SensorId,
    bool Created
);

public sealed record TelemetryAcceptance
{
    public Guid PacketId { get; init; }

    public int SensorId { get; init; }

    public string Status { get; init; } = "";
}

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
        string mac)
    {
        return sensors.FirstOrDefault(sensor =>
            string.Equals(
                sensor.MacAddress.Trim(),
                mac.Trim(),
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

        var sensors = await GetRegisteredSensorsAsync(
            cancellationToken
        );

        var existing = FindByMac(
            sensors, profile.MacAddress
        );

        if (existing is not null)
        {
            return new(
                existing.Id,
                Created: false
            );
        }

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

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var refreshed =
                await GetRegisteredSensorsAsync(
                    cancellationToken
                );

            var matching = FindByMac(
                refreshed,
                profile.MacAddress
            );

            if (matching is not null)
            {
                return new(
                    matching.Id,
                    Created: false
                );
            }

            throw new InvalidOperationException(
                "MAC conflict but no matching sensor found."
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
                "API did not return a valid sensor ID."
            );
        }

        return new(
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
                "Telemetry requires a valid SensorId."
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

            if (response.StatusCode ==
                HttpStatusCode.Accepted)
            {
                var receipt = await response.Content
                    .ReadFromJsonAsync<TelemetryAcceptance>(
                        _jsonOptions,
                        cancellationToken
                    );

                if (receipt is null ||
                    receipt.PacketId == Guid.Empty ||
                    receipt.SensorId != reading.SensorId ||
                    !string.Equals(
                        receipt.Status,
                        "Queued",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Unexpected telemetry receipt."
                    );
                }

                return new(
                    Accepted: true,
                    QueueFull: false,
                    Attempts: attempt,
                    PacketId: receipt.PacketId,
                    Message: "HTTP 202 Accepted"
                );
            }

            if (response.StatusCode ==
                HttpStatusCode.TooManyRequests)
            {
                if (attempt < MaxAttempts)
                {
                    int delay = 250 * (
                        1 << (attempt - 1)
                    );

                    await Task.Delay(
                        delay,
                        cancellationToken
                    );

                    continue;
                }

                return new(
                    Accepted: false,
                    QueueFull: true,
                    Attempts: attempt,
                    PacketId: null,
                    Message: "HTTP 429 Queue Full"
                );
            }

            string errorBody = await response.Content
                .ReadAsStringAsync(cancellationToken);

            return new(
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

    public async Task<RemoteDeviceCommand?>
        PollCommandAsync(
            int sensorId,
            CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            $"api/devices/{sensorId}/commands/pending",
            cancellationToken
        );

        // No outstanding command is normal.
        if (response.StatusCode ==
            HttpStatusCode.NoContent)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content
                .ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Command polling failed. " +
                $"HTTP {(int)response.StatusCode}: {body}"
            );
        }

        var command = await response.Content
            .ReadFromJsonAsync<RemoteDeviceCommand>(
                _jsonOptions,
                cancellationToken
            );

        if (command is null ||
            command.CommandId == Guid.Empty ||
            command.SensorId != sensorId)
        {
            throw new InvalidOperationException(
                "API returned an invalid device command."
            );
        }

        return command;
    }

    public async Task AcknowledgeCommandAsync(
        int sensorId,
        Guid commandId,
        bool applied,
        bool? reportedState,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            commandId,
            applied,
            reportedState,
            error
        };

        using var response = await _http.PostAsJsonAsync(
            $"api/devices/{sensorId}/commands/" +
            $"{commandId}/ack",
            payload,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content
                .ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Command acknowledgement failed. " +
                $"HTTP {(int)response.StatusCode}: {body}"
            );
        }
    }

    public async Task ReportControlStateAsync(
        int sensorId,
        string target,
        bool state,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            target,
            state
        };

        using var response = await _http.PostAsJsonAsync(
            $"api/devices/{sensorId}/control/report",
            payload,
            _jsonOptions,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content
                .ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Control-state report failed. " +
                $"HTTP {(int)response.StatusCode}: {body}"
            );
        }
    }
}
