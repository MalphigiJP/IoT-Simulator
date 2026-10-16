using System;
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class ActiveAlertServiceTests
{
    private static QueuedTelemetry Packet(
        DateTime receivedAt,
        double? temperature = null,
        double? moisture = null,
        int? wattage = null)
    {
        return new QueuedTelemetry
        {
            PacketId = Guid.NewGuid(),
            SensorId = 1,

            Temperature = temperature,
            SoilMoisture = moisture,
            Wattage = wattage,

            Timestamp = receivedAt,
            ReceivedAtUtc = receivedAt
        };
    }

    private static DeviceState Device(
        DateTime? receivedAt,
        double? temperature = null,
        double? moisture = null,
        int? wattage = null)
    {
        return new DeviceState
        {
            SensorId = 1,

            MacAddress = "AA:BB:CC:DD:EE:01",
            Location = "Greenhouse A",
            Category = "Environmental",

            Temperature = temperature,
            SoilMoisture = moisture,
            Wattage = wattage,

            LastReceivedAtUtc = receivedAt
        };
    }

    [Fact]
    public void RepeatedFaultProducesOneActiveAlert()
    {
        var alerts = new ActiveAlertService();

        var firstTime = DateTime.UtcNow;
        var secondTime = firstTime.AddSeconds(1);

        alerts.ProcessTelemetry(
            Packet(firstTime, temperature: 80),
            Device(firstTime, temperature: 80)
        );

        alerts.ProcessTelemetry(
            Packet(secondTime, temperature: 82),
            Device(secondTime, temperature: 82)
        );

        var active = alerts.GetActiveAlerts();

        Assert.Single(active);

        Assert.Equal(2, active[0].Observations);

        Assert.Equal(
            "Emergency",
            active[0].Severity
        );

        var status = alerts.GetStatus();

        Assert.Equal(1, status.Activations);
        Assert.Equal(1, status.RepeatedObservations);
    }

    [Fact]
    public void NormalTemperatureResolvesAlert()
    {
        var alerts = new ActiveAlertService();

        var firstTime = DateTime.UtcNow;
        var nextTime = firstTime.AddSeconds(1);

        alerts.ProcessTelemetry(
            Packet(firstTime, temperature: 80),
            Device(firstTime, temperature: 80)
        );

        Assert.Single(alerts.GetActiveAlerts());

        alerts.ProcessTelemetry(
            Packet(nextTime, temperature: 25),
            Device(nextTime, temperature: 25)
        );

        Assert.Empty(alerts.GetActiveAlerts());

        Assert.Equal(
            1,
            alerts.GetStatus().Resolutions
        );
    }

    [Fact]
    public void CriticalAlertEscalatesWithoutDuplication()
    {
        var alerts = new ActiveAlertService();

        var firstTime = DateTime.UtcNow;
        var nextTime = firstTime.AddSeconds(1);

        alerts.ProcessTelemetry(
            Packet(firstTime, temperature: 58),
            Device(firstTime, temperature: 58)
        );

        Assert.Equal(
            "Critical",
            alerts.GetActiveAlerts()[0].Severity
        );

        alerts.ProcessTelemetry(
            Packet(nextTime, temperature: 82),
            Device(nextTime, temperature: 82)
        );

        var active = alerts.GetActiveAlerts();

        Assert.Single(active);

        Assert.Equal(
            "Emergency",
            active[0].Severity
        );

        Assert.Equal(
            1,
            alerts.GetStatus().Activations
        );
    }

    [Fact]
    public void OneSensorCanHaveSeveralDifferentFaults()
    {
        var alerts = new ActiveAlertService();

        var now = DateTime.UtcNow;

        alerts.ProcessTelemetry(
            Packet(
                now,
                temperature: 80,
                moisture: 2,
                wattage: 9500
            ),
            Device(
                now,
                temperature: 80,
                moisture: 2,
                wattage: 9500
            )
        );

        var status = alerts.GetStatus();

        Assert.Equal(3, status.ActiveFaults);
        Assert.Equal(3, status.EmergencyAlerts);
    }

    [Fact]
    public void RepeatedConnectivityChecksDoNotDuplicateAlert()
    {
        var alerts = new ActiveAlertService();

        var now = DateTime.UtcNow;

        var disconnected = Device(
            now.AddSeconds(-130)
        );

        alerts.CheckConnectivity(
            new[] { disconnected },
            now
        );

        alerts.CheckConnectivity(
            new[] { disconnected },
            now.AddSeconds(10)
        );

        Assert.Single(
            alerts.GetDisconnectedSensorIds()
        );

        Assert.Single(
            alerts.GetActiveAlerts()
        );

        Assert.Equal(
            1,
            alerts.GetStatus().Activations
        );
    }

    [Fact]
    public void NewTelemetryResolvesDisconnectedAlert()
    {
        var alerts = new ActiveAlertService();

        var now = DateTime.UtcNow;

        alerts.CheckConnectivity(
            new[]
            {
                Device(now.AddMinutes(-3))
            },
            now
        );

        Assert.Single(
            alerts.GetDisconnectedSensorIds()
        );

        var recoveredAt = now.AddSeconds(1);

        alerts.ProcessTelemetry(
            Packet(
                recoveredAt,
                temperature: 25
            ),
            Device(
                recoveredAt,
                temperature: 25
            )
        );

        Assert.Empty(
            alerts.GetDisconnectedSensorIds()
        );

        Assert.Empty(
            alerts.GetActiveAlerts()
        );
    }

    [Fact]
    public void OlderPacketCannotReplaceCurrentAlertState()
    {
        var alerts = new ActiveAlertService();

        var now = DateTime.UtcNow;

        // First process the newer emergency.
        alerts.ProcessTelemetry(
            Packet(now, temperature: 80),
            Device(now, temperature: 80)
        );

        // Then process an older normal packet.
        alerts.ProcessTelemetry(
            Packet(
                now.AddSeconds(-5),
                temperature: 25
            ),
            Device(
                now,
                temperature: 80
            )
        );

        var active = alerts.GetActiveAlerts();

        Assert.Single(active);

        Assert.Equal(
            80.0,
            active[0].CurrentValue
        );

        Assert.Equal(
            AlertKind.HighTemperature,
            active[0].Kind
        );
    }

    [Fact]
    public void RemovingSensorClearsItsActiveAlerts()
    {
        var alerts = new ActiveAlertService();

        var now = DateTime.UtcNow;

        alerts.ProcessTelemetry(
            Packet(now, temperature: 80),
            Device(now, temperature: 80)
        );

        Assert.Single(
            alerts.GetActiveAlerts()
        );

        alerts.RemoveSensor(1);

        Assert.Empty(
            alerts.GetActiveAlerts()
        );

        Assert.Equal(
            0,
            alerts.GetStatus().ActiveFaults
        );
    }

    [Fact]
    public void SensorAwaitingTelemetryIsNotDisconnected()
    {
        var alerts = new ActiveAlertService();

        alerts.CheckConnectivity(
            new[]
            {
                Device(null)
            },
            DateTime.UtcNow
        );

        Assert.Empty(
            alerts.GetActiveAlerts()
        );

        Assert.Empty(
            alerts.GetDisconnectedSensorIds()
        );
    }
}
