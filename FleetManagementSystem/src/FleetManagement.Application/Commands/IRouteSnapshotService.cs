namespace FleetManagement.Application.Commands;

/// <summary>
/// ORIGINATOR (patrón MEMENTO) del orden de las paradas de una ruta: quien crea el recuerdo (<see cref="CaptureAsync"/>) y el
/// único que sabe restaurarlo (<see cref="RestoreStopOrderAsync"/>). Se hace con un servicio y no con la entidad
/// <c>DeliveryRoute</c> para no agregarle comportamiento de deshacer ni exponer sus paradas.
/// </summary>
public interface IRouteSnapshotService
{
    /// <summary>Toma el recuerdo del orden de paradas y de las estimaciones de la ruta. Lanza <see cref="KeyNotFoundException"/> si no existe.</summary>
    Task<RouteSnapshot> CaptureAsync(Guid routeId);

    /// <summary>
    /// Devuelve la ruta a lo que decía el recuerdo. Solo si sigue planificada y con las mismas paradas; si no (o si la ruta ya
    /// no existe), lanza <see cref="CommandNotUndoableException"/> sin cambiar nada. Un recuerdo que no creó este servicio
    /// (<see cref="ArgumentException"/>) o nulo (<see cref="ArgumentNullException"/>) se rechaza sin tocar la ruta.
    /// </summary>
    Task RestoreStopOrderAsync(IRouteMemento memento);
}
