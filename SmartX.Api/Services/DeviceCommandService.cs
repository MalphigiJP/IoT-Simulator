
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public enum CommandCreateOutcome
{
    Created,
    ReadOnlyDevice,
    TargetMismatch,
    DeviceBusy
}

public sealed record CommandCreationResult(
    CommandCreateOutcome Outcome,
    DeviceCommand? Command,
    DeviceControlState State
);

public enum CommandCancelOutcome
{
    Cancelled,
    NotFound,
    NotPending
}

public sealed record CommandCancellationResult(
    CommandCancelOutcome Outcome,
    DeviceCommand? Command
);

public enum CommandAckOutcome
{
    Applied,
    Failed,
    AlreadyAcknowledged,
    NotFound,
    InvalidStatus,
    InvalidReportedState
}

public sealed record CommandAckResult(
    CommandAckOutcome Outcome,
    DeviceCommand? Command,
    DeviceControlState? State
);

public enum ControlReportOutcome
{
    Updated,
    ReadOnlyDevice,
    TargetMismatch
}

public sealed record ControlReportResult(
    ControlReportOutcome Outcome,
    DeviceControlState State
);

public sealed class DeviceCommandService
{
    private readonly object _sync = new();

    // Unacknowledged deliveries can be reclaimed
    // if the original response was lost.
    public static readonly TimeSpan DeliveryLease =
        TimeSpan.FromSeconds(15);

    private readonly Dictionary<int, DeviceControlState>
        _states = new();

    private readonly Dictionary<Guid, DeviceCommand>
        _commands = new();

    private readonly Dictionary<int, List<Guid>>
        _commandsBySensor = new();

