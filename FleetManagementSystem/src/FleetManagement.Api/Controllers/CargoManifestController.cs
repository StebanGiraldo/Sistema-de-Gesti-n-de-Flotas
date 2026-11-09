using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Manifiesto jerárquico de la carga de una ruta (patrón Composite). Se
/// publica en un controlador propio, bajo el mismo prefijo api/routes, para no
/// modificar DeliveryRoutesController ni su servicio.
/// </summary>
[ApiController]
[Route("api/routes")]
public class CargoManifestController : ControllerBase
{
    private readonly ICargoManifestService _manifestService;

    public CargoManifestController(ICargoManifestService manifestService)
    {
        _manifestService = manifestService;
    }

    [HttpGet("{id:guid}/cargo-manifest")]
    public async Task<ActionResult<CargoManifestDto>> GetManifest(Guid id)
    {
        var manifest = await _manifestService.GetManifestAsync(id);
        return manifest is null ? NotFound(new { message = "Ruta no encontrada." }) : Ok(manifest);
    }

    /// <summary>
    /// Los mismos artículos del manifiesto, pero como lista plana en el orden del recorrido en profundidad
    /// (patrón Iterator). Endpoint adicional: no cambia el de arriba ni su contrato.
    /// </summary>
    [HttpGet("{id:guid}/cargo-items")]
    public async Task<ActionResult<CargoManifestItemsDto>> GetCargoItems(Guid id)
    {
        var items = await _manifestService.GetCargoItemsAsync(id);
        return items is null ? NotFound(new { message = "Ruta no encontrada." }) : Ok(items);
    }
}
