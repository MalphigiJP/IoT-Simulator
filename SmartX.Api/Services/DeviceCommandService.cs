
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

public enum UndoAvailabilityReason
{
    Available,
    ReadOnlyDevice,
    NoHistory,
    DeviceBusy,
    UnknownState,
    StateChanged
}

public sealed record UndoAvailability(
    bool CanUndo,
    int StackDepth,
    Guid? SourceCommandId,
    DeviceControlTarget? Target,
    bool? RestoreState,
    UndoAvailabilityReason Reason
);

public enum UndoCreateOutcome
{
    Created,
    ReadOnlyDevice,
    NoHistory,
    DeviceBusy,
    UnknownState,
    StateChanged
}

public sealed record UndoCreationResult(
    UndoCreateOutcome Outcome,
    DeviceCommand? Command,
    DeviceControlState State
);

public sealed partial class DeviceCommandService
{
    private readonly object _sync = new();

    public static readonly TimeSpan DeliveryLease =
        TimeSpan.FromSeconds(15);

    private readonly Dictionary<int, DeviceControlState>
        _states = new();

    private readonly Dictionary<Guid, DeviceCommand>
        _commands = new();

    private readonly Dictionary<int, List<Guid>>
        _commandsBySensor = new();

    private sealed record UndoEntry(
        Guid SourceCommandId,
        DeviceControlTarget Target,
        bool PreviousState,
        bool AppliedState
    );

    private readonly Dictionary<int, Stack<UndoEntry>>
        _undoStacks = new();

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

            AddCommandLocked(command);

            var updated = state with
            {
                DesiredState = requestedState,
                LastCommandId = command.Id,
                LastDesiredChangeAtUtc = now,
                Revision = state.Revision + 1
            };

            _states[sensor.Id] = updated;

            PersistLocked(
                sensor.Id,
                "CommandSubmitted",
                command.Id
            );

