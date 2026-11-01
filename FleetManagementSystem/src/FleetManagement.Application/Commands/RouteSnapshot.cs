namespace FleetManagement.Application.Commands;

/// <summary>Posición de una parada dentro de la ruta, identificada por la parada y no por su etiqueta (pueden repetirse).</summary>
public sealed record WaypointPosition(Guid WaypointId, int Order);

/// <summary>
/// Foto del orden de las paradas de una ruta y de sus estimaciones, tomada antes de optimizarla: es lo único que hace falta
/// para volver atrás (un recuerdo, como en el patrón Memento, pero sin exponer la entidad).
/// </summary>
public sealed record RouteSnapshot(
    Guid RouteId,
    string RouteName,
    IReadOnlyList<WaypointPosition> Stops,
    double EstimatedDistanceKm,
    double EstimatedDurationMinutes);
