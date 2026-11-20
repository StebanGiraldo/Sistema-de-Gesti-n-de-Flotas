using System.Reflection;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.RouteSheets;
using FleetManagement.Domain.Entities;
using Xunit;

namespace FleetManagement.Application.Tests.RouteSheets;

/// <summary>
/// El esqueleto del Template Method, probado con una variante espía y colaboradores de doble: el ORDEN de los pasos es siempre el
/// mismo, no se puede alterar desde una subclase y los pasos variables nunca corren si la parte fija rechaza la ruta.
/// </summary>
public class RouteSheetTemplateTests
{
    private static readonly string[] FixedOrder = { "load", "trips", "header", "stops", "cargo", "progress", "navigation" };

    private readonly CallLog _log = new();
    private readonly Vehicle _vehicle = SheetData.Vehicle();

    private (SpyTemplate Spy, DeliveryRoute Route, FakeTripProgress Trips) Rig()
    {
        var route = SheetData.Route(_vehicle);
        var trips = new FakeTripProgress(_log);
        var collaborators = new RouteSheetCollaborators(new FakeSourceLoader(_log, SheetData.Source(_vehicle, route)), new FakeNavigation(_log), trips);
        return (new SpyTemplate(_log, collaborators), route, trips);
    }

    [Fact]
    public async Task TheSteps_RunInTheFixedOrder()
    {
        var (spy, route, _) = Rig();

        await spy.BuildAsync(route.Id);

        Assert.Equal(FixedOrder, _log.Entries);
    }

    [Fact]
    public async Task TheOrder_IsTheSameInEveryCall()
    {
        var (spy, route, _) = Rig();

        await spy.BuildAsync(route.Id);
        await spy.BuildAsync(route.Id);

        Assert.Equal(FixedOrder.Concat(FixedOrder), _log.Entries);
    }

    [Fact]
    public void BuildAsync_IsNotVirtual_SoNoSubclassCanChangeTheOrder()
    {
        var build = typeof(RouteSheetTemplate).GetMethod(nameof(RouteSheetTemplate.BuildAsync))!;

        Assert.False(build.IsVirtual);
        Assert.False(build.IsAbstract);
        Assert.True(build.IsPublic);
    }

    [Fact]
    public void BuildAsync_IsTheOnlyPublicOperationOfTheTemplate()
    {
        var operations = typeof(RouteSheetTemplate)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name);

        Assert.Equal(new[] { nameof(RouteSheetTemplate.BuildAsync) }, operations);
    }

    [Fact]
    public async Task TheSheet_IsAssembledWithWhatEveryStepReturned()
    {
        var (spy, route, _) = Rig();

        var sheet = await spy.BuildAsync(route.Id);

        Assert.Equal("Driver", sheet.Audience);
        Assert.Equal("spy", sheet.Header.Title);
        Assert.Equal(route.Name, sheet.Header.RouteName);
        Assert.Equal(_vehicle.LicensePlate, sheet.Header.LicensePlate);
        Assert.Equal(new TripStopDto("S", 1, 2), Assert.Single(sheet.Stops));
        Assert.Equal(new RouteSheetCargo(1, 2, 3, 4, 5, null, false), sheet.Cargo);
        Assert.Equal(50, sheet.Progress!.Percent);
        Assert.Equal(SpyTemplate.NavigationUrl, sheet.NavigationUrl);
    }

    // ---------- La parte fija rechaza antes de que corra cualquier paso variable ----------

    [Fact]
    public async Task ARouteThatDoesNotExist_IsRejectedInTheFirstStep_AndNoVariableStepRuns()
    {
        var collaborators = SheetData.Doubles(_log, failure: new KeyNotFoundException("Ruta no encontrada."));
        var spy = new SpyTemplate(_log, collaborators);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => spy.BuildAsync(Guid.NewGuid()));

        Assert.Equal(new[] { "load" }, _log.Entries);
        Assert.Null(spy.Seen);
    }

    [Fact]
    public async Task ARouteWithoutVehicle_ThroughTheRealLoader_IsRejectedBeforeAnythingElse()
    {
        var world = new RouteSheetWorld();
        var route = SheetData.Route(vehicle: null);
        await world.Fleet.RouteRepository.AddAsync(route);
        var spy = new SpyTemplate(_log, new RouteSheetCollaborators(world.Collaborators.Source, new FakeNavigation(_log), new FakeTripProgress(_log)));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => spy.BuildAsync(route.Id));

        Assert.Contains(route.Name, error.Message);
        Assert.Empty(_log.Entries);   // ni siquiera se consultó el avance de los viajes
    }

    [Fact]
    public async Task ARouteThatDoesNotExist_ThroughTheRealLoader_IsRejectedBeforeAnythingElse()
    {
        var world = new RouteSheetWorld();
        var spy = new SpyTemplate(_log, new RouteSheetCollaborators(world.Collaborators.Source, new FakeNavigation(_log), new FakeTripProgress(_log)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => spy.BuildAsync(Guid.NewGuid()));

        Assert.Empty(_log.Entries);
    }

    // ---------- Lo que el esqueleto le entrega a los pasos ----------

    [Fact]
    public async Task TheSteps_ReceiveTheTripOfThisRoute_NotTheTripOfAnotherOne()
    {
        var (spy, route, trips) = Rig();
        trips.Trips.Add(SheetData.Trip(Guid.NewGuid(), percent: 90));
        trips.Trips.Add(SheetData.Trip(route.Id, percent: 35));

        await spy.BuildAsync(route.Id);

        Assert.Equal(35, spy.Seen!.Trip!.Percent);
    }

    [Fact]
    public async Task WithoutAnActiveTrip_TheStepsReceiveNoTrip()
    {
        var (spy, route, trips) = Rig();
        trips.Trips.Add(SheetData.Trip(Guid.NewGuid()));

        await spy.BuildAsync(route.Id);

        Assert.Null(spy.Seen!.Trip);
    }

    // ---------- Los hooks: «no aportar nada» es un valor por defecto válido ----------

    [Fact]
    public async Task AVariantThatOnlyDecidesTheMandatorySteps_GetsEmptyOptionalParts_AndNeverAsksForNavigation()
    {
        var navigation = new FakeNavigation(_log);
        var route = SheetData.Route(_vehicle);
        var collaborators = new RouteSheetCollaborators(
            new FakeSourceLoader(_log, SheetData.Source(_vehicle, route)), navigation, new FakeTripProgress(_log));

        var sheet = await new MinimalTemplate(collaborators).BuildAsync(route.Id);

        Assert.Equal("Dispatcher", sheet.Audience);
        Assert.Empty(sheet.Stops);
        Assert.Null(sheet.Cargo);
        Assert.Null(sheet.Progress);
        Assert.Null(sheet.NavigationUrl);
        Assert.Equal(0, navigation.Calls);
    }
}
