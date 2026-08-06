using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.DTOs;
using SmartX.Api.Models;
using SmartX.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

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

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

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
        var sensor = new Sensor
        {
            MacAddress = dto.MacAddress,
            Location = dto.Location,
            Category = dto.Category
        };

        db.Sensors.Add(sensor);

        await db.SaveChangesAsync();

        return Results.Created($"/api/sensors/{sensor.Id}", sensor);
    });

app.MapGet("/api/sensors",
    async (SensorService service) =>
    {
        return Results.Ok(await service.GetAllAsync());
    });



app.Run();