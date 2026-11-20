using FleetManagement.Application.RouteSheets;
using Xunit;

namespace FleetManagement.Application.Tests.RouteSheets;

/// <summary>El proveedor recibe todas las plantillas registradas y entrega la de la audiencia pedida, sin <c>switch</c> sobre variantes.</summary>
public class RouteSheetProviderTests
{
    private readonly RouteSheetWorld _world = new();

    [Theory]
    [InlineData("Driver", RouteSheetAudience.Driver)]
    [InlineData("Dispatcher", RouteSheetAudience.Dispatcher)]
    [InlineData("driver", RouteSheetAudience.Driver)]
    [InlineData("DISPATCHER", RouteSheetAudience.Dispatcher)]
    [InlineData("  Driver  ", RouteSheetAudience.Driver)]
    public void ItReturnsTheTemplateOfTheRequestedAudience_WithoutCaseSensitivity(string audience, RouteSheetAudience expected)
    {
        var template = _world.Sheets.For(audience);

        Assert.Equal(expected, template.Audience);
    }

    [Fact]
    public void ItReturnsTheRegisteredInstance_NotANewOne()
    {
        var registered = _world.AllTemplates();

        var provider = new RouteSheetProvider(registered);

        Assert.Same(registered.Single(template => template.Audience == RouteSheetAudience.Driver), provider.For("Driver"));
        Assert.Same(registered.Single(template => template.Audience == RouteSheetAudience.Dispatcher), provider.For("Dispatcher"));
    }

    [Fact]
    public void AnUnknownAudience_IsRejectedListingTheValidOnes_InAlphabeticalOrder()
    {
        var error = Assert.Throws<ArgumentException>(() => _world.Sheets.For("Pilot"));

        Assert.Equal("Audiencia desconocida: 'Pilot'. Valores válidos: Dispatcher, Driver.", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingAudience_IsRejectedAsMissing_ListingTheValidOnes(string? audience)
    {
        var error = Assert.Throws<ArgumentException>(() => _world.Sheets.For(audience));

        Assert.Equal("Falta la audiencia. Valores válidos: Dispatcher, Driver.", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public void TheNumbersOfTheEnum_AreNotAudiences(string audience)
    {
        // Un parseo directo del enum aceptaría «0» y «1»: el proveedor solo conoce los nombres de las plantillas registradas.
        Assert.Throws<ArgumentException>(() => _world.Sheets.For(audience));
    }

    [Fact]
    public void TheValidValuesListed_AreThoseOfTheTemplatesReallyRegistered()
    {
        var onlyDriver = new RouteSheetProvider(new RouteSheetTemplate[] { new DriverRouteSheet(_world.Collaborators) });

        var error = Assert.Throws<ArgumentException>(() => onlyDriver.For("Dispatcher"));

        Assert.Equal("Audiencia desconocida: 'Dispatcher'. Valores válidos: Driver.", error.Message);
        Assert.Equal(RouteSheetAudience.Driver, onlyDriver.For("Driver").Audience);
    }

    [Fact]
    public void ItDeliversWhateverTemplateIsRegisteredForAnAudience_SoANewVariantNeverChangesTheProvider()
    {
        var spy = new SpyTemplate(new CallLog(), _world.Collaborators);   // otra clase, con la audiencia Driver

        var provider = new RouteSheetProvider(new RouteSheetTemplate[] { spy, new DispatcherRouteSheet(_world.Collaborators) });

        Assert.Same(spy, provider.For("Driver"));
        Assert.IsType<DispatcherRouteSheet>(provider.For("Dispatcher"));
    }

    [Fact]
    public void TwoTemplatesForTheSameAudience_AreRejectedWhenTheProviderIsBuilt_NotSilentlyIgnored()
    {
        var templates = new RouteSheetTemplate[] { new DriverRouteSheet(_world.Collaborators), new SpyTemplate(new CallLog(), _world.Collaborators) };

        Assert.Throws<ArgumentException>(() => new RouteSheetProvider(templates));
    }

    [Fact]
    public async Task TheTemplateItReturns_BuildsTheSheetOfThatAudience()
    {
        var (_, route) = await _world.AddEquatorTripAsync();

        var driver = await _world.Sheets.For("driver").BuildAsync(route.Id);
        var dispatcher = await _world.Sheets.For("dispatcher").BuildAsync(route.Id);

        Assert.Equal("Driver", driver.Audience);
        Assert.Equal("Dispatcher", dispatcher.Audience);
    }
}
