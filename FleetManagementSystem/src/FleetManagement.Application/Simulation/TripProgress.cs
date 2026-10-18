namespace FleetManagement.Application.Simulation;

/// <summary>
/// Foto inmutable del avance de un viaje en un instante: dónde está el vehículo sobre la ruta y cuánto le falta. Es el
/// ESTADO EXTRÍNSECO que cambia en cada ciclo de la simulación (placa, posición, estado, progreso), por oposición al
/// estado intrínseco y compartido del perfil por tipo de vehículo (ver <c>VehicleTypeProfile</c>, patrón Flyweight).
///
/// No se guarda: se calcula bajo demanda a partir de la ruta, la distancia recorrida que registra
/// <see cref="ITripProgressTracker"/> y la velocidad del vehículo. Así hay una sola fuente de verdad (la distancia) y la
/// posición, el porcentaje o la ETA nunca pueden contradecirse.
/// </summary>
/// <param name="RouteId">Ruta que se está recorriendo.</param>
/// <param name="VehicleId">Vehículo que la recorre.</param>
/// <param name="DistanceCoveredKm">Kilómetros recorridos sobre la poligonal desde el origen.</param>
/// <param name="TotalKm">Longitud total de la poligonal (origen, paradas ordenadas, destino).</param>
/// <param name="Percent">Avance de 0 a 100.</param>
/// <param name="Latitude">Latitud actual del vehículo (grados decimales).</param>
/// <param name="Longitude">Longitud actual del vehículo (grados decimales).</param>
/// <param name="NextStopLabel">Etiqueta de la próxima parada (o "Destino" en el último tramo).</param>
/// <param name="RemainingKm">Kilómetros que faltan hasta el destino.</param>
/// <param name="EtaMinutes">Minutos que faltan para llegar, a la velocidad con la que avanza hoy el vehículo.</param>
/// <param name="Arrived">Verdadero cuando el vehículo ya llegó al destino.</param>
public sealed record TripProgress(
    Guid RouteId,
    Guid VehicleId,
    double DistanceCoveredKm,
    double TotalKm,
    double Percent,
    double Latitude,
    double Longitude,
    string NextStopLabel,
    double RemainingKm,
    double EtaMinutes,
    bool Arrived);
