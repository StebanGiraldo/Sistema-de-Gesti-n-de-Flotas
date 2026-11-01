using FleetManagement.Application.AlertHandling;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>Cuándo una alerta es grave: un accidente es crítico, una avería es seria y lo demás se vuelve serio si el retraso llega al umbral.</summary>
public class AlertSeverityPolicyTests
{
    private readonly AlertHandlingOptions _options = new();

    private AlertSeverity Classify(AlertType type, int delayMinutes) => new AlertSeverityPolicy(_options).Classify(type, delayMinutes);

    [Theory]
    [InlineData(AlertType.Accident, 0)]
    [InlineData(AlertType.Accident, 15)]
    [InlineData(AlertType.Accident, 600)]
    public void AnAccident_IsAlwaysCritical_WhateverTheDelay(AlertType type, int delay)
        => Assert.Equal(AlertSeverity.Critical, Classify(type, delay));

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    [InlineData(600)]
    public void ABreakdown_IsSerious_AndNeverCritical_WhateverTheDelay(int delay)
        => Assert.Equal(AlertSeverity.Serious, Classify(AlertType.Breakdown, delay));

    [Theory]
    [InlineData(AlertType.Delay)]
    [InlineData(AlertType.TrafficJam)]
    [InlineData(AlertType.WeatherCondition)]
    [InlineData(AlertType.Other)]
    public void AnythingElse_IsMinorBelowTheThreshold_AndSeriousFromIt(AlertType type)
    {
        Assert.Equal(AlertSeverity.Minor, Classify(type, 0));
        Assert.Equal(AlertSeverity.Minor, Classify(type, 59));
        Assert.Equal(AlertSeverity.Serious, Classify(type, 60));
        Assert.Equal(AlertSeverity.Serious, Classify(type, 600));
    }

    [Fact]
    public void ALongDelay_NeverMakesAnAlertCritical()
        => Assert.Equal(AlertSeverity.Serious, Classify(AlertType.Delay, 100000));

    [Fact]
    public void TheThreshold_ComesFromTheOptions()
    {
        _options.SeriousDelayMinutes = 30;

        Assert.Equal(AlertSeverity.Minor, Classify(AlertType.Delay, 29));
        Assert.Equal(AlertSeverity.Serious, Classify(AlertType.Delay, 30));
    }

    [Fact]
    public void TheDefaultThreshold_IsOneHour()
        => Assert.Equal(60, new AlertHandlingOptions().SeriousDelayMinutes);
}
