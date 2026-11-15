using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.States;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// CONCRETE COLLEAGUE de vehículos (patrón MEDIATOR). Cambia el estado del vehículo con el repositorio de vehículos
/// cuando el mediador se lo pide; no conoce al participante de rutas ni a ningún servicio.
///
/// Sigue siendo quien aplica la regla de disponibilidad al coordinar, pero ya no la decide con un <c>if</c> propio: la regla
/// vive en los estados del vehículo (patrón STATE, <see cref="VehicleStateContext"/>). Al empezar un viaje el estado deja al
/// vehículo En ruta; al terminar o cancelarse, un vehículo En ruta sólo se libera si no sigue en otro viaje, y uno en
/// Mantenimiento o Fuera de servicio conserva su estado (no se "repara" por terminar una ruta).
/// </summary>
public sealed class VehicleColleague : IVehicleColleague
{
    private readonly IVehicleRepository _vehicles;
    private readonly IVehicleStateResolver _states;

    /// <param name="vehicles">Repositorio de vehículos.</param>
    /// <param name="states">Estados del vehículo; si no se indica se usan los compartidos (<see cref="VehicleStateResolver.Default"/>).</param>
    public VehicleColleague(IVehicleRepository vehicles, IVehicleStateResolver? states = null)
    {
        _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
        _states = states ?? VehicleStateResolver.Default;
    }

    public async Task MarkEnRouteAsync(Guid vehicleId)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
            return;

        new VehicleStateContext(vehicle, _states).StartTrip();
        await _vehicles.UpdateAsync(vehicle);
    }

    public async Task ReleaseAfterTripAsync(Guid vehicleId, bool hasOtherActiveTrip)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
            return;

        new VehicleStateContext(vehicle, _states).FinishTrip(hasOtherActiveTrip);
        await _vehicles.UpdateAsync(vehicle);
    }
}
