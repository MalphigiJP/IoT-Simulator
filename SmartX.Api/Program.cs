using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;

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

var app = builder.Build();

app.UseCors("React");

app.MapGet("/", () => "Smart-X API Running");

app.Run();