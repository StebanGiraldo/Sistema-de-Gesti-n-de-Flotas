namespace FleetManagement.Application.DTOs;

/// <summary>Un orden de paradas con lo que cuesta recorrerlo: kilómetros en línea recta y minutos a la velocidad del vehículo.</summary>
public record RouteSequenceDto(List<WaypointDto> Waypoints, double DistanceKm, double DurationMinutes);

/// <summary>
/// Resultado de optimizar el orden de las paradas de una ruta: cómo está hoy, cómo quedaría y cuánto se ahorra. El origen y el
/// destino no se mueven; solo se reordenan las paradas intermedias. <paramref name="Improves"/> es falso cuando el orden
/// actual ya es el mejor que se encontró (entonces lo propuesto es igual a lo actual); <paramref name="Applied"/> indica si
/// se guardó el cambio.
/// </summary>
public record RouteOptimizationDto(
    Guid RouteId,
    string RouteName,
    string Status,
    RouteSequenceDto Current,
    RouteSequenceDto Proposed,
    double DistanceSavedKm,
    double DistanceSavedPercent,
    double MinutesSaved,
    bool Improves,
    bool Applied);
