using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class DeviceSimulationRunner
{
    private readonly SimulatedDevice _device;

    private readonly int _sensorId;

    private readonly SmartXApiClient _api;

    private readonly SimulationStatistics _statistics;

    private readonly TimeSpan _interval;

    private readonly int _maxReadings;

    public DeviceSimulationRunner(
        SimulatedDevice device,
        int sensorId,
        SmartXApiClient api,
        SimulationStatistics statistics,
        TimeSpan interval,
        int maxReadings)
    {
        _device = device;
        _sensorId = sensorId;
        _api = api;
        _statistics = statistics;
        _interval = interval;
        _maxReadings = maxReadings;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        int readingsAttempted = 0;

        using var timer = new PeriodicTimer(
            _interval
        );

        Console.WriteLine(
            $"[STARTED] {_device.Profile.Name} | " +
            $"Sensor {_sensorId} | " +
            $"Interval {_interval.TotalMilliseconds:0} ms"
        );

        while (
            !cancellationToken.IsCancellationRequested &&
            (
                _maxReadings == 0 ||
                readingsAttempted < _maxReadings
            )
        )
        {
            // Generate a new reading using
            // the device's existing profile.
            SimulatedTelemetry reading =
                _device.GenerateReading() with
                {
                    SensorId = _sensorId
                };

            readingsAttempted++;

            _statistics.RecordAttempt();

            try
            {
                var result = await _api.SendTelemetryAsync(
                    reading,
                    cancellationToken
                );

                _statistics.RecordResult(result);

                if (result.Accepted)
                {
                    Console.WriteLine(
                        $"[ACCEPTED] {_device.Profile.Name} | " +
                        $"Packet {result.PacketId} | " +
                        $"HTTP attempts: {result.Attempts}"
                    );
                }
                else if (result.QueueFull)
                {
                    Console.WriteLine(
                        $"[QUEUE FULL] {_device.Profile.Name} | " +
                        $"HTTP 429 after {result.Attempts} attempts"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"[REJECTED] {_device.Profile.Name} | " +
                        result.Message
                    );
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // User requested shutdown or
                // the configured duration expired.
                break;
            }
            catch (HttpRequestException ex)
            {
                _statistics.RecordError();

                Console.WriteLine(
                    $"[HTTP ERROR] {_device.Profile.Name} | " +
                    ex.Message
                );
            }
            catch (TaskCanceledException)
            {
                // Usually indicates an HTTP timeout
                // rather than a requested shutdown.
                _statistics.RecordError();

                Console.WriteLine(
                    $"[TIMEOUT] {_device.Profile.Name}"
                );
            }
            catch (Exception ex)
            {
                _statistics.RecordError();

                Console.WriteLine(
                    $"[DEVICE ERROR] {_device.Profile.Name} | " +
                    ex.Message
                );
            }

            if (
                _maxReadings > 0 &&
                readingsAttempted >= _maxReadings
            )
            {
                break;
            }

            try
            {
                bool nextTick =
                    await timer.WaitForNextTickAsync(
                        cancellationToken
                    );

                if (!nextTick)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        Console.WriteLine(
            $"[STOPPED] {_device.Profile.Name} | " +
            $"Readings attempted: {readingsAttempted}"
        );
    }
}
