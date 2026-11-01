using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Commands;

public sealed class RouteCommandFactory : IRouteCommandFactory
{
    private readonly IDeliveryRouteService _routes;
    private readonly IRouteOptimizationService _optimization;
    private readonly IDispatchPlanningService _dispatch;
    private readonly IRouteSnapshotService _snapshots;
    private readonly IPlannedRouteCanceller _canceller;

    public RouteCommandFactory(
        IDeliveryRouteService routes,
        IRouteOptimizationService optimization,
        IDispatchPlanningService dispatch,
        IRouteSnapshotService snapshots,
        IPlannedRouteCanceller canceller)
    {
        _routes = routes;
        _optimization = optimization;
        _dispatch = dispatch;
        _snapshots = snapshots;
        _canceller = canceller;
    }

    public IUndoableCommand<DeliveryRouteDto> CreateRoute(CreateDeliveryRouteRequest request)
        => new CreateRouteCommand(_routes, _canceller, request);

    public IUndoableCommand<RouteOptimizationDto> ApplyOptimization(Guid routeId)
        => new ApplyRouteOptimizationCommand(_optimization, _snapshots, routeId);

    public IUndoableCommand<DispatchApplyResultDto> ApplyDispatchPlan(DispatchPlanRequest request)
        => new ApplyDispatchPlanCommand(_dispatch, _canceller, request);
}
