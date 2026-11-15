namespace FleetManagement.Application.Monitoring;

/// <summary>
/// EVENTO del patrón OBSERVER: lo que el sujeto (<see cref="ITelemetryPublisher"/>) comunica a los observadores. Es un objeto de
/// valor inmutable (record): todos los observadores reciben la MISMA instancia y ninguno puede alterarla para los demás.
///
/// Lleva todo lo que un observador necesita saber (placa, ruta, posición...), de modo que ningún observador tiene que volver a
/// leer repositorios ni conocer la simulación: este espacio de nombres no depende de <c>Simulation</c>; la dependencia va en
/// un solo sentido (el simulador publica, Monitoring distribuye).
/// </summary>
/// <param name="VehicleId">Vehículo que originó el evento.</param>
/// <param name="LicensePlate">Placa del vehículo, para que un observador muestre o audite sin consultar nada.</param>
/// <param name="RouteId">Ruta que recorre el vehículo.</param>
/// <param name="OccurredAt">Instante (UTC) del paso de la simulación que lo produjo; todos los eventos de un mismo paso lo comparten.</param>
public abstract record TelemetryEvent(Guid VehicleId, string LicensePlate, Guid RouteId, DateTimeOffset OccurredAt);

/// <summary>
/// Un vehículo en viaje avanzó. Reúne los tres datos que cambian juntos en cada paso: la posición GPS simulada, el kilometraje
/// y el progreso del viaje. Se publica una vez por paso y por viaje activo.
/// </summary>
/// <param name="Latitude">Latitud actual (grados decimales).</param>
/// <param name="Longitude">Longitud actual (grados decimales).</param>
/// <param name="MileageKm">Kilometraje total del vehículo después de este paso.</param>
/// <param name="StepKm">Kilómetros que avanzó en este paso.</param>
/// <param name="ProgressPercent">Avance del viaje, de 0 a 100.</param>
/// <param name="NextStopLabel">Etiqueta de la próxima parada ("Destino" en el último tramo).</param>
/// <param name="RemainingKm">Kilómetros que faltan hasta el destino.</param>
/// <param name="EtaMinutes">Minutos que faltan para llegar a la velocidad actual.</param>
public sealed record VehicleMovedEvent(
    Guid VehicleId,
    string LicensePlate,
    Guid RouteId,
    DateTimeOffset OccurredAt,
    double Latitude,
    double Longitude,
    double MileageKm,
    double StepKm,
    double ProgressPercent,
    string NextStopLabel,
    double RemainingKm,
    double EtaMinutes) : TelemetryEvent(VehicleId, LicensePlate, RouteId, OccurredAt);

/// <summary>El vehículo pasó por una parada intermedia de su ruta (el destino no cuenta: eso es la llegada).</summary>
/// <param name="StopLabel">Etiqueta de la parada alcanzada.</param>
/// <param name="DistanceCoveredKm">Kilómetros recorridos desde el origen hasta esa parada.</param>
/// <param name="ProgressPercent">Avance del viaje en el momento de la parada, de 0 a 100.</param>
public sealed record TripStopReachedEvent(
    Guid VehicleId,
    string LicensePlate,
    Guid RouteId,
    DateTimeOffset OccurredAt,
    string StopLabel,
    double DistanceCoveredKm,
    double ProgressPercent) : TelemetryEvent(VehicleId, LicensePlate, RouteId, OccurredAt);

/// <summary>El vehículo llegó al destino y la ruta quedó completada.</summary>
/// <param name="TotalKm">Longitud total del recorrido.</param>
public sealed record TripArrivedEvent(
    Guid VehicleId,
    string LicensePlate,
    Guid RouteId,
    DateTimeOffset OccurredAt,
    double TotalKm) : TelemetryEvent(VehicleId, LicensePlate, RouteId, OccurredAt);
