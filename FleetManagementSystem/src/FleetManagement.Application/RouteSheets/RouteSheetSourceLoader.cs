using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.RouteSheets;

/// <summary>El paso fijo de la plantilla: cargar y validar la ruta, su vehículo y su conductor.</summary>
public interface IRouteSheetSourceLoader
{
    /// <exception cref="KeyNotFoundException">La ruta, su vehículo o su conductor no existen (la API responde 404).</exception>
    /// <exception cref="InvalidOperationException">La ruta no tiene vehículo asignado (la API responde 400).</exception>
    Task<RouteSheetSource> LoadAsync(Guid routeId);
}

/// <summary>
/// Lee los repositorios y rechaza lo que no se puede imprimir. El conductor es opcional (una ruta puede no tenerlo todavía),
/// pero si la ruta apunta a uno que no existe es un dato inconsistente y se rechaza. Se mira solo el conductor de la ruta
/// (<c>AssignedDriverId</c>), igual que el resto de los servicios de rutas.
/// </summary>
public sealed class RouteSheetSourceLoader : IRouteSheetSourceLoader
{
    private readonly IDeliveryRouteRepository _routes;
    private readonly IVehicleRepository _vehicles;
    private readonly IDriverRepository _drivers;

    public RouteSheetSourceLoader(IDeliveryRouteRepository routes, IVehicleRepository vehicles, IDriverRepository drivers)
    {
        _routes = routes;
        _vehicles = vehicles;
        _drivers = drivers;
    }

    public async Task<RouteSheetSource> LoadAsync(Guid routeId)
    {
        var route = await _routes.GetByIdAsync(routeId) ?? throw new KeyNotFoundException("Ruta no encontrada.");
        var vehicle = await LoadVehicleAsync(route);
        var driver = await LoadDriverAsync(route);
        return new RouteSheetSource(route, vehicle, driver);
    }

    private async Task<Vehicle> LoadVehicleAsync(DeliveryRoute route)
    {
        if (route.AssignedVehicleId is not { } vehicleId)
            throw new InvalidOperationException($"La ruta '{route.Name}' no tiene vehículo asignado: no se puede generar su hoja de ruta.");

        return await _vehicles.GetByIdAsync(vehicleId) ?? throw new KeyNotFoundException("El vehículo asignado a la ruta no existe.");
    }

    private async Task<Driver?> LoadDriverAsync(DeliveryRoute route)
    {
        if (route.AssignedDriverId is not { } driverId)
            return null;

        return await _drivers.GetByIdAsync(driverId) ?? throw new KeyNotFoundException("El conductor asignado a la ruta no existe.");
    }
}
