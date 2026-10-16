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

    // Priority-specific statistics
    public int NormalWaiting { get; init; }

    public int CriticalWaiting { get; init; }

    public int EmergencyWaiting { get; init; }

    public int ReservedPrioritySlots { get; init; }

    public long NormalAccepted { get; init; }

    public long CriticalAccepted { get; init; }

    public long EmergencyAccepted { get; init; }

    public long NormalProcessed { get; init; }

    public long CriticalProcessed { get; init; }

    public long EmergencyProcessed { get; init; }
}


public sealed class TelemetryQueue : IDisposable
{
    public const int MaxCapacity = 5000;

    public const int PriorityReserve = 500;

    public const int MaxConsecutivePriority = 10;

    // Standard FIFO telemetry.
    private readonly Queue<QueuedTelemetry>
        _normalQueue = new();

    // Critical and emergency telemetry.
    private readonly PriorityQueue<
        QueuedTelemetry,
        (int Level, long Sequence)
    > _priorityQueue = new();

    // Synchronisation for concurrent requests.
    private readonly object _sync = new();

    // Signals that a packet is available.
    private readonly SemaphoreSlim _signal = new(0);

    private long _sequence;

    private int _consecutivePriority;

    private int _processing;

    private int _highWatermark;

    private int _criticalWaiting;

    private int _emergencyWaiting;

    private long _accepted;

    private long _processed;

    private long _failed;

    private long _rejected;

    private long _normalAccepted;

    private long _criticalAccepted;

    private long _emergencyAccepted;

    private long _normalProcessed;

    private long _criticalProcessed;

    private long _emergencyProcessed;

    private DateTime? _lastProcessedUtc;

    public bool TryEnqueue(QueuedTelemetry packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        return TryEnqueueBatch(new[] { packet });
    }

    public bool TryEnqueueBatch(
        IReadOnlyList<QueuedTelemetry> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);

        if (packets.Count == 0)
        {
            return true;
        }

        // Classify packets before acquiring lock.
        var classified = packets
            .Select(packet => (
                Packet: packet,
                Priority:
                    TelemetryPriorityClassifier.Classify(packet)
            ))
            .ToArray();

        int normalIncoming = classified.Count(
            item =>
                item.Priority == TelemetryPriority.Normal
        );

        lock (_sync)
        {
            int totalWaiting =
                _normalQueue.Count +
                _priorityQueue.Count;

            int projectedWaiting =
                totalWaiting + classified.Length;

            // Never exceed total capacity.
            if (projectedWaiting > MaxCapacity)
            {
                _rejected += classified.Length;
                return false;
            }

            // Reserve capacity for urgent telemetry.
            int projectedNormalWaiting =
                _normalQueue.Count + normalIncoming;

            if (
                projectedNormalWaiting >
                MaxCapacity - PriorityReserve
            )
            {
                _rejected += classified.Length;
                return false;
            }

            foreach (var item in classified)
            {
                var packet = item.Packet;

                switch (item.Priority)
                {
                    case TelemetryPriority.Emergency:

                        _priorityQueue.Enqueue(
                            packet,
                            (
                                (int)TelemetryPriority.Emergency,
                                ++_sequence
                            )
                        );

                        _emergencyWaiting++;
                        _emergencyAccepted++;

                        break;

                    case TelemetryPriority.Critical:

                        _priorityQueue.Enqueue(
                            packet,
                            (
                                (int)TelemetryPriority.Critical,
                                ++_sequence
                            )
                        );

                        _criticalWaiting++;
                        _criticalAccepted++;

                        break;

                    default:

                        _normalQueue.Enqueue(packet);

                        _normalAccepted++;

                        break;
                }
            }

            _accepted += classified.Length;

            _highWatermark = Math.Max(
                _highWatermark,
                projectedWaiting
            );

            // Wake the background worker.
            _signal.Release(classified.Length);

            return true;
        }
    }

    public async Task<QueuedTelemetry> DequeueAsync(
        CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);

        lock (_sync)
        {
            bool priorityAvailable =
                _priorityQueue.Count > 0;

            bool normalAvailable =
                _normalQueue.Count > 0;

            // Prioritise emergencies and critical
            // packets, while preventing starvation.
            bool selectPriority =
                priorityAvailable &&
                (
                    !normalAvailable ||
                    _consecutivePriority <
                        MaxConsecutivePriority
                );

            QueuedTelemetry packet;

            if (selectPriority)
            {
                packet = _priorityQueue.Dequeue();

                var priority =
                    TelemetryPriorityClassifier.Classify(
                        packet
                    );

                if (
                    priority ==
                    TelemetryPriority.Emergency
                )
                {
                    _emergencyWaiting--;
                }
                else
                {
                    _criticalWaiting--;
                }

                _consecutivePriority++;
            }
            else
            {
                packet = _normalQueue.Dequeue();

                _consecutivePriority = 0;
            }

            _processing++;

            return packet;
        }
    }

    public void MarkProcessed(
        QueuedTelemetry packet)
    {
        var priority =
            TelemetryPriorityClassifier.Classify(packet);

        lock (_sync)
        {
            _processing--;

            _processed++;

            _lastProcessedUtc = DateTime.UtcNow;

            switch (priority)
            {
                case TelemetryPriority.Emergency:

                    _emergencyProcessed++;
                    break;

                case TelemetryPriority.Critical:

                    _criticalProcessed++;
                    break;

                default:

                    _normalProcessed++;
                    break;
            }
        }
    }

    public void MarkFailed(
        QueuedTelemetry packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

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

                Waiting =
                    _normalQueue.Count +
                    _priorityQueue.Count,

                Processing = _processing,

                HighWatermark = _highWatermark,

                Accepted = _accepted,

                Processed = _processed,

                Failed = _failed,

                Rejected = _rejected,

                LastProcessedUtc =
                    _lastProcessedUtc,

                NormalWaiting =
                    _normalQueue.Count,

                CriticalWaiting =
                    _criticalWaiting,

                EmergencyWaiting =
                    _emergencyWaiting,

                ReservedPrioritySlots =
                    PriorityReserve,

                NormalAccepted =
                    _normalAccepted,

                CriticalAccepted =
                    _criticalAccepted,

                EmergencyAccepted =
                    _emergencyAccepted,

                NormalProcessed =
                    _normalProcessed,

                CriticalProcessed =
                    _criticalProcessed,

                EmergencyProcessed =
                    _emergencyProcessed
            };
        }
    }

    public void Dispose()
    {
        _signal.Dispose();
    }
}
