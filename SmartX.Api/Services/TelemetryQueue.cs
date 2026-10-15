
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record TelemetryQueueStatus
{
    public int Capacity { get; init; }

    public int Waiting { get; init; }

    public int Processing { get; init; }

    public int HighWatermark { get; init; }

    public long Accepted { get; init; }

    public long Processed { get; init; }

    public long Failed { get; init; }

    public long Rejected { get; init; }

    public DateTime? LastProcessedUtc { get; init; }
}

public sealed class TelemetryQueue : IDisposable
{
    // Maximum packets awaiting processing.
    public const int MaxCapacity = 5000;

    // FIFO data structure required by Part 2.
    private readonly Queue<QueuedTelemetry> _queue = new();

    // Protects access from concurrent API requests.
    private readonly object _sync = new();

    // Signals the worker when packets become available.
    private readonly SemaphoreSlim _signal = new(0);

    private int _processing;
    private int _highWatermark;

    private long _accepted;
    private long _processed;
    private long _failed;
    private long _rejected;

    private DateTime? _lastProcessedUtc;

    public bool TryEnqueue(QueuedTelemetry packet)
    {
        return TryEnqueueBatch(new[] { packet });
    }

    public bool TryEnqueueBatch(
        IReadOnlyList<QueuedTelemetry> packets)
    {
        if (packets.Count == 0)
        {
            return true;
        }

        lock (_sync)
        {
            int remainingCapacity =
                MaxCapacity - _queue.Count;

            // Reject the entire batch if it cannot fit.
            if (packets.Count > remainingCapacity)
            {
                _rejected += packets.Count;
                return false;
            }

            foreach (var packet in packets)
            {
                _queue.Enqueue(packet);
            }

            _accepted += packets.Count;

            _highWatermark = Math.Max(
                _highWatermark,
                _queue.Count
            );

            // Wake the background worker.
            _signal.Release(packets.Count);

            return true;
        }
    }

    public async Task<QueuedTelemetry> DequeueAsync(
        CancellationToken cancellationToken)
    {
        // Wait without busy-looping.
        await _signal.WaitAsync(cancellationToken);

        lock (_sync)
        {
            var packet = _queue.Dequeue();

            _processing++;

            return packet;
        }
    }

    public void MarkProcessed()
    {
        lock (_sync)
        {
            _processing--;
            _processed++;

            _lastProcessedUtc = DateTime.UtcNow;
        }
    }

    public void MarkFailed()
    {
        lock (_sync)
        {
            _processing--;
            _failed++;
        }
    }

    public TelemetryQueueStatus GetStatus()
    {
        lock (_sync)
        {
            return new TelemetryQueueStatus
            {
                Capacity = MaxCapacity,

                Waiting = _queue.Count,

                Processing = _processing,

                HighWatermark = _highWatermark,

                Accepted = _accepted,

                Processed = _processed,

                Failed = _failed,

                Rejected = _rejected,

                LastProcessedUtc = _lastProcessedUtc
            };
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}
