
namespace SmartX.Api.Services.Mesh;

public sealed record MeshSensorInput(
    int Id,
    string MacAddress,
    string Location,
    string Category,
    DateTime? LastTelemetryAtUtc
);

public sealed record MeshDeviceHealth(
    int SensorId,
    string MacAddress,
    string Category,
    string Location,
    string Status,
    DateTime? LastTelemetryAtUtc,
    long? AgeSeconds,
    bool CanRelay
);

public sealed record MeshNetworkSnapshot(
    DateTime GeneratedAtUtc,
    int GatewayId,
    string TopologyMode,
    int RegisteredSensorCount,
    int OnlineCount,
    int DelayedCount,
    int OfflineCount,
    int UnknownCount,
    int ReachableSensorCount,
    IReadOnlyList<int> UnreachableSensorIds,
    IReadOnlyList<MeshNode> Nodes,
    IReadOnlyList<MeshLink> Links,
    IReadOnlyList<MeshDeviceHealth> DeviceHealth,
    string Notice
);
