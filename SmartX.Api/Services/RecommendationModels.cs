
namespace SmartX.Api.Services;

public sealed record RecommendationDeviceSnapshot(
    int SensorId,
    string Category,
    string Location,
    DateTime? LastTelemetryAtUtc,
    double? Temperature,
    double? SoilMoisture,
    int? Wattage,
    bool? DesiredState,
    bool? ReportedState,
    bool HasOutstandingCommand
);

public sealed record RecommendationScoreBreakdown(
    double Repetition,
    double FollowUpRate,
    double Recency,
    double SearchContext,
    double CurrentAnomaly,
    double ActuatorReadiness
);

public sealed record RankedRecommendation(
    string Id,
    int Rank,
    string Condition,
    int SourceSensorId,
    int ActionSensorId,
    string SourceLocation,
    string Target,
    bool RequestedState,
    string SuggestedAction,
    string Readiness,
    double Score,
    string EvidenceLevel,
    int ObservedEpisodes,
    int MatchedEpisodes,
    double FollowUpRatePercent,
    int RelatedSearches,
    DateTime? LastHistoricalActionAtUtc,
    double? CurrentObservedValue,
    string Unit,
    RecommendationScoreBreakdown ScoreBreakdown,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Limitations
);

public sealed record RecommendationReport(
    DateTime GeneratedAtUtc,
    DateTime HistoricalFromUtc,
    DateTime HistoricalToUtc,
    int WindowMinutes,
    int HistoricalPatterns,
    int ReviewCount,
    int MonitorCount,
    int BlockedCount,
    bool DataLimitReached,
    string Disclaimer,
    IReadOnlyList<RankedRecommendation> Recommendations
);
