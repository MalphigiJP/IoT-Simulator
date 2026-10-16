
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
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddHostedService<DeviceRegistryInitializer>();
builder.Services.AddSingleton<TelemetryQueue>();
builder.Services.AddHostedService<TelemetryQueueWorker>();


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
    (TelemetryDto dto, TelemetryQueue queue) =>
    {

        if (dto.SensorId <= 0)
        {
            return Results.BadRequest(new
            {
                message = "A valid SensorId is required."
            });
        }

        bool hasMeasurement =
            dto.SoilMoisture.HasValue ||
            dto.Temperature.HasValue ||
            dto.Wattage.HasValue ||
            dto.ValveOpen.HasValue;

        if (!hasMeasurement)
        {
            return Results.BadRequest(new
            {
                message =
                    "At least one telemetry measurement is required."
            });
        }

        var packet = QueuedTelemetry.FromDto(dto);

        if (!queue.TryEnqueue(packet))
        {
            return Results.Problem(
                title: "Telemetry queue is full",
                detail:
                    "The gateway is temporarily unable " +
                    "to accept additional telemetry.",
                statusCode: 429
            );
        }

        return Results.Json(
            new
            {
                message =
                    "Telemetry accepted for FIFO processing.",

                packetId = packet.PacketId,

                sensorId = packet.SensorId,

                receivedAtUtc = packet.ReceivedAtUtc,

                status = "Queued"
            },
            statusCode: StatusCodes.Status202Accepted
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
    (
        List<TelemetryDto> payload,
        TelemetryQueue queue
    ) =>
    {

        if (payload is null || payload.Count == 0)
        {
            return Results.BadRequest(new
            {
                message = "Telemetry batch cannot be empty."
            });
        }

        // Prevent excessively large requests.
        if (payload.Count > 500)
        {
            return Results.BadRequest(new
            {
                message =
                    "Maximum batch size is 500 telemetry records."
            });
        }

        bool hasInvalidPackets = payload.Any(dto =>
            dto is null ||
            dto.SensorId <= 0 ||
            !(
                dto.SoilMoisture.HasValue ||
                dto.Temperature.HasValue ||
                dto.Wattage.HasValue ||
                dto.ValveOpen.HasValue
            )
        );

        if (hasInvalidPackets)
        {
            return Results.BadRequest(new
            {
                message =
                    "Every packet must have a valid SensorId " +
                    "and at least one measurement."
            });
        }

        var packets = payload
            .Select(QueuedTelemetry.FromDto)
            .ToList();

        // Enqueue the entire batch or none of it.
        if (!queue.TryEnqueueBatch(packets))
        {
            return Results.Problem(
                title: "Telemetry queue is full",
                detail:
                    "Insufficient queue capacity " +
                    "for the complete batch.",
                statusCode: 429
            );
        }

        return Results.Json(
            new
            {
                message =
                    "Telemetry batch accepted for FIFO processing.",

                acceptedRecords = packets.Count,

                status = "Queued"
            },
            statusCode: StatusCodes.Status202Accepted
        );
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


app.MapGet("/api/telemetry/queue/status",
    (TelemetryQueue queue) =>
    {
        return Results.Ok(queue.GetStatus());
    })
    .WithTags("Telemetry")
    .WithName("GetTelemetryQueueStatus");


// ==========================================
// PART 2 - LIVE DEVICE REGISTRY API
// ==========================================

// GET ALL LIVE DEVICE SNAPSHOTS
app.MapGet("/api/devices/live",
    (DeviceRegistry registry) =>
    {
        var now = DateTime.UtcNow;

        var devices = registry.GetAll()
            .Select(device => new
            {
                device.SensorId,
                device.MacAddress,
                device.Location,
                device.Category,

                device.Temperature,
                device.SoilMoisture,
                device.Wattage,
                device.ValveOpen,

                device.LastSampleTimestampUtc,
                device.LastReceivedAtUtc,

                device.PacketsProcessed,

                ConnectionStatus =
                    DeviceRegistry.GetConnectionStatus(
                        device,
                        now
                    )
            })
            .ToList();

        return Results.Ok(devices);
    })
    .WithTags("Live Devices")
    .WithName("GetLiveDevices");

// GET DEVICE BY MAC ADDRESS
app.MapGet("/api/devices/live/{macAddress}",
    (
        string macAddress,
        DeviceRegistry registry
    ) =>
    {
        if (!registry.TryGetByMac(
            macAddress,
            out var device))
        {
            return Results.NotFound(new
            {
                message =
                    "Device not found in live registry."
            });
        }

        return Results.Ok(new
        {
            device,
            connectionStatus =
                DeviceRegistry.GetConnectionStatus(
                    device!,
                    DateTime.UtcNow
                )
        });
    })
    .WithTags("Live Devices")
    .WithName("GetLiveDeviceByMac");


app.Run();
