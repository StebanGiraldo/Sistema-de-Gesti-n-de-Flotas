using FleetManagement.Application.DTOs;
using FleetManagement.Application.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Telemetría en vivo de los vehículos: lo último que publicó la simulación de cada uno (patrón Observer). Es un controlador
/// aparte a propósito: <c>FleetController</c> y <c>TripProgressController</c> no se modifican y siguen sirviendo lo mismo que
/// antes. Igual que la consulta de vehículos y la de progreso, es de lectura y no exige rol.
/// </summary>
[ApiController]
[Route("api/monitoring")]
public class MonitoringController : ControllerBase
{
    private readonly ILiveTelemetryBoard _board;

    public MonitoringController(ILiveTelemetryBoard board)
    {
        _board = board;
    }

    /// <summary>
    /// Última telemetría conocida de cada vehículo que ha estado en viaje: posición, kilometraje, progreso, próxima parada y
    /// última parada alcanzada. Se alimenta sola con los eventos que publica el simulador; no lee repositorios.
    /// </summary>
    [HttpGet("telemetry")]
    public ActionResult<IReadOnlyList<VehicleLiveStatusDto>> GetTelemetry()
        => Ok(_board.GetAll());

    /// <summary>Última telemetría conocida de un vehículo; 404 si no ha estado en viaje desde que arrancó la aplicación.</summary>
    [HttpGet("telemetry/{vehicleId:guid}")]
    public ActionResult<VehicleLiveStatusDto> GetVehicleTelemetry(Guid vehicleId)
        => _board.Get(vehicleId) is { } status
            ? Ok(status)
            : NotFound(new { message = "No hay telemetría de ese vehículo: no ha estado en viaje desde que arrancó la aplicación." });
}
