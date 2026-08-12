namespace SmartX.Api.DTOs;

public class TelemetrySimulationResult
{
    public int RequestedRecords { get; set; }

    public int InsertedRecords { get; set; }

    public int AnomaliesGenerated { get; set; }

    public int SensorsUsed { get; set; }

    public int BatchesProcessed { get; set; }

    public long DurationMilliseconds { get; set; }
}