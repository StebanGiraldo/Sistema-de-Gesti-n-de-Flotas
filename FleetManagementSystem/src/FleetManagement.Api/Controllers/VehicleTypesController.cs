using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// Perfiles por tipo de vehículo (velocidad máxima, restricciones de navegación y plantillas de mantenimiento). Es un
/// controlador aparte a propósito: <c>FleetController</c> no se modifica. Los perfiles son el estado compartido del
/// patrón Flyweight, así que esta consulta nunca los reconstruye.
/// </summary>
[ApiController]
[Route("api/fleet/vehicle-types")]
public class VehicleTypesController : ControllerBase
{
    private readonly IVehicleTypeService _vehicleTypeService;

    public VehicleTypesController(IVehicleTypeService vehicleTypeService)
    {
        _vehicleTypeService = vehicleTypeService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<VehicleTypeProfileDto>> GetVehicleTypes()
        => Ok(_vehicleTypeService.GetAllProfiles());
}
