
using SmartX.Api.DTOs;

namespace SmartX.Api.Models;

public sealed record QueuedTelemetry
{
    public Guid PacketId { get; init; }

    public int SensorId { get; init; }

    public double? SoilMoisture { get; init; }

    public double? Temperature { get; init; }

    public int? Wattage { get; init; }

    public bool? ValveOpen { get; init; }

    public DateTime Timestamp { get; init; }

    public DateTime ReceivedAtUtc { get; init; }

    public static QueuedTelemetry FromDto(
        TelemetryDto dto)
    {
        var now = DateTime.UtcNow;

        return new QueuedTelemetry
        {
            PacketId = Guid.NewGuid(),

            SensorId = dto.SensorId,

            SoilMoisture = dto.SoilMoisture,
            Temperature = dto.Temperature,
            Wattage = dto.Wattage,
            ValveOpen = dto.ValveOpen,

            Timestamp = dto.Timestamp ?? now,
            ReceivedAtUtc = now
        };
    }
}
