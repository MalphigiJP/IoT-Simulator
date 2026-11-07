
namespace SmartX.Api.Services.Mesh;

public sealed record MeshNode(
    int Id,
    string MacAddress,
    string Location,
    bool IsOnline,
    bool IsGateway = false
);

public sealed record MeshLink(
    int FromId,
    int ToId,
    double Cost
);

public sealed record MeshRouteResult(
    int SourceId,
    int GatewayId,
    bool Reachable,
    IReadOnlyList<int> Path,
    double? TotalCost,
    int HopCount,
    string? Reason
);

public sealed class MeshTreeNode
{
    public int NodeId { get; }

    public List<MeshTreeNode> Children { get; } = [];

    public MeshTreeNode(int nodeId)
    {
        NodeId = nodeId;
    }
}

public sealed record MeshTreeResult(
    int GatewayId,
    MeshTreeNode? Root,
    IReadOnlyList<MeshLink> Edges,
    IReadOnlyList<int> ConnectedNodeIds,
    IReadOnlyList<int> UnreachableNodeIds,
    double TotalCost
);
