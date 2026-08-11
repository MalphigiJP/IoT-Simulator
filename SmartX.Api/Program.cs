using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;
using SmartX.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("React", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

builder.Services.AddDbContext<SmartXContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SmartXConnection")));

builder.Services.AddScoped<SensorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("React");

app.MapGet("/", () => "Smart-X API Running");

app.MapPost("/api/sensors",
    async (CreateSensorDto dto, SmartXContext db) =>
    {
        var validationError = SensorValidator.Validate(dto);

        if (validationError is not null)
        {
            return Results.BadRequest(new
            {
                message = validationError
            });
        }

        var existingSensor = await db.Sensors
            .FirstOrDefaultAsync(s =>
                s.MacAddress == dto.MacAddress);

        if (existingSensor is not null)
        {
            return Results.Conflict(new
            {
                message = "A sensor with this MAC address already exists."
            });
        }

        var sensor = new Sensor
        {
            MacAddress = dto.MacAddress,
            Location = dto.Location,
            Category = dto.Category
        };

        db.Sensors.Add(sensor);

        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/sensors/{sensor.Id}",
            sensor);
    });

app.MapGet("/api/sensors",
    async (SensorService service) =>
    {
        return Results.Ok(await service.GetAllAsync());
    });

app.MapGet("/api/sensors/{id:int}",
    async (int id, SmartXContext db) =>
    {
        var sensor = await db.Sensors.FindAsync(id);

        return sensor is null
            ? Results.NotFound()
            : Results.Ok(sensor);
    });

app.MapPut("/api/sensors/{id:int}",
    async (
        int id,
        UpdateSensorDto dto,
        SmartXContext db) =>
    {
        var validationError = SensorValidator.Validate(dto);

        if (validationError is not null)
        {
            return Results.BadRequest(new
            {
                message = validationError
            });
        }

        var sensor = await db.Sensors.FindAsync(id);

        if (sensor is null)
        {
            return Results.NotFound();
        }

        var existingSensor = await db.Sensors
            .FirstOrDefaultAsync(s =>
                s.MacAddress == dto.MacAddress &&
                s.Id != id);

        if (existingSensor is not null)
        {
            return Results.Conflict(new
            {
                message = "A sensor with this MAC address already exists."
            });
        }

        sensor.MacAddress = dto.MacAddress;
        sensor.Location = dto.Location;
        sensor.Category = dto.Category;

        await db.SaveChangesAsync();

        return Results.Ok(sensor);
    });

app.MapDelete("/api/sensors/{id:int}",
    async (
        int id,
        SmartXContext db) =>
    {
        var sensor = await db.Sensors.FindAsync(id);

        if (sensor is null)
        {
            return Results.NotFound();
        }

        db.Sensors.Remove(sensor);

        await db.SaveChangesAsync();

        return Results.NoContent();
    });

app.MapPost("/api/telemetry",
    async (
        TelemetryDto dto,
        SmartXContext db) =>
    {
        var sensor = await db.Sensors
            .FindAsync(dto.SensorId);

        if (sensor is null)
        {
            return Results.BadRequest(new
            {
                message = "Sensor does not exist."
            });
        }

        var telemetry = new TelemetryRecord
        {
            SensorId = dto.SensorId,
            SoilMoisture = dto.SoilMoisture,
            Temperature = dto.Temperature,
            Wattage = dto.Wattage,
            ValveOpen = dto.ValveOpen,
            Timestamp = dto.Timestamp ?? DateTime.UtcNow
        };

        db.TelemetryRecords.Add(telemetry);

        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/telemetry/{telemetry.Id}",
            new
            {
                telemetry.Id,
                telemetry.SensorId,
                telemetry.SoilMoisture,
                telemetry.Temperature,
                telemetry.Wattage,
                telemetry.ValveOpen,
                telemetry.Timestamp
            });

    });

app.MapGet("/api/telemetry",
    async (SmartXContext db) =>
    {
        var telemetry = await db.TelemetryRecords
            .OrderByDescending(t => t.Timestamp)
            .Take(100)
            .ToListAsync();

        return Results.Ok(telemetry);
    });

app.MapGet("/api/sensors/{sensorId:int}/telemetry",
    async (
        int sensorId,
        SmartXContext db) =>
    {
        var telemetry = await db.TelemetryRecords
            .Where(t => t.SensorId == sensorId)
            .OrderByDescending(t => t.Timestamp)
            .Take(100)
            .ToListAsync();

        return Results.Ok(telemetry);
    });

app.MapPost("/api/telemetry/batch",
    async (
        List<TelemetryDto> payload,
        SmartXContext db) =>
    {
        if (payload.Count == 0)
        {
            return Results.BadRequest(new
            {
                message = "Telemetry batch cannot be empty."
            });
        }

        var sensorIds = payload
            .Select(t => t.SensorId)
            .Distinct()
            .ToList();

        var existingSensorIds = await db.Sensors
            .Where(s => sensorIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existingIds = existingSensorIds.ToHashSet();

        var invalidSensorIds = sensorIds
            .Where(id => !existingIds.Contains(id))
            .ToList();

        if (invalidSensorIds.Count > 0)
        {
            return Results.BadRequest(new
            {
                message = "One or more sensors do not exist.",
                sensorIds = invalidSensorIds
            });
        }

        var records = payload.Select(dto =>
            new TelemetryRecord
            {
                SensorId = dto.SensorId,
                SoilMoisture = dto.SoilMoisture,
                Temperature = dto.Temperature,
                Wattage = dto.Wattage,
                ValveOpen = dto.ValveOpen,
                Timestamp = dto.Timestamp ?? DateTime.UtcNow
            })
            .ToList();

        await db.TelemetryRecords.AddRangeAsync(records);

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            inserted = records.Count
        });
    });

app.Run();