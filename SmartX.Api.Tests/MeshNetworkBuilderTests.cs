
using SmartX.Api.Services.Mesh;
using Xunit;

namespace SmartX.Api.Tests;

public sealed class MeshNetworkBuilderTests
{
    private static readonly DateTime Now =
        new(
            2026,
            11,
            7,
            12,
            0,
            0,
            DateTimeKind.Utc
        );

    private static MeshSensorInput Sensor(
        int id,
        int? secondsAgo = 5,
        string location = "Lab A",
        string? mac = null)
    {
        return new MeshSensorInput(
            id,
            mac ?? $"02:53:58:00:{id / 256:X2}:{id % 256:X2}",
            location,
            "EnvironmentalEsp32",
            secondsAgo.HasValue
                ? Now.AddSeconds(-secondsAgo.Value)
                : null
        );
    }

    private static MeshNetworkSnapshot Build(
        params MeshSensorInput[] sensors)
    {
        return MeshNetworkBuilder.Build(
            sensors,
            Now
        );
    }

    [Fact]
    public void EmptyRegistryCreatesGatewayOnly()
    {
        var snapshot = Build();

        Assert.Equal(0, snapshot.RegisteredSensorCount);
        Assert.Single(snapshot.Nodes);
        Assert.Empty(snapshot.Links);
        Assert.Equal(0, snapshot.ReachableSensorCount);
        Assert.True(snapshot.Nodes[0].IsGateway);
    }

    [Fact]
    public void RegisteredSensorsKeepTheirDatabaseIds()
    {
        var snapshot = Build(
            Sensor(11),
            Sensor(42),
            Sensor(73)
        );

        Assert.Equal(3, snapshot.RegisteredSensorCount);
        Assert.Equal(4, snapshot.Nodes.Count);

        Assert.Contains(
            snapshot.Nodes,
            node => node.Id == 11
        );

        Assert.Contains(
            snapshot.Nodes,
            node => node.Id == 42
        );

        Assert.Contains(
            snapshot.Nodes,
            node => node.Id == 73
        );
    }

    [Fact]
    public void GeneratedNetworkIncludesMeshLinks()
    {
        var snapshot = Build(
            Sensor(1),
            Sensor(2),
            Sensor(3),
            Sensor(4)
        );

        Assert.Contains(
            snapshot.Links,
            link =>
                link.FromId != 0 &&
                link.ToId != 0
        );

        Assert.Contains(
            snapshot.Links,
            link =>
                link.FromId == 0 ||
                link.ToId == 0
        );
    }

