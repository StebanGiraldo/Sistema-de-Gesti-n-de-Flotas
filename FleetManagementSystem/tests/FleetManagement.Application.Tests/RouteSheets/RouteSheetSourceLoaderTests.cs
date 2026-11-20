using FleetManagement.Application.RouteSheets;
using Xunit;

namespace FleetManagement.Application.Tests.RouteSheets;

/// <summary>El paso fijo de la plantilla: cargar y validar ruta, vehículo y conductor, con los repositorios reales en memoria.</summary>
public class RouteSheetSourceLoaderTests
{
    private readonly RouteSheetWorld _world = new();

    private IRouteSheetSourceLoader Loader => _world.Collaborators.Source;

    [Fact]
    public async Task ItLoadsTheRoute_TheVehicle_AndTheDriverOfTheRoute()
    {
        var driver = SheetData.Driver();
        var (vehicle, route) = await _world.AddEquatorTripAsync(driver: driver);

        var source = await Loader.LoadAsync(route.Id);

        Assert.Same(route, source.Route);
        Assert.Same(vehicle, source.Vehicle);
        Assert.Same(driver, source.Driver);
        Assert.Null(source.Trip);   // el avance lo agrega la plantilla, no el cargador
    }

    [Fact]
    public async Task ARouteWithoutDriver_IsValid_AndHasNoDriver()
    {
        var (_, route) = await _world.AddEquatorTripAsync();

        var source = await Loader.LoadAsync(route.Id);

        Assert.Null(source.Driver);
    }

    [Fact]
    public async Task ARouteThatDoesNotExist_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => Loader.LoadAsync(Guid.NewGuid()));

        Assert.Equal("Ruta no encontrada.", error.Message);
    }

    [Fact]
    public async Task ARouteWithoutVehicle_IsRejectedNamingTheRoute()
    {
        var route = SheetData.Route(vehicle: null);
        await _world.Fleet.RouteRepository.AddAsync(route);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Loader.LoadAsync(route.Id));

        Assert.Contains(route.Name, error.Message);
        Assert.Contains("no tiene vehículo asignado", error.Message);
    }

    [Fact]
    public async Task ARouteWhoseVehicleNoLongerExists_IsNotFound()
    {
        var route = SheetData.Route(SheetData.Vehicle());   // el vehículo nunca se guardó
        await _world.Fleet.RouteRepository.AddAsync(route);

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => Loader.LoadAsync(route.Id));

        Assert.Contains("vehículo", error.Message);
    }

    [Fact]
    public async Task ARouteWhoseDriverNoLongerExists_IsNotFound_NotSilentlyPrintedWithoutDriver()
    {
        var vehicle = SheetData.Vehicle();
        await _world.Fleet.VehicleRepository.AddAsync(vehicle);
        var route = SheetData.Route(vehicle, driverId: Guid.NewGuid());
        await _world.Fleet.RouteRepository.AddAsync(route);

        var error = await Assert.ThrowsAsync<KeyNotFoundException>(() => Loader.LoadAsync(route.Id));

        Assert.Contains("conductor", error.Message);
    }
}
