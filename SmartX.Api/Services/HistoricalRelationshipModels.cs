
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record RelationshipSensor(
    int Id,
    string Location
);

public sealed record RelationshipReading(
    long Id,
    int SensorId,
    DateTime TimestampUtc,
    double? Temperature,
    double? SoilMoisture,
    int? Wattage
);

public sealed record RelationshipAction(
    Guid CommandId,
    int SensorId,
    DeviceControlTarget Target,
    bool RequestedState,
    DateTime RequestedAtUtc,
    DeviceCommandStatus Status,
    DeviceCommandOrigin Origin
);

public sealed record RelationshipSearch(
    long Id,
    int? SensorId,
    DateTime RecordedAtUtc,
    string Scope
);

public sealed record HistoricalRelationshipOptions(
    int Days = 7,
    int WindowMinutes = 30,
    int? SensorId = null
);

public sealed record HistoricalRelationshipData(
    IReadOnlyList<RelationshipSensor> Sensors,
    IReadOnlyList<RelationshipReading> Readings,
    IReadOnlyList<RelationshipAction> Actions,
    IReadOnlyList<RelationshipSearch> Searches,
    bool TelemetryTruncated = false,
    bool CommandsTruncated = false,
    bool SearchesTruncated = false
);

public sealed record RelationshipExample(
    DateTime ObservationAtUtc,
    double ObservedValue,
    Guid CommandId,
    DateTime CommandRequestedAtUtc,
    double DelayMinutes
);

public sealed record HistoricalRelationshipPattern(
    string Condition,
    string Metric,
    string Comparison,
    double Threshold,
    string Unit,
    int SourceSensorId,
    string SourceLocation,
    int ActionSensorId,
    string Target,
    bool RequestedState,
    string MatchScope,
    int ObservedEpisodes,
    int MatchedEpisodes,
    double FollowUpRatePercent,
    int RelatedSearches,
    double AverageDelayMinutes,
    string EvidenceLevel,
    IReadOnlyList<RelationshipExample> Examples
);

public sealed record HistoricalRelationshipReport(
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime EligibleObservationCutoffUtc,
    int WindowMinutes,
    int LoadedTelemetryRecords,
    int LoadedAppliedManualCommands,
    int LoadedSearches,
    int ObservedEpisodes,
    int TotalDetectedPatterns,
    bool DataLimitReached,
    string Interpretation,
    IReadOnlyList<HistoricalRelationshipPattern> Patterns
);
