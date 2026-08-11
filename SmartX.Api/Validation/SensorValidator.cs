using System.Text.RegularExpressions;
using SmartX.Api.DTOs;

namespace SmartX.Api.Validation;

public static class SensorValidator
{
    private static readonly string[] ValidCategories =
    {
        "Environmental",
        "Power Consumption",
        "Actuator"
    };

    public static string? Validate(CreateSensorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MacAddress))
        {
            return "MAC address is required.";
        }

        if (!IsValidMacAddress(dto.MacAddress))
        {
            return "Invalid MAC address.";
        }

        if (string.IsNullOrWhiteSpace(dto.Location))
        {
            return "Location is required.";
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
        {
            return "Sensor category is required.";
        }

        if (!ValidCategories.Contains(dto.Category))
        {
            return "Invalid sensor category.";
        }

        return null;
    }

    public static string? Validate(UpdateSensorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MacAddress))
        {
            return "MAC address is required.";
        }

        if (!IsValidMacAddress(dto.MacAddress))
        {
            return "Invalid MAC address.";
        }

        if (string.IsNullOrWhiteSpace(dto.Location))
        {
            return "Location is required.";
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
        {
            return "Sensor category is required.";
        }

        if (!ValidCategories.Contains(dto.Category))
        {
            return "Invalid sensor category.";
        }

        return null;
    }

    public static bool IsValidMacAddress(string macAddress)
    {
        return Regex.IsMatch(
            macAddress,
            "^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$");
    }
}