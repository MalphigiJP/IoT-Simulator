
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public sealed class HistoricalRelationshipAnalyzerTests
{
    private static readonly DateTime Now =
        new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private static readonly RelationshipSensor[] Sensors =
    [
        new(1, "Greenhouse A"),
        new(2, "Greenhouse A"),
        new(3, "Remote Shed")
    ];

    private static RelationshipReading Reading(
        long id,
        int sensorId,
        int minutesAgo,
        double? moisture = null,
        double? temperature = null,
        int? wattage = null)
    {
        return new RelationshipReading(
            id,
            sensorId,
            Now.AddMinutes(-minutesAgo),
            temperature,
            moisture,
            wattage
        );
    }

    private static RelationshipAction Action(
        int sensorId,
        int minutesAgo,
        DeviceControlTarget target =
            DeviceControlTarget.Pump,
        bool requestedState = true,
        DeviceCommandStatus status =
            DeviceCommandStatus.Applied,
        DeviceCommandOrigin origin =
            DeviceCommandOrigin.Manual)
    {
        return new RelationshipAction(
            Guid.NewGuid(),
            sensorId,
            target,
            requestedState,
            Now.AddMinutes(-minutesAgo),
            status,
            origin
        );
    }

    private static HistoricalRelationshipReport Analyze(
        RelationshipReading[] readings,
        RelationshipAction[] actions,
        RelationshipSearch[]? searches = null,
        int windowMinutes = 30)
    {
        var data = new HistoricalRelationshipData(
            Sensors,
            readings,
            actions,
            searches ?? Array.Empty<RelationshipSearch>()
        );

        return HistoricalRelationshipAnalyzer.Analyze(
            data,
            new HistoricalRelationshipOptions(
                Days: 7,
                WindowMinutes: windowMinutes
            ),
            Now
        );
    }

    [Fact]
    public void SameLocationCommandCreatesAnObservedRelationship()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 7.5
                )
            ],
            [
                Action(
                    2,
                    70
                )
            ],
            [
                new RelationshipSearch(
                    1,
                    1,
                    Now.AddMinutes(-75),
                    "telemetry"
                )
            ]
        );

        var pattern = Assert.Single(
            report.Patterns
        );

        Assert.Equal(
            "LowSoilMoisture",
            pattern.Condition
        );

        Assert.Equal(1, pattern.SourceSensorId);
        Assert.Equal(2, pattern.ActionSensorId);
        Assert.Equal("Pump", pattern.Target);
        Assert.True(pattern.RequestedState);
        Assert.Equal("SameLocation", pattern.MatchScope);
        Assert.Equal(1, pattern.ObservedEpisodes);
        Assert.Equal(1, pattern.MatchedEpisodes);
        Assert.Equal(100, pattern.FollowUpRatePercent);
        Assert.Equal(1, pattern.RelatedSearches);
        Assert.Equal(10, pattern.AverageDelayMinutes);
        Assert.Equal("Exploratory", pattern.EvidenceLevel);
    }

    [Fact]
    public void DifferentLocationsAreNotLinked()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 5
                )
            ],
            [
                Action(
                    3,
                    70
                )
            ]
        );

        Assert.Empty(report.Patterns);
        Assert.Equal(1, report.ObservedEpisodes);
    }

    [Fact]
    public void FailedAndUndoCommandsAreIgnored()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 4
                )
            ],
            [
                Action(
                    2,
                    75,
                    status: DeviceCommandStatus.Failed
                ),
                Action(
                    2,
                    72,
                    origin: DeviceCommandOrigin.Undo
                ),
                Action(
                    2,
                    70,
                    status: DeviceCommandStatus.Pending
                )
            ]
        );

        Assert.Empty(report.Patterns);
        Assert.Equal(
            0,
            report.LoadedAppliedManualCommands
        );
    }

    [Fact]
    public void ConsecutiveAbnormalReadingsFormOneEpisode()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    2,
                    110,
                    wattage: 7000
                ),
                Reading(
                    2,
                    2,
                    109,
                    wattage: 8000
                ),
                Reading(
                    3,
                    2,
                    108,
                    wattage: 9000
                ),
                Reading(
                    4,
                    2,
                    90,
                    wattage: 100
                ),
                Reading(
                    5,
                    2,
                    80,
                    wattage: 7000
                )
            ],
            [
                Action(
                    2,
                    70,
                    DeviceControlTarget.Relay,
                    false
                )
            ]
        );

        var pattern = Assert.Single(
            report.Patterns
        );

        Assert.Equal("PowerSurge", pattern.Condition);
        Assert.Equal(2, report.ObservedEpisodes);
        Assert.Equal(2, pattern.ObservedEpisodes);
        Assert.Equal(1, pattern.MatchedEpisodes);
        Assert.Equal(50, pattern.FollowUpRatePercent);
        Assert.Equal("SameSensor", pattern.MatchScope);
    }

    [Fact]
    public void RecentEpisodesAreExcludedUntilWindowExpires()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    10,
                    moisture: 2
                )
            ],
            [
                Action(
                    2,
                    5
                )
            ]
        );

        Assert.Equal(0, report.ObservedEpisodes);
        Assert.Empty(report.Patterns);
    }

    [Fact]
    public void CommandsOutsideTimeWindowAreNotAssociated()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    90,
                    moisture: 5
                )
            ],
            [
                Action(
                    2,
                    50
                )
            ]
        );

        Assert.Equal(1, report.ObservedEpisodes);
        Assert.Empty(report.Patterns);
    }

    [Fact]
    public void CommandsBeforeAnomalyAreNotAssociated()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 5
                )
            ],
            [
                Action(
                    2,
                    90
                )
            ]
        );

        Assert.Empty(report.Patterns);
    }

    [Fact]
    public void UnscopedSearchesDoNotCountAsRelatedEvidence()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 5
                )
            ],
            [
                Action(
                    2,
                    70
                )
            ],
            [
                new RelationshipSearch(
                    1,
                    null,
                    Now.AddMinutes(-75),
                    "sensors"
                ),
                new RelationshipSearch(
                    2,
                    3,
                    Now.AddMinutes(-74),
                    "telemetry"
                )
            ]
        );

        var pattern = Assert.Single(report.Patterns);

        Assert.Equal(0, pattern.RelatedSearches);
    }

    [Fact]
    public void ExactThresholdValuesAreNotAnomalies()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 10,
                    temperature: 50,
                    wattage: 6000
                )
            ],
            [
                Action(
                    2,
                    70
                )
            ]
        );

        Assert.Equal(0, report.ObservedEpisodes);
        Assert.Empty(report.Patterns);
    }

    [Fact]
    public void HistoricalMatchDoesNotExecuteACommand()
    {
        var actions =
            new[]
            {
                Action(
                    2,
                    70
                )
            };

        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    moisture: 6
                )
            ],
            actions
        );

        Assert.Single(report.Patterns);
        Assert.Single(actions);
        Assert.Equal(
            DeviceCommandStatus.Applied,
            actions[0].Status
        );
        Assert.Contains(
            "not proof of causation",
            report.Interpretation
        );
    }

    [Fact]
    public void HighTemperatureIsDetected()
    {
        var report = Analyze(
            [
                Reading(
                    1,
                    1,
                    80,
                    temperature: 54
                )
            ],
            [
                Action(
                    2,
                    70
                )
            ]
        );

        var pattern = Assert.Single(report.Patterns);

        Assert.Equal(
            "HighTemperature",
            pattern.Condition
        );

        Assert.Equal(
            "Temperature",
            pattern.Metric
        );

        Assert.Equal("°C", pattern.Unit);
    }

    [Fact]
    public void TruncationIsReported()
    {
        var data = new HistoricalRelationshipData(
            Sensors,
            Array.Empty<RelationshipReading>(),
            Array.Empty<RelationshipAction>(),
            Array.Empty<RelationshipSearch>(),
            TelemetryTruncated: true
        );

        var report = HistoricalRelationshipAnalyzer.Analyze(
            data,
            new HistoricalRelationshipOptions(),
            Now
        );

        Assert.True(report.DataLimitReached);
    }
}
