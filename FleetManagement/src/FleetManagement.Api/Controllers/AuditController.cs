using FleetManagement.Api.Filters;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>Consulta del registro de auditoría centralizado (demuestra el patrón Singleton: FleetAuditLogger).</summary>
[ApiController]
[Route("api/audit")]
[RequireRole(UserRole.Admin)]
public class AuditController : ControllerBase
{
    private readonly IFleetAuditLogger _auditLogger;

    public AuditController(IFleetAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    [HttpGet("logs")]
    public ActionResult<IReadOnlyList<AuditLogEntryDto>> GetLogs([FromQuery] int count = 100)
        => Ok(_auditLogger.GetRecentLogs(count));
}
