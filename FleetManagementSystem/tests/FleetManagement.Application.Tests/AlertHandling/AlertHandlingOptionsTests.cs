using System.ComponentModel.DataAnnotations;
using FleetManagement.Application.AlertHandling;
using Xunit;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>Las reglas del manejo de alertas viven en la configuración y se validan al arrancar: un valor imposible detiene el arranque con un mensaje claro.</summary>
public class AlertHandlingOptionsTests
{
    [Fact]
    public void TheDefaults_AreTheDocumentedOnes_AndAreValid()
    {
        var options = new AlertHandlingOptions();

        Assert.Equal("AlertHandling", AlertHandlingOptions.SectionName);
        Assert.Equal(500, options.MaxDescriptionLength);
        Assert.Equal(1440, options.MaxDelayMinutes);
        Assert.Equal(60, options.SeriousDelayMinutes);
        Assert.Equal("supervisor@fleetcontrol.demo", options.SupervisorRecipient);
        Assert.Equal("administrador@fleetcontrol.demo", options.AdministratorRecipient);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void ADescriptionLimitOutsideItsRange_IsInvalid(int limit)
        => AssertInvalid(new AlertHandlingOptions { MaxDescriptionLength = limit }, nameof(AlertHandlingOptions.MaxDescriptionLength));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ADelayLimitThatIsNotPositive_IsInvalid(int limit)
        => AssertInvalid(new AlertHandlingOptions { MaxDelayMinutes = limit }, nameof(AlertHandlingOptions.MaxDelayMinutes));

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void ASeriousThresholdThatIsNotPositive_IsInvalid(int threshold)
        => AssertInvalid(new AlertHandlingOptions { SeriousDelayMinutes = threshold }, nameof(AlertHandlingOptions.SeriousDelayMinutes));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankRecipient_IsInvalid(string recipient)
    {
        AssertInvalid(new AlertHandlingOptions { SupervisorRecipient = recipient }, nameof(AlertHandlingOptions.SupervisorRecipient));
        AssertInvalid(new AlertHandlingOptions { AdministratorRecipient = recipient }, nameof(AlertHandlingOptions.AdministratorRecipient));
    }

    [Fact]
    public void AMissingRecipient_IsInvalid()
    {
        AssertInvalid(new AlertHandlingOptions { SupervisorRecipient = null! }, nameof(AlertHandlingOptions.SupervisorRecipient));
        AssertInvalid(new AlertHandlingOptions { AdministratorRecipient = null! }, nameof(AlertHandlingOptions.AdministratorRecipient));
    }

    [Fact]
    public void TheLimitsOfTheRanges_AreValid()
    {
        Assert.Empty(Validate(new AlertHandlingOptions { MaxDescriptionLength = 1, MaxDelayMinutes = 1, SeriousDelayMinutes = 1 }));
        Assert.Empty(Validate(new AlertHandlingOptions { MaxDescriptionLength = 10000, MaxDelayMinutes = 100000, SeriousDelayMinutes = 100000 }));
    }

    private static void AssertInvalid(AlertHandlingOptions options, string member)
    {
        var failure = Assert.Single(Validate(options));
        Assert.Contains(member, failure.MemberNames);
    }

    private static List<ValidationResult> Validate(AlertHandlingOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