    [Fact]
    public void ThirtySecondOldTelemetryIsOnline()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: 30)
        );

        var health = Assert.Single(
            snapshot.DeviceHealth
        );

        Assert.Equal("Online", health.Status);
        Assert.True(health.CanRelay);
        Assert.Equal(1, snapshot.ReachableSensorCount);
    }

    [Fact]
    public void DelayedTelemetryExcludesDeviceFromRouting()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: 31),
            Sensor(2, secondsAgo: 120)
        );

        Assert.Equal(2, snapshot.DelayedCount);

        Assert.All(
            snapshot.DeviceHealth,
            item =>
            {
                Assert.Equal("Delayed", item.Status);
                Assert.False(item.CanRelay);
            }
        );

        Assert.Equal(0, snapshot.ReachableSensorCount);
    }

    [Fact]
    public void StaleTelemetryIsOffline()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: 121)
        );

        var health = Assert.Single(
            snapshot.DeviceHealth
        );

        Assert.Equal("Offline", health.Status);
        Assert.False(health.CanRelay);
        Assert.Contains(1, snapshot.UnreachableSensorIds);
    }

    [Fact]
    public void MissingTelemetryIsUnknown()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: null)
        );

        var health = Assert.Single(
            snapshot.DeviceHealth
        );

        Assert.Equal("Unknown", health.Status);
        Assert.Null(health.AgeSeconds);
        Assert.False(health.CanRelay);
    }

    [Fact]
    public void FutureTelemetryDoesNotAppearOnline()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: -60)
        );

        var health = Assert.Single(
            snapshot.DeviceHealth
        );

        Assert.Equal("Unknown", health.Status);
        Assert.False(health.CanRelay);
    }

    [Fact]
    public void LinkGenerationIsDeterministic()
    {
        var forward = Build(
            Sensor(1),
            Sensor(2),
            Sensor(3),
            Sensor(4),
            Sensor(5)
        );

        var reversed = Build(
            Sensor(5),
            Sensor(4),
            Sensor(3),
            Sensor(2),
            Sensor(1)
        );

        Assert.Equal(
            forward.Links,
            reversed.Links
        );

        Assert.Equal(
            forward.Nodes,
            reversed.Nodes
        );
    }

    [Fact]
    public void DuplicateSensorIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(
                Sensor(1),
                Sensor(1)
            )
        );
    }

    [Fact]
    public void DuplicateMacAddressesAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(
                Sensor(
                    1,
                    mac: "AA:BB:CC:DD:EE:01"
                ),
                Sensor(
                    2,
                    mac: "aa:bb:cc:dd:ee:01"
                )
            )
        );
    }

    [Fact]
    public void UndirectedLinksAreUnique()
    {
        var snapshot = Build(
            Sensor(1),
            Sensor(2),
            Sensor(3),
            Sensor(4),
            Sensor(5)
        );

        var pairs = snapshot.Links
            .Select(link => (
                From: Math.Min(
                    link.FromId,
                    link.ToId
                ),
                To: Math.Max(
                    link.FromId,
                    link.ToId
                )
            ))
            .ToArray();

        Assert.Equal(
            pairs.Length,
            pairs.Distinct().Count()
        );
    }

    [Fact]
    public void OnlineNodesCanReachGateway()
    {
        var snapshot = Build(
            Sensor(1),
            Sensor(2),
            Sensor(3),
            Sensor(4)
        );

        var graph = new MeshGraph(
            snapshot.Nodes,
            snapshot.Links
        );

        var route = graph.FindShortestRoute(
            2,
            0
        );

        Assert.True(route.Reachable);
        Assert.Equal(2, route.Path.First());
        Assert.Equal(0, route.Path.Last());
        Assert.True(route.TotalCost > 0);
        Assert.Equal(4, snapshot.ReachableSensorCount);
    }

    [Fact]
    public void OfflineGatewayRelayUsesAnotherAnchor()
    {
        var healthy = Build(
            Sensor(1),
            Sensor(2),
            Sensor(3),
            Sensor(4)
        );

        var failure = Build(
            Sensor(1, secondsAgo: 180),
            Sensor(2),
            Sensor(3),
            Sensor(4)
        );

        var healthyGraph = new MeshGraph(
            healthy.Nodes,
            healthy.Links
        );

        var failureGraph = new MeshGraph(
            failure.Nodes,
            failure.Links
        );

        var normalRoute =
            healthyGraph.FindShortestRoute(
                2,
                0
            );

        var fallbackRoute =
            failureGraph.FindShortestRoute(
                2,
                0
            );

        Assert.True(normalRoute.Reachable);
        Assert.True(fallbackRoute.Reachable);
        Assert.DoesNotContain(1, fallbackRoute.Path);
        Assert.Equal(0, fallbackRoute.Path.Last());
        Assert.Contains(1, failure.UnreachableSensorIds);
    }

    [Fact]
    public void UnavailableGatewayAnchorsDisconnectMesh()
    {
        var snapshot = Build(
            Sensor(1, secondsAgo: 180),
            Sensor(2),
            Sensor(3, secondsAgo: 180),
            Sensor(4)
        );

        Assert.Equal(0, snapshot.ReachableSensorCount);

        Assert.Contains(2, snapshot.UnreachableSensorIds);
        Assert.Contains(4, snapshot.UnreachableSensorIds);
    }

    [Fact]
    public void LargeNetworkUsesBoundedLinkCount()
    {
        var sensors = Enumerable
            .Range(1, 1000)
            .Select(id => Sensor(id))
            .ToArray();

        var snapshot = Build(sensors);

        Assert.Equal(
            1000,
            snapshot.RegisteredSensorCount
        );

        Assert.Equal(
            1000,
            snapshot.ReachableSensorCount
        );

        Assert.True(
            snapshot.Links.Count <= 2003
        );
    }
}
