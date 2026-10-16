using SmartX.DeviceSimulator.Models;

namespace SmartX.DeviceSimulator.Services;

public sealed class DeviceSimulationRunner
{
    private readonly SimulatedDevice _device;
    private readonly int _sensorId;
    private readonly SmartXApiClient _api;
    private readonly SimulationStatistics _statistics;
    private readonly SimulationSettings _settings;
    private readonly FaultInjectionEngine _faultEngine;

    private readonly TimeSpan _interval;
    private readonly int _maxReadings;

    public DeviceSimulationRunner(
        SimulatedDevice device,
        int sensorId,
        SmartXApiClient api,
        SimulationStatistics statistics,
        SimulationSettings settings)
    {
        _device = device;
        _sensorId = sensorId;
        _api = api;
        _statistics = statistics;
        _settings = settings;

        _faultEngine = new FaultInjectionEngine(settings);

        _interval = settings.GetInterval(
            device.Profile.Kind
        );

        _maxReadings =
            settings.MaxReadingsPerDevice;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        int readingsAttempted = 0;

        // An outage occurs once per simulation run.
        bool outageTriggered = false;

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

            bool shouldDisconnect =
                _settings.OutageEnabled &&
                !outageTriggered &&
                _device.Profile.Kind ==
                    _settings.OutageDevice &&
                readingsAttempted >=
                    _settings.OutageAfterReadings;

            if (shouldDisconnect)
            {
                outageTriggered = true;

                _statistics.RecordOutageStarted();

                Console.WriteLine();
                Console.WriteLine(
                    $"[DEVICE OFFLINE] {_device.Profile.Name}"
                );

                Console.WriteLine(
                    $"  Pausing HTTP transmission for " +
                    $"{_settings.OutageSeconds} seconds."
                );

                // No requests are sent by this device
                // during the outage. Other devices
                // continue independently.
                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            _settings.OutageSeconds
                        ),
                        cancellationToken
                    );
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine(
                        $"[OUTAGE INTERRUPTED] " +
                        $"{_device.Profile.Name}"
                    );

                    break;
                }

                _statistics.RecordOutageRecovered();

                Console.WriteLine(
                    $"[DEVICE RECONNECTED] " +
                    $"{_device.Profile.Name}"
                );

                Console.WriteLine(
                    "  Resuming HTTP transmission."
                );
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var normalReading = _device
                .GenerateReading() with
            {
                SensorId = _sensorId
            };

            readingsAttempted++;

            var injection = _faultEngine.Apply(
                _device.Profile.Kind,
                normalReading,
                readingsAttempted
            );

            if (injection.HasFault)
            {
                _statistics.RecordInjectedFault();

                Console.WriteLine();
                Console.WriteLine(
                    $"[FAULT INJECTED] " +
                    $"{_device.Profile.Name}"
                );

                Console.WriteLine(
                    $"  {injection.Description}"
                );

                Console.WriteLine(
                    $"  Reading number: {readingsAttempted}"
                );
            }

            _statistics.RecordAttempt();

            try
            {
                var sendResult = await _api.SendTelemetryAsync(
                    injection.Reading,
                    cancellationToken
                );

                _statistics.RecordResult(sendResult);

                if (sendResult.Accepted)
                {
                    Console.WriteLine(
                        $"[ACCEPTED] {_device.Profile.Name} | " +
                        $"Packet {sendResult.PacketId}"
                    );
                }
                else if (sendResult.QueueFull)
                {
                    Console.WriteLine(
                        $"[QUEUE FULL] {_device.Profile.Name} | " +
                        $"HTTP 429"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"[REJECTED] {_device.Profile.Name} | " +
                        sendResult.Message
                    );
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
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
                if (!await timer.WaitForNextTickAsync(
                    cancellationToken))
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
