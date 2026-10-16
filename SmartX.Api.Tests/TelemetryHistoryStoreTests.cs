
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class TelemetryHistoryStoreTests
{
    private static readonly DateTime BaseTime =
        new(
            2026, 10, 15,
            12, 0, 0,
            DateTimeKind.Utc
        );

    private static TelemetryRecord CreateRecord(
        long id,
        int sensorId,
        DateTime timestamp,
        double temperature)
    {
        return new TelemetryRecord
        {
            Id = id,
            SensorId = sensorId,
            Timestamp = timestamp,
            Temperature = temperature
        };
    }

    [Fact]
    public void OutOfOrderRecordsAreSortedByTimestamp()
    {
        var store = new TelemetryHistoryStore();

        var latest = CreateRecord(
            3, 1, BaseTime.AddSeconds(3), 30
        );

        var oldest = CreateRecord(
            1, 1, BaseTime.AddSeconds(1), 20
        );

        var middle = CreateRecord(
            2, 1, BaseTime.AddSeconds(2), 25
        );

        // Insert in deliberately incorrect order.
        store.AddProcessed(latest);
        store.AddProcessed(oldest);
        store.AddProcessed(middle);

        var timeline = store.GetTimeline(1);

        Assert.Equal(
            new long[] { 1, 2, 3 },
            timeline.Select(r => r.Id).ToArray()
        );
    }

    [Fact]
    public void IdenticalTimestampsDoNotOverwriteRecords()
    {
        var store = new TelemetryHistoryStore();

        store.AddProcessed(
            CreateRecord(2, 1, BaseTime, 27)
        );

        store.AddProcessed(
            CreateRecord(1, 1, BaseTime, 25)
        );

        var timeline = store.GetTimeline(1);

        Assert.Equal(2, timeline.Count);

        Assert.Equal(
            new long[] { 1, 2 },
            timeline.Select(r => r.Id).ToArray()
        );
    }

    [Fact]
    public void DuplicateDatabaseIdsAreIgnored()
    {
        var store = new TelemetryHistoryStore();

        var record = CreateRecord(
            100, 1, BaseTime, 25
        );

        store.AddProcessed(record);
        store.AddProcessed(record);

        Assert.Single(store.GetTimeline(1));
    }

    [Fact]
    public void StoreKeepsOnlyOneHundredNewestRecords()
    {
        var store = new TelemetryHistoryStore();

        for (int i = 1; i <= 105; i++)
        {
            store.AddProcessed(
                CreateRecord(
                    i,
                    1,
                    BaseTime.AddSeconds(i),
                    20 + i
                )
            );
        }

        var timeline = store.GetTimeline(1);

        Assert.Equal(100, timeline.Count);

        // Oldest five must have been evicted.
        Assert.Equal(6L, timeline.First().Id);
        Assert.Equal(105L, timeline.Last().Id);
    }

    [Fact]
    public void HydrationMergesExistingAndLiveReadings()
    {
        var store = new TelemetryHistoryStore();

        // Already received through the worker.
        store.AddProcessed(
            CreateRecord(
                3, 1,
                BaseTime.AddSeconds(3),
                30
            )
        );

        // Previously saved SQL records.
        var persisted = new[]
        {
            CreateRecord(
                1, 1,
                BaseTime.AddSeconds(1),
                20
            ),
            CreateRecord(
                2, 1,
                BaseTime.AddSeconds(2),
                25
            ),
            CreateRecord(
                3, 1,
                BaseTime.AddSeconds(3),
                30
            )
        };

        store.HydrateFromDatabase(
            1,
            persisted
        );

        Assert.True(store.IsHydrated(1));

        var timeline = store.GetTimeline(1);

        Assert.Equal(
            new long[] { 1, 2, 3 },
            timeline.Select(r => r.Id).ToArray()
        );
    }

    [Fact]
    public void SensorsHaveIndependentTimelines()
    {
        var store = new TelemetryHistoryStore();

        store.AddProcessed(
            CreateRecord(1, 1, BaseTime, 25)
        );

        store.AddProcessed(
            CreateRecord(2, 2, BaseTime, 40)
        );

        Assert.Single(store.GetTimeline(1));
        Assert.Single(store.GetTimeline(2));

        store.RemoveSensor(1);

        Assert.Empty(store.GetTimeline(1));
        Assert.Single(store.GetTimeline(2));
    }
}
