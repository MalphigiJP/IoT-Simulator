
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public sealed class RecommendationRankerTests
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static HistoricalRelationshipPattern Pattern()
    {
        return new HistoricalRelationshipPattern(
            Condition: "LowSoilMoisture",
            Metric: "SoilMoisture",
            Comparison: "<",
            Threshold: 10,
            Unit: "%",
            SourceSensorId: 1,
            SourceLocation: "Greenhouse A",
            ActionSensorId: 2,
            Target: "Pump",
            RequestedState: true,
            MatchScope: "SameLocation",
            ObservedEpisodes: 5,
            MatchedEpisodes: 3,
            FollowUpRatePercent: 60,
            RelatedSearches: 2,
            AverageDelayMinutes: 8,
            EvidenceLevel: "Repeated",
            Examples:
            [
                new RelationshipExample(
                    Now.AddHours(-4),
                    5,
                    Guid.NewGuid(),
                    Now.AddHours(-4).AddMinutes(8),
                    8
                ),
                new RelationshipExample(
                    Now.AddHours(-3),
                    6,
                    Guid.NewGuid(),
                    Now.AddHours(-3).AddMinutes(8),
                    8
                ),
                new RelationshipExample(
                    Now.AddHours(-1),
                    7,
                    Guid.NewGuid(),
                    Now.AddHours(-1).AddMinutes(8),
                    8
                )
            ]
        );
    }

    private static HistoricalRelationshipReport History(
        HistoricalRelationshipPattern? pattern = null,
        bool truncated = false)
    {
        var patterns = pattern is null
            ? new[] { Pattern() }
            : new[] { pattern };

        return new HistoricalRelationshipReport(
            FromUtc: Now.AddDays(-7),
            ToUtc: Now,
            EligibleObservationCutoffUtc:
                Now.AddMinutes(-30),
            WindowMinutes: 30,
            LoadedTelemetryRecords: 100,
            LoadedAppliedManualCommands: 10,
            LoadedSearches: 5,
            ObservedEpisodes: 5,
            TotalDetectedPatterns: patterns.Length,
            DataLimitReached: truncated,
            Interpretation: "Historical association only.",
            Patterns: patterns
        );
    }

    private static Dictionary<
        int,
        RecommendationDeviceSnapshot
    > Devices()
    {
        return new()
        {
            [1] = new RecommendationDeviceSnapshot(
                SensorId: 1,
                Category: "EnvironmentalEsp32",
                Location: "Greenhouse A",
                LastTelemetryAtUtc: Now.AddSeconds(-5),
                Temperature: 24,
                SoilMoisture: 5,
                Wattage: null,
                DesiredState: null,
                ReportedState: null,
                HasOutstandingCommand: false
            ),
            [2] = new RecommendationDeviceSnapshot(
                SensorId: 2,
                Category: "PumpController",
                Location: "Greenhouse A",
                LastTelemetryAtUtc: Now.AddSeconds(-5),
                Temperature: null,
                SoilMoisture: null,
                Wattage: 0,
                DesiredState: false,
                ReportedState: false,
                HasOutstandingCommand: false
            )
        };
    }

    private static RankedRecommendation Evaluate(
        Dictionary<int, RecommendationDeviceSnapshot>? devices = null,
        HistoricalRelationshipPattern? pattern = null,
        bool truncated = false)
    {
        var report = RecommendationRanker.Rank(
            History(pattern, truncated),
            devices ?? Devices(),
            Now
        );

        return Assert.Single(report.Recommendations);
    }

    [Fact]
    public void RepeatedCurrentAnomalyProducesReview()
    {
        var recommendation = Evaluate();

        Assert.Equal("Review", recommendation.Readiness);
        Assert.Equal("Pump", recommendation.Target);
        Assert.True(recommendation.RequestedState);
        Assert.Equal(3, recommendation.MatchedEpisodes);
        Assert.Equal(60, recommendation.FollowUpRatePercent);
        Assert.Equal(78.3, recommendation.Score);
        Assert.Equal(1, recommendation.Rank);
    }

    [Fact]
    public void RecommendationScoreEqualsItsComponents()
    {
        var recommendation = Evaluate();
        var score = recommendation.ScoreBreakdown;

        double total =
            score.Repetition +
            score.FollowUpRate +
            score.Recency +
            score.SearchContext +
            score.CurrentAnomaly +
            score.ActuatorReadiness;

        Assert.Equal(
            recommendation.Score,
            Math.Round(total, 1)
        );

        Assert.InRange(
            recommendation.Score,
            0,
            100
        );
    }

    [Fact]
    public void HistoricalPatternWithoutCurrentAnomalyIsMonitor()
    {
        var devices = Devices();

        devices[1] = devices[1] with
        {
            SoilMoisture = 35
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Monitor",
            recommendation.Readiness
        );

        Assert.Equal(
            8,
            recommendation.ScoreBreakdown.CurrentAnomaly
        );
    }

    [Fact]
    public void StaleSourceTelemetryBlocksRecommendation()
    {
        var devices = Devices();

        devices[1] = devices[1] with
        {
            LastTelemetryAtUtc = Now.AddMinutes(-3)
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );

        Assert.Contains(
            recommendation.Limitations,
            item => item.Contains(
                "source sensor has no sufficiently recent telemetry"
            )
        );
    }

    [Fact]
    public void StaleActuatorTelemetryBlocksRecommendation()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            LastTelemetryAtUtc = Now.AddMinutes(-5)
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void OutstandingCommandBlocksRecommendation()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            HasOutstandingCommand = true
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );

        Assert.Contains(
            recommendation.Limitations,
            item => item.Contains(
                "outstanding command"
            )
        );
    }

    [Fact]
    public void AlreadyRequestedStateBlocksRecommendation()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            DesiredState = true,
            ReportedState = true
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void UnknownActuatorStateBlocksRecommendation()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            ReportedState = null
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void ChangedLocationsBlockCrossDeviceRelationship()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            Location = "Pump Station"
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );

        Assert.Contains(
            recommendation.Limitations,
            item => item.Contains(
                "location relationship"
            )
        );
    }

    [Fact]
    public void IncompatibleDeviceCategoryBlocksRecommendation()
    {
        var devices = Devices();

        devices[2] = devices[2] with
        {
            Category = "SmartPlug"
        };

        var recommendation = Evaluate(devices);

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void IncompleteHistoryBlocksRecommendation()
    {
        var report = RecommendationRanker.Rank(
            History(truncated: true),
            Devices(),
            Now
        );

        var recommendation = Assert.Single(
            report.Recommendations
        );

        Assert.True(report.DataLimitReached);
        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void SingleHistoricalMatchRemainsExploratory()
    {
        var pattern = Pattern() with
        {
            MatchedEpisodes = 1,
            FollowUpRatePercent = 20,
            EvidenceLevel = "Exploratory"
        };

        var recommendation = Evaluate(
            pattern: pattern
        );

        Assert.Equal(
            "Monitor",
            recommendation.Readiness
        );
    }

    [Fact]
    public void OldHistoricalActionsRemainMonitor()
    {

        var pattern = Pattern() with
        {
            Examples =
            [
                new RelationshipExample(
            Now.AddDays(-9),
            5,
            Guid.NewGuid(),
            Now.AddDays(-9).AddMinutes(8),
            8
        )
            ]
        };


        var recommendation = Evaluate(
        pattern: pattern
    );

    Assert.Equal(
            "Monitor",
            recommendation.Readiness
        );

        Assert.Equal(
            0,
            recommendation.ScoreBreakdown.Recency
        );
    }

    [Fact]
    public void MissingDeviceInformationBlocksRecommendation()
    {
        var recommendation = Evaluate(
            new Dictionary<
                int,
                RecommendationDeviceSnapshot
            >()
        );

        Assert.Equal(
            "Blocked",
            recommendation.Readiness
        );
    }

    [Fact]
    public void RankingDoesNotModifyInputDeviceStates()
    {
        var devices = Devices();

        var originalSource = devices[1];
        var originalActuator = devices[2];

        var recommendation = Evaluate(devices);

        Assert.Equal("Review", recommendation.Readiness);
        Assert.Equal(originalSource, devices[1]);
        Assert.Equal(originalActuator, devices[2]);
    }

    [Fact]
    public void ReportContainsNoAutomaticExecutionCapability()
    {
        var report = RecommendationRanker.Rank(
            History(),
            Devices(),
            Now
        );

        Assert.Single(report.Recommendations);

        Assert.Contains(
            "No commands are executed",
            report.Disclaimer
        );

        Assert.Equal(1, report.ReviewCount);
        Assert.Equal(0, report.BlockedCount);
    }
}
