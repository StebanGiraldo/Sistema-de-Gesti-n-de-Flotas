using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Monitoring;

/// <summary>
/// Lado de consulta del tablero de telemetría en vivo: lo que necesita quien solo quiere LEER la última telemetría conocida
/// (el controlador de monitoreo). Está separado del contrato de observador (ISP): quien consulta no puede alimentar el tablero.
/// </summary>
public interface ILiveTelemetryBoard
{
    /// <summary>Última telemetría de cada vehículo que ha estado en viaje, ordenada por placa. Vacía si todavía no hubo ninguna.</summary>
    IReadOnlyList<VehicleLiveStatusDto> GetAll();

    /// <summary>Última telemetría del vehículo, o <c>null</c> si no ha estado en viaje desde que arrancó la aplicación.</summary>
    VehicleLiveStatusDto? Get(Guid vehicleId);
}
