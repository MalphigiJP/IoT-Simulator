
using SmartX.Api.Services.Mesh;
using Xunit;

namespace SmartX.Api.Tests;

public sealed class MeshGraphTests
{
    private static MeshNode[] Nodes(
        bool relayOnline = true)
    {
        return
        [
            new(
                0,
                "GATEWAY",
                "Central Gateway",
                true,
                true
            ),
            new(
                1,
                "02:53:58:00:00:01",
                "Greenhouse A",
                true
            ),
            new(
                2,
                "02:53:58:00:00:02",
                "Greenhouse A",
                relayOnline
            ),
            new(
                3,
                "02:53:58:00:00:03",
                "Greenhouse B",
                true
            )
        ];
    }

    private static MeshLink[] RoutingLinks()
    {
        return
        [
            new(1, 0, 9),
            new(1, 2, 2),
            new(2, 0, 2),
            new(1, 3, 3),
            new(3, 0, 3)
        ];
    }

    [Fact]
    public void DijkstraFindsLowestCostRoute()
    {
        var graph = new MeshGraph(
            Nodes(),
            RoutingLinks()
        );

        var result = graph.FindShortestRoute(1, 0);

        Assert.True(result.Reachable);

        Assert.Equal(
            new[] { 1, 2, 0 },
            result.Path
        );

        Assert.Equal(4d, result.TotalCost!.Value);
        Assert.Equal(2, result.HopCount);
    }

    [Fact]
    public void OfflineRelayUsesAlternativePath()
    {
        var graph = new MeshGraph(
            Nodes(relayOnline: false),
            RoutingLinks()
        );

        var result = graph.FindShortestRoute(1, 0);

        Assert.True(result.Reachable);

        Assert.Equal(
            new[] { 1, 3, 0 },
            result.Path
        );

        Assert.Equal(6d, result.TotalCost!.Value);
    }

    [Fact]
    public void DisconnectedNodeIsUnreachable()
    {
        var graph = new MeshGraph(
            Nodes(),
            [
                new MeshLink(1, 2, 2),
                new MeshLink(2, 3, 4)
            ]
        );

        var result = graph.FindShortestRoute(1, 0);

        Assert.False(result.Reachable);
        Assert.Empty(result.Path);
        Assert.Null(result.TotalCost);
    }

    [Fact]
    public void OfflineSourceCannotRoutePackets()
    {
        var graph = new MeshGraph(
            Nodes(relayOnline: false),
            RoutingLinks()
        );

        var result = graph.FindShortestRoute(2, 0);

        Assert.False(result.Reachable);
        Assert.Contains(
            "offline",
            result.Reason!,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void OfflineGatewayBlocksRouting()
    {
        var nodes = Nodes();

        nodes[0] = nodes[0] with
        {
            IsOnline = false
        };

        var graph = new MeshGraph(
            nodes,
            RoutingLinks()
        );

        var route = graph.FindShortestRoute(1, 0);
        var tree = graph.BuildMinimumSpanningTree(0);

        Assert.False(route.Reachable);
        Assert.Null(tree.Root);
        Assert.Empty(tree.Edges);
    }

    [Fact]
    public void PrimFindsMinimumCostSpanningTree()
    {
        var graph = new MeshGraph(
            Nodes(),
            [
                new MeshLink(0, 1, 1),
                new MeshLink(0, 2, 5),
                new MeshLink(1, 2, 2),
                new MeshLink(1, 3, 4),
                new MeshLink(2, 3, 3)
            ]
        );

        var result = graph.BuildMinimumSpanningTree(0);

        Assert.NotNull(result.Root);
        Assert.Equal(3, result.Edges.Count);
        Assert.Equal(4, result.ConnectedNodeIds.Count);
        Assert.Empty(result.UnreachableNodeIds);
        Assert.Equal(6d, result.TotalCost);

        Assert.Equal(
            result.ConnectedNodeIds.Count - 1,
            result.Edges.Count
        );
    }

    [Fact]
    public void MinimumSpanningTreeSkipsOfflineNodes()
    {
        var graph = new MeshGraph(
            Nodes(relayOnline: false),
            [
                new MeshLink(0, 1, 1),
                new MeshLink(1, 2, 2),
                new MeshLink(1, 3, 4),
                new MeshLink(2, 3, 3)
            ]
        );

        var result = graph.BuildMinimumSpanningTree(0);

        Assert.Contains(2, result.UnreachableNodeIds);
        Assert.DoesNotContain(2, result.ConnectedNodeIds);
        Assert.Equal(2, result.Edges.Count);
        Assert.Equal(5d, result.TotalCost);
    }

    [Fact]
    public void MinimumSpanningTreeCreatesHierarchy()
    {
        var graph = new MeshGraph(
            Nodes(),
            [
                new MeshLink(0, 1, 1),
                new MeshLink(1, 2, 2),
                new MeshLink(2, 3, 3)
            ]
        );

        var result = graph.BuildMinimumSpanningTree(0);

        var root = Assert.IsType<MeshTreeNode>(
            result.Root
        );

        Assert.Equal(0, root.NodeId);

        var firstChild = Assert.Single(root.Children);
        Assert.Equal(1, firstChild.NodeId);

        var secondChild = Assert.Single(
            firstChild.Children
        );

        Assert.Equal(2, secondChild.NodeId);

        var thirdChild = Assert.Single(
            secondChild.Children
        );

        Assert.Equal(3, thirdChild.NodeId);
    }

    [Fact]
    public void DuplicateLinksUseLowestCost()
    {
        var graph = new MeshGraph(
            Nodes(),
            [
                new MeshLink(1, 0, 8),
                new MeshLink(0, 1, 3)
            ]
        );

        var result = graph.FindShortestRoute(1, 0);

        Assert.True(result.Reachable);
        Assert.Equal(3d, result.TotalCost!.Value);
    }

    [Fact]
    public void InvalidLinkCostIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new MeshGraph(
                Nodes(),
                [
                    new MeshLink(
                        0,
                        1,
                        double.NaN
                    )
                ]
            )
        );
    }

    [Fact]
    public void UnknownLinkEndpointIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new MeshGraph(
                Nodes(),
                [
                    new MeshLink(0, 999, 2)
                ]
            )
        );
    }

    [Fact]
    public void GatewayCanRouteToItself()
    {
        var graph = new MeshGraph(
            Nodes(),
            RoutingLinks()
        );

        var result = graph.FindShortestRoute(0, 0);

        Assert.True(result.Reachable);
        Assert.Equal(new[] { 0 }, result.Path);
        Assert.Equal(0d, result.TotalCost!.Value);
        Assert.Equal(0, result.HopCount);
    }
}
