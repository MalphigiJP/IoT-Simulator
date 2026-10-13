
using Microsoft.EntityFrameworkCore;

using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;
using SmartX.Api.Endpoints;

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
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "SmartXConnection"
        ),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
            );
        }
    );
});

builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<TelemetrySeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("React");

app.MapGet("/", () =>
    Results.Ok("Smart-X API Running")
);

app.MapSensorEndpoints();


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
            }
        );
    })
    .WithTags("Telemetry");


app.MapGet("/api/telemetry",
    async (SmartXContext db) =>
    {
        var telemetry = await db.TelemetryRecords
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .Take(100)
            .ToListAsync();

        return Results.Ok(telemetry);
    })
    .WithTags("Telemetry");

app.MapGet("/api/sensors/{sensorId:int}/telemetry",
    async (
        int sensorId,
        SmartXContext db) =>
    {
        var telemetry = await db.TelemetryRecords
            .AsNoTracking()
            .Where(t => t.SensorId == sensorId)
            .OrderByDescending(t => t.Timestamp)
            .Take(100)
            .ToListAsync();

        return Results.Ok(telemetry);
    })
    .WithTags("Telemetry");

app.MapPost("/api/telemetry/batch",
    async (
        List<TelemetryDto> payload,
        SmartXContext db) =>
    {
        if (payload is null || payload.Count == 0)
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

        var existingIds =
            existingSensorIds.ToHashSet();

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

        await db.TelemetryRecords
            .AddRangeAsync(records);

        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            inserted = records.Count
        });
    })
    .WithTags("Telemetry");

app.MapPost("/api/telemetry/simulate",
    async (
        TelemetrySimulationDto request,
        TelemetrySeeder seeder) =>
    {
        if (request.RecordCount <= 0)
        {
            return Results.BadRequest(new
            {
                message =
                    "Record count must be greater than zero."
            });
        }

        if (request.BatchSize <= 0)
        {
            return Results.BadRequest(new
            {
                message =
                    "Batch size must be greater than zero."
            });
        }

        if (request.AnomalyRate < 0 ||
            request.AnomalyRate > 1)
        {
            return Results.BadRequest(new
            {
                message =
                    "Anomaly rate must be between 0 and 1."
            });
        }

        try
        {
            var result =
                await seeder.GenerateAsync(request);

            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new
            {
                message = ex.Message
            });
        }
    })
    .WithTags("Telemetry");

app.Run();
