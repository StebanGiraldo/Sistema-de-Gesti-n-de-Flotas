using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>Integración con sistemas de navegación (requerimiento #4).</summary>
[ApiController]
[Route("api/navigation")]
public class NavigationController : ControllerBase
{
    private readonly INavigationService _navigationService;

    public NavigationController(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    /// <summary>
    /// Genera un enlace de navegación externo (Google Maps) con la ruta
    /// asignada ya cargada, para que el conductor pueda abrirlo desde su
    /// teléfono y seguir el camino hasta el destino final.
    /// </summary>
    [HttpGet("route/{routeId:guid}")]
    public async Task<IActionResult> GetNavigationLink(Guid routeId)
    {
        try
        {
            var link = await _navigationService.GenerateExternalNavigationLinkAsync(routeId);
            return Ok(link);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
