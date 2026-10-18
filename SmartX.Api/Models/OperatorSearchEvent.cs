
namespace SmartX.Api.Models;

public sealed class OperatorSearchEvent
{
    public long Id { get; set; }

    public string Scope { get; set; } = "";

    public string SearchText { get; set; } = "";

    public int? SensorId { get; set; }

    public int? ResultCount { get; set; }

    public DateTime RecordedAtUtc { get; set; }
        = DateTime.UtcNow;
}
