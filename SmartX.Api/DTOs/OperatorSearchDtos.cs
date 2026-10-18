
namespace SmartX.Api.DTOs;

public sealed record RecordOperatorSearchDto
{
    public string? Query { get; init; }

    public string? Scope { get; init; }

    public int? SensorId { get; init; }

    public int? ResultCount { get; init; }
}

public sealed record OperatorInteractionDto(
    string Type,
    string ReferenceId,
    DateTime OccurredAtUtc,
    int? SensorId,
    string? Scope,
    string? Query,
    Guid? CommandId,
    string? Target,
    bool? RequestedState,
    string? CommandStatus,
    string? Origin
);
