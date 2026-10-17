
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceCommandServiceTests
{
    private static Sensor CreateSensor(
        int id,
        string category,
        string? mac = null)
    {
        return new Sensor
        {
            Id = id,
            Category = category,

            MacAddress = mac ??
                $"02:53:58:00:00:{id:X2}",

            Location = "Test Zone",
            RegisteredAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void PumpCommandStartsPending()
    {
        var service = new DeviceCommandService();

        var result = service.Submit(
            CreateSensor(1, "PumpController"),
            DeviceControlTarget.Pump,
            true
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            result.Outcome
        );

        var command =
            Assert.IsType<DeviceCommand>(
                result.Command
            );

        Assert.Equal(
            DeviceCommandStatus.Pending,
            command.Status
        );

        Assert.Equal(1, command.SensorId);
        Assert.True(command.RequestedState);
        Assert.NotEqual(Guid.Empty, command.Id);
    }

    [Fact]
    public void SubmittingCommandDoesNotConfirmDeviceState()
    {
        var service = new DeviceCommandService();

        var result = service.Submit(
            CreateSensor(1, "PumpController"),
            DeviceControlTarget.Pump,
            true
        );

        Assert.True(result.State.DesiredState);

        // No device acknowledgement exists yet.
        Assert.Null(result.State.ReportedState);

        Assert.Equal(1, result.State.Revision);
    }

    [Fact]
    public void EnvironmentalSensorRejectsCommands()
    {
        var service = new DeviceCommandService();

        var result = service.Submit(
            CreateSensor(1, "Environmental"),
            DeviceControlTarget.Pump,
            true
        );

        Assert.Equal(
            CommandCreateOutcome.ReadOnlyDevice,
            result.Outcome
        );

        Assert.Null(result.Command);
        Assert.Empty(service.GetCommands(1));
    }

    [Fact]
    public void SmartPlugRejectsPumpCommand()
    {
        var service = new DeviceCommandService();

        var result = service.Submit(
            CreateSensor(1, "SmartPlug"),
            DeviceControlTarget.Pump,
            true
        );

        Assert.Equal(
            CommandCreateOutcome.TargetMismatch,
            result.Outcome
        );

        Assert.Null(result.Command);
    }

    [Fact]
    public void DeviceCannotHaveTwoPendingCommands()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "PumpController"
        );

        var first = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var second = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            first.Outcome
        );

        Assert.Equal(
            CommandCreateOutcome.DeviceBusy,
            second.Outcome
        );

        Assert.Single(service.GetCommands(1));
    }

    [Fact]
    public void CanFindCommandById()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            CreateSensor(1, "IrrigationValve"),
            DeviceControlTarget.Valve,
            true
        );

        var command =
            Assert.IsType<DeviceCommand>(
                created.Command
            );

        bool found = service.TryGetCommand(
            command.Id,
            out var retrieved
        );

        Assert.True(found);
        Assert.NotNull(retrieved);

        Assert.Equal(
            command.Id,
            retrieved.Id
        );
    }

    [Fact]
    public void CancellingPendingCommandRestoresDesiredState()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "PumpController"
        );

        var created = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var command =
            Assert.IsType<DeviceCommand>(
                created.Command
            );

        var cancellation = service.Cancel(
            command.Id
        );

        Assert.Equal(
            CommandCancelOutcome.Cancelled,
            cancellation.Outcome
        );

        Assert.Equal(
            DeviceCommandStatus.Cancelled,
            cancellation.Command!.Status
        );

        var state = service.GetOrInitialize(
            sensor
        );

        // Desired state was unknown before
        // the command, so it returns to null.
        Assert.Null(state.DesiredState);
        Assert.Null(state.ReportedState);

        Assert.Equal(2, state.Revision);
    }

    [Fact]
    public void CannotCancelSameCommandTwice()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            CreateSensor(1, "SmartPlug"),
            DeviceControlTarget.Relay,
            false
        );

        var command =
            Assert.IsType<DeviceCommand>(
                created.Command
            );

        service.Cancel(command.Id);

        var secondCancellation =
            service.Cancel(command.Id);

        Assert.Equal(
            CommandCancelOutcome.NotPending,
            secondCancellation.Outcome
        );
    }

    [Fact]
    public void UnknownCommandCannotBeCancelled()
    {
        var service = new DeviceCommandService();

        var result = service.Cancel(
            Guid.NewGuid()
        );

        Assert.Equal(
            CommandCancelOutcome.NotFound,
            result.Outcome
        );
    }

    [Fact]
    public void CanSubmitNewCommandAfterCancellation()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "PumpController"
        );

        var first = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var firstCommand =
            Assert.IsType<DeviceCommand>(
                first.Command
            );

        service.Cancel(firstCommand.Id);

        var second = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            second.Outcome
        );

        Assert.Equal(
            2,
            service.GetCommands(1).Count
        );
    }

    [Fact]
    public void DifferentDevicesCanReceiveCommands()
    {
        var service = new DeviceCommandService();

        var pump = service.Submit(
            CreateSensor(1, "PumpController"),
            DeviceControlTarget.Pump,
            true
        );

        var valve = service.Submit(
            CreateSensor(2, "IrrigationValve"),
            DeviceControlTarget.Valve,
            false
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            pump.Outcome
        );

        Assert.Equal(
            CommandCreateOutcome.Created,
            valve.Outcome
        );

        Assert.Single(service.GetCommands(1));
        Assert.Single(service.GetCommands(2));
    }

    [Fact]
    public async Task ConcurrentRequestsAcceptOnlyOneCommand()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "SmartPlug"
        );

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() =>
                service.Submit(
                    sensor,
                    DeviceControlTarget.Relay,
                    true
                )
            ))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Single(
            results.Where(result =>
                result.Outcome ==
                CommandCreateOutcome.Created)
        );

        Assert.Equal(
            19,
            results.Count(result =>
                result.Outcome ==
                CommandCreateOutcome.DeviceBusy)
        );

        Assert.Single(service.GetCommands(1));
    }

    [Fact]
    public void ChangingDeviceTypeCancelsPendingCommand()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "PumpController"
        );

        var created = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var command =
            Assert.IsType<DeviceCommand>(
                created.Command
            );

        sensor.Category = "Environmental";

        var updatedState =
            service.GetOrInitialize(sensor);

        Assert.Null(updatedState.SupportedTarget);
        Assert.Null(updatedState.DesiredState);
        Assert.Null(updatedState.ReportedState);

        service.TryGetCommand(
            command.Id,
            out var updatedCommand
        );

        Assert.Equal(
            DeviceCommandStatus.Cancelled,
            updatedCommand!.Status
        );
    }

    [Fact]
    public void RemovingSensorClearsItsCommandState()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            CreateSensor(1, "SmartPlug"),
            DeviceControlTarget.Relay,
            true
        );

        var command =
            Assert.IsType<DeviceCommand>(
                created.Command
            );

        service.RemoveSensor(1);

        Assert.Empty(service.GetCommands(1));

        Assert.False(
            service.TryGetCommand(command.Id, out _)
        );
    }

    [Fact]
    public void HistoryReturnsNewestCommandFirst()
    {
        var service = new DeviceCommandService();

        var sensor = CreateSensor(
            1, "PumpController"
        );

        var first = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var firstCommand =
            Assert.IsType<DeviceCommand>(
                first.Command
            );

        service.Cancel(firstCommand.Id);

        var second = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var secondCommand =
            Assert.IsType<DeviceCommand>(
                second.Command
            );

        var history = service.GetCommands(1);

        Assert.Equal(2, history.Count);

        Assert.Equal(
            secondCommand.Id,
            history[0].Id
        );

        Assert.Equal(
            firstCommand.Id,
            history[1].Id
        );
    }
}
