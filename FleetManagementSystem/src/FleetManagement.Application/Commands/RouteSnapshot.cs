namespace FleetManagement.Application.Commands;

/// <summary>Posición de una parada dentro de la ruta, identificada por la parada y no por su etiqueta (pueden repetirse).</summary>
public sealed record WaypointPosition(Guid WaypointId, int Order);

/// <summary>
/// MEMENTO (patrón MEMENTO), interfaz ANCHA: la foto del orden de las paradas de una ruta y de sus estimaciones, tomada antes
/// de optimizarla. Es lo único que hace falta para volver atrás (sin exponer la entidad de dominio ni guardar coordenadas,
/// carga ni estado de la ruta, que no cambian al optimizar).
///
/// Está pensada para no poder alterarse después de creada:
/// <list type="bullet">
///   <item>Solo la crea el Originator (<see cref="RouteSnapshotService"/>): el constructor no es público, así que nadie más
///         puede fabricar un recuerdo con datos a medida.</item>
///   <item>Sus propiedades son de solo lectura y <see cref="Stops"/> es una copia propia de solo lectura: ni los cambios
///         posteriores de la ruta ni los de quien entregó las paradas llegan hasta aquí, ni se puede convertir en una lista
///         modificable.</item>
/// </list>
/// Quien solo la guarda (el Caretaker) la ve a través de <see cref="IRouteMemento"/>.
/// </summary>
public sealed class RouteSnapshot : IRouteMemento
{
    internal RouteSnapshot(
        Guid routeId,
        string routeName,
        IEnumerable<WaypointPosition> stops,
        double estimatedDistanceKm,
        double estimatedDurationMinutes)
    {
        ArgumentNullException.ThrowIfNull(routeName);
        ArgumentNullException.ThrowIfNull(stops);

        RouteId = routeId;
        RouteName = routeName;
        Stops = Array.AsReadOnly(stops.ToArray());
        EstimatedDistanceKm = estimatedDistanceKm;
        EstimatedDurationMinutes = estimatedDurationMinutes;
    }

    public Guid RouteId { get; }
    public string RouteName { get; }

    /// <summary>Las paradas con el orden que tenían, en el orden en que estaban guardadas.</summary>
    public IReadOnlyList<WaypointPosition> Stops { get; }

    public double EstimatedDistanceKm { get; }
    public double EstimatedDurationMinutes { get; }
}
