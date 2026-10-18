namespace FleetManagement.Application.Simulation;

/// <summary>
/// Memoria del avance de los viajes: guarda, por ruta, cuántos kilómetros de la poligonal lleva recorridos el vehículo.
/// Es lo ÚNICO que la simulación necesita recordar entre un paso y el siguiente; la posición, el porcentaje y la ETA se
/// derivan de este valor (ver <see cref="RoutePath.ProgressAt"/>).
///
/// La interfaz vive en Application y la implementación en memoria (segura para varios hilos) en Infrastructure, igual que
/// los repositorios: el día que el progreso deba sobrevivir a un reinicio, se cambia la implementación sin tocar a nadie.
/// </summary>
public interface ITripProgressTracker
{
    /// <summary>Kilómetros recorridos de la ruta, o <c>false</c> si todavía no se registró avance (el viaje no ha empezado).</summary>
    bool TryGetCoveredKm(Guid routeId, out double coveredKm);

    void SetCoveredKm(Guid routeId, double coveredKm);

    /// <summary>Olvida el avance de la ruta (llegó al destino o dejó de estar activa).</summary>
    void Clear(Guid routeId);

    /// <summary>Olvida el avance de todas las rutas que no estén en <paramref name="routeIdsToKeep"/>.</summary>
    void ClearExcept(IReadOnlySet<Guid> routeIdsToKeep);
}
