
namespace SmartX.Api.Services.Mesh;

public static class MeshNetworkBuilder
{
    public const int GatewayId = 0;

    private const int OnlineSeconds = 30;
    private const int DelayedSeconds = 120;

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc
            )
        };
    }

    private static string NormalizeLocation(
        string? location)
    {
        return string.Join(
            " ",
            (location ?? "")
                .Trim()
                .Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries
                )
        ).ToUpperInvariant();
    }

    private static MeshDeviceHealth GetHealth(
        MeshSensorInput sensor,
        DateTime nowUtc)
    {
        if (!sensor.LastTelemetryAtUtc.HasValue)
        {
            return new MeshDeviceHealth(
                sensor.Id,
                sensor.MacAddress,
                sensor.Category,
                sensor.Location,
                "Unknown",
                null,
                null,
                false
            );
        }

        var lastSeen = AsUtc(
            sensor.LastTelemetryAtUtc.Value
        );

        double ageSeconds =
            (nowUtc - lastSeen).TotalSeconds;

        if (ageSeconds < 0)
        {
            return new MeshDeviceHealth(
                sensor.Id,
                sensor.MacAddress,
                sensor.Category,
                sensor.Location,
                "Unknown",
                lastSeen,
                null,
                false
            );
        }

        string status = ageSeconds <= OnlineSeconds
            ? "Online"
            : ageSeconds <= DelayedSeconds
                ? "Delayed"
                : "Offline";

        return new MeshDeviceHealth(
            sensor.Id,
            sensor.MacAddress,
            sensor.Category,
            sensor.Location,
            status,
            lastSeen,
            (long)Math.Ceiling(ageSeconds),
            status == "Online"
        );
    }

    private static double GetLinkCost(
        MeshSensorInput first,
        MeshSensorInput second)
    {
        string firstLocation = NormalizeLocation(
            first.Location
        );

        string secondLocation = NormalizeLocation(
            second.Location
        );

        bool sameLocation =
            firstLocation.Length > 0 &&
            firstLocation == secondLocation;

        int from = Math.Min(first.Id, second.Id);
        int to = Math.Max(first.Id, second.Id);

        int variation = (int)(
            ((long)from * 31 + (long)to * 17) % 5
        );

        return (sameLocation ? 3 : 6) + variation;
    }

    public static MeshNetworkSnapshot Build(
        IEnumerable<MeshSensorInput> sensors,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sensors);

        DateTime capturedAtUtc = AsUtc(nowUtc);

        var records = sensors
            .OrderBy(sensor => sensor.Id)
            .ToArray();

        if (records.Any(sensor => sensor.Id <= 0))
        {
            throw new ArgumentException(
                "Registered sensor IDs must be positive.",
                nameof(sensors)
            );
        }

        if (
            records.Select(sensor => sensor.Id)
                .Distinct()
                .Count() != records.Length
        )
        {
            throw new ArgumentException(
                "Duplicate sensor IDs are not allowed.",
                nameof(sensors)
            );
        }

        bool duplicateMac = records
            .Where(sensor =>
                !string.IsNullOrWhiteSpace(
                    sensor.MacAddress
                )
            )
            .GroupBy(
                sensor => sensor.MacAddress.Trim(),
                StringComparer.OrdinalIgnoreCase
            )
            .Any(group => group.Count() > 1);

        if (duplicateMac)
        {
            throw new ArgumentException(
                "Duplicate MAC addresses are not allowed.",
                nameof(sensors)
            );
        }

        var health = records
            .Select(sensor =>
                GetHealth(sensor, capturedAtUtc)
            )
            .ToArray();

        var healthById = health.ToDictionary(
            item => item.SensorId
        );

        var nodes = new List<MeshNode>
        {
            new(
                GatewayId,
                "VIRTUAL-GATEWAY",
                "Central Gateway",
                true,
                true
            )
        };

        foreach (var sensor in records)
        {
            bool canRelay =
                healthById[sensor.Id].CanRelay;

            nodes.Add(
                new MeshNode(
                    sensor.Id,
                    sensor.MacAddress,
                    sensor.Location,
                    canRelay
                )
            );
        }

        var linkMap = new Dictionary<
            (int From, int To),
            double
        >();

        void AddLink(
            int firstId,
            int secondId,
            double cost)
        {
            if (firstId == secondId)
            {
                return;
            }

            int from = Math.Min(
                firstId,
                secondId
            );

            int to = Math.Max(
                firstId,
                secondId
            );

            var key = (from, to);

            if (
                !linkMap.TryGetValue(
                    key,
                    out double existing
                ) ||
                cost < existing
            )
            {
                linkMap[key] = cost;
            }
        }

        int count = records.Length;

        for (int index = 0; index < count; index++)
        {
            var current = records[index];

            if (count >= 2)
            {
                var next = records[
                    (index + 1) % count
                ];

                AddLink(
                    current.Id,
                    next.Id,
                    GetLinkCost(current, next)
                );
            }

            if (count >= 4)
            {
                var alternate = records[
                    (index + 2) % count
                ];

                AddLink(
                    current.Id,
                    alternate.Id,
                    GetLinkCost(current, alternate)
                );
            }
        }

        if (count > 0)
        {
            int[] anchorIndices = count switch
            {
                1 => [0],
                2 => [0, 1],
                >= 6 => [0, count / 3, (2 * count) / 3],
                _ => [0, count / 2]
            };

            foreach (
                int index in anchorIndices.Distinct()
            )
            {
                int sensorId = records[index].Id;

                double gatewayCost =
                    3 + sensorId % 4;

                AddLink(
                    GatewayId,
                    sensorId,
                    gatewayCost
                );
            }
        }

        var links = linkMap
            .OrderBy(item => item.Key.From)
            .ThenBy(item => item.Key.To)
            .Select(item => new MeshLink(
                item.Key.From,
                item.Key.To,
                item.Value
            ))
            .ToArray();

        var graph = new MeshGraph(
            nodes,
            links
        );

        var spanningTree =
            graph.BuildMinimumSpanningTree(
                GatewayId
            );

        int reachableSensorCount =
            spanningTree.ConnectedNodeIds
                .Count(id => id != GatewayId);

        return new MeshNetworkSnapshot(
            capturedAtUtc,
            GatewayId,
            "Simulated",
            count,
            health.Count(item =>
                item.Status == "Online"
            ),
            health.Count(item =>
                item.Status == "Delayed"
            ),
            health.Count(item =>
                item.Status == "Offline"
            ),
            health.Count(item =>
                item.Status == "Unknown"
            ),
            reachableSensorCount,
            spanningTree.UnreachableNodeIds,
            nodes.ToArray(),
            links,
            health,
            "Logical mesh connections and routing costs are simulated. Online status is inferred from telemetry freshness, not physical radio discovery."
        );
    }
}
