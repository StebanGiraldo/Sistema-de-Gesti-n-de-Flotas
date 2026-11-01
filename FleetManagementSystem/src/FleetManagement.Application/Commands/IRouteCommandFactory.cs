using FleetManagement.Application.DTOs;

namespace FleetManagement.Application.Commands;

/// <summary>Arma cada comando con sus dependencias, para que quien lo pide (el controlador) no las conozca.</summary>
public interface IRouteCommandFactory
{
    IUndoableCommand<DeliveryRouteDto> CreateRoute(CreateDeliveryRouteRequest request);
    IUndoableCommand<RouteOptimizationDto> ApplyOptimization(Guid routeId);
    IUndoableCommand<DispatchApplyResultDto> ApplyDispatchPlan(DispatchPlanRequest request);
}
