using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Avance de los viajes en curso para el mapa y el portal del operador. Es un controlador aparte a propósito:
/// <c>FleetController</c> no se modifica. Igual que la consulta de vehículos, es de lectura y no exige rol.
/// </summary>
[ApiController]
[Route("api/progress")]
public class TripProgressController : ControllerBase
{
    private readonly ITripProgressService _tripProgressService;

    public TripProgressController(ITripProgressService tripProgressService)
    {
        _tripProgressService = tripProgressService;
    }

    /// <summary>
    /// Devuelve, en una sola llamada, el avance de todos los viajes activos: porcentaje, kilómetros restantes, próxima
    /// parada, hora estimada de llegada (en minutos) y los puntos de la ruta. El frontend lo consulta cada 5 s junto con
    /// los vehículos (en paralelo), por eso no existe una consulta por viaje.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripProgressDto>>> GetActiveTrips()
        => Ok(await _tripProgressService.GetActiveTripsAsync());
}
