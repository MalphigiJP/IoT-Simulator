
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceCommandRecoveryTests
{
    private static Sensor CreateSensor()
    {
        return new Sensor
        {
            Id = 1,
            MacAddress = "02:53:58:00:00:04",
            Category = "PumpController",
            Location = "Pump Station",
            RegisteredAt = DateTime.UtcNow
        };
    }

    private static DeviceCommand ApplyManual(
        DeviceCommandService service,
        Sensor sensor,
        bool requestedState)
    {
        var created = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            requestedState
        );

        var command = Assert.IsType<DeviceCommand>(
            created.Command
        );

        service.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        var acknowledged = service.Acknowledge(
            sensor.Id,
            command.Id,
            true,
            requestedState,
            null
        );

        Assert.Equal(
            CommandAckOutcome.Applied,
            acknowledged.Outcome
        );

        return command;
    }

    private static DeviceCommandAuditEvent Event(
        long id,
        Guid commandId,
        string action)
    {
        return new DeviceCommandAuditEvent
        {
            Id = id,
            SensorId = 1,
            CommandId = commandId,
            Action = action,
            OccurredAtUtc = DateTime.UtcNow
        };
    }

    [Fact]
    public void AppliedManualCommandRestoresUndoStack()
    {
        var sensor = CreateSensor();
        var original = new DeviceCommandService();

        original.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var command = ApplyManual(
            original,
            sensor,
            true
        );

        var snapshot = new DeviceCommandSnapshot(
            original.GetCommands(sensor.Id),
            new[] { original.GetOrInitialize(sensor) },
            new[]
            {
                Event(1, command.Id, "CommandSubmitted"),
                Event(2, command.Id, "CommandApplied")
            }
        );

        var restored = new DeviceCommandService();

        restored.RestoreFromSnapshot(snapshot);

        var availability =
            restored.GetUndoAvailability(sensor);

        Assert.True(availability.CanUndo);
        Assert.Equal(1, availability.StackDepth);
        Assert.Equal(command.Id, availability.SourceCommandId);
        Assert.False(availability.RestoreState);
    }

    [Fact]
    public void CompletedUndoDoesNotReappearAfterRecovery()
    {
        var sensor = CreateSensor();
        var original = new DeviceCommandService();

        original.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var manual = ApplyManual(
            original,
            sensor,
            true
        );

        var createdUndo = original.CreateUndo(sensor);

        var undo = Assert.IsType<DeviceCommand>(
            createdUndo.Command
        );

        original.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        var acknowledged = original.Acknowledge(
            sensor.Id,
            undo.Id,
            true,
            false,
            null
        );

        Assert.Equal(
            CommandAckOutcome.Applied,
            acknowledged.Outcome
        );

        var snapshot = new DeviceCommandSnapshot(
            original.GetCommands(sensor.Id),
            new[] { original.GetOrInitialize(sensor) },
            new[]
            {
                Event(1, manual.Id, "CommandSubmitted"),
                Event(2, manual.Id, "CommandApplied"),
                Event(3, undo.Id, "UndoRequested"),
                Event(4, undo.Id, "UndoApplied")
            }
        );

        var restored = new DeviceCommandService();

        restored.RestoreFromSnapshot(snapshot);

        var availability =
            restored.GetUndoAvailability(sensor);

        Assert.False(availability.CanUndo);
        Assert.Equal(0, availability.StackDepth);

        Assert.Equal(2, restored.GetCommands(sensor.Id).Count);
    }

    [Fact]
    public void PendingCommandSurvivesRecovery()
    {
        var sensor = CreateSensor();
        var original = new DeviceCommandService();

        var created = original.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var command = Assert.IsType<DeviceCommand>(
            created.Command
        );

        var snapshot = new DeviceCommandSnapshot(
            original.GetCommands(sensor.Id),
            new[] { original.GetOrInitialize(sensor) },
            new[]
            {
                Event(1, command.Id, "CommandSubmitted")
            }
        );

        var restored = new DeviceCommandService();

        restored.RestoreFromSnapshot(snapshot);

        var claimed = restored.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        Assert.NotNull(claimed);
        Assert.Equal(command.Id, claimed.Id);
        Assert.Equal(
            DeviceCommandStatus.Delivered,
            claimed.Status
        );
    }

    [Fact]
    public void RecoveredCommandsRetainTheirAuditOrder()
    {
        var sensor = CreateSensor();
        var original = new DeviceCommandService();

        original.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var first = ApplyManual(
            original,
            sensor,
            true
        );

        var second = ApplyManual(
            original,
            sensor,
            false
        );

        var snapshot = new DeviceCommandSnapshot(
            original.GetCommands(sensor.Id),
            new[] { original.GetOrInitialize(sensor) },
            new[]
            {
                Event(1, first.Id, "CommandSubmitted"),
                Event(2, first.Id, "CommandApplied"),
                Event(3, second.Id, "CommandSubmitted"),
                Event(4, second.Id, "CommandApplied")
            }
        );

        var restored = new DeviceCommandService();

        restored.RestoreFromSnapshot(snapshot);

        var history = restored.GetCommands(sensor.Id);

        Assert.Equal(2, history.Count);
        Assert.Equal(second.Id, history[0].Id);
        Assert.Equal(first.Id, history[1].Id);

        var availability =
            restored.GetUndoAvailability(sensor);

        Assert.Equal(2, availability.StackDepth);
        Assert.Equal(second.Id, availability.SourceCommandId);
        Assert.True(availability.RestoreState);
    }
}