    public DeviceControlState GetOrInitialize(
        Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_sync)
        {
            return GetOrInitializeLocked(sensor);
        }
    }

    public CommandCreationResult Submit(
        Sensor sensor,
        DeviceControlTarget target,
        bool requestedState)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_sync)
        {
            var state = GetOrInitializeLocked(sensor);

            if (state.SupportedTarget is null)
            {
                return new(
                    CommandCreateOutcome.ReadOnlyDevice,
                    null,
                    state
                );
            }

            if (state.SupportedTarget != target)
            {
                return new(
                    CommandCreateOutcome.TargetMismatch,
                    null,
                    state
                );
            }

            if (HasOutstandingCommandLocked(sensor.Id))
            {
                return new(
                    CommandCreateOutcome.DeviceBusy,
                    null,
                    state
                );
            }

            DateTime now = DateTime.UtcNow;

            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                SensorId = sensor.Id,
                Target = target,
                RequestedState = requestedState,
                PreviousDesiredState = state.DesiredState,
                Origin = DeviceCommandOrigin.Manual,
                Status = DeviceCommandStatus.Pending,
                CreatedAtUtc = now
            };

            _commands.Add(command.Id, command);

            if (!_commandsBySensor.TryGetValue(
                    sensor.Id,
                    out var ids))
            {
                ids = new List<Guid>();
                _commandsBySensor.Add(sensor.Id, ids);
            }

            ids.Add(command.Id);

            var updated = state with
            {
                DesiredState = requestedState,
                LastCommandId = command.Id,
                LastDesiredChangeAtUtc = now,
                Revision = state.Revision + 1
            };

            _states[sensor.Id] = updated;

            return new(
                CommandCreateOutcome.Created,
                command,
                updated
            );
        }
    }

    public DeviceCommand? ClaimNextForDevice(
        int sensorId,
        DateTime nowUtc)
    {
        lock (_sync)
        {
            if (!_commandsBySensor.TryGetValue(
                    sensorId,
                    out var ids))
            {
                return null;
            }

            foreach (Guid id in ids)
            {
                var command = _commands[id];

                bool pending =
                    command.Status ==
                    DeviceCommandStatus.Pending;

                bool leaseExpired =
                    command.Status ==
                    DeviceCommandStatus.Delivered &&
                    (
                        command.DeliveredAtUtc is null ||
                        nowUtc - command.DeliveredAtUtc.Value
                            >= DeliveryLease
                    );

                if (!pending && !leaseExpired)
                {
                    continue;
                }

                var delivered = command with
                {
                    Status = DeviceCommandStatus.Delivered,
                    DeliveredAtUtc = nowUtc
                };

                _commands[id] = delivered;

                return delivered;
            }

            return null;
        }
    }

    public CommandAckResult Acknowledge(
        int sensorId,
        Guid commandId,
        bool applied,
        bool? reportedState,
        string? error)
    {
        lock (_sync)
        {
            if (!_commands.TryGetValue(
                    commandId,
                    out var command) ||
                command.SensorId != sensorId)
            {
                return new(
                    CommandAckOutcome.NotFound,
                    null,
                    null
                );
            }

            _states.TryGetValue(
                sensorId,
                out var state
            );

            // A previously successful acknowledgement
            // may be repeated after a lost HTTP reply.
            if (command.Status ==
                    DeviceCommandStatus.Applied &&
                applied &&
                reportedState == command.RequestedState)
            {
                return new(
                    CommandAckOutcome.AlreadyAcknowledged,
                    command,
                    state
                );
            }

            if (command.Status ==
                    DeviceCommandStatus.Failed &&
                !applied)
            {
                return new(
                    CommandAckOutcome.AlreadyAcknowledged,
                    command,
                    state
                );
            }

            // A device may only acknowledge a command
            // that the server actually delivered.
            if (command.Status !=
                DeviceCommandStatus.Delivered)
            {
                return new(
                    CommandAckOutcome.InvalidStatus,
                    command,
                    state
                );
            }

            if (state is null)
            {
                return new(
                    CommandAckOutcome.InvalidStatus,
                    command,
                    null
                );
            }

            // A successful execution must confirm
            // the exact state the operator requested.
            if (applied &&
                reportedState != command.RequestedState)
            {
                return new(
                    CommandAckOutcome.InvalidReportedState,
                    command,
                    state
                );
            }

            DateTime now = DateTime.UtcNow;

            var completed = command with
            {
                Status = applied
                    ? DeviceCommandStatus.Applied
                    : DeviceCommandStatus.Failed,

                CompletedAtUtc = now,

                FailureReason = applied
                    ? null
                    : string.IsNullOrWhiteSpace(error)
                        ? "Device could not apply command."
                        : error.Trim()
            };

            _commands[commandId] = completed;

            var updated = state with
            {

                DesiredState = applied
                    ? command.RequestedState
                    : command.PreviousDesiredState
                        ?? reportedState
                        ?? state.ReportedState,

                ReportedState =
                    reportedState ?? state.ReportedState,

                LastCommandId = commandId,

                LastReportedAtUtc =
                    reportedState.HasValue
                        ? now
                        : state.LastReportedAtUtc,

                LastDesiredChangeAtUtc =
                    applied
                        ? state.LastDesiredChangeAtUtc
                        : now,

                Revision = state.Revision + 1
            };

            _states[sensorId] = updated;

            return new(
                applied
                    ? CommandAckOutcome.Applied
                    : CommandAckOutcome.Failed,
                completed,
                updated
            );
        }
    }

    public ControlReportResult ReportState(
        Sensor sensor,
        DeviceControlTarget target,
        bool reportedState)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_sync)
        {
            var state = GetOrInitializeLocked(sensor);

            if (state.SupportedTarget is null)
            {
                return new(
                    ControlReportOutcome.ReadOnlyDevice,
                    state
                );
            }

            if (state.SupportedTarget != target)
            {
                return new(
                    ControlReportOutcome.TargetMismatch,
                    state
                );
            }

            bool outstanding =
                HasOutstandingCommandLocked(sensor.Id);

            bool? desired = outstanding
                ? state.DesiredState
                : reportedState;

            bool changed =
                state.ReportedState != reportedState ||
                state.DesiredState != desired;

            DateTime now = DateTime.UtcNow;

            var updated = state with
            {
                ReportedState = reportedState,
                DesiredState = desired,

                LastReportedAtUtc = now,

                LastDesiredChangeAtUtc =
                    state.DesiredState != desired
                        ? now
                        : state.LastDesiredChangeAtUtc,

                Revision = changed
                    ? state.Revision + 1
                    : state.Revision
            };

            _states[sensor.Id] = updated;

            return new(
                ControlReportOutcome.Updated,
                updated
            );
        }
    }

    public bool TryGetCommand(
        Guid commandId,
        out DeviceCommand? command)
    {
        lock (_sync)
        {
            return _commands.TryGetValue(
                commandId,
                out command
            );
        }
    }

    public IReadOnlyList<DeviceCommand> GetCommands(
        int sensorId)
    {
        lock (_sync)
        {
            if (!_commandsBySensor.TryGetValue(
                    sensorId,
                    out var ids))
            {
                return Array.Empty<DeviceCommand>();
            }

            return ids
                .AsEnumerable()
                .Reverse()
                .Select(id => _commands[id])
                .ToArray();
        }
    }

    public CommandCancellationResult Cancel(
        Guid commandId)
    {
        lock (_sync)
        {
            if (!_commands.TryGetValue(
                    commandId,
                    out var command))
            {
                return new(
                    CommandCancelOutcome.NotFound,
                    null
                );
            }

            if (command.Status !=
                DeviceCommandStatus.Pending)
            {
                return new(
                    CommandCancelOutcome.NotPending,
                    command
                );
            }

            DateTime now = DateTime.UtcNow;

            var cancelled = command with
            {
                Status = DeviceCommandStatus.Cancelled,
                CompletedAtUtc = now
            };

            _commands[commandId] = cancelled;

            if (_states.TryGetValue(
                    command.SensorId,
                    out var state))
            {
                _states[command.SensorId] = state with
                {
                    DesiredState =
                        command.PreviousDesiredState,

                    LastCommandId = commandId,
                    LastDesiredChangeAtUtc = now,
                    Revision = state.Revision + 1
                };
            }

            return new(
                CommandCancelOutcome.Cancelled,
                cancelled
            );
        }
    }

    public void RemoveSensor(int sensorId)
    {
        lock (_sync)
        {
            _states.Remove(sensorId);

            if (_commandsBySensor.Remove(
                    sensorId,
                    out var ids))
            {
                foreach (Guid id in ids)
                {
                    _commands.Remove(id);
                }
            }
        }
    }

    private bool HasOutstandingCommandLocked(
        int sensorId)
    {
        if (!_commandsBySensor.TryGetValue(
                sensorId,
                out var ids))
        {
            return false;
        }

        return ids.Any(id =>
            _commands[id].Status is
                DeviceCommandStatus.Pending or
                DeviceCommandStatus.Delivered
        );
    }

    private DeviceControlState GetOrInitializeLocked(
        Sensor sensor)
    {
        if (sensor.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sensor),
                "Sensor must be registered."
            );
        }

        if (!_states.TryGetValue(
                sensor.Id,
                out var existing))
        {
            var initial =
                DeviceControlCatalog.CreateInitialState(
                    sensor
                );

            _states.Add(sensor.Id, initial);

            return initial;
        }

        var target =
            DeviceControlCatalog.GetSupportedTarget(
                sensor.Category
            );

        bool macChanged = !string.Equals(
            existing.MacAddress,
            sensor.MacAddress,
            StringComparison.OrdinalIgnoreCase
        );

        bool capabilityChanged =
            existing.SupportedTarget != target;

        if (macChanged || capabilityChanged)
        {
            DateTime now = DateTime.UtcNow;

            if (_commandsBySensor.TryGetValue(
                    sensor.Id,
                    out var ids))
            {
                foreach (Guid id in ids)
                {
                    var command = _commands[id];

                    if (command.Status ==
                        DeviceCommandStatus.Pending)
                    {
                        _commands[id] = command with
                        {
                            Status =
                                DeviceCommandStatus.Cancelled,

                            CompletedAtUtc = now,

                            FailureReason =
                                "Device identity changed."
                        };
                    }
                    else if (command.Status ==
                        DeviceCommandStatus.Delivered)
                    {
                        // Already delivered: execution
                        // cannot be ruled out.
                        _commands[id] = command with
                        {
                            Status =
                                DeviceCommandStatus.Failed,

                            CompletedAtUtc = now,

                            FailureReason =
                                "Device identity changed while " +
                                "execution was unconfirmed."
                        };
                    }
                }
            }

            var reset =
                DeviceControlCatalog.CreateInitialState(
                    sensor
                ) with
                {
                    Revision = existing.Revision + 1
                };

            _states[sensor.Id] = reset;

            return reset;
        }

        var refreshed = existing with
        {
            MacAddress = sensor.MacAddress,
            Category = sensor.Category
        };

        _states[sensor.Id] = refreshed;

        return refreshed;
    }
}
