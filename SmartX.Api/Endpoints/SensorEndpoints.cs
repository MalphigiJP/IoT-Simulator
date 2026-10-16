using Microsoft.EntityFrameworkCore;

using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class SensorEndpoints
{
    public static IEndpointRouteBuilder MapSensorEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sensors")
            .WithTags("Sensors");

        group.MapGet("/", async (
            SensorService service,
            CancellationToken cancellationToken) =>
        {
            var sensors = await service.GetAllAsync(
                cancellationToken
            );

            return Results.Ok(sensors);
        })
        .WithName("GetSensors");

        group.MapGet("/health", async (
            SmartXContext db,
            CancellationToken cancellationToken) =>
        {
            var sensors = await db.Sensors
                .AsNoTracking()
                .Select(sensor => new
                {
                    Sensor = sensor,

                    LastTelemetry = sensor.TelemetryRecords
                        .OrderByDescending(t => t.Timestamp)
                        .Select(t => (DateTime?)t.Timestamp)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            var result = sensors.Select(item =>
            {
                var sensor = item.Sensor;
                var lastTelemetry = item.LastTelemetry;

                if (lastTelemetry is null)
                {
                    return new SensorHealthDto
                    {
                        SensorId = sensor.Id,
                        MacAddress = sensor.MacAddress,
                        Location = sensor.Location,
                        Category = sensor.Category,
                        Status = "Offline",
                        LastTelemetry = null,
                        SecondsSinceLastTelemetry = null
                    };
                }

                double secondsSince =
                    (now - lastTelemetry.Value).TotalSeconds;

                string status = secondsSince switch
                {
                    <= 30 => "Online",
                    <= 120 => "Delayed",
                    _ => "Offline"
                };

                return new SensorHealthDto
                {
                    SensorId = sensor.Id,
                    MacAddress = sensor.MacAddress,
                    Location = sensor.Location,
                    Category = sensor.Category,
                    Status = status,
                    LastTelemetry = lastTelemetry,
                    SecondsSinceLastTelemetry = secondsSince
                };

            }).ToList();

            return Results.Ok(result);
        })
        .WithName("GetSensorHealth");

        group.MapGet("/{id:int}", async (
            int id,
            SmartXContext db,
            CancellationToken cancellationToken) =>
        {
            var sensor = await db.Sensors
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.Id == id,
                    cancellationToken
                );

            return sensor is null
                ? Results.NotFound()
                : Results.Ok(sensor);
        })
        .WithName("GetSensorById");

        group.MapPost("/", async (
            CreateSensorDto dto,
            SmartXContext db,
            DeviceRegistry registry,
            CancellationToken cancellationToken) =>
        {
            // Validate required fields.
            if (
                string.IsNullOrWhiteSpace(dto.MacAddress) ||
                string.IsNullOrWhiteSpace(dto.Location) ||
                string.IsNullOrWhiteSpace(dto.Category)
            )
            {
                return Results.BadRequest(new
                {
                    message =
                        "MAC address, location and category are required."
                });
            }

            string macAddress = dto.MacAddress.Trim();

            // Prevent duplicate MAC addresses.
            var exists = await db.Sensors.AnyAsync(
                s => s.MacAddress == macAddress,
                cancellationToken
            );

            if (exists)
            {
                return Results.Conflict(new
                {
                    message =
                        "A sensor with this MAC address already exists."
                });
            }

            var sensor = new Sensor
            {
                MacAddress = macAddress,
                Location = dto.Location.Trim(),
                Category = dto.Category.Trim(),
                RegisteredAt = DateTime.UtcNow
            };

            db.Sensors.Add(sensor);

            // Save permanently first.
            await db.SaveChangesAsync(
                cancellationToken
            );

            // Register in the live device dictionary.
            registry.UpsertSensor(sensor);

            return Results.Created(
                $"/api/sensors/{sensor.Id}",
                sensor
            );
        })
        .WithName("CreateSensor");

        group.MapPut("/{id:int}", async (
            int id,
            UpdateSensorDto dto,
            SmartXContext db,
            DeviceRegistry registry,
            CancellationToken cancellationToken) =>
        {
            var sensor = await db.Sensors.FindAsync(
                new object[] { id },
                cancellationToken
            );

            if (sensor is null)
            {
                return Results.NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            // Validate required fields.
            if (
                string.IsNullOrWhiteSpace(dto.MacAddress) ||
                string.IsNullOrWhiteSpace(dto.Location) ||
                string.IsNullOrWhiteSpace(dto.Category)
            )
            {
                return Results.BadRequest(new
                {
                    message =
                        "MAC address, location and category are required."
                });
            }

            string macAddress = dto.MacAddress.Trim();

            // Prevent duplicate MAC addresses.
            var duplicate = await db.Sensors.AnyAsync(
                s => s.MacAddress == macAddress &&
                     s.Id != id,
                cancellationToken
            );

            if (duplicate)
            {
                return Results.Conflict(new
                {
                    message =
                        "Another sensor already uses this MAC address."
                });
            }

            // Update sensor metadata.
            sensor.MacAddress = macAddress;
            sensor.Location = dto.Location.Trim();
            sensor.Category = dto.Category.Trim();

            // Save changes to SQL Server.
            await db.SaveChangesAsync(
                cancellationToken
            );

            // Synchronise the live device registry.
            // Existing telemetry state is preserved.
            registry.UpsertSensor(sensor);

            return Results.Ok(sensor);
        })
        .WithName("UpdateSensor");

        group.MapDelete("/{id:int}", async (
            int id,
            SmartXContext db,
            DeviceRegistry registry,
            TelemetryHistoryStore history,
            CancellationToken cancellationToken) =>
        {
            var sensor = await db.Sensors.FindAsync(
                new object[] { id },
                cancellationToken
            );

            if (sensor is null)
            {
                return Results.NotFound(new
                {
                    message = "Sensor not found."
                });
            }

            // Delete permanently from SQL Server.
            db.Sensors.Remove(sensor);

            await db.SaveChangesAsync(cancellationToken);


            // Remove from the live dictionary.
            registry.RemoveSensor(id);
            registry.RemoveSensor(id);
            history.RemoveSensor(id);

            return Results.NoContent();
        })
        .WithName("DeleteSensor");

        return app;
    }
}
