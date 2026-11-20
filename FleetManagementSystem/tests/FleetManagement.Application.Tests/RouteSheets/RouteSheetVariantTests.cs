using System.Reflection;
using FleetManagement.Application.RouteSheets;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.RouteSheets;

/// <summary>
/// El contrato que TODA variante debe cumplir. Las variantes se descubren por reflexión en el ensamblado de la aplicación, así
/// que una variante nueva queda cubierta sin tocar esta clase (y, si rompe el contrato, estas pruebas lo dicen).
/// </summary>
public class RouteSheetVariantContractTests
{
    public static IEnumerable<object[]> Variants()
        => VariantTypes().Select(type => new object[] { type.Name });

    private static IEnumerable<Type> VariantTypes()
        => typeof(RouteSheetTemplate).Assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(RouteSheetTemplate)));

    private static Type VariantNamed(string name) => VariantTypes().Single(type => type.Name == name);

    private static RouteSheetTemplate Create(string name, RouteSheetCollaborators collaborators)
        => (RouteSheetTemplate)Activator.CreateInstance(VariantNamed(name), collaborators)!;

    [Fact]
    public void ThereIsExactlyOneVariantForEachAudience()
    {
        var world = new RouteSheetWorld();

        var audiences = world.AllTemplates().Select(template => template.Audience).Order().ToList();

        Assert.Equal(Enum.GetValues<RouteSheetAudience>().Order(), audiences);
        Assert.Equal(audiences.Count, VariantTypes().Count());
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryVariant_IsBuiltOnlyFromTheCollaborators(string variant)
    {
        var constructor = Assert.Single(VariantNamed(variant).GetConstructors());

        var parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(RouteSheetCollaborators), parameter.ParameterType);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task EveryVariant_BuildsASheetForATripInProgress_WithItsOwnAudienceAndTheCommonHeader(string variant)
    {
        var world = new RouteSheetWorld();
        var (vehicle, route) = await world.AddEquatorTripAsync();
        var template = Create(variant, world.Collaborators);

        var sheet = await template.BuildAsync(route.Id);

        Assert.Equal(template.Audience.ToString(), sheet.Audience);
        Assert.False(string.IsNullOrWhiteSpace(sheet.Header.Title));
        Assert.Equal(route.Name, sheet.Header.RouteName);
        Assert.Equal(route.Status.ToString(), sheet.Header.Status);
        Assert.Equal(vehicle.LicensePlate, sheet.Header.LicensePlate);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task EveryVariant_AlsoWorksWithoutDriverWithoutCargoAndWithoutAnActiveTrip(string variant)
    {
        var world = new RouteSheetWorld();
        var vehicle = SheetData.Vehicle(status: VehicleStatus.Available);
        await world.Fleet.VehicleRepository.AddAsync(vehicle);
        var route = SheetData.Route(vehicle);
        route.Status = DeliveryRouteStatus.Planned;
        await world.Fleet.RouteRepository.AddAsync(route);

        var sheet = await Create(variant, world.Collaborators).BuildAsync(route.Id);

        Assert.Null(sheet.Header.DriverName);
        Assert.NotNull(sheet.Stops);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task EveryVariant_ReportsAMissingRouteTheSameWay(string variant)
    {
        var world = new RouteSheetWorld();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Create(variant, world.Collaborators).BuildAsync(Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryVariant_OverridesOnlyProtectedMembers_AndLeavesTheSkeletonAlone(string variant)
    {
        var type = VariantNamed(variant);

        var overrides = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetBaseDefinition().DeclaringType != type)
            .ToList();

        Assert.NotEmpty(overrides);
        Assert.All(overrides, method => Assert.True(method.IsFamily, $"{type.Name}.{method.Name} no es protected"));
        Assert.DoesNotContain(overrides, method => method.Name == nameof(RouteSheetTemplate.BuildAsync));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryVariant_DecidesTheTwoMandatorySteps_ThatTheBaseLeavesAbstract(string variant)
    {
        var declared = VariantNamed(variant)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet();

        Assert.Contains("BuildHeader", declared);
        Assert.Contains("BuildProgress", declared);
    }
}

/// <summary>Qué cuenta cada audiencia, con los servicios reales (navegación con proveedor de conteo, avance de viajes real).</summary>
public class RouteSheetContentTests
{
    // ---------- Conductor ----------

    [Fact]
    public async Task TheDriverSheet_ListsTheStopsInOrder_FromOriginToDestination()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();

        var sheet = await world.Sheets.For("Driver").BuildAsync(route.Id);

        Assert.Equal(new[] { "Origen", "Primera", "Segunda", "Destino" }, sheet.Stops.Select(stop => stop.Label));
        Assert.Equal(new[] { 0.0, 0.10, 0.30, 0.60 }, sheet.Stops.Select(stop => stop.Longitude).ToArray(), new PrecisionComparer());
    }

    [Fact]
    public async Task TheDriverSheet_CarriesTheNavigationLinkTheNavigationServiceGenerates()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();

        var sheet = await world.Sheets.For("Driver").BuildAsync(route.Id);

        Assert.StartsWith("https://proveedor.test/ruta", sheet.NavigationUrl);
        Assert.Contains("paradas=2", sheet.NavigationUrl);   // las dos paradas intermedias de la ruta
        Assert.Equal(1, world.NavigationProvider.Calls);     // la hoja pide el enlace al servicio una sola vez
    }

    [Fact]
    public async Task TheDriverSheet_ShowsTheNextStopAndTheEta_ExactlyAsTheTripProgressServiceReportsThem()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();
        var trip = Assert.Single(await world.Progress.GetActiveTripsAsync());

        var sheet = await world.Sheets.For("Driver").BuildAsync(route.Id);

        Assert.Equal(trip.NextStopLabel, sheet.Progress!.NextStop);
        Assert.Equal(trip.EtaMinutes, sheet.Progress.EtaMinutes);
        Assert.Equal(trip.RemainingKm, sheet.Progress.RemainingKm);
        Assert.Null(sheet.Progress.Percent);
        Assert.Null(sheet.Progress.DelayMinutes);
    }

    [Fact]
    public async Task TheDriverSheet_HasNoCargoSummary_NorDriverNameInTheHeader()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync(driver: SheetData.Driver());

        var sheet = await world.Sheets.For("Driver").BuildAsync(route.Id);

        Assert.Null(sheet.Cargo);
        Assert.Null(sheet.Header.DriverName);
        Assert.Equal("Hoja de ruta del conductor", sheet.Header.Title);
    }

    [Fact]
    public async Task TheDriverSheet_OfATripThatIsNotInProgress_StillListsStopsAndLink_ButHasNoProgress()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync(vehicleStatus: VehicleStatus.Available, routeStatus: DeliveryRouteStatus.Planned);

        var sheet = await world.Sheets.For("Driver").BuildAsync(route.Id);

        Assert.Equal(4, sheet.Stops.Count);
        Assert.NotNull(sheet.NavigationUrl);
        Assert.Null(sheet.Progress);
    }

    [Fact]
    public async Task AVehicleInMaintenance_HasNoActiveTrip_SoItsSheetHasNoProgress()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync(vehicleStatus: VehicleStatus.Maintenance);

        var driver = await world.Sheets.For("Driver").BuildAsync(route.Id);
        var dispatcher = await world.Sheets.For("Dispatcher").BuildAsync(route.Id);

        Assert.Null(driver.Progress);
        Assert.Null(dispatcher.Progress!.Percent);
        Assert.Null(dispatcher.Progress.RemainingKm);
    }

    // ---------- Despachador ----------

    [Fact]
    public async Task TheDispatcherSheet_ComparesTheCargoWithTheCapacityOfTheVehicle()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();

        var sheet = await world.Sheets.For("Dispatcher").BuildAsync(route.Id);

        var cargo = sheet.Cargo!;
        Assert.Equal(2, cargo.Items);
        Assert.Equal(365, cargo.TotalWeightKg);
        Assert.Equal(1500, cargo.CapacityKg);
        Assert.Equal(24.3, cargo.WeightUsagePercent, 6);
        Assert.Equal(2.2, cargo.TotalVolumeM3, 6);
        Assert.Equal(10, cargo.CapacityM3);
        Assert.False(cargo.ExceedsCapacity);
    }

    [Fact]
    public async Task TheDispatcherSheet_ShowsTheDriverNameAndTheRouteDelay_ButNoStopsNorLink()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync(routeStatus: DeliveryRouteStatus.Delayed, driver: SheetData.Driver("Ana Pérez"), delayMinutes: 20);

        var sheet = await world.Sheets.For("Dispatcher").BuildAsync(route.Id);

        Assert.Equal("Ana Pérez", sheet.Header.DriverName);
        Assert.Equal(20, sheet.Progress!.DelayMinutes);
        Assert.Empty(sheet.Stops);
        Assert.Null(sheet.NavigationUrl);
        Assert.Equal(0, world.NavigationProvider.Calls);   // el despachador no pide el enlace de navegación
    }

    [Fact]
    public async Task TheDispatcherSheet_ShowsThePercentAndKilometersLeft_ExactlyAsTheTripProgressServiceReportsThem()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();
        var trip = Assert.Single(await world.Progress.GetActiveTripsAsync());

        var sheet = await world.Sheets.For("Dispatcher").BuildAsync(route.Id);

        Assert.Equal(trip.Percent, sheet.Progress!.Percent);
        Assert.Equal(trip.RemainingKm, sheet.Progress.RemainingKm);
        Assert.Null(sheet.Progress.NextStop);
        Assert.Null(sheet.Progress.EtaMinutes);
        Assert.Equal("Hoja de despacho", sheet.Header.Title);
    }

    [Fact]
    public async Task TheDispatcherSheet_WithoutAnAssignedDriver_HasNoDriverName()
    {
        var world = new RouteSheetWorld();
        var (_, route) = await world.AddEquatorTripAsync();

        var sheet = await world.Sheets.For("Dispatcher").BuildAsync(route.Id);

        Assert.Null(sheet.Header.DriverName);
    }

    // ---------- Solo lectura ----------

    [Fact]
    public async Task BuildingASheet_ChangesNothing_AndWritesNoAuditNorNotifications()
    {
        var world = new RouteSheetWorld();
        var (vehicle, route) = await world.AddEquatorTripAsync(driver: SheetData.Driver());
        var statusBefore = (vehicle.Status, route.Status, route.DelayMinutes, vehicle.MileageKm);

        foreach (var template in world.AllTemplates())
            await template.BuildAsync(route.Id);

        Assert.Equal(statusBefore, (vehicle.Status, route.Status, route.DelayMinutes, vehicle.MileageKm));
        Assert.Empty(world.Fleet.Audit.Entries);
        Assert.Empty(world.Fleet.Channel.SentMessages);
    }

    private sealed class PrecisionComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        public int GetHashCode(double value) => 0;
    }
}

