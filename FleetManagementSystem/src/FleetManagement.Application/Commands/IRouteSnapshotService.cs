namespace FleetManagement.Application.Commands;

public interface IRouteSnapshotService
{
    /// <summary>Toma la foto del orden de paradas y de las estimaciones de la ruta. Lanza <see cref="KeyNotFoundException"/> si no existe.</summary>
    Task<RouteSnapshot> CaptureAsync(Guid routeId);

    /// <summary>Devuelve la ruta a lo que decía la foto. Solo si sigue planificada y con las mismas paradas; si no, lanza <see cref="CommandNotUndoableException"/> sin cambiar nada.</summary>
    Task RestoreStopOrderAsync(RouteSnapshot snapshot);
}
