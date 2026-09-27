using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>Mantenimiento predictivo de vehículos (requerimiento #3).</summary>
[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetAll()
        => Ok(await _maintenanceService.GetAllAsync());

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetByVehicle(Guid vehicleId)
        => Ok(await _maintenanceService.GetByVehicleAsync(vehicleId));

    /// <summary>Vehículos con mantenimiento vencido por fecha o por kilometraje (módulo predictivo).</summary>
    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceDueDto>>> GetDue()
        => Ok(await _maintenanceService.GetVehiclesDueForMaintenanceAsync());

    [HttpPost]
    [RequireRole(UserRole.Admin)]
    public async Task<ActionResult<MaintenanceRecordDto>> Create([FromBody] CreateMaintenanceRecordRequest request)
    {
        try
        {
            var record = await _maintenanceService.CreateAsync(request);
            return Ok(record);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
