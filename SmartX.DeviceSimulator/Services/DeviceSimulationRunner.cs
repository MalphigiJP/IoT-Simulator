
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

    private bool? _lastReportedControlState;

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

        _maxReadings = settings.MaxReadingsPerDevice;
    }

    private async Task ProcessPendingCommandAsync(
        CancellationToken cancellationToken)
    {
        string? target = _device.GetControlTarget();

        if (target is null)
        {
            return;
        }

        var command = await _api.PollCommandAsync(
            _sensorId,
            cancellationToken
        );

        if (command is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"[COMMAND RECEIVED] {_device.Profile.Name}"
        );

        Console.WriteLine(
            $"  Command: {command.CommandId}"
        );

        Console.WriteLine(
            $"  Target: {command.Target}"
        );

        Console.WriteLine(
            $"  Requested state: {command.RequestedState}"
        );

        bool applied = _device.TryApplyControl(
            command.Target,
            command.RequestedState,
            out string? error
        );

        bool? currentState =
            _device.GetCurrentControlState();

        await _api.AcknowledgeCommandAsync(
            _sensorId,
            command.CommandId,
            applied,
            currentState,
            error,
            cancellationToken
        );

        if (currentState.HasValue)
        {
            _lastReportedControlState =
                currentState.Value;
        }

        if (applied)
        {
            Console.ForegroundColor =
                ConsoleColor.Green;

            Console.WriteLine(
                $"[COMMAND APPLIED] " +
                $"{_device.Profile.Name} | " +
                $"{command.Target} = " +
                $"{currentState}"
            );

            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;

            Console.WriteLine(
                $"[COMMAND FAILED] " +
                $"{_device.Profile.Name} | " +
                error
            );

            Console.ResetColor();
        }
    }

    private async Task ReportStateIfChangedAsync(
        CancellationToken cancellationToken)
    {
        string? target = _device.GetControlTarget();

        bool? currentState =
            _device.GetCurrentControlState();

        if (target is null ||
            currentState is null)
        {
            return;
        }

        // Avoid sending duplicate state reports.
        if (_lastReportedControlState ==
            currentState.Value)
        {
            return;
        }

        await _api.ReportControlStateAsync(
            _sensorId,
            target,
            currentState.Value,
            cancellationToken
        );

        _lastReportedControlState =
            currentState.Value;

        Console.WriteLine(
            $"[STATE REPORTED] " +
            $"{_device.Profile.Name} | " +
            $"{target} = {currentState.Value}"
        );
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        int readingsAttempted = 0;

        bool outageTriggered = false;

        using var timer = new PeriodicTimer(
            _interval
        );

        Console.WriteLine(
            $"[STARTED] {_device.Profile.Name} | " +
            $"Sensor {_sensorId} | " +
            $"Interval {_interval.TotalMilliseconds:0} ms"
        );

        try
        {
            await ReportStateIfChangedAsync(
                cancellationToken
            );
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _statistics.RecordError();

            Console.WriteLine(
                $"[INITIAL STATE ERROR] " +
                $"{_device.Profile.Name} | {ex.Message}"
            );
        }

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

                Console.WriteLine(
                    $"[DEVICE OFFLINE] {_device.Profile.Name}"
                );

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
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await ProcessPendingCommandAsync(
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _statistics.RecordError();

                Console.WriteLine(
                    $"[COMMAND ERROR] " +
                    $"{_device.Profile.Name} | " +
                    ex.Message
                );
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            var normalReading = _device
                .GenerateReading() with
            {
                SensorId = _sensorId
            };

            readingsAttempted++;

            // Retain existing Phase 3.4 faults.
            var injection = _faultEngine.Apply(
                _device.Profile.Kind,
                normalReading,
                readingsAttempted
            );

            if (injection.HasFault)
            {
                _statistics.RecordInjectedFault();

                Console.WriteLine(
                    $"[FAULT INJECTED] " +
                    $"{_device.Profile.Name} | " +
                    injection.Description
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
                        $"[QUEUE FULL] {_device.Profile.Name}"
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
            catch (Exception ex)
            {
                _statistics.RecordError();

                Console.WriteLine(
                    $"[TELEMETRY ERROR] " +
                    $"{_device.Profile.Name} | " +
                    ex.Message
                );
            }

            try
            {
                await ReportStateIfChangedAsync(
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _statistics.RecordError();

                Console.WriteLine(
                    $"[STATE REPORT ERROR] " +
                    $"{_device.Profile.Name} | " +
                    ex.Message
                );
            }

            if (_maxReadings > 0 &&
                readingsAttempted >= _maxReadings)
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
