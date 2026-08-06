namespace SmartX.Api.DTOs;

public class CreateSensorDto
{
    public string MacAddress { get; set; } = string.Empty;

    public string Location {  get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
}