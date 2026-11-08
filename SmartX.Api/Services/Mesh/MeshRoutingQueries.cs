
namespace SmartX.Api.Services.Mesh;

public static class MeshRoutingQueries
{
    public static int? FindSensorIdByMac(
        MeshNetworkSnapshot snapshot,
        string? macAddress)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var searchedMac = NormalizeMac(macAddress);

        if (searchedMac.Length == 0)
        {
            return null;
        }

        return snapshot.Nodes
            .Where(node => !node.IsGateway)
            .FirstOrDefault(node =>
                NormalizeMac(node.MacAddress) == searchedMac
            )?.Id;
    }

    public static MeshRouteResponse? RouteBySensorId(
        MeshNetworkSnapshot snapshot,
        int sensorId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var source = snapshot.Nodes.FirstOrDefault(node =>
            node.Id == sensorId && !node.IsGateway
        );

        if (source is null)
        {
            return null;
        }

        var sourceHealth = snapshot.DeviceHealth.First(item =>
            item.SensorId == sensorId
        );

        var graph = new MeshGraph(
            snapshot.Nodes,
            snapshot.Links
        );

        var route = graph.FindShortestRoute(
            sensorId,
            snapshot.GatewayId
        );

        var nodesById = snapshot.Nodes.ToDictionary(
            node => node.Id
        );

        var costs = snapshot.Links.ToDictionary(
            link => (
                From: Math.Min(link.FromId, link.ToId),
                To: Math.Max(link.FromId, link.ToId)
            ),
            link => link.Cost
        );

        var pathNodes = route.Path
            .Select(id => nodesById[id])
            .ToArray();

        var hops = new List<MeshRouteHop>();

        for (int index = 1; index < route.Path.Count; index++)
        {
            int from = route.Path[index - 1];
            int to = route.Path[index];

            var linkKey = (
                From: Math.Min(from, to),
                To: Math.Max(from, to)
            );

            hops.Add(new MeshRouteHop(
                from,
                to,
                costs[linkKey]
            ));
        }

        return new MeshRouteResponse(
            snapshot.GeneratedAtUtc,
            snapshot.TopologyMode,
            source.Id,
            source.MacAddress,
            source.Location,
            sourceHealth,
            snapshot.GatewayId,
            route.Reachable,
            route.Path,
            pathNodes,
            hops,
            route.TotalCost,
            route.HopCount,
            route.Reason,
            snapshot.Notice
        );
    }

    public static MeshRouteResponse? RouteByMac(
        MeshNetworkSnapshot snapshot,
        string? macAddress)
    {
        int? sensorId = FindSensorIdByMac(
            snapshot,
            macAddress
        );

        return sensorId.HasValue
            ? RouteBySensorId(snapshot, sensorId.Value)
            : null;
    }

    public static MeshNodeLookupResponse? NodeBySensorId(
        MeshNetworkSnapshot snapshot,
        int sensorId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var node = snapshot.Nodes.FirstOrDefault(item =>
            item.Id == sensorId && !item.IsGateway
        );

        if (node is null)
        {
            return null;
        }

        var health = snapshot.DeviceHealth.First(item =>
            item.SensorId == sensorId
        );

        var route = RouteBySensorId(snapshot, sensorId)!;

        return new MeshNodeLookupResponse(
            snapshot.GeneratedAtUtc,
            node,
            health,
            route
        );
    }

    public static MeshNodeLookupResponse? NodeByMac(
        MeshNetworkSnapshot snapshot,
        string? macAddress)
    {
        int? sensorId = FindSensorIdByMac(
            snapshot,
            macAddress
        );

        return sensorId.HasValue
            ? NodeBySensorId(snapshot, sensorId.Value)
            : null;
    }

    public static MeshMinimumSpanningTreeResponse MinimumSpanningTree(
        MeshNetworkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var graph = new MeshGraph(
            snapshot.Nodes,
            snapshot.Links
        );

        var mst = graph.BuildMinimumSpanningTree(
            snapshot.GatewayId
        );

        return new MeshMinimumSpanningTreeResponse(
            snapshot.GeneratedAtUtc,
            snapshot.TopologyMode,
            snapshot.GatewayId,
            mst.TotalCost,
            mst.ConnectedNodeIds.Count(id =>
                id != snapshot.GatewayId
            ),
            mst.Edges,
            mst.ConnectedNodeIds,
            mst.UnreachableNodeIds,
            snapshot.Nodes,
            snapshot.Notice
        );
    }

    private static string NormalizeMac(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(character =>
                character != ':' &&
                character != '-' &&
                character != '.' &&
                !char.IsWhiteSpace(character)
            )
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
