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

app.Run();