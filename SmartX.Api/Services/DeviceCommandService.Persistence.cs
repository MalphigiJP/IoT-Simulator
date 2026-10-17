
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed partial class DeviceCommandService
{
    private readonly DeviceCommandPersistence? _persistence;

    public DeviceCommandService(
        DeviceCommandPersistence? persistence = null)
    {
        _persistence = persistence;
    }

    public void RestoreFromDatabase()
    {
        if (_persistence is null)
        {
            return;
        }

        var snapshot = _persistence.Load();

        RestoreFromSnapshot(snapshot);
    }

    public void RestoreFromSnapshot(
        DeviceCommandSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_sync)
        {
            RestoreLocked(snapshot);
        }
    }

    private void RestoreLocked(
        DeviceCommandSnapshot snapshot)
    {
        _states.Clear();
        _commands.Clear();
        _commandsBySensor.Clear();
        _undoStacks.Clear();

        foreach (var state in snapshot.States)
        {
            _states[state.SensorId] = state;
        }

        var creationOrder = snapshot.Events
            .Where(e =>
                e.CommandId.HasValue &&
                (
                    e.Action == "CommandSubmitted" ||
                    e.Action == "UndoRequested"
                )
            )
            .GroupBy(e => e.CommandId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Min(e => e.Id)
            );

        var orderedCommands = snapshot.Commands
            .OrderBy(c =>
                creationOrder.TryGetValue(
                    c.Id,
                    out long order)
                    ? order
                    : long.MaxValue
            )
            .ThenBy(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id);

        foreach (var command in orderedCommands)
        {
            _commands[command.Id] = command;

            if (!_commandsBySensor.TryGetValue(
                    command.SensorId,
                    out var ids))
            {
                ids = new List<Guid>();
                _commandsBySensor[command.SensorId] = ids;
            }

            ids.Add(command.Id);
        }

        foreach (var auditEvent in snapshot.Events
            .OrderBy(e => e.Id))
        {
            if (
                auditEvent.Action == "IdentityChanged" ||
                auditEvent.Action == "SensorRemoved"
            )
            {
                _undoStacks.Remove(auditEvent.SensorId);
                continue;
            }

            if (!auditEvent.CommandId.HasValue)
            {
                continue;
            }

            if (!_commands.TryGetValue(
                    auditEvent.CommandId.Value,
                    out var command))
            {
                continue;
            }

            if (
                auditEvent.Action == "CommandApplied" &&
                command.Origin == DeviceCommandOrigin.Manual &&
                command.PreviousDesiredState.HasValue &&
                command.PreviousDesiredState.Value !=
                    command.RequestedState
            )
            {
                if (!_undoStacks.TryGetValue(
                        command.SensorId,
                        out var stack))
                {
                    stack = new Stack<UndoEntry>();

                    _undoStacks[command.SensorId] = stack;
                }

                stack.Push(new UndoEntry(
                    command.Id,
                    command.Target,
                    command.PreviousDesiredState.Value,
                    command.RequestedState
                ));
            }

            if (
                auditEvent.Action == "UndoApplied" &&
                command.Origin == DeviceCommandOrigin.Undo &&
                _undoStacks.TryGetValue(
                    command.SensorId,
                    out var undoStack) &&
                undoStack.TryPeek(out var top) &&
                top.SourceCommandId ==
                    command.UndoOfCommandId
            )
            {
                undoStack.Pop();
            }
        }
    }

    private void PersistLocked(
        int sensorId,
        string action,
        Guid? commandId)
    {
        if (_persistence is null)
        {
            return;
        }

        _states.TryGetValue(
            sensorId,
            out var state
        );

        IReadOnlyList<DeviceCommand> commands;

        if (_commandsBySensor.TryGetValue(
                sensorId,
                out var ids))
        {
            commands = ids
                .Select(id => _commands[id])
                .ToArray();
        }
        else
        {
            commands = Array.Empty<DeviceCommand>();
        }

        try
        {
            _persistence.SaveSensor(
                sensorId,
                state,
                commands,
                action,
                commandId
            );
        }
        catch
        {
            var snapshot = _persistence.Load();
            RestoreLocked(snapshot);
            throw;
        }
    }
}
