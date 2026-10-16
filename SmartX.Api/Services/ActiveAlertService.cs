
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record ActiveAlertStatus
{
    public int ActiveAlerts { get; init; }
    public int ActiveFaults { get; init; }
    public int DisconnectedSensors { get; init; }

    public int EmergencyAlerts { get; init; }
    public int CriticalAlerts { get; init; }

    public long Activations { get; init; }
    public long Resolutions { get; init; }
    public long RepeatedObservations { get; init; }
}

public sealed class ActiveAlertService
{
    private static readonly TimeSpan OfflineThreshold =
        TimeSpan.FromSeconds(120);

    // Required HashSet: one entry for each active
    // sensor + fault-type combination.
    private readonly HashSet<AlertKey> _activeFaults = new();

    // Required HashSet: unique disconnected sensor IDs.
    private readonly HashSet<int> _disconnectedSensors = new();

    // Details of active faults and disconnections.
    private readonly Dictionary<AlertKey, ActiveAlert>
        _activeAlerts = new();

    private readonly object _sync = new();

    private long _activations;
    private long _resolutions;
    private long _repeatedObservations;

    private void ActivateOrUpdate(
        DeviceState device,
        AlertKind kind,
        string severity,
        string message,
        double? value,
        DateTime observedAtUtc)
    {
        var key = new AlertKey(device.SensorId, kind);

        if (_activeAlerts.TryGetValue(
            key,
            out var existing))
        {
            // Same fault, so update rather than
            // generate a new alert.
            _activeAlerts[key] = existing with
            {
                MacAddress = device.MacAddress,
                Location = device.Location,
                Category = device.Category,

                Severity = severity,
                Message = message,
                CurrentValue = value,

                LastObservedAtUtc = observedAtUtc,
                Observations = existing.Observations + 1
            };

            _repeatedObservations++;
            return;
        }

        _activeAlerts[key] = new ActiveAlert
        {
            SensorId = device.SensorId,
            MacAddress = device.MacAddress,
            Location = device.Location,
            Category = device.Category,

            Kind = kind,
            Severity = severity,
            Message = message,
            CurrentValue = value,

            ActivatedAtUtc = observedAtUtc,
            LastObservedAtUtc = observedAtUtc,
            Observations = 1
        };

        if (kind == AlertKind.Disconnected)
        {
            _disconnectedSensors.Add(device.SensorId);
        }
        else
        {
            _activeFaults.Add(key);
        }

        _activations++;
    }

    private void Resolve(AlertKey key)
    {
        if (!_activeAlerts.Remove(key))
        {
            return;
        }

        if (key.Kind == AlertKind.Disconnected)
        {
            _disconnectedSensors.Remove(key.SensorId);
        }
        else
        {
            _activeFaults.Remove(key);
        }

        _resolutions++;
    }

    private void ReconcileFault(
        DeviceState device,
        AlertKind kind,
        bool isActive,
        string severity,
        string message,
        double? value,
        DateTime observedAtUtc)
    {
        var key = new AlertKey(device.SensorId, kind);

        if (isActive)
        {
            ActivateOrUpdate(
                device,
                kind,
                severity,
                message,
                value,
                observedAtUtc
            );
        }
        else
        {
            Resolve(key);
        }
    }

    public void ProcessTelemetry(
        QueuedTelemetry packet,
        DeviceState device)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(device);

        if (
            device.LastReceivedAtUtc is null ||
            packet.ReceivedAtUtc <
                device.LastReceivedAtUtc.Value
        )
        {
            return;
        }

        var now = DateTime.UtcNow;

        lock (_sync)
        {
            // A newly received and saved packet
            // means the device has communicated again.
            Resolve(new AlertKey(
                device.SensorId,
                AlertKind.Disconnected
            ));

            if (packet.Temperature.HasValue &&
                device.Temperature.HasValue)
            {
                double value = device.Temperature.Value;

                bool emergency = value >= 70;
                bool critical = value > 50;

                ReconcileFault(
                    device,
                    AlertKind.HighTemperature,
                    critical,
                    emergency ? "Emergency" : "Critical",
                    emergency
                        ? "Emergency temperature threshold exceeded."
                        : "Critical temperature threshold exceeded.",
                    value,
                    now
                );
            }

            if (packet.SoilMoisture.HasValue &&
                device.SoilMoisture.HasValue)
            {
                double value = device.SoilMoisture.Value;

                bool emergency = value <= 5;
                bool critical = value < 10;

                ReconcileFault(
                    device,
                    AlertKind.LowSoilMoisture,
                    critical,
                    emergency ? "Emergency" : "Critical",
                    emergency
                        ? "Emergency soil moisture drop detected."
                        : "Critical soil moisture drop detected.",
                    value,
                    now
                );
            }

            if (packet.Wattage.HasValue &&
                device.Wattage.HasValue)
            {
                double value = device.Wattage.Value;

                bool emergency = value >= 9000;
                bool critical = value > 6000;

                ReconcileFault(
                    device,
                    AlertKind.PowerSpike,
                    critical,
                    emergency ? "Emergency" : "Critical",
                    emergency
                        ? "Emergency power spike detected."
                        : "Critical power spike detected.",
                    value,
                    now
                );
            }
        }
    }

    public void CheckConnectivity(
        IEnumerable<DeviceState> devices,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(devices);

        lock (_sync)
        {
            foreach (var device in devices)
            {
                var key = new AlertKey(
                    device.SensorId,
                    AlertKind.Disconnected
                );

                if (device.LastReceivedAtUtc is null)
                {
                    Resolve(key);
                    continue;
                }

                bool disconnected =
                    nowUtc - device.LastReceivedAtUtc.Value >
                    OfflineThreshold;

                if (disconnected)
                {
                    ActivateOrUpdate(
                        device,
                        AlertKind.Disconnected,
                        "Critical",
                        "No telemetry received for more than 120 seconds.",
                        null,
                        nowUtc
                    );
                }
                else
                {
                    Resolve(key);
                }
            }
        }
    }

    public void RemoveSensor(int sensorId)
    {
        lock (_sync)
        {
            var keys = _activeAlerts.Keys
                .Where(key => key.SensorId == sensorId)
                .ToList();

            foreach (var key in keys)
            {
                Resolve(key);
            }

            _disconnectedSensors.Remove(sensorId);
        }
    }

    public List<ActiveAlert> GetActiveAlerts()
    {
        lock (_sync)
        {
            return _activeAlerts.Values
                .OrderBy(alert =>
                    alert.Severity == "Emergency" ? 0 : 1)
                .ThenBy(alert => alert.ActivatedAtUtc)
                .ThenBy(alert => alert.SensorId)
                .ToList();
        }
    }

    public List<int> GetDisconnectedSensorIds()
    {
        lock (_sync)
        {
            return _disconnectedSensors
                .OrderBy(id => id)
                .ToList();
        }
    }

    public ActiveAlertStatus GetStatus()
    {
        lock (_sync)
        {
            return new ActiveAlertStatus
            {
                ActiveAlerts = _activeAlerts.Count,

                ActiveFaults = _activeFaults.Count,

                DisconnectedSensors =
                    _disconnectedSensors.Count,

                EmergencyAlerts = _activeAlerts.Values
                    .Count(a => a.Severity == "Emergency"),

                CriticalAlerts = _activeAlerts.Values
                    .Count(a => a.Severity == "Critical"),

                Activations = _activations,
                Resolutions = _resolutions,

                RepeatedObservations =
                    _repeatedObservations
            };
        }
    }
}
