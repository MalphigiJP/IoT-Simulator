namespace SmartX.Api.Models;

public class TelemetryRecord
{
    public long Id { get; set; }

    public int SensorId { get; set; }

    public double? SoilMoisture { get; set; }

    public double? Temperature { get; set; }

    public int? Wattage { get; set; }

    public bool? ValveOpen { get; set; }

    public DateTime Timestamp { get; set; }

    public Sensor? Sensor { get; set; }
}