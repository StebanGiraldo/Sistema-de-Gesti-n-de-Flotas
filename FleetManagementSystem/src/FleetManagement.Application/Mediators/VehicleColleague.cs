using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// CONCRETE COLLEAGUE de vehículos (patrón MEDIATOR). Cambia el estado del vehículo con el repositorio de vehículos
/// cuando el mediador se lo pide; no conoce al participante de rutas ni a ningún servicio. Es dueño de la regla de
/// disponibilidad: un vehículo sólo se libera si iba En ruta y no sigue en otro viaje, y uno en Mantenimiento o Fuera de
/// servicio no se "repara" por terminar una ruta.
/// </summary>
public sealed class VehicleColleague : IVehicleColleague
{
    private readonly IVehicleRepository _vehicles;

    public VehicleColleague(IVehicleRepository vehicles)
    {
        _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
    }

    public async Task MarkEnRouteAsync(Guid vehicleId)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
            return;

        vehicle.Status = VehicleStatus.EnRoute;
        await _vehicles.UpdateAsync(vehicle);
    }

    public async Task ReleaseAfterTripAsync(Guid vehicleId, bool hasOtherActiveTrip)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
            return;

        vehicle.Status = ReleasedStatus(vehicle.Status, hasOtherActiveTrip);
        await _vehicles.UpdateAsync(vehicle);
    }

    /// <summary>Sólo se libera al vehículo que iba "En ruta" y que no sigue ocupado en otro viaje activo; los demás estados se conservan.</summary>
    private static VehicleStatus ReleasedStatus(VehicleStatus current, bool usedByAnotherTrip)
        => current == VehicleStatus.EnRoute && !usedByAnotherTrip ? VehicleStatus.Available : current;
}
