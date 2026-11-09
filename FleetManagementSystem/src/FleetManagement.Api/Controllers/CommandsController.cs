using FleetManagement.Api.Filters;
using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FleetManagement.Api.Controllers;

/// <summary>
/// PATRÓN COMMAND — punto de entrada de las operaciones que se pueden deshacer: crear una ruta (por el formulario, express o
/// duplicando otra), aplicar el orden optimizado de una ruta y aplicar el plan de reparto. Cada una se arma como una orden
/// (<see cref="IRouteCommandFactory"/>) y se ejecuta con el invocador, que la guarda en el historial de quien la pidió;
/// <c>undo</c> y <c>redo</c> actúan sobre ese historial. Los endpoints anteriores (<c>POST api/routes</c>,
/// <c>POST api/routes/express</c>, <c>POST api/routes/{id}/duplicate</c>, <c>POST api/routes/{id}/optimization</c> y
/// <c>POST api/dispatch/apply</c>) siguen funcionando igual, sin historial. Solo administradores.
/// </summary>
[ApiController]
[Route("api/commands")]
[RequireRole(UserRole.Admin)]
public class CommandsController : ControllerBase
{
    private readonly ICommandInvoker _invoker;
    private readonly IRouteCommandFactory _commands;

    public CommandsController(ICommandInvoker invoker, IRouteCommandFactory commands)
    {
        _invoker = invoker;
        _commands = commands;
    }

    /// <summary>Crea una ruta (como <c>POST api/routes</c>) y deja la operación en el historial.</summary>
    [HttpPost("routes")]
    public Task<ActionResult<DeliveryRouteDto>> CreateRoute([FromBody] CreateDeliveryRouteRequest request)
        => Guard<DeliveryRouteDto>(async () => CreatedRoute(await _invoker.ExecuteAsync(CurrentUser, _commands.CreateRoute(request))));

    /// <summary>Crea una ruta express (como <c>POST api/routes/express</c>) y deja la operación en el historial.</summary>
    [HttpPost("routes/express")]
    public Task<ActionResult<DeliveryRouteDto>> CreateExpressRoute([FromBody] CreateExpressRouteRequest request)
        => Guard<DeliveryRouteDto>(async () => CreatedRoute(await _invoker.ExecuteAsync(CurrentUser, _commands.CreateExpressRoute(request))));

    /// <summary>Duplica una ruta (como <c>POST api/routes/{id}/duplicate</c>) y deja la operación en el historial.</summary>
    [HttpPost("routes/{routeId:guid}/duplicate")]
    public Task<ActionResult<DeliveryRouteDto>> DuplicateRoute(Guid routeId, [FromBody] DuplicateRouteRequest request)
        => Guard<DeliveryRouteDto>(async () => CreatedRoute(await _invoker.ExecuteAsync(CurrentUser, _commands.DuplicateRoute(routeId, request))));

    /// <summary>Aplica el orden optimizado a una ruta planificada (como <c>POST api/routes/{id}/optimization</c>) y deja la operación en el historial.</summary>
    [HttpPost("routes/{routeId:guid}/optimization")]
    public Task<ActionResult<RouteOptimizationDto>> ApplyOptimization(Guid routeId)
        => Guard<RouteOptimizationDto>(async () =>
            Ok(await _invoker.ExecuteAsync(CurrentUser, _commands.ApplyOptimization(routeId))));

    /// <summary>Crea una ruta planificada por cada viaje del plan (como <c>POST api/dispatch/apply</c>) y deja la operación en el historial.</summary>
    [HttpPost("dispatch/apply")]
    public Task<ActionResult<DispatchApplyResultDto>> ApplyDispatchPlan([FromBody] DispatchPlanRequest request)
        => Guard<DispatchApplyResultDto>(async () =>
            Ok(await _invoker.ExecuteAsync(CurrentUser, _commands.ApplyDispatchPlan(request))));

    /// <summary>Lo que el usuario puede deshacer y rehacer ahora, de lo más reciente a lo más antiguo.</summary>
    [HttpGet("history")]
    public Task<ActionResult<CommandHistoryDto>> GetHistory()
        => Guard<CommandHistoryDto>(async () => Ok(await _invoker.GetHistoryAsync(CurrentUser)));

    /// <summary>Deshace la última operación del usuario (crear una ruta la elimina). 409 si ya no se puede (y se quita del historial).</summary>
    [HttpPost("undo")]
    public Task<ActionResult<CommandStepDto>> Undo()
        => Guard<CommandStepDto>(async () => Ok(await _invoker.UndoAsync(CurrentUser)));

    /// <summary>Rehace la última operación deshecha (volver a crear una ruta crea una NUEVA). 409 si ya no se puede (y se quita del historial).</summary>
    [HttpPost("redo")]
    public Task<ActionResult<CommandStepDto>> Redo()
        => Guard<CommandStepDto>(async () => Ok(await _invoker.RedoAsync(CurrentUser)));

    // Igual que en api/routes: el Location apunta a la ruta creada (GET api/routes/{id}), no a este controlador.
    private ActionResult<DeliveryRouteDto> CreatedRoute(DeliveryRouteDto route)
        => CreatedAtAction(nameof(DeliveryRoutesController.GetById), "DeliveryRoutes", new { id = route.Id }, route);

    // El historial es de cada usuario. El filtro de rol ya garantiza que hay un usuario autenticado.
    private string CurrentUser => User.Identity?.Name ?? throw new InvalidOperationException("No se pudo identificar al usuario.");

    /// <summary>Traduce las fallas de las órdenes a respuestas HTTP, igual en todas las acciones.</summary>
    private async Task<ActionResult<T>> Guard<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is CommandNotUndoableException or CommandNotRedoableException) // el estado cambió y ya no se puede deshacer ni rehacer (debe ir antes que InvalidOperationException)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) // datos inválidos, regla incumplida, nada que deshacer/rehacer
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
