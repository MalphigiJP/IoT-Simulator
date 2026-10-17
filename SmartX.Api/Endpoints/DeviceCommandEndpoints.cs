
using Microsoft.EntityFrameworkCore;

using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class DeviceCommandEndpoints
{
    public static IEndpointRouteBuilder
        MapDeviceCommandEndpoints(
            this IEndpointRouteBuilder app)
    {
        var devices = app.MapGroup("/api/devices")
            .WithTags("Device Commands");

        devices.MapGet(
            "/{sensorId:int}/control",
            async (
                int sensorId,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId, ct);

                if (sensor is null)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                return Results.Ok(
                    ToControlResponse(
                        commands.GetOrInitialize(sensor)
                    )
                );
            }
        );

        devices.MapPost(
            "/{sensorId:int}/commands",
            async (
                int sensorId,
                CreateDeviceCommandDto request,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                if (request.State is null)
                    return Results.BadRequest(new
                    {
                        error = "State must be true or false."
                    });

                if (!TryParseTarget(
                        request.Target, out var target))
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Target must be Relay, Valve or Pump."
                    });
                }

                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId, ct);

                if (sensor is null)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                var result = commands.Submit(
                    sensor, target, request.State.Value);

                return result.Outcome switch
                {
                    CommandCreateOutcome.ReadOnlyDevice =>
                        Results.BadRequest(new
                        {
                            error =
                                "This device is read-only."
                        }),

                    CommandCreateOutcome.TargetMismatch =>
                        Results.BadRequest(new
                        {
                            error =
                                "Unsupported control target.",
                            supportedTarget =
                                result.State.SupportedTarget
                                    ?.ToString()
                        }),

                    CommandCreateOutcome.DeviceBusy =>
                        Results.Conflict(new
                        {
                            error =
                                "Device has an outstanding command."
                        }),

                    _ => Results.Created(
                        $"/api/commands/{result.Command!.Id}",
                        ToCommandResponse(result.Command)
                    )
                };
            }
        );

        devices.MapGet(
            "/{sensorId:int}/commands",
            async (
                int sensorId,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                bool exists = await db.Sensors
                    .AnyAsync(
                        s => s.Id == sensorId, ct);

                if (!exists)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                return Results.Ok(
                    commands.GetCommands(sensorId)
                        .Select(ToCommandResponse)
                        .ToArray()
                );
            }
        );

        devices.MapGet(
            "/{sensorId:int}/commands/pending",
            async (
                int sensorId,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId, ct);

                if (sensor is null)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                commands.GetOrInitialize(sensor);

                var command = commands.ClaimNextForDevice(
                    sensorId,
                    DateTime.UtcNow
                );

                if (command is null)
                    return Results.NoContent();

                return Results.Ok(
                    ToCommandResponse(command)
                );
            }
        );

        devices.MapPost(
            "/{sensorId:int}/commands/{commandId:guid}/ack",
            async (
                int sensorId,
                Guid commandId,
                DeviceCommandAcknowledgementDto request,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                if (request.CommandId != commandId)
                    return Results.BadRequest(new
                    {
                        error =
                            "Body commandId must match the URL."
                    });

                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId, ct);

                if (sensor is null)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                commands.GetOrInitialize(sensor);

                var result = commands.Acknowledge(
                    sensorId,
                    commandId,
                    request.Applied,
                    request.ReportedState,
                    request.Error
                );

                return result.Outcome switch
                {
                    CommandAckOutcome.NotFound =>
                        Results.NotFound(new
                        {
                            error = "Command not found."
                        }),

                    CommandAckOutcome.InvalidStatus =>
                        Results.Conflict(new
                        {
                            error =
                                "Command is not awaiting acknowledgement."
                        }),

                    CommandAckOutcome.InvalidReportedState =>
                        Results.BadRequest(new
                        {
                            error =
                                "Successful acknowledgement must " +
                                "confirm the requested state."
                        }),

                    _ => Results.Ok(new
                    {
                        command =
                            ToCommandResponse(result.Command!),

                        control =
                            ToControlResponse(result.State!),

                        alreadyAcknowledged =
                            result.Outcome ==
                            CommandAckOutcome.AlreadyAcknowledged
                    })
                };
            }
        );

        devices.MapPost(
            "/{sensorId:int}/control/report",
            async (
                int sensorId,
                DeviceControlReportDto request,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken ct) =>
            {
                if (request.State is null ||
                    !TryParseTarget(
                        request.Target, out var target))
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "A valid target and Boolean state are required."
                    });
                }

                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId, ct);

                if (sensor is null)
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });

                var result = commands.ReportState(
                    sensor,
                    target,
                    request.State.Value
                );

                if (result.Outcome ==
                    ControlReportOutcome.ReadOnlyDevice)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Read-only device cannot report control state."
                    });
                }

                if (result.Outcome ==
                    ControlReportOutcome.TargetMismatch)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Control target does not match the device."
                    });
                }

                return Results.Ok(
                    ToControlResponse(result.State)
                );
            }
        );

        var commandRoutes = app.MapGroup(
            "/api/commands"
        ).WithTags("Device Commands");

        commandRoutes.MapGet(
            "/{commandId:guid}",
            (
                Guid commandId,
                DeviceCommandService commands) =>
            {
                if (!commands.TryGetCommand(
                        commandId, out var command))
                {
                    return Results.NotFound(new
                    {
                        error = "Command not found."
                    });
                }

                return Results.Ok(
                    ToCommandResponse(command!)
                );
            }
        );

        commandRoutes.MapPost(
            "/{commandId:guid}/cancel",
            (
                Guid commandId,
                DeviceCommandService commands) =>
            {
                var result = commands.Cancel(commandId);

                if (result.Outcome ==
                    CommandCancelOutcome.NotFound)
                {
                    return Results.NotFound(new
                    {
                        error = "Command not found."
                    });
                }

                if (result.Outcome ==
                    CommandCancelOutcome.NotPending)
                {
                    return Results.Conflict(new
                    {
                        error =
                            "Only pending commands can be cancelled."
                    });
                }

                return Results.Ok(
                    ToCommandResponse(result.Command!)
                );
            }
        );

        return app;
    }

    private static bool TryParseTarget(
        string? value,
        out DeviceControlTarget target)
    {
        target = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string name = value.Trim();

        foreach (var possible in
            Enum.GetValues<DeviceControlTarget>())
        {
            if (string.Equals(
                possible.ToString(),
                name,
                StringComparison.OrdinalIgnoreCase))
            {
                target = possible;
                return true;
            }
        }

        return false;
    }

    private static object ToCommandResponse(
        DeviceCommand command)
    {
        return new
        {
            commandId = command.Id,
            sensorId = command.SensorId,
            target = command.Target.ToString(),
            requestedState = command.RequestedState,
            previousDesiredState =
                command.PreviousDesiredState,
            origin = command.Origin.ToString(),
            status = command.Status.ToString(),
            undoOfCommandId = command.UndoOfCommandId,
            createdAtUtc = command.CreatedAtUtc,
            deliveredAtUtc = command.DeliveredAtUtc,
            completedAtUtc = command.CompletedAtUtc,
            failureReason = command.FailureReason
        };
    }

    private static object ToControlResponse(
        DeviceControlState state)
    {
        return new
        {
            sensorId = state.SensorId,
            macAddress = state.MacAddress,
            category = state.Category,
            supportedTarget =
                state.SupportedTarget?.ToString(),
            desiredState = state.DesiredState,
            reportedState = state.ReportedState,
            lastCommandId = state.LastCommandId,
            lastDesiredChangeAtUtc =
                state.LastDesiredChangeAtUtc,
            lastReportedAtUtc =
                state.LastReportedAtUtc,
            revision = state.Revision
        };
    }
}
