namespace SmartX.DeviceSimulator.Services;

public sealed record SimulationStatisticsSnapshot
{
    public long Attempted { get; init; }

    public long Accepted { get; init; }

    public long QueueFull { get; init; }

    public long Rejected { get; init; }

    public long Errors { get; init; }
}

public sealed class SimulationStatistics
{
    private long _attempted;
    private long _accepted;
    private long _queueFull;
    private long _rejected;
    private long _errors;

    public void RecordAttempt()
    {
        Interlocked.Increment(ref _attempted);
    }

    public void RecordResult(
        TelemetrySendResult result)
    {
        if (result.Accepted)
        {
            Interlocked.Increment(ref _accepted);
        }
        else if (result.QueueFull)
        {
            Interlocked.Increment(ref _queueFull);
        }
        else
        {
            Interlocked.Increment(ref _rejected);
        }
    }

    public void RecordError()
    {
        Interlocked.Increment(ref _errors);
    }

    public SimulationStatisticsSnapshot GetSnapshot()
    {
        return new SimulationStatisticsSnapshot
        {
            Attempted = Interlocked.Read(
                ref _attempted
            ),

            Accepted = Interlocked.Read(
                ref _accepted
            ),

            QueueFull = Interlocked.Read(
                ref _queueFull
            ),

            Rejected = Interlocked.Read(
                ref _rejected
            ),

            Errors = Interlocked.Read(
                ref _errors
            )
        };
    }
}
