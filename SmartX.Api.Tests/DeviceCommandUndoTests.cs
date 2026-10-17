
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceCommandUndoTests
{
    private static Sensor Sensor(
        int id = 1,
        string category = "PumpController")
    {
        return new Sensor
        {
            Id = id,
            MacAddress = $"02:53:58:00:00:{id:X2}",
            Location = "Undo Test Station",
            Category = category,
            RegisteredAt = DateTime.UtcNow
        };
    }

    private static void Report(
        DeviceCommandService service,
        Sensor sensor,
        bool state)
    {
        var target = DeviceControlCatalog
            .GetSupportedTarget(sensor.Category)!.Value;

        var result = service.ReportState(
            sensor,
            target,
            state
        );

        Assert.Equal(
            ControlReportOutcome.Updated,
            result.Outcome
        );
    }

    private static DeviceCommand ApplyManual(
        DeviceCommandService service,
        Sensor sensor,
        bool requestedState)
    {
        var target = DeviceControlCatalog
            .GetSupportedTarget(sensor.Category)!.Value;

        var created = service.Submit(
            sensor,
            target,
            requestedState
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            created.Outcome
        );

        var command = Assert.IsType<DeviceCommand>(
            created.Command
        );

        var delivered = service.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        Assert.NotNull(delivered);
        Assert.Equal(command.Id, delivered.Id);

        var ack = service.Acknowledge(
            sensor.Id,
            command.Id,
            applied: true,
            reportedState: requestedState,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.Applied,
            ack.Outcome
        );

        return ack.Command!;
    }

    private static DeviceCommand CreateUndo(
        DeviceCommandService service,
        Sensor sensor)
    {
        var result = service.CreateUndo(sensor);

        Assert.Equal(
            UndoCreateOutcome.Created,
            result.Outcome
        );

        return Assert.IsType<DeviceCommand>(
            result.Command
        );
    }

    private static void ApplyUndo(
        DeviceCommandService service,
        Sensor sensor,
        DeviceCommand command)
    {
        var claimed = service.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        Assert.NotNull(claimed);
        Assert.Equal(command.Id, claimed.Id);

        var ack = service.Acknowledge(
            sensor.Id,
            command.Id,
            applied: true,
            reportedState: command.RequestedState,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.Applied,
            ack.Outcome
        );
    }

    [Fact]
    public void NewDeviceHasNoUndoHistory()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.False(status.CanUndo);
        Assert.Equal(0, status.StackDepth);

        Assert.Equal(
            UndoAvailabilityReason.NoHistory,
            status.Reason
        );
    }

    [Fact]
    public void EnvironmentalSensorCannotUndo()
    {
        var service = new DeviceCommandService();

        var result = service.CreateUndo(
            Sensor(category: "Environmental")
        );

        Assert.Equal(
            UndoCreateOutcome.ReadOnlyDevice,
            result.Outcome
        );
    }

    [Fact]
    public void PendingCommandIsNotPushed()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.Equal(0, status.StackDepth);
        Assert.False(status.CanUndo);
    }

    [Fact]
    public void CommandWithUnknownPreviousStateCannotUndo()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        // No initial state report.
        ApplyManual(service, sensor, true);

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.False(status.CanUndo);
        Assert.Equal(0, status.StackDepth);
    }

    [Fact]
    public void AppliedManualCommandEntersUndoStack()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        var original = ApplyManual(
            service,
            sensor,
            true
        );

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.True(status.CanUndo);
        Assert.Equal(1, status.StackDepth);

        Assert.Equal(
            original.Id,
            status.SourceCommandId
        );

        Assert.False(status.RestoreState);
    }

    [Fact]
    public void UndoCreatesNewPendingCommand()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        var original = ApplyManual(
            service,
            sensor,
            true
        );

        var undo = CreateUndo(
            service,
            sensor
        );

        Assert.NotEqual(original.Id, undo.Id);

        Assert.Equal(
            DeviceCommandOrigin.Undo,
            undo.Origin
        );

        Assert.Equal(
            original.Id,
            undo.UndoOfCommandId
        );

        Assert.False(undo.RequestedState);

        Assert.Equal(
            DeviceCommandStatus.Pending,
            undo.Status
        );

        // Undo has only changed the desired state.
        var state = service.GetOrInitialize(sensor);

        Assert.False(state.DesiredState);
        Assert.True(state.ReportedState);

        // Original command is still retained.
        Assert.True(
            service.TryGetCommand(original.Id, out _)
        );
    }

    [Fact]
    public void AppliedUndoPopsStack()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        var undo = CreateUndo(service, sensor);

        // Still on the stack while pending.
        Assert.Equal(
            1,
            service.GetUndoAvailability(sensor).StackDepth
        );

        ApplyUndo(service, sensor, undo);

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.Equal(0, status.StackDepth);
        Assert.False(status.CanUndo);

        var state = service.GetOrInitialize(sensor);

        Assert.False(state.DesiredState);
        Assert.False(state.ReportedState);
    }

    [Fact]
    public void MultipleCommandsUndoLastInFirstOut()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        var first = ApplyManual(
            service, sensor, true
        );

        var second = ApplyManual(
            service, sensor, false
        );

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.Equal(2, status.StackDepth);
        Assert.Equal(second.Id, status.SourceCommandId);

        // Undo second command: OFF -> ON.
        var undoSecond = CreateUndo(
            service, sensor
        );

        Assert.True(undoSecond.RequestedState);
        Assert.Equal(second.Id, undoSecond.UndoOfCommandId);

        ApplyUndo(service, sensor, undoSecond);

        status = service.GetUndoAvailability(sensor);

        Assert.Equal(1, status.StackDepth);
        Assert.Equal(first.Id, status.SourceCommandId);

        // Undo first command: ON -> OFF.
        var undoFirst = CreateUndo(
            service, sensor
        );

        Assert.False(undoFirst.RequestedState);
        Assert.Equal(first.Id, undoFirst.UndoOfCommandId);

        ApplyUndo(service, sensor, undoFirst);

        Assert.Equal(
            0,
            service.GetUndoAvailability(sensor).StackDepth
        );
    }

    [Fact]
    public void CancelledUndoDoesNotPopStack()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        var undo = CreateUndo(service, sensor);

        var cancelled = service.Cancel(undo.Id);

        Assert.Equal(
            CommandCancelOutcome.Cancelled,
            cancelled.Outcome
        );

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.True(status.CanUndo);
        Assert.Equal(1, status.StackDepth);

        // We can submit a replacement undo.
        var retry = CreateUndo(service, sensor);

        Assert.NotEqual(undo.Id, retry.Id);
    }

    [Fact]
    public void FailedUndoRetainsUndoEntry()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        var undo = CreateUndo(service, sensor);

        service.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        var ack = service.Acknowledge(
            sensor.Id,
            undo.Id,
            applied: false,
            reportedState: true,
            error: "Actuator temporarily unavailable."
        );

        Assert.Equal(
            CommandAckOutcome.Failed,
            ack.Outcome
        );

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.True(status.CanUndo);
        Assert.Equal(1, status.StackDepth);
    }

    [Fact]
    public void UndoRejectsOutstandingCommands()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var undoResult = service.CreateUndo(
            sensor
        );

        Assert.Equal(
            UndoCreateOutcome.DeviceBusy,
            undoResult.Outcome
        );
    }

    [Fact]
    public void UndoRejectsUnexpectedDeviceState()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        // Device unexpectedly stops after the
        // manual command was applied.
        Report(service, sensor, false);

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.False(status.CanUndo);

        Assert.Equal(
            UndoAvailabilityReason.StateChanged,
            status.Reason
        );

        Assert.Equal(
            UndoCreateOutcome.StateChanged,
            service.CreateUndo(sensor).Outcome
        );
    }

    [Fact]
    public void DuplicateManualAckDoesNotPushTwice()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        var manual = ApplyManual(
            service, sensor, true
        );

        var duplicate = service.Acknowledge(
            sensor.Id,
            manual.Id,
            applied: true,
            reportedState: true,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.AlreadyAcknowledged,
            duplicate.Outcome
        );

        Assert.Equal(
            1,
            service.GetUndoAvailability(sensor).StackDepth
        );
    }

    [Fact]
    public void NoOpManualCommandIsNotUndoable()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, true);

        // The pump is already running.
        ApplyManual(service, sensor, true);

        Assert.Equal(
            0,
            service.GetUndoAvailability(sensor).StackDepth
        );
    }

    [Fact]
    public void ChangingDeviceIdentityClearsUndoStack()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        sensor.MacAddress = "02:53:58:FF:FF:FF";

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.False(status.CanUndo);
        Assert.Equal(0, status.StackDepth);
    }

    [Fact]
    public void RemovingSensorClearsUndoHistory()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        service.RemoveSensor(sensor.Id);

        var status = service.GetUndoAvailability(
            sensor
        );

        Assert.False(status.CanUndo);
        Assert.Equal(0, status.StackDepth);
    }

    [Fact]
    public async Task ConcurrentUndoAcceptsOnlyOneRequest()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);
        ApplyManual(service, sensor, true);

        var tasks = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() =>
                service.CreateUndo(sensor)
            ))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Single(results.Where(result =>
            result.Outcome == UndoCreateOutcome.Created
        ));

        Assert.Equal(
            11,
            results.Count(result =>
                result.Outcome == UndoCreateOutcome.DeviceBusy
            )
        );
    }

    [Fact]
    public void UndoPreservesOriginalCommandHistory()
    {
        var service = new DeviceCommandService();
        var sensor = Sensor();

        Report(service, sensor, false);

        var original = ApplyManual(
            service, sensor, true
        );

        var undo = CreateUndo(
            service, sensor
        );

        ApplyUndo(service, sensor, undo);

        var history = service.GetCommands(sensor.Id);

        Assert.Equal(2, history.Count);

        Assert.Equal(undo.Id, history[0].Id);
        Assert.Equal(original.Id, history[1].Id);

        Assert.Equal(
            DeviceCommandStatus.Applied,
            history[0].Status
        );

        Assert.Equal(
            DeviceCommandStatus.Applied,
            history[1].Status
        );

        Assert.Equal(
            DeviceCommandOrigin.Undo,
            history[0].Origin
        );

        Assert.Equal(
            original.Id,
            history[0].UndoOfCommandId
        );
    }
}
