
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


public sealed class DeviceCommandService
{
    private readonly object _sync = new();

    // Latest desired and reported state for each device.
    private readonly Dictionary<int, DeviceControlState>
        _states = new();

    // Command lookup by command ID.
    private readonly Dictionary<Guid, DeviceCommand>
        _commands = new();

    // Command IDs in creation order for each sensor.
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

            // Read-only and unknown device types
            // cannot receive manual commands.
            if (state.SupportedTarget is null)
            {
                return new CommandCreationResult(
                    CommandCreateOutcome.ReadOnlyDevice,
                    null,
                    state
                );
            }

            // A smart plug cannot execute a pump
            // command, for example.
            if (state.SupportedTarget != target)
            {
                return new CommandCreationResult(
                    CommandCreateOutcome.TargetMismatch,
                    null,
                    state
                );
            }

            // Only one outstanding command
            // per device is permitted.
            if (HasOutstandingCommandLocked(sensor.Id))
            {
                return new CommandCreationResult(
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
                    out var commandIds))
            {
                commandIds = new List<Guid>();

                _commandsBySensor.Add(
                    sensor.Id,
                    commandIds
                );
            }

            commandIds.Add(command.Id);

            // Update DESIRED state only.
            //
            // The device has not confirmed that
            // it executed the command.
            var updatedState = state with
            {
                DesiredState = requestedState,

                LastCommandId = command.Id,

                LastDesiredChangeAtUtc = now,

                Revision = state.Revision + 1
            };

            _states[sensor.Id] = updatedState;

            return new CommandCreationResult(
                CommandCreateOutcome.Created,
                command,
                updatedState
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
                    out var commandIds))
            {
                return Array.Empty<DeviceCommand>();
            }

            // Newest command first.
            return commandIds
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
                return new CommandCancellationResult(
                    CommandCancelOutcome.NotFound,
                    null
                );
            }

            // Only commands that have not reached
            // the device can be cancelled safely.
            if (command.Status !=
                DeviceCommandStatus.Pending)
            {
                return new CommandCancellationResult(
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

            // Cancellation restores the previous
            // desired state, not the reported state.
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

            return new CommandCancellationResult(
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
                    out var commandIds))
            {
                foreach (Guid id in commandIds)
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
                out var commandIds))
        {
            return false;
        }

        return commandIds.Any(id =>
        {
            var status = _commands[id].Status;

            return status is
                DeviceCommandStatus.Pending or
                DeviceCommandStatus.Delivered;
        });
    }

    private DeviceControlState GetOrInitializeLocked(
        Sensor sensor)
    {
        if (sensor.Id <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sensor),
                "The sensor must be registered."
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

        var currentTarget =
            DeviceControlCatalog.GetSupportedTarget(
                sensor.Category
            );

        bool macChanged = !string.Equals(
            existing.MacAddress,
            sensor.MacAddress,
            StringComparison.OrdinalIgnoreCase
        );

        bool capabilityChanged =
            existing.SupportedTarget != currentTarget;

        // A different MAC or device capability
        // represents a new control identity.
        if (macChanged || capabilityChanged)
        {
            DateTime now = DateTime.UtcNow;

            // Cancel undelivered commands referencing
            // the old device identity.
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
                                "Device identity or capability changed."
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

        // Metadata may be updated without
        // losing the command state.
        var refreshed = existing with
        {
            MacAddress = sensor.MacAddress,
            Category = sensor.Category
        };

        _states[sensor.Id] = refreshed;

        return refreshed;
    }
}
