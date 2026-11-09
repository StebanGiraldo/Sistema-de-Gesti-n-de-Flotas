using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

public sealed class RouteCommandFactory : IRouteCommandFactory
{
    private readonly IDeliveryRouteService _routes;
    private readonly IRouteOptimizationService _optimization;
    private readonly IDispatchPlanningService _dispatch;
    private readonly IRouteSnapshotService _snapshots;
    private readonly IPlannedRouteDeleter _deleter;

    public RouteCommandFactory(
        IDeliveryRouteService routes,
        IRouteOptimizationService optimization,
        IDispatchPlanningService dispatch,
        IRouteSnapshotService snapshots,
        IPlannedRouteDeleter deleter)
    {
        _routes = routes;
        _optimization = optimization;
        _dispatch = dispatch;
        _snapshots = snapshots;
        _deleter = deleter;
    }

    public IUndoableCommand<DeliveryRouteDto> CreateRoute(CreateDeliveryRouteRequest request)
        => new CreateRouteCommand(_routes, _deleter, request);

    public IUndoableCommand<DeliveryRouteDto> CreateExpressRoute(CreateExpressRouteRequest request)
        => new CreateExpressRouteCommand(_routes, _deleter, request);

    public IUndoableCommand<DeliveryRouteDto> DuplicateRoute(Guid routeId, DuplicateRouteRequest request)
        => new DuplicateRouteCommand(_routes, _deleter, routeId, request);

    public IUndoableCommand<RouteOptimizationDto> ApplyOptimization(Guid routeId)
        => new ApplyRouteOptimizationCommand(_optimization, _snapshots, routeId);

    public IUndoableCommand<DispatchApplyResultDto> ApplyDispatchPlan(DispatchPlanRequest request)
        => new ApplyDispatchPlanCommand(_dispatch, _deleter, request);
}
