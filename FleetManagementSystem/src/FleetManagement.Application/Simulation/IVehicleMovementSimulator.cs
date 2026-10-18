namespace FleetManagement.Application.Simulation;

/// <summary>
/// Hace avanzar la simulación: mueve cada vehículo en recorrido a lo largo de su ruta asignada. Contiene TODA la lógica de
/// movimiento; el servicio de fondo solo le avisa cuánto tiempo pasó.
///
/// Recibe el tiempo transcurrido como parámetro (en vez de leer el reloj) para que el resultado sea determinista y se
/// pueda probar sin esperar: con el mismo estado y el mismo tiempo, el vehículo termina siempre en el mismo punto.
/// </summary>
public interface IVehicleMovementSimulator
{
    /// <param name="elapsed">Tiempo real transcurrido desde el paso anterior (no puede ser negativo).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="elapsed"/> es negativo.</exception>
    /// <exception cref="OperationCanceledException">Si se cancela antes de terminar el paso.</exception>
    Task AdvanceAsync(TimeSpan elapsed, CancellationToken cancellationToken = default);
}
