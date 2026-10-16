using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class TelemetryQueueTests
{
    private static QueuedTelemetry CreatePacket(
        double temperature = 25,
        double? moisture = null,
        int? wattage = null)
    {
        var now = DateTime.UtcNow;

        return new QueuedTelemetry
        {
            PacketId = Guid.NewGuid(),
            SensorId = 1,
            Temperature = temperature,
            SoilMoisture = moisture,
            Wattage = wattage,
            Timestamp = now,
            ReceivedAtUtc = now
        };
    }

    [Fact]
    public async Task NormalPacketsPreserveFifoOrder()
    {
        using var queue = new TelemetryQueue();

        var first = CreatePacket(20);
        var second = CreatePacket(21);
        var third = CreatePacket(22);

        Assert.True(queue.TryEnqueueBatch(
            new[] { first, second, third }
        ));

        foreach (var expected in new[]
        {
            first, second, third
        })
        {
            var actual = await queue.DequeueAsync(
                CancellationToken.None
            );

            Assert.Equal(
                expected.PacketId,
                actual.PacketId
            );

            queue.MarkProcessed(actual);
        }

        var status = queue.GetStatus();

        Assert.Equal(3, status.Accepted);
        Assert.Equal(3, status.Processed);
        Assert.Equal(0, status.Waiting);
        Assert.Equal(0, status.Processing);
    }

    [Fact]
    public async Task QueueStatisticsTrackSuccessfulProcessing()
    {
        using var queue = new TelemetryQueue();

        var packet = CreatePacket();

        Assert.True(queue.TryEnqueue(packet));

        Assert.Equal(1, queue.GetStatus().Waiting);
        Assert.Equal(1, queue.GetStatus().Accepted);

        var next = await queue.DequeueAsync(
            CancellationToken.None
        );

        Assert.Equal(0, queue.GetStatus().Waiting);
        Assert.Equal(1, queue.GetStatus().Processing);

        queue.MarkProcessed(next);

        Assert.Equal(1, queue.GetStatus().Processed);
        Assert.Equal(0, queue.GetStatus().Processing);
    }

    [Fact]
    public async Task FailedPacketsIncreaseFailureCounter()
    {
        using var queue = new TelemetryQueue();

        var packet = CreatePacket();

        Assert.True(queue.TryEnqueue(packet));

        var next = await queue.DequeueAsync(
            CancellationToken.None
        );

        queue.MarkFailed(next);

        var status = queue.GetStatus();

        Assert.Equal(1, status.Failed);
        Assert.Equal(0, status.Processed);
        Assert.Equal(0, status.Processing);
    }

    [Fact]
    public void OversizedBatchIsRejectedAtomically()
    {
        using var queue = new TelemetryQueue();

        var packets = Enumerable
            .Range(0, TelemetryQueue.MaxCapacity + 1)
            .Select(_ => CreatePacket())
            .ToList();

        Assert.False(
            queue.TryEnqueueBatch(packets)
        );

        var status = queue.GetStatus();

        Assert.Equal(0, status.Waiting);
        Assert.Equal(0, status.Accepted);

        Assert.Equal(
            packets.Count,
            status.Rejected
        );
    }

    [Theory]
    [InlineData(25, TelemetryPriority.Normal)]
    [InlineData(58, TelemetryPriority.Critical)]
    [InlineData(82, TelemetryPriority.Emergency)]
    public void TemperatureIsClassifiedCorrectly(
        double temperature,
        TelemetryPriority expected)
    {
        var packet = CreatePacket(temperature);

        Assert.Equal(
            expected,
            TelemetryPriorityClassifier.Classify(packet)
        );
    }

    [Fact]
    public void CriticalMoistureIsDetected()
    {
        var packet = CreatePacket(
            temperature: 25,
            moisture: 8
        );

        Assert.Equal(
            TelemetryPriority.Critical,
            TelemetryPriorityClassifier.Classify(packet)
        );
    }

    [Fact]
    public void EmergencyPowerSpikeIsDetected()
    {
        var packet = CreatePacket(
            temperature: 25,
            wattage: 9500
        );

        Assert.Equal(
            TelemetryPriority.Emergency,
            TelemetryPriorityClassifier.Classify(packet)
        );
    }

    [Fact]
    public async Task EmergencyOvertakesOlderPackets()
    {
        using var queue = new TelemetryQueue();

        var normal = CreatePacket(25);
        var critical = CreatePacket(60);
        var emergency = CreatePacket(80);

        // Normal arrived first.
        Assert.True(queue.TryEnqueueBatch(
            new[] { normal, critical, emergency }
        ));

        var first = await queue.DequeueAsync(
            CancellationToken.None
        );

        Assert.Equal(
            emergency.PacketId,
            first.PacketId
        );
        queue.MarkProcessed(first);

        var second = await queue.DequeueAsync(
            CancellationToken.None
        );

        Assert.Equal(
            critical.PacketId,
            second.PacketId
        );
        queue.MarkProcessed(second);

        var third = await queue.DequeueAsync(
            CancellationToken.None
        );

        Assert.Equal(
            normal.PacketId,
            third.PacketId
        );
        queue.MarkProcessed(third);
    }

    [Fact]
    public async Task EqualPriorityPreservesArrivalOrder()
    {
        using var queue = new TelemetryQueue();

        var first = CreatePacket(75);
        var second = CreatePacket(80);
        var third = CreatePacket(85);

        Assert.True(queue.TryEnqueueBatch(
            new[] { first, second, third }
        ));

        foreach (var expected in new[]
        {
            first, second, third
        })
        {
            var actual = await queue.DequeueAsync(
                CancellationToken.None
            );

            Assert.Equal(
                expected.PacketId,
                actual.PacketId
            );

            queue.MarkProcessed(actual);
        }
    }

    [Fact]
    public async Task NormalTrafficIsNotStarved()
    {
        using var queue = new TelemetryQueue();

        var emergencies = Enumerable
            .Range(0, 12)
            .Select(_ => CreatePacket(80))
            .ToList();

        var normal = CreatePacket(25);

        var packets = emergencies
            .Append(normal)
            .ToList();

        Assert.True(
            queue.TryEnqueueBatch(packets)
        );

        // The first ten should be emergency.
        for (int i = 0; i < 10; i++)
        {
            var packet = await queue.DequeueAsync(
                CancellationToken.None
            );

            Assert.Equal(
                TelemetryPriority.Emergency,
                TelemetryPriorityClassifier.Classify(packet)
            );

            queue.MarkProcessed(packet);
        }

        // Normal receives a turn after ten
        // consecutive priority packets.
        var next = await queue.DequeueAsync(
            CancellationToken.None
        );

        Assert.Equal(normal.PacketId, next.PacketId);

        queue.MarkProcessed(next);
    }

    [Fact]
    public async Task PriorityCountersTrackEachCategory()
    {
        using var queue = new TelemetryQueue();

        Assert.True(queue.TryEnqueueBatch(
            new[]
            {
                CreatePacket(25),
                CreatePacket(58),
                CreatePacket(82)
            }
        ));

        for (int i = 0; i < 3; i++)
        {
            var packet = await queue.DequeueAsync(
                CancellationToken.None
            );

            queue.MarkProcessed(packet);
        }

        var status = queue.GetStatus();

        Assert.Equal(1, status.NormalProcessed);
        Assert.Equal(1, status.CriticalProcessed);
        Assert.Equal(1, status.EmergencyProcessed);
        Assert.Equal(3, status.Processed);
    }
}
