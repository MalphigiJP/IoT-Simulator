
namespace SmartX.Api.Services.Mesh;

public sealed class MeshGraph
{
    private sealed record Neighbour(
        int NodeId,
        double Cost
    );

    private readonly Dictionary<int, MeshNode> _nodes;

    private readonly Dictionary<
        int,
        List<Neighbour>
    > _adjacency;

    public MeshGraph(
        IEnumerable<MeshNode> nodes,
        IEnumerable<MeshLink> links)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(links);

        _nodes = new Dictionary<int, MeshNode>();

        foreach (var node in nodes)
        {
            if (node.Id < 0)
            {
                throw new ArgumentException(
                    "Node IDs cannot be negative.",
                    nameof(nodes)
                );
            }

            if (!_nodes.TryAdd(node.Id, node))
            {
                throw new ArgumentException(
                    $"Duplicate node ID: {node.Id}.",
                    nameof(nodes)
                );
            }
        }

        if (_nodes.Count == 0)
        {
            throw new ArgumentException(
                "A mesh graph requires at least one node.",
                nameof(nodes)
            );
        }

        _adjacency = _nodes.Keys.ToDictionary(
            id => id,
            _ => new List<Neighbour>()
        );

        var uniqueLinks =
            new Dictionary<(int From, int To), double>();

        foreach (var link in links)
        {
            if (
                !_nodes.ContainsKey(link.FromId) ||
                !_nodes.ContainsKey(link.ToId)
            )
            {
                throw new ArgumentException(
                    "Every link endpoint must be a registered graph node.",
                    nameof(links)
                );
            }

            if (link.FromId == link.ToId)
            {
                throw new ArgumentException(
                    "A node cannot connect to itself.",
                    nameof(links)
                );
            }

            if (
                !double.IsFinite(link.Cost) ||
                link.Cost <= 0
            )
            {
                throw new ArgumentException(
                    "Link cost must be a positive finite number.",
                    nameof(links)
                );
            }

            int from = Math.Min(
                link.FromId,
                link.ToId
            );

            int to = Math.Max(
                link.FromId,
                link.ToId
            );

            var key = (from, to);

            if (
                !uniqueLinks.TryGetValue(
                    key,
                    out double existingCost
                ) ||
                link.Cost < existingCost
            )
            {
                uniqueLinks[key] = link.Cost;
            }
        }

        foreach (var link in uniqueLinks)
        {
            int from = link.Key.From;
            int to = link.Key.To;
            double cost = link.Value;

            _adjacency[from].Add(
                new Neighbour(to, cost)
            );

            _adjacency[to].Add(
                new Neighbour(from, cost)
            );
        }

