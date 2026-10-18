
using SmartX.Api.DTOs;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public sealed record SearchValidationResult(
    bool IsValid,
    string? Error,
    OperatorSearchEvent? Entry
);

public static class OperatorSearchValidator
{
    private static readonly HashSet<string> AllowedScopes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "sensors",
            "telemetry",
            "commands",
            "alerts",
            "topology"
        };

    public static SearchValidationResult Validate(
        RecordOperatorSearchDto request,
        DateTime nowUtc)
    {
        if (request is null)
        {
            return new(
                false,
                "Search request is required.",
                null
            );
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new(
                false,
                "Search query is required.",
                null
            );
        }

        string query = string.Join(
            " ",
            request.Query.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
        );

        if (query.Length < 2 || query.Length > 160)
        {
            return new(
                false,
                "Search query must contain 2 to 160 characters.",
                null
            );
        }

        string scope = (
            request.Scope ?? ""
        ).Trim().ToLowerInvariant();

        if (!AllowedScopes.Contains(scope))
        {
            return new(
                false,
                "Scope must be sensors, telemetry, commands, alerts or topology.",
                null
            );
        }

        if (request.SensorId is <= 0)
        {
            return new(
                false,
                "SensorId must be a positive integer.",
                null
            );
        }

        if (request.ResultCount is < 0)
        {
            return new(
                false,
                "ResultCount cannot be negative.",
                null
            );
        }

        return new(
            true,
            null,
            new OperatorSearchEvent
            {
                Scope = scope,
                SearchText = query,
                SensorId = request.SensorId,
                ResultCount = request.ResultCount,
                RecordedAtUtc = nowUtc
            }
        );
    }
}
