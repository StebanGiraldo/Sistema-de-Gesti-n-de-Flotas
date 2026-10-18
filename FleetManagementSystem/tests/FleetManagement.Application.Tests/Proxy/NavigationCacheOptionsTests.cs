using System.ComponentModel.DataAnnotations;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Proxy;

/// <summary>
/// Pruebas de la configuración del caché de navegación: los valores por defecto y las reglas que la aplicación valida al
/// arrancar (<c>ValidateOnStart</c>), para que un valor imposible detenga el arranque con un mensaje claro.
/// </summary>
public class NavigationCacheOptionsTests
{
    [Fact]
    public void Defaults_AreTenMinutesAndFiveHundredEntries_AndAreValid()
    {
        var options = new NavigationCacheOptions();

        Assert.Equal("NavigationCache", NavigationCacheOptions.SectionName);
        Assert.Equal(10.0, options.TimeToLiveMinutes);
        Assert.Equal(500, options.MaxEntries);
        Assert.Equal(TimeSpan.FromMinutes(10), options.TimeToLive);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(10_081.0)] // más de una semana
    public void TimeToLiveMinutes_MustBeAboveZeroAndNotMoreThanAWeek(double minutes)
        => AssertOnlyInvalid(new NavigationCacheOptions { TimeToLiveMinutes = minutes }, nameof(NavigationCacheOptions.TimeToLiveMinutes));

    [Theory]
    [InlineData(0.01)]
    [InlineData(10_080.0)] // exactamente una semana
    public void TimeToLiveMinutes_AcceptsAnyValueUpToAWeek(double minutes)
        => Assert.Empty(Validate(new NavigationCacheOptions { TimeToLiveMinutes = minutes }));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxEntries_MustBeAtLeastOne(int maxEntries)
        => AssertOnlyInvalid(new NavigationCacheOptions { MaxEntries = maxEntries }, nameof(NavigationCacheOptions.MaxEntries));

    [Fact]
    public void MaxEntries_OfOne_IsValid()
        => Assert.Empty(Validate(new NavigationCacheOptions { MaxEntries = 1 }));

    private static void AssertOnlyInvalid(NavigationCacheOptions options, string propertyName)
    {
        var failure = Assert.Single(Validate(options));
        Assert.Contains(propertyName, failure.MemberNames);
        Assert.Contains(NavigationCacheOptions.SectionName + ":" + propertyName, failure.ErrorMessage);
    }

    private static List<ValidationResult> Validate(NavigationCacheOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }
}
