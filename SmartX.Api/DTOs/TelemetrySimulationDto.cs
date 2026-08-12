namespace SmartX.Api.DTOs;

public class TelemetrySimulationDto
{
    public int RecordCount { get; set; } = 1000;

    public int BatchSize { get; set; } = 500;

    public double AnomalyRate { get; set; } = 0.02;
}