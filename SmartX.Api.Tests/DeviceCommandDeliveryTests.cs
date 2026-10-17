
using SmartX.Api.Models;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class DeviceCommandDeliveryTests
{
    private static Sensor Sensor(
        int id = 1,
        string category = "PumpController")
    {
        return new Sensor
        {
            Id = id,
            MacAddress =
                $"02:53:58:00:00:{id:X2}",
            Location = "Test Station",
            Category = category,
            RegisteredAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void DeviceReportEstablishesKnownState()
    {
        var service = new DeviceCommandService();

        var report = service.ReportState(
            Sensor(),
            DeviceControlTarget.Pump,
            false
        );

        Assert.Equal(
            ControlReportOutcome.Updated,
            report.Outcome
        );

        Assert.False(report.State.DesiredState);
        Assert.False(report.State.ReportedState);
        Assert.NotNull(report.State.LastReportedAtUtc);
    }

    [Fact]
    public void EnvironmentalSensorCannotReportPumpState()
    {
        var service = new DeviceCommandService();

        var report = service.ReportState(
            Sensor(category: "Environmental"),
            DeviceControlTarget.Pump,
            true
        );

        Assert.Equal(
            ControlReportOutcome.ReadOnlyDevice,
            report.Outcome
        );
    }

    [Fact]
    public void ClaimChangesPendingToDelivered()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        var delivered = service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        Assert.NotNull(delivered);

        Assert.Equal(
            created.Command!.Id,
            delivered.Id
        );

        Assert.Equal(
            DeviceCommandStatus.Delivered,
            delivered.Status
        );

        Assert.NotNull(delivered.DeliveredAtUtc);
    }

    [Fact]
    public void CommandIsNotImmediatelyDeliveredTwice()
    {
        var service = new DeviceCommandService();

        service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        var now = DateTime.UtcNow;

        var first = service.ClaimNextForDevice(
            1, now
        );

        var second = service.ClaimNextForDevice(
            1,
            now.AddSeconds(5)
        );

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void ExpiredCommandCanBeDeliveredAgain()
    {
        var service = new DeviceCommandService();

        service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        var now = DateTime.UtcNow;

        var first = service.ClaimNextForDevice(
            1, now
        );

        var retry = service.ClaimNextForDevice(
            1,
            now + DeviceCommandService.DeliveryLease +
            TimeSpan.FromSeconds(1)
        );

        Assert.NotNull(first);
        Assert.NotNull(retry);

        Assert.Equal(first.Id, retry.Id);
    }

    [Fact]
    public void SuccessfulAckUpdatesReportedState()
    {
        var service = new DeviceCommandService();

        var sensor = Sensor();

        service.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var created = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            sensor.Id,
            DateTime.UtcNow
        );

        var ack = service.Acknowledge(
            sensor.Id,
            created.Command!.Id,
            applied: true,
            reportedState: true,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.Applied,
            ack.Outcome
        );

        Assert.Equal(
            DeviceCommandStatus.Applied,
            ack.Command!.Status
        );

        Assert.True(ack.State!.DesiredState);
        Assert.True(ack.State.ReportedState);
    }

    [Fact]
    public void CannotClaimSuccessWithoutMatchingState()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        var ack = service.Acknowledge(
            1,
            created.Command!.Id,
            applied: true,
            reportedState: false,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.InvalidReportedState,
            ack.Outcome
        );

        Assert.Equal(
            DeviceCommandStatus.Delivered,
            ack.Command!.Status
        );
    }

    [Fact]
    public void DuplicateSuccessAckIsIdempotent()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        service.Acknowledge(
            1,
            created.Command!.Id,
            true,
            true,
            null
        );

        long revision = service
            .GetOrInitialize(Sensor()).Revision;

        var secondAck = service.Acknowledge(
            1,
            created.Command.Id,
            true,
            true,
            null
        );

        Assert.Equal(
            CommandAckOutcome.AlreadyAcknowledged,
            secondAck.Outcome
        );

        Assert.Equal(
            revision,
            secondAck.State!.Revision
        );
    }

    [Fact]
    public void FailedExecutionRestoresDesiredState()
    {
        var service = new DeviceCommandService();

        var sensor = Sensor();

        service.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        var created = service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        var ack = service.Acknowledge(
            1,
            created.Command!.Id,
            applied: false,
            reportedState: false,
            error: "Motor failed to start."
        );

        Assert.Equal(
            CommandAckOutcome.Failed,
            ack.Outcome
        );

        Assert.False(ack.State!.DesiredState);
        Assert.False(ack.State.ReportedState);

        Assert.Equal(
            "Motor failed to start.",
            ack.Command!.FailureReason
        );
    }

    [Fact]
    public void OtherSensorCannotAcknowledgeCommand()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            Sensor(id: 1),
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        var ack = service.Acknowledge(
            sensorId: 2,
            commandId: created.Command!.Id,
            applied: true,
            reportedState: true,
            error: null
        );

        Assert.Equal(
            CommandAckOutcome.NotFound,
            ack.Outcome
        );
    }

    [Fact]
    public void DeliveredCommandCannotBeCancelled()
    {
        var service = new DeviceCommandService();

        var created = service.Submit(
            Sensor(),
            DeviceControlTarget.Pump,
            true
        );

        service.ClaimNextForDevice(
            1,
            DateTime.UtcNow
        );

        var cancellation = service.Cancel(
            created.Command!.Id
        );

        Assert.Equal(
            CommandCancelOutcome.NotPending,
            cancellation.Outcome
        );
    }

    [Fact]
    public void DeviceReportDoesNotErasePendingDesiredState()
    {
        var service = new DeviceCommandService();

        var sensor = Sensor();

        service.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        service.Submit(
            sensor,
            DeviceControlTarget.Pump,
            true
        );

        var report = service.ReportState(
            sensor,
            DeviceControlTarget.Pump,
            false
        );

        Assert.True(report.State.DesiredState);
        Assert.False(report.State.ReportedState);
    }
}
