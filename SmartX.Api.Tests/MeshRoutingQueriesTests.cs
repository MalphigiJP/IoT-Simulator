
using System.Text.Json;
using SmartX.Api.Services.Mesh;
using Xunit;

namespace SmartX.Api.Tests;

public sealed class MeshRoutingQueriesTests
{
    private static readonly DateTime Now = new(
        2026, 11, 8, 12, 0, 0, DateTimeKind.Utc
    );

    private static MeshSensorInput Sensor(
        int id,
        int? secondsAgo = 5)
    {
        return new MeshSensorInput(
            id,
            $"02:53:58:00:{id / 256:X2}:{id % 256:X2}",
            "Greenhouse",
            "EnvironmentalEsp32",
            secondsAgo.HasValue
                ? Now.AddSeconds(-secondsAgo.Value)
                : null
        );
    }

    private static MeshNetworkSnapshot Snapshot(
        params MeshSensorInput[] sensors)
    {
        return MeshNetworkBuilder.Build(sensors, Now);
    }

    [Fact]
    public void RouteByIdStartsAtSensorAndEndsAtGateway()
    {
        var snapshot = Snapshot(
            Sensor(1), Sensor(2), Sensor(3), Sensor(4)
        );

        var result = Assert.IsType<MeshRouteResponse>(
            MeshRoutingQueries.RouteBySensorId(snapshot, 2)
        );

        Assert.True(result.Reachable);
        Assert.Equal(2, result.SourceId);
        Assert.Equal(2, result.Path.First());
        Assert.Equal(0, result.Path.Last());
        Assert.Equal(result.HopCount, result.Hops.Count);
        Assert.Equal(result.Path.Count, result.PathNodes.Count);
        Assert.Equal(
            result.TotalCost!.Value,
            result.Hops.Sum(hop => hop.Cost),
            precision: 8
        );
    }

    [Fact]
    public void RouteByIdMatchesGraphShortestPath()
    {
        var snapshot = Snapshot(
            Sensor(1), Sensor(2), Sensor(3), Sensor(4)
        );

        var expected = new MeshGraph(
            snapshot.Nodes, snapshot.Links
        ).FindShortestRoute(2, 0);

        var actual = Assert.IsType<MeshRouteResponse>(
            MeshRoutingQueries.RouteBySensorId(snapshot, 2)
        );

        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.TotalCost, actual.TotalCost);
    }

    [Fact]
    public void UnknownSensorReturnsNull()
    {
        var snapshot = Snapshot(Sensor(1));

        Assert.Null(MeshRoutingQueries.RouteBySensorId(snapshot, 999));
        Assert.Null(MeshRoutingQueries.NodeBySensorId(snapshot, 999));
        Assert.Null(MeshRoutingQueries.RouteBySensorId(snapshot, 0));
    }

    [Fact]
    public void MacLookupIgnoresCaseAndSeparators()
    {
        var snapshot = Snapshot(Sensor(1));

        Assert.Equal(
            1,
            MeshRoutingQueries.FindSensorIdByMac(
                snapshot,
                "02-53-58-00-00-01"
            )
        );

        var route = Assert.IsType<MeshRouteResponse>(
            MeshRoutingQueries.RouteByMac(
                snapshot,
                " 025358000001 "
            )
        );

        Assert.Equal(1, route.SourceId);
    }

    [Fact]
    public void MissingMacReturnsNull()
    {
        var snapshot = Snapshot(Sensor(1));

        Assert.Null(MeshRoutingQueries.FindSensorIdByMac(snapshot, ""));
        Assert.Null(MeshRoutingQueries.RouteByMac(snapshot, "DEADBEEF0000"));
        Assert.Null(MeshRoutingQueries.NodeByMac(snapshot, "   "));
    }

    [Fact]
    public void NodeLookupIncludesHealthAndRoute()
    {
        var snapshot = Snapshot(Sensor(1));

        var result = Assert.IsType<MeshNodeLookupResponse>(
            MeshRoutingQueries.NodeByMac(
                snapshot,
                "02:53:58:00:00:01"
            )
        );

        Assert.Equal(1, result.Node.Id);
        Assert.Equal("Online", result.Health.Status);
        Assert.Equal(1, result.Route.SourceId);
        Assert.True(result.Route.Reachable);
    }

    [Fact]
    public void OfflineSourceReturnsUnreachableRoute()
    {
        var snapshot = Snapshot(
            Sensor(1, secondsAgo: 180)
        );

        var result = Assert.IsType<MeshRouteResponse>(
            MeshRoutingQueries.RouteBySensorId(snapshot, 1)
        );

        Assert.False(result.Reachable);
        Assert.Empty(result.Path);
        Assert.Empty(result.Hops);
        Assert.Null(result.TotalCost);
        Assert.Equal("Offline", result.SourceHealth.Status);
    }

    [Fact]
    public void OfflineRelayIsExcludedFromFallbackRoute()
    {
        var snapshot = Snapshot(
            Sensor(1, secondsAgo: 180),
            Sensor(2),
            Sensor(3),
            Sensor(4)
        );

        var route = Assert.IsType<MeshRouteResponse>(
            MeshRoutingQueries.RouteBySensorId(snapshot, 2)
        );

        Assert.True(route.Reachable);
        Assert.DoesNotContain(1, route.Path);
        Assert.Equal(0, route.Path.Last());
    }

    [Fact]
    public void MstReturnsSpanningTreeEdgesAndTotals()
    {
        var snapshot = Snapshot(
            Sensor(1), Sensor(2), Sensor(3), Sensor(4)
        );

        var result = MeshRoutingQueries.MinimumSpanningTree(snapshot);
        var expected = new MeshGraph(
            snapshot.Nodes, snapshot.Links
        ).BuildMinimumSpanningTree(snapshot.GatewayId);

        Assert.Equal(4, result.ConnectedSensorCount);
        Assert.Equal(4, result.Edges.Count);
        Assert.Equal(expected.TotalCost, result.TotalCost);
        Assert.Equal(expected.Edges, result.Edges);
        Assert.Empty(result.UnreachableNodeIds);
    }

    [Fact]
    public void MstReportsOfflineDevicesAsUnreachable()
    {
        var snapshot = Snapshot(
            Sensor(1, secondsAgo: 180),
            Sensor(2),
            Sensor(3)
        );

        var result = MeshRoutingQueries.MinimumSpanningTree(snapshot);

        Assert.Contains(1, result.UnreachableNodeIds);
        Assert.DoesNotContain(1, result.ConnectedNodeIds);
        Assert.DoesNotContain(
            result.Edges,
            edge => edge.FromId == 1 || edge.ToId == 1
        );
    }

    [Fact]
    public void EmptyNetworkHasGatewayOnlyMst()
    {
        var result = MeshRoutingQueries.MinimumSpanningTree(
            Snapshot()
        );

        Assert.Equal(0, result.ConnectedSensorCount);
        Assert.Empty(result.Edges);
        Assert.Equal(new[] { 0 }, result.ConnectedNodeIds);
    }

    [Fact]
    public void LargeMstSerializesWithoutNestedTreeDepthFailures()
    {
        var sensors = Enumerable.Range(1, 1000)
            .Select(id => Sensor(id))
            .ToArray();

        var snapshot = Snapshot(sensors);
        var result = MeshRoutingQueries.MinimumSpanningTree(snapshot);

        string json = JsonSerializer.Serialize(result);

        Assert.Equal(1000, result.ConnectedSensorCount);
        Assert.Contains("ConnectedSensorCount", json);
    }
}
