
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record DeviceCommandSnapshot(
    IReadOnlyList<DeviceCommand> Commands,
    IReadOnlyList<DeviceControlState> States,
    IReadOnlyList<DeviceCommandAuditEvent> Events
);

public sealed class DeviceCommandPersistence
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DeviceCommandPersistence(
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public DeviceCommandSnapshot Load()
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SmartXContext>();

        var commands = db.DeviceCommands
            .AsNoTracking()
            .ToArray();

        var states = db.DeviceControlStates
            .AsNoTracking()
            .ToArray();

        var events = db.DeviceCommandAuditEvents
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .ToArray();

        return new DeviceCommandSnapshot(
            commands,
            states,
            events
        );
    }

    public void SaveSensor(
        int sensorId,
        DeviceControlState? state,
        IReadOnlyList<DeviceCommand> commands,
        string action,
        Guid? commandId)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SmartXContext>();

        using var transaction =
            db.Database.BeginTransaction();

        var storedState = db.DeviceControlStates
            .SingleOrDefault(s => s.SensorId == sensorId);

        if (state is null)
        {
            if (storedState is not null)
            {
                db.DeviceControlStates.Remove(storedState);
            }
        }
        else if (storedState is null)
        {
            db.DeviceControlStates.Add(state);
        }
        else
        {
            db.Entry(storedState)
                .CurrentValues.SetValues(state);
        }

        var storedCommands = db.DeviceCommands
            .Where(c => c.SensorId == sensorId)
            .ToDictionary(c => c.Id);

        var currentCommands = commands
            .ToDictionary(c => c.Id);

        foreach (var stored in storedCommands.Values)
        {
            if (!currentCommands.TryGetValue(
                    stored.Id,
                    out var current))
            {
                db.DeviceCommands.Remove(stored);
            }
            else
            {
                db.Entry(stored)
                    .CurrentValues.SetValues(current);
            }
        }

        foreach (var current in currentCommands.Values)
        {
            if (!storedCommands.ContainsKey(current.Id))
            {
                db.DeviceCommands.Add(current);
            }
        }

        db.DeviceCommandAuditEvents.Add(
            new DeviceCommandAuditEvent
            {
                SensorId = sensorId,
                CommandId = commandId,
                Action = action,
                DesiredState = state?.DesiredState,
                ReportedState = state?.ReportedState,
                OccurredAtUtc = DateTime.UtcNow
            }
        );

        db.SaveChanges();
        transaction.Commit();
    }
}
