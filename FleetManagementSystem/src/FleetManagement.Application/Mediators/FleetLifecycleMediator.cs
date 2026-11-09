using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Mediators;

/// <summary>
/// CONCRETE MEDIATOR (patrón MEDIATOR): reúne en un solo lugar las reglas que relacionan el estado de una ruta con el de
/// su vehículo, que antes estaban dentro de <c>DeliveryRouteService.UpdateRouteStatusAsync</c>. Las reglas son
/// exactamente las de siempre; lo que cambió es QUIÉN las aplica:
///
///   ruta → En progreso / Retrasada ......... el vehículo queda En ruta                                        (regla 1)
///   ruta → Completada / Cancelada .......... el vehículo queda Disponible sólo si iba En ruta Y no sigue
///                                             en otro viaje activo; Mantenimiento / Fuera de servicio
///                                             conservan su estado                                              (reglas 2 y 3)
///   ruta → Planificada ..................... el vehículo no se toca
///   ruta sin vehículo asignado ............. no hay nada que coordinar                                         (regla 4)
///
/// Cada regla se reparte entre los participantes según lo que sabe cada uno: el de rutas sabe qué es un viaje activo
/// (se lo pregunta a quien le avisó) y el de vehículos sabe cuándo un vehículo puede quedar Disponible. Entre ellos no
/// hay ninguna referencia: el mediador pasa la respuesta de uno al otro.
///
/// Depende sólo de la interfaz <see cref="IVehicleColleague"/>. Al participante de rutas lo conoce cuando éste le avisa
/// (viene en la llamada), así que no hay referencias circulares entre mediador y participantes.
/// </summary>
public sealed class FleetLifecycleMediator : IFleetLifecycleMediator
{
    private readonly IVehicleColleague _vehicles;

    public FleetLifecycleMediator(IVehicleColleague vehicles)
    {
        _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
    }

    public async Task RouteStatusChangedAsync(IRouteColleague sender, DeliveryRoute route)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(route);

        // Regla 4: una ruta sin vehículo asignado no coordina nada con ningún vehículo.
        if (route.AssignedVehicleId is not { } vehicleId)
            return;

        switch (route.Status)
        {
            case DeliveryRouteStatus.InProgress:
            case DeliveryRouteStatus.Delayed:
                // Regla 1: un viaje en marcha mantiene al vehículo En ruta.
                await _vehicles.MarkEnRouteAsync(vehicleId);
                break;

            case DeliveryRouteStatus.Completed:
            case DeliveryRouteStatus.Cancelled:
                // Reglas 2 y 3: lo que se sabe de las rutas (¿sigue en otro viaje?) viaja por aquí hacia el participante
                // de vehículos, que es quien decide si puede quedar Disponible.
                var stillInAnotherTrip = await sender.HasOtherActiveTripAsync(vehicleId, route.Id);
                await _vehicles.ReleaseAfterTripAsync(vehicleId, stillInAnotherTrip);
                break;

            // Planificada: el vehículo no cambia.
        }
    }

    public Task RouteDelayReportedAsync(IRouteColleague sender, DeliveryRoute route)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(route);

        // DECISIÓN CONSERVADA, no una regla nueva: el retraso que reporta una alerta siempre marcó la ruta como Retrasada
        // sin tocar al vehículo ni auditar un cambio de estado, y así se mantiene. Es distinto de pedir el estado
        // "Retrasada" por la API (RouteStatusChangedAsync), que sí deja al vehículo En ruta. Si el equipo decide que la
        // regla 1 también debe aplicar a las alertas, el cambio es una línea aquí (llamar a _vehicles.MarkEnRouteAsync) y
        // la prueba que fija el comportamiento actual (RouteDelayReported_DoesNotTouchTheVehicle...) pasa a esperar lo nuevo.
        return Task.CompletedTask;
    }
}
