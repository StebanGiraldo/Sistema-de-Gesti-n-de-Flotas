using FleetManagement.Application.RouteSheets;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Hoja de ruta (patrón Template Method, <c>Application/RouteSheets</c>): mismo esqueleto para todos, contenido distinto según
/// la audiencia. Es una consulta de lectura y, como el resto de las consultas de rutas (<c>GET api/routes/{id}</c>,
/// <c>GET api/routes/driver/{driverId}</c>, <c>GET api/navigation/route/{id}</c>), no exige rol: el proyecto no tiene una política
/// de acceso por conductor que repetir.
/// </summary>
[ApiController]
[Route("api/route-sheets")]
public class RouteSheetsController : ControllerBase
{
    private readonly IRouteSheetProvider _sheets;

    public RouteSheetsController(IRouteSheetProvider sheets)
    {
        _sheets = sheets;
    }

    /// <summary>
    /// Hoja de la ruta para una audiencia: <c>Driver</c> (paradas, navegación, próxima parada y ETA) o <c>Dispatcher</c> (carga
    /// frente a capacidad, retraso y avance). Una audiencia desconocida responde 400 con los valores válidos; una ruta inexistente,
    /// 404; una ruta sin vehículo, 400.
    /// </summary>
    [HttpGet("{routeId:guid}")]
    public async Task<IActionResult> Get(Guid routeId, [FromQuery] string? audience)
    {
        try
        {
            return Ok(await _sheets.For(audience).BuildAsync(routeId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
