
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public static class RecommendationRanker
{
    private static readonly TimeSpan FreshnessLimit =
        TimeSpan.FromSeconds(30);

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static bool IsFresh(
        DateTime? timestamp,
        DateTime nowUtc)
    {
        if (!timestamp.HasValue)
        {
            return false;
        }

        var age = AsUtc(nowUtc) -
            AsUtc(timestamp.Value);

        return age >= TimeSpan.Zero &&
            age <= FreshnessLimit;
    }

    private static string NormalizeLocation(string? value)
    {
        return string.Join(
            " ",
            (value ?? "").Trim().Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
        ).ToUpperInvariant();
    }

    private static double? GetObservedValue(
        HistoricalRelationshipPattern pattern,
        RecommendationDeviceSnapshot? source)
    {
        if (source is null)
        {
            return null;
        }

        return pattern.Condition switch
        {
            "LowSoilMoisture" => source.SoilMoisture,
            "HighTemperature" => source.Temperature,
            "PowerSurge" => source.Wattage,
            _ => null
        };
    }

    private static bool IsCurrentlyAbnormal(
        HistoricalRelationshipPattern pattern,
        double? value)
    {
        if (!value.HasValue)
        {
            return false;
        }

        return pattern.Condition switch
        {
            "LowSoilMoisture" => value.Value < 10,
            "HighTemperature" => value.Value > 50,
            "PowerSurge" => value.Value > 6000,
            _ => false
        };
    }

    private static double GetRecencyPoints(
        DateTime? lastAction,
        DateTime nowUtc)
    {
        if (!lastAction.HasValue)
        {
            return 0;
        }

        var age = AsUtc(nowUtc) -
            AsUtc(lastAction.Value);

        if (age < TimeSpan.Zero)
        {
            return 0;
        }

        if (age <= TimeSpan.FromDays(1))
        {
            return 15;
        }

        if (age <= TimeSpan.FromDays(3))
        {
            return 10;
        }

        if (age <= TimeSpan.FromDays(7))
        {
            return 5;
        }

        return 0;
    }

    private static string GetActionText(
        string target,
        bool state)
    {
        return target switch
        {
            "Pump" => state
                ? "Consider starting the pump"
                : "Consider stopping the pump",
            "Valve" => state
                ? "Consider opening the valve"
                : "Consider closing the valve",
            "Relay" => state
                ? "Consider turning on the relay"
                : "Consider turning off the relay",
            _ => "Review the recorded operator action"
        };
    }

    public static RecommendationReport Rank(
        HistoricalRelationshipReport history,
        IReadOnlyDictionary<int, RecommendationDeviceSnapshot>
            devices,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(devices);

        var now = AsUtc(nowUtc);

        var recommendations = history.Patterns
            .Select(pattern =>
                Evaluate(
                    pattern,
                    history.DataLimitReached,
                    devices,
                    now
                )
            )
            .OrderBy(r => r.Readiness switch
            {
                "Review" => 0,
                "Monitor" => 1,
                _ => 2
            })
            .ThenByDescending(r => r.Score)
            .ThenByDescending(r => r.MatchedEpisodes)
            .ThenBy(r => r.Id)
            .Select((recommendation, index) =>
                recommendation with
                {
                    Rank = index + 1
                }
            )
            .ToArray();

        return new RecommendationReport(
            now,
            history.FromUtc,
            history.ToUtc,
            history.WindowMinutes,
            history.TotalDetectedPatterns,
            recommendations.Count(r =>
                r.Readiness == "Review"
            ),
            recommendations.Count(r =>
                r.Readiness == "Monitor"
            ),
            recommendations.Count(r =>
                r.Readiness == "Blocked"
            ),
            history.DataLimitReached,
            "Advisory only. Scores are heuristic rankings, not probabilities or proof that an action will resolve an anomaly. No commands are executed.",
            recommendations
        );
    }

    private static RankedRecommendation Evaluate(
        HistoricalRelationshipPattern pattern,
        bool dataLimitReached,
        IReadOnlyDictionary<int, RecommendationDeviceSnapshot>
            devices,
        DateTime nowUtc)
    {
        devices.TryGetValue(
            pattern.SourceSensorId,
            out var source
        );

        devices.TryGetValue(
            pattern.ActionSensorId,
            out var actuator
        );

        DateTime? lastAction =
            pattern.Examples.Count > 0
                ? pattern.Examples.Max(e =>
                    AsUtc(e.CommandRequestedAtUtc))
                : null;

        bool sourceFresh = IsFresh(
            source?.LastTelemetryAtUtc,
            nowUtc
        );

        bool actuatorFresh = IsFresh(
            actuator?.LastTelemetryAtUtc,
            nowUtc
        );

        double? observedValue = GetObservedValue(
            pattern,
            source
        );

        bool anomalyActive =
            sourceFresh &&
            IsCurrentlyAbnormal(
                pattern,
                observedValue
            );

        bool categoryMatches =
            actuator is not null &&
            DeviceControlCatalog
                .GetSupportedTarget(actuator.Category)
                ?.ToString() == pattern.Target;

        bool locationMatches =
            source is not null &&
            actuator is not null &&
            (
                pattern.SourceSensorId ==
                    pattern.ActionSensorId ||
                (
                    NormalizeLocation(source.Location).Length > 0 &&
                    NormalizeLocation(source.Location) ==
                    NormalizeLocation(actuator.Location)
                )
            );

        bool stateKnown =
            actuator?.DesiredState != null &&
            actuator.ReportedState != null;

        bool actionAlreadyRequested =
            actuator is not null &&
            (
                actuator.DesiredState == pattern.RequestedState ||
                actuator.ReportedState == pattern.RequestedState
            );

        bool actuatorReady =
            actuatorFresh &&
            categoryMatches &&
            locationMatches &&
            stateKnown &&
            actuator?.HasOutstandingCommand == false &&
            !actionAlreadyRequested;

        double repetitionPoints = Math.Min(
            25,
            pattern.MatchedEpisodes * 5.0
        );

        double followUpPoints = Math.Clamp(
            pattern.FollowUpRatePercent,
            0,
            100
        ) * 0.25;

        double recencyPoints = GetRecencyPoints(
            lastAction,
            nowUtc
        );

        double searchPoints = Math.Min(
            5,
            pattern.RelatedSearches * (5.0 / 3)
        );

        double anomalyPoints = anomalyActive
            ? 20
            : sourceFresh
                ? 8
                : 0;

        double readinessPoints = actuatorReady
            ? 10
            : 0;

        var breakdown = new RecommendationScoreBreakdown(
            Math.Round(repetitionPoints, 1),
            Math.Round(followUpPoints, 1),
            Math.Round(recencyPoints, 1),
            Math.Round(searchPoints, 1),
            Math.Round(anomalyPoints, 1),
            Math.Round(readinessPoints, 1)
        );

        double totalScore = Math.Round(
            breakdown.Repetition +
            breakdown.FollowUpRate +
            breakdown.Recency +
            breakdown.SearchContext +
            breakdown.CurrentAnomaly +
            breakdown.ActuatorReadiness,
            1
        );

        var evidence = new List<string>
        {
            $"{pattern.MatchedEpisodes} of {pattern.ObservedEpisodes} historical episodes were followed by this action.",
            $"Observed follow-up rate: {pattern.FollowUpRatePercent:0.#}%.",
            $"Average historical delay: {pattern.AverageDelayMinutes:0.#} minutes.",
            $"Matching scope: {pattern.MatchScope}.",
            $"Related sensor-scoped searches: {pattern.RelatedSearches}."
        };

        if (lastAction.HasValue)
        {
            evidence.Add(
                $"Most recent retained matching action: {lastAction.Value:O}."
            );
        }

        var checks = new List<string>();
        var blockers = new List<string>();

        if (dataLimitReached)
        {
            blockers.Add(
                "Historical data was truncated. The analysis may not represent the full observation period."
            );
        }

        if (source is null)
        {
            blockers.Add(
                "The anomaly source sensor could not be resolved."
            );
        }
        else if (!sourceFresh)
        {
            blockers.Add(
                "The source sensor has no sufficiently recent telemetry."
            );
        }
        else
        {
            checks.Add(
                $"Recent source telemetry is available. Current value: {observedValue?.ToString("0.##") ?? "Unknown"} {pattern.Unit}."
            );
        }

        if (actuator is null)
        {
            blockers.Add(
                "The target actuator could not be resolved."
            );
        }
        else
        {
            if (!actuatorFresh)
            {
                blockers.Add(
                    "The actuator has no sufficiently recent telemetry."
                );
            }

            if (!categoryMatches)
            {
                blockers.Add(
                    "The actuator category does not support the historically observed command target."
                );
            }

            if (!locationMatches)
            {
                blockers.Add(
                    "The devices no longer have a supported sensor or location relationship."
                );
            }

            if (!stateKnown)
            {
                blockers.Add(
                    "The actuator's desired or reported state is unknown."
                );
            }

            if (actuator.HasOutstandingCommand)
            {
                blockers.Add(
                    "The actuator already has an outstanding command."
                );
            }

            if (actionAlreadyRequested)
            {
                blockers.Add(
                    "The actuator is already in, or has already requested, the suggested state."
                );
            }
        }

        if (anomalyActive)
        {
            checks.Add(
                "The historical anomaly condition is present in the latest telemetry."
            );
        }
        else if (sourceFresh)
        {
            checks.Add(
                "The historical anomaly condition is not currently confirmed."
            );
        }

        if (actuatorReady)
        {
            checks.Add(
                "The actuator has recent telemetry, a known control state and no outstanding command."
            );
        }

        bool repeatedEvidence =
            pattern.MatchedEpisodes >= 3 &&
            pattern.ObservedEpisodes >= 3 &&
            pattern.FollowUpRatePercent >= 50 &&
            recencyPoints > 0;

        string readiness;

        if (blockers.Count > 0)
        {
            readiness = "Blocked";
        }
        else if (repeatedEvidence && anomalyActive)
        {
            readiness = "Review";
        }
        else
        {
            readiness = "Monitor";

            if (!repeatedEvidence)
            {
                checks.Add(
                    "More repeated and recent historical observations are needed before prioritising this suggestion."
                );
            }
        }

        var limitations = new List<string>
        {
            "Historical association does not establish causation or the appropriateness of an action.",
            "Recent telemetry is not proof of authenticated device connectivity.",
            "The operator must verify conditions before submitting any command."
        };

        limitations.AddRange(blockers);

        string id = string.Join(
            ":",
            pattern.Condition,
            pattern.SourceSensorId,
            pattern.ActionSensorId,
            pattern.Target,
            pattern.RequestedState ? "On" : "Off"
        );

        return new RankedRecommendation(
            id,
            0,
            pattern.Condition,
            pattern.SourceSensorId,
            pattern.ActionSensorId,
            pattern.SourceLocation,
            pattern.Target,
            pattern.RequestedState,
            GetActionText(
                pattern.Target,
                pattern.RequestedState
            ),
            readiness,
            totalScore,
            pattern.EvidenceLevel,
            pattern.ObservedEpisodes,
            pattern.MatchedEpisodes,
            pattern.FollowUpRatePercent,
            pattern.RelatedSearches,
            lastAction,
            observedValue,
            pattern.Unit,
            breakdown,
            evidence,
            checks,
            limitations
        );
    }
}
