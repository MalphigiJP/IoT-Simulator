using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceRegistryTests
{
    private static Sensor CreateSensor(
        int id,
        string mac,
        string location = "Greenhouse A")
    {
        return new Sensor
        {
            Id = id,
            MacAddress = mac,
            Location = location,
            Category = "Environmental",
            RegisteredAt = DateTime.UtcNow
        };
    }

    private static QueuedTelemetry CreatePacket(
        int sensorId,
        double temperature,
        DateTime receivedAt)
    {
        return new QueuedTelemetry
        {
            PacketId = Guid.NewGuid(),
            SensorId = sensorId,
            Temperature = temperature,
            Timestamp = receivedAt,
            ReceivedAtUtc = receivedAt
        };
    }

    [Fact]
    public void CanFindDeviceUsingMacAddress()
    {
        var registry = new DeviceRegistry();

        registry.UpsertSensor(
            CreateSensor(
                1,
                "AA:BB:CC:DD:EE:01"
            )
        );

        // MAC lookup should ignore letter case.
        bool found = registry.TryGetByMac(
            "aa:bb:cc:dd:ee:01",
            out var device
        );

        Assert.True(found);
        Assert.NotNull(device);
        Assert.Equal(1, device.SensorId);
    }

    [Fact]
    public void CanFindDeviceUsingSensorId()
    {
        var registry = new DeviceRegistry();

        registry.UpsertSensor(
            CreateSensor(
                12,
                "AA:BB:CC:DD:EE:12"
            )
        );

        bool found = registry.TryGetById(
            12,
            out var device
        );

        Assert.True(found);
        Assert.NotNull(device);

        Assert.Equal(
            "AA:BB:CC:DD:EE:12",
            device.MacAddress
        );
    }

    [Fact]
    public void UpdatingSensorPreservesTelemetry()
    {
        var registry = new DeviceRegistry();

        var sensor = CreateSensor(
            1,
            "AA:BB:CC:DD:EE:01"
        );

        registry.UpsertSensor(sensor);

        var now = DateTime.UtcNow;

        Assert.True(
            registry.RecordTelemetry(
                CreatePacket(1, 29.5, now)
            )
        );

        // Update metadata.
        sensor.Location = "Greenhouse B";

        registry.UpsertSensor(sensor);

        Assert.True(
            registry.TryGetById(
                1,
                out var state
            )
        );

        Assert.NotNull(state);
        Assert.Equal("Greenhouse B", state.Location);
        Assert.Equal(29.5, state.Temperature);
        Assert.Equal(1, state.PacketsProcessed);
    }

    [Fact]
    public void OlderPacketDoesNotReplaceNewerState()
    {
        var registry = new DeviceRegistry();

        registry.UpsertSensor(
            CreateSensor(
                1,
                "AA:BB:CC:DD:EE:01"
            )
        );

        var now = DateTime.UtcNow;

        var newer = CreatePacket(
            1,
            80,
            now
        );

        var older = CreatePacket(
            1,
            25,
            now.AddSeconds(-10)
        );

        registry.RecordTelemetry(newer);
        registry.RecordTelemetry(older);

        registry.TryGetById(1, out var state);

        Assert.NotNull(state);

        // Older packet cannot reset the reading.
        Assert.Equal(80, state.Temperature);

        // Both successfully processed packets count.
        Assert.Equal(2, state.PacketsProcessed);
    }

    [Fact]
    public void DeletingSensorRemovesBothIndexes()
    {
        var registry = new DeviceRegistry();

        registry.UpsertSensor(
            CreateSensor(
                1,
                "AA:BB:CC:DD:EE:01"
            )
        );

        Assert.True(registry.RemoveSensor(1));

        Assert.False(
            registry.TryGetById(1, out _)
        );

        Assert.False(
            registry.TryGetByMac(
                "AA:BB:CC:DD:EE:01",
                out _
            )
        );

        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void ChangingMacAddressUpdatesBothIndexes()
    {
        var registry = new DeviceRegistry();

        var sensor = CreateSensor(
            1,
            "AA:BB:CC:DD:EE:01"
        );

        registry.UpsertSensor(sensor);

        sensor.MacAddress =
            "AA:BB:CC:DD:EE:99";

        registry.UpsertSensor(sensor);

        Assert.False(
            registry.TryGetByMac(
                "AA:BB:CC:DD:EE:01",
                out _
            )
        );

        Assert.True(
            registry.TryGetByMac(
                "AA:BB:CC:DD:EE:99",
                out var updated
            )
        );

        Assert.NotNull(updated);
        Assert.Equal(1, updated.SensorId);
    }

    [Fact]
    public void DifferentSensorsCannotShareMacAddress()
    {
        var registry = new DeviceRegistry();

        registry.UpsertSensor(
            CreateSensor(
                1,
                "AA:BB:CC:DD:EE:01"
            )
        );

        Assert.Throws<InvalidOperationException>(
            () => registry.UpsertSensor(
                CreateSensor(
                    2,
                    "AA:BB:CC:DD:EE:01"
                )
            )
        );

        Assert.Single(registry.GetAll());
    }
}
