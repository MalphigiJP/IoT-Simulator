
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed class DeviceRegistry
{
    // Primary dictionary: MAC address -> device state.
    private readonly Dictionary<string, DeviceState>
        _devicesByMac = new(
            StringComparer.OrdinalIgnoreCase
        );

    // Secondary index: Sensor ID -> MAC address.
    private readonly Dictionary<int, string>
        _macBySensorId = new();

    private readonly object _sync = new();

    private static string NormalizeMac(string mac)
    {
        return mac.Trim().ToUpperInvariant();
    }

    public void UpsertSensor(Sensor sensor)
    {
        var mac = NormalizeMac(sensor.MacAddress);

        if (string.IsNullOrWhiteSpace(mac))
        {
            throw new ArgumentException(
                "A device must have a MAC address."
            );
        }

        lock (_sync)
        {
            // Prevent different sensors from sharing
            // the same dictionary key.
            if (
                _devicesByMac.TryGetValue(
                    mac,
                    out var conflictingDevice
                ) &&
                conflictingDevice.SensorId != sensor.Id
            )
            {
                throw new InvalidOperationException(
                    "Another registered device uses this MAC address."
                );
            }

            DeviceState? previous = null;

            if (
                _macBySensorId.TryGetValue(
                    sensor.Id,
                    out var previousMac
                )
            )
            {
                _devicesByMac.TryGetValue(
                    previousMac,
                    out previous
                );

                // Support changing a device's MAC address.
                if (
                    !string.Equals(
                        previousMac,
                        mac,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    _devicesByMac.Remove(previousMac);
                }
            }

            var updated = (previous ?? new DeviceState()) with
            {
                SensorId = sensor.Id,
                MacAddress = mac,
                Location = sensor.Location,
                Category = sensor.Category,
                RegisteredAtUtc = sensor.RegisteredAt
            };

            _devicesByMac[mac] = updated;

            _macBySensorId[sensor.Id] = mac;
        }
    }

    public bool TryGetByMac(
        string macAddress,
        out DeviceState? device)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            device = null;
            return false;
        }

        var mac = NormalizeMac(macAddress);

        lock (_sync)
        {
            return _devicesByMac.TryGetValue(
                mac,
                out device
            );
        }
    }

    public bool TryGetById(
        int sensorId,
        out DeviceState? device)
    {
        lock (_sync)
        {
            device = null;

            if (!_macBySensorId.TryGetValue(
                sensorId,
                out var mac))
            {
                return false;
            }

            return _devicesByMac.TryGetValue(
                mac,
                out device
            );
        }
    }

    public bool RecordTelemetry(QueuedTelemetry packet)
    {
        lock (_sync)
        {
            if (
                !_macBySensorId.TryGetValue(
                    packet.SensorId,
                    out var mac
                ) ||
                !_devicesByMac.TryGetValue(
                    mac,
                    out var current
                )
            )
            {
                return false;
            }

            bool isNewer =
                current.LastReceivedAtUtc is null ||
                packet.ReceivedAtUtc >=
                    current.LastReceivedAtUtc.Value;

            DeviceState updated;

            if (isNewer)
            {
                updated = current with
                {
                    Temperature =
                        packet.Temperature ??
                        current.Temperature,

                    SoilMoisture =
                        packet.SoilMoisture ??
                        current.SoilMoisture,

                    Wattage =
                        packet.Wattage ??
                        current.Wattage,

                    ValveOpen =
                        packet.ValveOpen ??
                        current.ValveOpen,

                    LastSampleTimestampUtc =
                        packet.Timestamp,

                    LastReceivedAtUtc =
                        packet.ReceivedAtUtc,

                    PacketsProcessed =
                        current.PacketsProcessed + 1
                };
            }
            else
            {
                updated = current with
                {
                    PacketsProcessed =
                        current.PacketsProcessed + 1
                };
            }

            _devicesByMac[mac] = updated;

            return true;
        }
    }

    public bool RemoveSensor(int sensorId)
    {
        lock (_sync)
        {
            if (
                !_macBySensorId.TryGetValue(
                    sensorId,
                    out var mac
                )
            )
            {
                return false;
            }

            _macBySensorId.Remove(sensorId);
            _devicesByMac.Remove(mac);

            return true;
        }
    }

    public List<DeviceState> GetAll()
    {
        lock (_sync)
        {
            return _devicesByMac.Values
                .OrderBy(device => device.SensorId)
                .ToList();
        }
    }

    public static string GetConnectionStatus(
        DeviceState device,
        DateTime nowUtc)
    {
        if (device.LastReceivedAtUtc is null)
        {
            return "AwaitingTelemetry";
        }

        double seconds = (
            nowUtc - device.LastReceivedAtUtc.Value
        ).TotalSeconds;

        return seconds switch
        {
            <= 30 => "Online",
            <= 120 => "Delayed",
            _ => "Offline"
        };
    }
}
