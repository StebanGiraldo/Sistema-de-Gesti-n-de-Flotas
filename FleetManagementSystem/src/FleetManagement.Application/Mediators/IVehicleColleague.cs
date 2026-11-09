namespace FleetManagement.Application.Mediators;

/// <summary>
/// COLLEAGUE de vehículos (patrón MEDIATOR): el participante que es dueño de la disponibilidad del vehículo durante un
/// viaje. Cambia el estado del vehículo cuando el mediador se lo pide y no conoce al participante de rutas: lo que
/// necesita saber de ellas (si el vehículo sigue en otro viaje) se lo da el mediador.
///
/// No usa <c>IVehicleService</c> a propósito: ese servicio es el de los cambios manuales y, además de guardar, audita y
/// notifica cada cambio; el ajuste automático por el ciclo de vida de la ruta nunca hizo ninguna de las dos cosas.
/// </summary>
public interface IVehicleColleague
{
    /// <summary>El vehículo queda En ruta. Si el vehículo ya no existe, no hace nada.</summary>
    Task MarkEnRouteAsync(Guid vehicleId);

    /// <summary>
    /// Un viaje terminó o se canceló. El vehículo queda Disponible sólo si iba En ruta y no sigue en otro viaje activo
    /// (<paramref name="hasOtherActiveTrip"/>); en cualquier otro estado, por ejemplo Mantenimiento o Fuera de servicio,
    /// conserva el que tiene: terminar una ruta no lo repara. Si el vehículo ya no existe, no hace nada.
    /// </summary>
    Task ReleaseAfterTripAsync(Guid vehicleId, bool hasOtherActiveTrip);
}
