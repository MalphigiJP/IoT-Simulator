
namespace SmartX.Api.DTOs;

public class SensorHealthDto
{
    public int SensorId { get; set; }

    public string MacAddress { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? LastTelemetry { get; set; }

    public double? SecondsSinceLastTelemetry { get; set; }
}
