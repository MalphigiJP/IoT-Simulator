using System.ComponentModel.DataAnnotations;

namespace SmartX.Api.Models;

public class Sensor
{
    public int Id { get; set; }

    [Required]
    [MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    [Required]
    public string Location { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}