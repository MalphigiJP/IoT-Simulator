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
                CancellationToken cancellationToken) =>
            {
                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId,
                        cancellationToken
                    );

                if (sensor is null)
                {
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });
                }

                var state = commands.GetOrInitialize(
                    sensor
                );

                return Results.Ok(
                    ToControlResponse(state)
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
                CancellationToken cancellationToken) =>
            {
                if (request.State is null)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "The state field is required. " +
                            "Use true or false."
                    });
                }

                // Accept enum names, not numeric values.
                bool validTarget =
                    !string.IsNullOrWhiteSpace(
                        request.Target
                    ) &&
                    Enum.GetNames<DeviceControlTarget>()
                        .Any(name => string.Equals(
                            name,
                            request.Target.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        ));

                if (!validTarget)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "Target must be Relay, Valve or Pump."
                    });
                }

                Enum.TryParse<DeviceControlTarget>(
                    request.Target!.Trim(),
                    true,
                    out var target
                );

                var sensor = await db.Sensors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        s => s.Id == sensorId,
                        cancellationToken
                    );

                if (sensor is null)
                {
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });
                }

                var result = commands.Submit(
                    sensor,
                    target,
                    request.State.Value
                );

                if (result.Outcome ==
                    CommandCreateOutcome.ReadOnlyDevice)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "This sensor does not support manual control."
                    });
                }

                if (result.Outcome ==
                    CommandCreateOutcome.TargetMismatch)
                {
                    return Results.BadRequest(new
                    {
                        error =
                            "The requested control target " +
                            "is not supported by this device.",

                        supportedTarget =
                            result.State.SupportedTarget?.ToString()
                    });
                }

                if (result.Outcome ==
                    CommandCreateOutcome.DeviceBusy)
                {
                    return Results.Conflict(new
                    {
                        error =
                            "This device already has an " +
                            "outstanding command."
                    });
                }

                var command = result.Command!;

                return Results.Created(
                    $"/api/commands/{command.Id}",
                    ToCommandResponse(command)
                );
            }
        );

        devices.MapGet(
            "/{sensorId:int}/commands",
            async (
                int sensorId,
                SmartXContext db,
                DeviceCommandService commands,
                CancellationToken cancellationToken) =>
            {
                bool exists = await db.Sensors
                    .AnyAsync(
                        s => s.Id == sensorId,
                        cancellationToken
                    );

                if (!exists)
                {
                    return Results.NotFound(new
                    {
                        error = "Sensor not found."
                    });
                }

                var history = commands
                    .GetCommands(sensorId)
                    .Select(ToCommandResponse)
                    .ToArray();

                return Results.Ok(history);
            }
        );

        var commandRoutes = app
            .MapGroup("/api/commands")
            .WithTags("Device Commands");

        // GET ONE COMMAND BY ID
        commandRoutes.MapGet(
            "/{commandId:guid}",
            (
                Guid commandId,
                DeviceCommandService commands) =>
            {
                if (!commands.TryGetCommand(
                        commandId,
                        out var command))
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

        // CANCEL A COMMAND BEFORE DELIVERY
        commandRoutes.MapPost(
            "/{commandId:guid}/cancel",
            (
                Guid commandId,
                DeviceCommandService commands) =>
            {
                var result = commands.Cancel(
                    commandId
                );

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

            undoOfCommandId =
                command.UndoOfCommandId,

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