/// <summary>
/// La comparación de la carga con la capacidad, probada a través de la hoja del despachador (la clase que la calcula es interna)
/// con colaboradores de doble: cada caso límite con un vehículo y una carga escritos a mano.
/// </summary>
public class RouteSheetCargoSummaryTests
{
    private static async Task<RouteSheetCargo> CargoOf(Vehicle vehicle, params CargoItem[] items)
    {
        var route = SheetData.Route(vehicle, cargo: items);
        var collaborators = SheetData.Doubles(new CallLog(), SheetData.Source(vehicle, route));

        return (await new DispatcherRouteSheet(collaborators).BuildAsync(route.Id)).Cargo!;
    }

    [Fact]
    public async Task ACargoExactlyTheSizeOfTheVehicle_IsNotExceeded_AndUsesAHundredPercent()
    {
        var cargo = await CargoOf(SheetData.Vehicle(capacityKg: 1500), SheetData.Cargo(1000), SheetData.Cargo(500));

        Assert.False(cargo.ExceedsCapacity);
        Assert.Equal(100, cargo.WeightUsagePercent);
    }

    [Fact]
    public async Task ARoundingErrorOfTheSum_DoesNotMakeAnExactCargoLookExceeded()
    {
        // 0,1 + 0,2 = 0,30000000000000004 en doble: sin tolerancia figuraría como excedida frente a una capacidad de 0,3.
        var cargo = await CargoOf(SheetData.Vehicle(capacityKg: 0.3), SheetData.Cargo(0.1), SheetData.Cargo(0.2));

        Assert.False(cargo.ExceedsCapacity);
    }

