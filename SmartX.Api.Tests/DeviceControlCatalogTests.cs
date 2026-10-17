
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceControlCatalogTests
{
    private static Sensor CreateSensor(
        string category,
        int id = 1)
    {
        return new Sensor
        {
            Id = id,
            MacAddress = "02:53:58:00:00:01",
            Location = "Test Zone",
            Category = category,
            RegisteredAt = DateTime.UtcNow
        };
    }

    [Theory]
    [InlineData(
        "SmartPlug",
        DeviceControlTarget.Relay)]
    [InlineData(
        "IrrigationValve",
        DeviceControlTarget.Valve)]
    [InlineData(
        "PumpController",
        DeviceControlTarget.Pump)]
    public void ControllableDevicesHaveCorrectTarget(
        string category,
        DeviceControlTarget expected)
    {
        var target =
            DeviceControlCatalog.GetSupportedTarget(
                category
            );

        Assert.Equal(expected, target);
    }

    [Fact]
    public void EnvironmentalSensorIsReadOnly()
    {
        var target =
            DeviceControlCatalog.GetSupportedTarget(
                "Environmental"
            );

        Assert.Null(target);
    }

    [Fact]
    public void UnknownCategoryHasNoControls()
    {
        var target =
            DeviceControlCatalog.GetSupportedTarget(
                "UnknownDevice"
            );

        Assert.Null(target);
    }

    [Fact]
    public void CategoryLookupIgnoresCaseAndWhitespace()
    {
        var target =
            DeviceControlCatalog.GetSupportedTarget(
                "  smartplug  "
            );

        Assert.Equal(
            DeviceControlTarget.Relay,
            target
        );
    }

    [Fact]
    public void SmartPlugSupportsRelayCommands()
    {
        Assert.True(
            DeviceControlCatalog.Supports(
                "SmartPlug",
                DeviceControlTarget.Relay
            )
        );
    }

    [Fact]
    public void SmartPlugRejectsPumpCommands()
    {
        Assert.False(
            DeviceControlCatalog.Supports(
                "SmartPlug",
                DeviceControlTarget.Pump
            )
        );
    }

    [Fact]
    public void NewDeviceDoesNotAssumeItIsOff()
    {
        var state =
            DeviceControlCatalog.CreateInitialState(
                CreateSensor("PumpController")
            );

        Assert.Equal(1, state.SensorId);

        Assert.Equal(
            DeviceControlTarget.Pump,
            state.SupportedTarget
        );

        Assert.Null(state.DesiredState);
        Assert.Null(state.ReportedState);
        Assert.Null(state.LastCommandId);

        Assert.Equal(0, state.Revision);
    }

    [Fact]
    public void EnvironmentalStateHasNoControlTarget()
    {
        var state =
            DeviceControlCatalog.CreateInitialState(
                CreateSensor("Environmental")
            );

        Assert.Null(state.SupportedTarget);
        Assert.Null(state.DesiredState);
        Assert.Null(state.ReportedState);
    }

    [Fact]
    public void UnregisteredSensorCannotGetControlState()
    {
        var sensor = CreateSensor(
            "PumpController",
            id: 0
        );

        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeviceControlCatalog.CreateInitialState(
                sensor
            )
        );
    }

    [Fact]
    public void NewCommandStartsPending()
    {
        var command = new DeviceCommand
        {
            SensorId = 1,
            Target = DeviceControlTarget.Pump,
            RequestedState = true,
            Origin = DeviceCommandOrigin.Manual
        };

        Assert.NotEqual(Guid.Empty, command.Id);

        Assert.Equal(
            DeviceCommandStatus.Pending,
            command.Status
        );

        Assert.Equal(
            DeviceCommandOrigin.Manual,
            command.Origin
        );

        Assert.Null(command.DeliveredAtUtc);
        Assert.Null(command.CompletedAtUtc);

        Assert.Equal(
            DateTimeKind.Utc,
            command.CreatedAtUtc.Kind
        );
    }

    [Fact]
    public void CommandDtoDistinguishesMissingState()
    {
        var missing = new CreateDeviceCommandDto
        {
            Target = "Pump"
        };

        var turnOff = new CreateDeviceCommandDto
        {
            Target = "Pump",
            State = false
        };

        Assert.Null(missing.State);
        Assert.False(turnOff.State);
    }

    [Fact]
    public void UndoCommandReferencesOriginalCommand()
    {
        var original = new DeviceCommand
        {
            SensorId = 1,
            Target = DeviceControlTarget.Pump,
            RequestedState = true,
            PreviousDesiredState = false
        };

        var undo = new DeviceCommand
        {
            SensorId = 1,
            Target = DeviceControlTarget.Pump,
            RequestedState = false,
            PreviousDesiredState = true,
            Origin = DeviceCommandOrigin.Undo,
            UndoOfCommandId = original.Id
        };

        Assert.Equal(
            original.Id,
            undo.UndoOfCommandId
        );

        Assert.Equal(
            DeviceCommandOrigin.Undo,
            undo.Origin
        );

        Assert.False(undo.RequestedState);

        Assert.NotEqual(
            original.Id,
            undo.Id
        );
    }
}
