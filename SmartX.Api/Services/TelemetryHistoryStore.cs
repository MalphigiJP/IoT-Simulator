
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record TelemetryHistoryStatus
{
    public int SensorTimelines { get; init; }
    public int HydratedTimelines { get; init; }
    public int CachedRecords { get; init; }
    public int MaxRecordsPerSensor { get; init; }
}

public sealed class TelemetryHistoryStore
{
    // Maximum recent readings retained per sensor.
    public const int MaxRecordsPerSensor = 100;

    // Sensor ID -> chronological timeline.
    private readonly Dictionary<int, SensorTimeline>
        _timelines = new();

    private readonly object _sync = new();

    private sealed class SensorTimeline
    {
        // The required SortedDictionary structure.
        public SortedDictionary<
            DateTime,
            List<TelemetryRecord>
        > RecordsByTimestamp
        { get; } = new();

        // Avoid duplicate database record IDs.
        public HashSet<long> RecordIds { get; } = new();

        public int Count { get; set; }

        // Indicates whether existing SQL history
        // has been loaded into this timeline.
        public bool Hydrated { get; set; }
    }

    private SensorTimeline GetOrCreate(int sensorId)
    {
        if (!_timelines.TryGetValue(
            sensorId,
            out var timeline))
        {
            timeline = new SensorTimeline();
            _timelines[sensorId] = timeline;
        }

        return timeline;
    }

    private static TelemetryRecord Copy(
        TelemetryRecord record)
    {
        return new TelemetryRecord
        {
            Id = record.Id,
            SensorId = record.SensorId,
            Temperature = record.Temperature,
            SoilMoisture = record.SoilMoisture,
            Wattage = record.Wattage,
            ValveOpen = record.ValveOpen,
            Timestamp = record.Timestamp
        };
    }

    private static void Insert(
        SensorTimeline timeline,
        TelemetryRecord record)
    {
        if (record.Id <= 0)
        {
            throw new ArgumentException(
                "Telemetry must be saved before caching."
            );
        }

        // Ignore duplicate database records.
        if (!timeline.RecordIds.Add(record.Id))
        {
            return;
        }

        if (!timeline.RecordsByTimestamp.TryGetValue(
            record.Timestamp,
            out var records))
        {
            records = new List<TelemetryRecord>();

            timeline.RecordsByTimestamp[
                record.Timestamp
            ] = records;
        }

        records.Add(Copy(record));

        // Consistent order for identical timestamps.
        records.Sort((a, b) => a.Id.CompareTo(b.Id));

        timeline.Count++;

        while (timeline.Count > MaxRecordsPerSensor)
        {
            // SortedDictionary iterates from
            // the oldest timestamp to the newest.
            var oldest = timeline.RecordsByTimestamp.First();

            var oldestRecords = oldest.Value;

            // Among equal timestamps, remove
            // the oldest database ID first.
            var removed = oldestRecords[0];

            oldestRecords.RemoveAt(0);

            timeline.RecordIds.Remove(removed.Id);

            timeline.Count--;

            if (oldestRecords.Count == 0)
            {
                timeline.RecordsByTimestamp.Remove(
                    oldest.Key
                );
            }
        }
    }

    public void AddProcessed(TelemetryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_sync)
        {
            var timeline = GetOrCreate(
                record.SensorId
            );

            Insert(timeline, record);
        }
    }

    public bool IsHydrated(int sensorId)
    {
        lock (_sync)
        {
            return _timelines.TryGetValue(
                sensorId,
                out var timeline
            ) && timeline.Hydrated;
        }
    }

    public void HydrateFromDatabase(
        int sensorId,
        IEnumerable<TelemetryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        lock (_sync)
        {
            var timeline = GetOrCreate(sensorId);

            if (timeline.Hydrated)
            {
                return;
            }

            // Merge SQL records with new readings
            // already received by the live worker.
            foreach (var record in records
                .Where(r => r.SensorId == sensorId)
                .OrderBy(r => r.Timestamp)
                .ThenBy(r => r.Id))
            {
                Insert(timeline, record);
            }

            timeline.Hydrated = true;
        }
    }

    public List<TelemetryRecord> GetTimeline(int sensorId)
    {
        lock (_sync)
        {
            if (!_timelines.TryGetValue(
                sensorId,
                out var timeline))
            {
                return new List<TelemetryRecord>();
            }

            // Already ordered by timestamp,
            // then by record ID within each bucket.
            return timeline.RecordsByTimestamp
                .Values
                .SelectMany(records => records)
                .Select(Copy)
                .ToList();
        }
    }

    public void RemoveSensor(int sensorId)
    {
        lock (_sync)
        {
            _timelines.Remove(sensorId);
        }
    }

    public TelemetryHistoryStatus GetStatus()
    {
        lock (_sync)
        {
            return new TelemetryHistoryStatus
            {
                SensorTimelines = _timelines.Count,

                HydratedTimelines = _timelines.Values
                    .Count(timeline => timeline.Hydrated),

                CachedRecords = _timelines.Values
                    .Sum(timeline => timeline.Count),

                MaxRecordsPerSensor = MaxRecordsPerSensor
            };
        }
    }
}