    [Fact]
    public async Task OneGramOverTheCapacity_IsExceeded()
    {
        var cargo = await CargoOf(SheetData.Vehicle(capacityKg: 1500), SheetData.Cargo(1500.001));

        Assert.True(cargo.ExceedsCapacity);
    }

    [Fact]
    public async Task TheVolume_IsComparedOnlyWhenTheVehicleDeclaresIt()
    {
        var undeclared = await CargoOf(SheetData.Vehicle(capacityM3: null), SheetData.Cargo(10, volumeM3: 99));
        var fits = await CargoOf(SheetData.Vehicle(capacityM3: 10), SheetData.Cargo(10, volumeM3: 10));
        var over = await CargoOf(SheetData.Vehicle(capacityM3: 10), SheetData.Cargo(10, volumeM3: 10.5));

        Assert.False(undeclared.ExceedsCapacity);
        Assert.Null(undeclared.CapacityM3);
        Assert.Equal(99, undeclared.TotalVolumeM3);
        Assert.False(fits.ExceedsCapacity);
        Assert.True(over.ExceedsCapacity);
    }

    [Fact]
    public async Task AVehicleWithoutDeclaredCapacity_HasNoPercentageButAnyCargoExceedsIt()
    {
        var cargo = await CargoOf(SheetData.Vehicle(capacityKg: 0), SheetData.Cargo(5));

        Assert.Equal(0, cargo.WeightUsagePercent);
        Assert.True(cargo.ExceedsCapacity);
    }

    [Fact]
    public async Task ARouteWithoutCargo_ReportsZeroItemsAndZeroUsage()
    {
        var cargo = await CargoOf(SheetData.Vehicle());

        Assert.Equal(0, cargo.Items);
        Assert.Equal(0, cargo.TotalWeightKg);
        Assert.Equal(0, cargo.TotalVolumeM3);
        Assert.Equal(0, cargo.WeightUsagePercent);
        Assert.False(cargo.ExceedsCapacity);
    }
}
