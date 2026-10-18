
using SmartX.Api.DTOs;
using SmartX.Api.Services;
using Xunit;

namespace SmartX.Api.Tests;

public class OperatorSearchValidatorTests
{
    private static readonly DateTime TestTime =
        new(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidSearchCreatesAnEntry()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "low soil moisture",
            Scope = "telemetry",
            SensorId = 12,
            ResultCount = 4
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.True(result.IsValid);
        Assert.NotNull(result.Entry);
        Assert.Equal("telemetry", result.Entry.Scope);
        Assert.Equal(
            "low soil moisture",
            result.Entry.SearchText
        );
        Assert.Equal(12, result.Entry.SensorId);
        Assert.Equal(4, result.Entry.ResultCount);
        Assert.Equal(TestTime, result.Entry.RecordedAtUtc);
    }

    [Fact]
    public void SearchWhitespaceIsNormalized()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "  pump   controller  ",
            Scope = "sensors"
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.True(result.IsValid);
        Assert.Equal(
            "pump controller",
            result.Entry!.SearchText
        );
    }

    [Fact]
    public void ScopeIsNormalized()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "valve",
            Scope = " COMMANDS "
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.True(result.IsValid);
        Assert.Equal("commands", result.Entry!.Scope);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public void MissingOrShortQueriesAreRejected(
        string? query)
    {
        var request = new RecordOperatorSearchDto
        {
            Query = query,
            Scope = "sensors"
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.False(result.IsValid);
        Assert.Null(result.Entry);
    }

    [Fact]
    public void ExcessivelyLongQueryIsRejected()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = new string('x', 161),
            Scope = "telemetry"
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("settings")]
    [InlineData("")]
    public void InvalidScopesAreRejected(string scope)
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "pump",
            Scope = scope
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NegativeResultCountIsRejected()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "wattage",
            Scope = "telemetry",
            ResultCount = -1
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void InvalidSensorIdIsRejected()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "pump",
            Scope = "sensors",
            SensorId = 0
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UnscopedSensorSearchIsAllowed()
    {
        var request = new RecordOperatorSearchDto
        {
            Query = "greenhouse",
            Scope = "sensors"
        };

        var result = OperatorSearchValidator.Validate(
            request,
            TestTime
        );

        Assert.True(result.IsValid);
        Assert.Null(result.Entry!.SensorId);
        Assert.Null(result.Entry.ResultCount);
    }
}