        foreach (var neighbours in _adjacency.Values)
        {
            neighbours.Sort(
                (left, right) =>
                    left.NodeId.CompareTo(right.NodeId)
            );
        }
    }

    private void ValidateNode(int nodeId)
    {
        if (!_nodes.ContainsKey(nodeId))
        {
            throw new ArgumentException(
                $"Node {nodeId} does not exist.",
                nameof(nodeId)
            );
        }
    }

    private void ValidateGateway(int gatewayId)
    {
        ValidateNode(gatewayId);

        if (!_nodes[gatewayId].IsGateway)
        {
            throw new ArgumentException(
                $"Node {gatewayId} is not a gateway.",
                nameof(gatewayId)
            );
        }
    }

    public MeshRouteResult FindShortestRoute(
        int sourceId,
        int gatewayId)
    {
        ValidateNode(sourceId);
        ValidateGateway(gatewayId);

        if (!_nodes[sourceId].IsOnline)
        {
            return Unreachable(
                sourceId,
                gatewayId,
                "Source node is offline."
            );
        }

        if (!_nodes[gatewayId].IsOnline)
        {
            return Unreachable(
                sourceId,
                gatewayId,
                "Gateway is offline."
            );
        }

        var distances = new Dictionary<int, double>
        {
            [sourceId] = 0
        };

        var previous = new Dictionary<int, int>();

        var frontier =
            new PriorityQueue<int, (double Cost, int Id)>();

        frontier.Enqueue(
            sourceId,
            (0, sourceId)
        );

        while (
            frontier.TryDequeue(
                out int current,
                out var priority
            )
        )
        {
            if (
                !distances.TryGetValue(
                    current,
                    out double currentDistance
                ) ||
                priority.Cost > currentDistance
            )
            {
                continue;
            }

            if (current == gatewayId)
            {
                break;
            }

            foreach (var neighbour in _adjacency[current])
            {
                if (!_nodes[neighbour.NodeId].IsOnline)
                {
                    continue;
                }

                double candidate =
                    currentDistance + neighbour.Cost;

                if (
                    distances.TryGetValue(
                        neighbour.NodeId,
                        out double existingDistance
                    ) &&
                    candidate >= existingDistance
                )
                {
                    continue;
                }

                distances[neighbour.NodeId] = candidate;

                previous[neighbour.NodeId] = current;

                frontier.Enqueue(
                    neighbour.NodeId,
                    (candidate, neighbour.NodeId)
                );
            }
        }

        if (!distances.ContainsKey(gatewayId))
        {
            return Unreachable(
                sourceId,
                gatewayId,
                "No active route to the gateway."
            );
        }

        var path = new List<int>();
        int cursor = gatewayId;

        path.Add(cursor);

        while (cursor != sourceId)
        {
            cursor = previous[cursor];
            path.Add(cursor);
        }

        path.Reverse();

        return new MeshRouteResult(
            sourceId,
            gatewayId,
            true,
            path,
            distances[gatewayId],
            path.Count - 1,
            null
        );
    }

    public MeshTreeResult BuildMinimumSpanningTree(
        int gatewayId)
    {
        ValidateGateway(gatewayId);

        if (!_nodes[gatewayId].IsOnline)
        {
            return new MeshTreeResult(
                gatewayId,
                null,
                [],
                [],
                _nodes.Keys.OrderBy(id => id).ToArray(),
                0
            );
        }

        var visited = new HashSet<int>
        {
            gatewayId
        };

        var selectedEdges = new List<MeshLink>();

        var frontier =
            new PriorityQueue<
                MeshLink,
                (double Cost, int To, int From)
            >();

        void AddAvailableLinks(int nodeId)
        {
            foreach (var neighbour in _adjacency[nodeId])
            {
                if (
                    visited.Contains(neighbour.NodeId) ||
                    !_nodes[neighbour.NodeId].IsOnline
                )
                {
                    continue;
                }

                var edge = new MeshLink(
                    nodeId,
                    neighbour.NodeId,
                    neighbour.Cost
                );

                frontier.Enqueue(
                    edge,
                    (
                        edge.Cost,
                        edge.ToId,
                        edge.FromId
                    )
                );
            }
        }

        AddAvailableLinks(gatewayId);

        while (
            frontier.TryDequeue(
                out var edge,
                out _
            )
        )
        {
            if (visited.Contains(edge.ToId))
            {
                continue;
            }

            visited.Add(edge.ToId);
            selectedEdges.Add(edge);

            AddAvailableLinks(edge.ToId);
        }

        var treeNodes = visited.ToDictionary(
            id => id,
            id => new MeshTreeNode(id)
        );

        foreach (var edge in selectedEdges)
        {
            treeNodes[edge.FromId].Children.Add(
                treeNodes[edge.ToId]
            );
        }

        var connected = visited
            .OrderBy(id => id)
            .ToArray();

        var unreachable = _nodes.Keys
            .Where(id => !visited.Contains(id))
            .OrderBy(id => id)
            .ToArray();

        return new MeshTreeResult(
            gatewayId,
            treeNodes[gatewayId],
            selectedEdges,
            connected,
            unreachable,
            selectedEdges.Sum(edge => edge.Cost)
        );
    }

    private static MeshRouteResult Unreachable(
        int sourceId,
        int gatewayId,
        string reason)
    {
        return new MeshRouteResult(
            sourceId,
            gatewayId,
            false,
            [],
            null,
            0,
            reason
        );
    }
}