            return new(
                CommandCreateOutcome.Created,
                command,
                updated
            );
        }
    }

    public UndoAvailability GetUndoAvailability(
        Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_sync)
        {
            var state = GetOrInitializeLocked(sensor);

            return GetUndoAvailabilityLocked(state);
        }
    }

    public UndoCreationResult CreateUndo(
        Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        lock (_sync)
        {
            var state = GetOrInitializeLocked(sensor);

            var availability =
                GetUndoAvailabilityLocked(state);

            if (!availability.CanUndo)
            {
                var outcome = availability.Reason switch
                {
                    UndoAvailabilityReason.ReadOnlyDevice =>
                        UndoCreateOutcome.ReadOnlyDevice,

                    UndoAvailabilityReason.NoHistory =>
                        UndoCreateOutcome.NoHistory,

                    UndoAvailabilityReason.DeviceBusy =>
                        UndoCreateOutcome.DeviceBusy,

                    UndoAvailabilityReason.UnknownState =>
                        UndoCreateOutcome.UnknownState,

                    _ => UndoCreateOutcome.StateChanged
                };

                return new(outcome, null, state);
            }

            var entry = _undoStacks[sensor.Id].Peek();

            DateTime now = DateTime.UtcNow;

            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                SensorId = sensor.Id,
                Target = entry.Target,

                // Restore the state that existed
                // before the original manual command.
                RequestedState = entry.PreviousState,

                // Current desired state, before undo.
                PreviousDesiredState = state.DesiredState,

                Origin = DeviceCommandOrigin.Undo,

                UndoOfCommandId = entry.SourceCommandId,

                Status = DeviceCommandStatus.Pending,
                CreatedAtUtc = now
            };

            AddCommandLocked(command);

            var updated = state with
            {
                DesiredState = entry.PreviousState,
                LastCommandId = command.Id,
                LastDesiredChangeAtUtc = now,
                Revision = state.Revision + 1
            };

            _states[sensor.Id] = updated;

            PersistLocked(
                sensor.Id,
                "UndoRequested",
                command.Id
            );

            return new(
                UndoCreateOutcome.Created,
                command,
                updated
            );
        }
    }

    private UndoAvailability GetUndoAvailabilityLocked(
        DeviceControlState state)
    {
        _undoStacks.TryGetValue(
            state.SensorId,
            out var stack
        );

        int depth = stack?.Count ?? 0;

        if (state.SupportedTarget is null)
        {
            return new(
                false, depth, null, null, null,
                UndoAvailabilityReason.ReadOnlyDevice
            );
        }

        if (stack is null || stack.Count == 0)
        {
            return new(
                false, 0, null, null, null,
                UndoAvailabilityReason.NoHistory
            );
        }

        var entry = stack.Peek();

        if (HasOutstandingCommandLocked(state.SensorId))
        {
            return new(
                false,
                depth,
                entry.SourceCommandId,
                entry.Target,
                entry.PreviousState,
                UndoAvailabilityReason.DeviceBusy
            );
        }

        // We need both a desired and confirmed state
        // before automatically reversing a command.
        if (!state.DesiredState.HasValue ||
            !state.ReportedState.HasValue)
        {
            return new(
                false,
                depth,
                entry.SourceCommandId,
                entry.Target,
                entry.PreviousState,
                UndoAvailabilityReason.UnknownState
            );
        }

        bool matches =
            state.SupportedTarget == entry.Target &&
            state.DesiredState.Value == entry.AppliedState &&
            state.ReportedState.Value == entry.AppliedState;

        if (!matches)
        {
            return new(
                false,
                depth,
                entry.SourceCommandId,
                entry.Target,
                entry.PreviousState,
                UndoAvailabilityReason.StateChanged
            );
        }

        return new(
            true,
            depth,
            entry.SourceCommandId,
            entry.Target,
            entry.PreviousState,
            UndoAvailabilityReason.Available
        );
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

                PersistLocked(
                    sensorId,
                    "CommandDelivered",
                    id
                );

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

            if (command.Status !=
                DeviceCommandStatus.Delivered ||
                state is null)
            {
                return new(
                    CommandAckOutcome.InvalidStatus,
                    command,
                    state
                );
            }

            if (applied &&
                reportedState != command.RequestedState)
            {
                return new(
                    CommandAckOutcome.InvalidReportedState,
                    command,
                    state
                );
            }

            if (applied &&
                command.Origin == DeviceCommandOrigin.Undo)
            {
                bool matchesTop =
                    _undoStacks.TryGetValue(
                        sensorId,
                        out var undoStack
                    ) &&
                    undoStack.TryPeek(out var top) &&
                    command.UndoOfCommandId ==
                        top.SourceCommandId;

                if (!matchesTop)
                {
                    return new(
                        CommandAckOutcome.InvalidStatus,
                        command,
                        state
                    );
                }
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

            if (applied)
            {
                if (command.Origin ==
                    DeviceCommandOrigin.Manual)
                {

                    if (command.PreviousDesiredState.HasValue &&
                        command.PreviousDesiredState.Value !=
                            command.RequestedState)
                    {
                        if (!_undoStacks.TryGetValue(
                                sensorId,
                                out var stack))
                        {
                            stack = new Stack<UndoEntry>();

                            _undoStacks.Add(
                                sensorId,
                                stack
                            );
                        }

                        stack.Push(new UndoEntry(
                            command.Id,
                            command.Target,
                            command.PreviousDesiredState.Value,
                            command.RequestedState
                        ));
                    }
                }
                else if (command.Origin ==
                         DeviceCommandOrigin.Undo)
                {
                    // The reversal really happened.
                    // Only NOW may the stack shrink.
                    _undoStacks[sensorId].Pop();
                }
            }


            string auditAction = command.Origin switch
            {
                DeviceCommandOrigin.Undo when applied =>
                    "UndoApplied",

                DeviceCommandOrigin.Undo =>
                    "UndoFailed",

                _ when applied =>
                    "CommandApplied",

                _ =>
                    "CommandFailed"
            };

            PersistLocked(
                sensorId,
                auditAction,
                commandId
            );


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

            PersistLocked(
                sensor.Id,
                "StateReported",
                null
            );

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

            PersistLocked(
                command.SensorId,
                "CommandCancelled",
                commandId
            );

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

            _undoStacks.Remove(sensorId);

            PersistLocked(
                sensorId,
                "SensorRemoved",
                null
            );

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

    private void AddCommandLocked(
        DeviceCommand command)
    {
        _commands.Add(command.Id, command);

        if (!_commandsBySensor.TryGetValue(
                command.SensorId,
                out var ids))
        {
            ids = new List<Guid>();

            _commandsBySensor.Add(
                command.SensorId,
                ids
            );
        }

        ids.Add(command.Id);
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

            PersistLocked(
                sensor.Id,
                "StateInitialized",
                null
            );

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

            _undoStacks.Remove(sensor.Id);

            var reset =
                DeviceControlCatalog.CreateInitialState(
                    sensor
                ) with
                {
                    Revision = existing.Revision + 1
                };

            _states[sensor.Id] = reset;

            PersistLocked(
                sensor.Id,
                "IdentityChanged",
                null
            );

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
