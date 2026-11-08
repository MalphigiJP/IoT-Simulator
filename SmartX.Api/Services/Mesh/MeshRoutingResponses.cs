
namespace SmartX.Api.Services.Mesh;

public sealed record MeshRouteHop(
    int FromId,
    int ToId,
    double Cost
);

public sealed record MeshRouteResponse(
    DateTime GeneratedAtUtc,
    string TopologyMode,
    int SourceId,
    string SourceMacAddress,
    string SourceLocation,
    MeshDeviceHealth SourceHealth,
    int GatewayId,
    bool Reachable,
    IReadOnlyList<int> Path,
    IReadOnlyList<MeshNode> PathNodes,
    IReadOnlyList<MeshRouteHop> Hops,
    double? TotalCost,
    int HopCount,
    string? Reason,
    string Notice
);

public sealed record MeshNodeLookupResponse(
    DateTime GeneratedAtUtc,
    MeshNode Node,
    MeshDeviceHealth Health,
    MeshRouteResponse Route
);

public sealed record MeshMinimumSpanningTreeResponse(
    DateTime GeneratedAtUtc,
    string TopologyMode,
    int GatewayId,
    double TotalCost,
    int ConnectedSensorCount,
    IReadOnlyList<MeshLink> Edges,
    IReadOnlyList<int> ConnectedNodeIds,
    IReadOnlyList<int> UnreachableNodeIds,
    IReadOnlyList<MeshNode> Nodes,
    string Notice
);
