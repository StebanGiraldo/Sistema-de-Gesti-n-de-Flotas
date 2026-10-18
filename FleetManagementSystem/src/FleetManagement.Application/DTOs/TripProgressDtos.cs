namespace FleetManagement.Application.DTOs;

/// <summary>Punto de paso de una ruta (origen, parada intermedia o destino) tal como lo dibuja el mapa.</summary>
public record TripStopDto(string Label, double Latitude, double Longitude);

/// <summary>
/// Avance de un viaje en curso: cuánto lleva recorrido, dónde está, cuál es la próxima parada y cuánto falta. Incluye los
/// puntos de la ruta (<see cref="Stops"/>) para que el mapa dibuje el recorrido con UNA sola consulta de todos los viajes
/// activos, sin pedir cada ruta aparte (evita el problema N+1 del sondeo periódico del frontend).
/// Los números vienen redondeados a la precisión que tiene sentido mostrar (metros, décimas de minuto).
/// </summary>
public record TripProgressDto(
    Guid RouteId,
    string RouteName,
    Guid VehicleId,
    string LicensePlate,
    string RouteStatus,
    double DistanceCoveredKm,
    double TotalKm,
    double Percent,
    double Latitude,
    double Longitude,
    string NextStopLabel,
    double RemainingKm,
    double EtaMinutes,
    bool Arrived,
    IReadOnlyList<TripStopDto> Stops);
