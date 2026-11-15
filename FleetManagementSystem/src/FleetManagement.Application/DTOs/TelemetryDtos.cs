namespace FleetManagement.Application.DTOs;

/// <summary>
/// Última telemetría conocida de un vehículo, tal como la dejó el tablero en vivo al recibir los eventos del recorrido (patrón
/// Observer). A diferencia de <see cref="TripProgressDto"/>, que se calcula bajo demanda solo para los viajes activos, esto es
/// lo ÚLTIMO que se publicó: sigue disponible cuando el viaje termina (con <see cref="Arrived"/> en verdadero) hasta que el
/// vehículo vuelve a salir. Los números van redondeados a la precisión que tiene sentido mostrar.
/// </summary>
/// <param name="VehicleId">Vehículo.</param>
/// <param name="LicensePlate">Placa del vehículo.</param>
/// <param name="RouteId">Ruta del último viaje con telemetría.</param>
/// <param name="Latitude">Última latitud publicada.</param>
/// <param name="Longitude">Última longitud publicada.</param>
/// <param name="MileageKm">Kilometraje del vehículo en el último paso.</param>
/// <param name="Percent">Avance del viaje, de 0 a 100.</param>
/// <param name="NextStopLabel">Próxima parada ("Destino" en el último tramo).</param>
/// <param name="LastStopReached">Última parada intermedia que alcanzó en este viaje, o <c>null</c> si todavía no pasó por ninguna.</param>
/// <param name="RemainingKm">Kilómetros que faltan hasta el destino.</param>
/// <param name="EtaMinutes">Minutos que faltaban para llegar en ese paso.</param>
/// <param name="Arrived">Verdadero cuando el vehículo ya llegó al destino.</param>
/// <param name="UpdatedAt">Instante (UTC) del último evento recibido.</param>
public record VehicleLiveStatusDto(
    Guid VehicleId,
    string LicensePlate,
    Guid RouteId,
    double Latitude,
    double Longitude,
    double MileageKm,
    double Percent,
    string NextStopLabel,
    string? LastStopReached,
    double RemainingKm,
    double EtaMinutes,
    bool Arrived,
    DateTimeOffset UpdatedAt);
