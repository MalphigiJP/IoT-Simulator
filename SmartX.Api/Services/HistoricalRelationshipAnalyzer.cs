
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public static class HistoricalRelationshipAnalyzer
{
    private static readonly TimeSpan EpisodeGap =
        TimeSpan.FromMinutes(15);

    private sealed record Episode(
        int Number,
        int SourceSensorId,
        string Condition,
        string Metric,
        string Comparison,
        double Threshold,
        string Unit,
        double ObservedValue,
        DateTime ObservedAtUtc
    );

    private sealed record PatternKey(
        int SourceSensorId,
        string Condition,
        int ActionSensorId,
        DeviceControlTarget Target,
        bool RequestedState,
        string MatchScope
    );

    private sealed class PatternAccumulator
    {
        public List<RelationshipExample> Examples { get; } = [];

        public HashSet<long> SearchIds { get; } = [];

        public int MatchedEpisodes { get; set; }

        public double TotalDelayMinutes { get; set; }
    }

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

    private static void Observe(
        List<Episode> episodes,
        Dictionary<(int SensorId, string Condition), DateTime>
            lastAbnormal,
        int sensorId,
        DateTime timestamp,
        string condition,
        string metric,
        string comparison,
        double threshold,
        string unit,
        double? value,
        bool abnormal)
    {
        if (!value.HasValue)
        {
            return;
        }

        var key = (sensorId, condition);

        if (!abnormal)
        {
            lastAbnormal.Remove(key);
            return;
        }

        if (
            !lastAbnormal.TryGetValue(
                key,
                out var previousAbnormal
            ) ||
            timestamp - previousAbnormal > EpisodeGap
        )
        {
            episodes.Add(new Episode(
                episodes.Count + 1,
                sensorId,
                condition,
                metric,
                comparison,
                threshold,
                unit,
                value.Value,
                timestamp
            ));
        }

        lastAbnormal[key] = timestamp;
    }

    private static List<Episode> BuildEpisodes(
        IEnumerable<RelationshipReading> readings,
        DateTime fromUtc,
        DateTime nowUtc)
    {
        var episodes = new List<Episode>();

        var lastAbnormal = new Dictionary<
            (int SensorId, string Condition),
            DateTime
        >();

        foreach (
            var reading in readings
                .OrderBy(r => AsUtc(r.TimestampUtc))
                .ThenBy(r => r.Id)
        )
        {
            var timestamp = AsUtc(
                reading.TimestampUtc
            );

            if (
                timestamp < fromUtc ||
                timestamp > nowUtc
            )
            {
                continue;
            }

            Observe(
                episodes,
                lastAbnormal,
                reading.SensorId,
                timestamp,
                "LowSoilMoisture",
                "SoilMoisture",
                "<",
                10,
                "%",
                reading.SoilMoisture,
                reading.SoilMoisture is < 10
            );

            Observe(
                episodes,
                lastAbnormal,
                reading.SensorId,
                timestamp,
                "HighTemperature",
                "Temperature",
                ">",
                50,
                "°C",
                reading.Temperature,
                reading.Temperature is > 50
            );

            Observe(
                episodes,
                lastAbnormal,
                reading.SensorId,
                timestamp,
                "PowerSurge",
                "Wattage",
                ">",
                6000,
                "W",
                reading.Wattage,
                reading.Wattage is > 6000
            );
        }

        return episodes;
    }

    public static HistoricalRelationshipReport Analyze(
        HistoricalRelationshipData data,
        HistoricalRelationshipOptions options,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Days is < 1 or > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Days)
            );
        }

        if (options.WindowMinutes is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.WindowMinutes)
            );
        }

        var now = AsUtc(nowUtc);

        var fromUtc = now.AddDays(
            -options.Days
        );

        var cutoffUtc = now.AddMinutes(
            -options.WindowMinutes
        );

        var window = TimeSpan.FromMinutes(
            options.WindowMinutes
        );

        var sensors = data.Sensors
            .ToDictionary(s => s.Id);

        var normalizedLocations = sensors.ToDictionary(
            pair => pair.Key,
            pair => NormalizeLocation(
                pair.Value.Location
            )
        );

        var episodes = BuildEpisodes(
                data.Readings,
                fromUtc,
                now
            )
            .Where(e =>
                e.ObservedAtUtc <= cutoffUtc &&
                (
                    !options.SensorId.HasValue ||
                    e.SourceSensorId ==
                    options.SensorId.Value
                )
            )
            .ToArray();

        var episodeTotals = episodes
            .GroupBy(e => (
                e.SourceSensorId,
                e.Condition
            ))
            .ToDictionary(
                group => group.Key,
                group => group.Count()
            );

        var actions = data.Actions
            .Where(a =>
                a.Status == DeviceCommandStatus.Applied &&
                a.Origin == DeviceCommandOrigin.Manual
            )
            .Where(a =>
                AsUtc(a.RequestedAtUtc) >= fromUtc &&
                AsUtc(a.RequestedAtUtc) <= now
            )
            .OrderBy(a =>
                AsUtc(a.RequestedAtUtc)
            )
            .ToArray();

        var searches = data.Searches
            .Where(s => s.SensorId.HasValue)
            .Where(s =>
                AsUtc(s.RecordedAtUtc) >= fromUtc &&
                AsUtc(s.RecordedAtUtc) <= now
            )
            .ToArray();

        var accumulators =
            new Dictionary<PatternKey, PatternAccumulator>();

        foreach (var episode in episodes)
        {
            if (!sensors.ContainsKey(
                episode.SourceSensorId
            ))
            {
                continue;
            }

            string sourceLocation =
                normalizedLocations[
                    episode.SourceSensorId
                ];

            var firstMatchingActions =
                new Dictionary<PatternKey, RelationshipAction>();

            foreach (var action in actions)
            {
                var actionTime = AsUtc(
                    action.RequestedAtUtc
                );

                if (
                    actionTime <= episode.ObservedAtUtc ||
                    actionTime >
                    episode.ObservedAtUtc + window
                )
                {
                    continue;
                }

                if (!sensors.ContainsKey(action.SensorId))
                {
                    continue;
                }

                string matchScope;

                if (
                    action.SensorId ==
                    episode.SourceSensorId
                )
                {
                    matchScope = "SameSensor";
                }
                else if (
                    sourceLocation.Length > 0 &&
                    sourceLocation ==
                    normalizedLocations[action.SensorId]
                )
                {
                    matchScope = "SameLocation";
                }
                else
                {
                    continue;
                }

                var key = new PatternKey(
                    episode.SourceSensorId,
                    episode.Condition,
                    action.SensorId,
                    action.Target,
                    action.RequestedState,
                    matchScope
                );

                if (!firstMatchingActions.ContainsKey(key))
                {
                    firstMatchingActions[key] = action;
                }
            }

            foreach (var pair in firstMatchingActions)
            {
                var key = pair.Key;
                var action = pair.Value;

                if (!accumulators.TryGetValue(
                        key,
                        out var accumulator))
                {
                    accumulator = new PatternAccumulator();
                    accumulators[key] = accumulator;
                }

                double delayMinutes =
                    (AsUtc(action.RequestedAtUtc) -
                        episode.ObservedAtUtc)
                    .TotalMinutes;

                accumulator.MatchedEpisodes++;
                accumulator.TotalDelayMinutes += delayMinutes;

                if (accumulator.Examples.Count < 3)
                {
                    accumulator.Examples.Add(
                        new RelationshipExample(
                            episode.ObservedAtUtc,
                            episode.ObservedValue,
                            action.CommandId,
                            AsUtc(action.RequestedAtUtc),
                            Math.Round(delayMinutes, 1)
                        )
                    );
                }

                foreach (var search in searches)
                {
                    if (
                        search.SensorId !=
                            episode.SourceSensorId &&
                        search.SensorId != action.SensorId
                    )
                    {
                        continue;
                    }

                    var searchTime = AsUtc(
                        search.RecordedAtUtc
                    );

                    if (
                        searchTime >= episode.ObservedAtUtc &&
                        searchTime <=
                            AsUtc(action.RequestedAtUtc)
                    )
                    {
                        accumulator.SearchIds.Add(
                            search.Id
                        );
                    }
                }
            }
        }

        var patterns = accumulators
            .Select(pair =>
            {
                var key = pair.Key;
                var accumulator = pair.Value;

                int totalEpisodes = episodeTotals[
                    (
                        key.SourceSensorId,
                        key.Condition
                    )
                ];

                var exampleEpisode = episodes.First(
                    episode =>
                        episode.SourceSensorId ==
                            key.SourceSensorId &&
                        episode.Condition ==
                            key.Condition
                );

                double rate = totalEpisodes == 0
                    ? 0
                    : 100.0 *
                      accumulator.MatchedEpisodes /
                      totalEpisodes;

                return new HistoricalRelationshipPattern(
                    key.Condition,
                    exampleEpisode.Metric,
                    exampleEpisode.Comparison,
                    exampleEpisode.Threshold,
                    exampleEpisode.Unit,
                    key.SourceSensorId,
                    sensors[
                        key.SourceSensorId
                    ].Location,
                    key.ActionSensorId,
                    key.Target.ToString(),
                    key.RequestedState,
                    key.MatchScope,
                    totalEpisodes,
                    accumulator.MatchedEpisodes,
                    Math.Round(rate, 1),
                    accumulator.SearchIds.Count,
                    Math.Round(
                        accumulator.TotalDelayMinutes /
                        accumulator.MatchedEpisodes,
                        1
                    ),
                    accumulator.MatchedEpisodes >= 3
                        ? "Repeated"
                        : "Exploratory",
                    accumulator.Examples.ToArray()
                );
            })
            .OrderByDescending(p =>
                p.MatchedEpisodes
            )
            .ThenByDescending(p =>
                p.FollowUpRatePercent
            )
            .ThenBy(p =>
                p.SourceSensorId
            )
            .ToArray();

        return new HistoricalRelationshipReport(
            fromUtc,
            now,
            cutoffUtc,
            options.WindowMinutes,
            data.Readings.Count,
            actions.Length,
            data.Searches.Count,
            episodes.Length,
            patterns.Length,
            data.TelemetryTruncated ||
                data.CommandsTruncated ||
                data.SearchesTruncated,
            "Associations are time-ordered observations, not proof of causation or recommendations to execute commands.",
            patterns.Take(50).ToArray()
        );
    }
}
