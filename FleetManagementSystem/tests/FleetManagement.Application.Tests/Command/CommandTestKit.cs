using FleetManagement.Application.Assignment;
using FleetManagement.Application.Builders;
using FleetManagement.Application.Commands;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Optimization;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Assignment;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Commands;
using NetTopologySuite.Geometries;

namespace FleetManagement.Application.Tests.Command;

/// <summary>
/// Todo lo que usan las órdenes, armado como en producción pero con repositorios en memoria: el servicio de rutas protegido
/// por las reglas de asignación, la optimización, el reparto, la foto de paradas, el eliminador de rutas, la fábrica de órdenes y el
/// invocador. Así las pruebas de las órdenes ejercitan los servicios reales, no copias.
/// </summary>
public sealed class CommandTestKit
{
    public const string Admin = "admin";

    public CommandTestKit()
    {
        var profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());
        var optimizer = new TwoOptRouteOptimizer(new NearestNeighborRouteOptimizer());

        Routes = new AssignmentValidatingRouteService(
            new DeliveryRouteService(Fleet.Routes, Fleet.Vehicles, Fleet.Drivers, new DeliveryRouteBuilder(), new DeliveryRouteDirector(), Audit, FleetMediators.CreateRouteColleague(Fleet.Routes, Fleet.Vehicles), profiles),
            Fleet.Validator);
        Optimization = new RouteOptimizationService(Fleet.Routes, Fleet.Vehicles, optimizer, Audit, profiles);
        Dispatch = new DispatchPlanningService(
            Fleet.Vehicles, Fleet.Routes, Fleet.Validator, Routes,
            new DispatchPlanner(new PriorityBestFitStrategy(), new PriorityStopSequencer(optimizer)),
            Audit, profiles);
        Snapshots = new RouteSnapshotService(Fleet.Routes);
        Deleter = new PlannedRouteDeleter(Fleet.Routes, Audit);
        Commands = new RouteCommandFactory(Routes, Optimization, Dispatch, Snapshots, Deleter);
        Invoker = new CommandInvoker(Store, Audit, Clock);
    }

    public AssignmentKit Fleet { get; } = new();
    public UserRecordingAuditLogger Audit { get; } = new();
    public ManualTimeProvider Clock { get; } = new();
    public InMemoryCommandHistoryStore Store { get; } = new();

    public IDeliveryRouteService Routes { get; }
    public RouteOptimizationService Optimization { get; }
    public DispatchPlanningService Dispatch { get; }
    public RouteSnapshotService Snapshots { get; }
    public PlannedRouteDeleter Deleter { get; }
    public RouteCommandFactory Commands { get; }
    public CommandInvoker Invoker { get; }

    private static Point PointAt(double longitude) => new(longitude, 0) { SRID = 4326 };

    /// <summary>Una ruta planificada sobre el ecuador con las paradas en zig-zag (A 0,5° → B 0,1° → C 0,3°): optimizarla da B → C → A.</summary>
    public async Task<DeliveryRoute> PlannedZigZagRouteAsync(string name = "Ruta zig-zag", DeliveryRouteStatus status = DeliveryRouteStatus.Planned)
    {
        var route = new DeliveryRoute
        {
            Name = name,
            Origin = PointAt(0),
            Destination = PointAt(0.6),
            Status = status,
            EstimatedDistanceKm = 155.7,
            EstimatedDurationMinutes = 117
        };
        route.Waypoints.Add(new Waypoint { Order = 0, Label = "A", Location = PointAt(0.5) });
        route.Waypoints.Add(new Waypoint { Order = 1, Label = "B", Location = PointAt(0.1) });
        route.Waypoints.Add(new Waypoint { Order = 2, Label = "C", Location = PointAt(0.3) });
        return await Fleet.Routes.AddAsync(route);
    }

    /// <summary>Las paradas de la ruta, tal como están guardadas ahora, por orden de visita.</summary>
    public async Task<string[]> StopLabelsAsync(Guid routeId)
        => (await Fleet.Routes.GetByIdAsync(routeId))!.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label).ToArray();

    public async Task<DeliveryRoute> RouteAsync(Guid routeId) => (await Fleet.Routes.GetByIdAsync(routeId))!;

    /// <summary>Si la ruta sigue guardada. Deshacer su creación la elimina: ya no existe (no queda «cancelada»).</summary>
    public async Task<bool> RouteExistsAsync(Guid routeId) => await Fleet.Routes.GetByIdAsync(routeId) is not null;

    public async Task<IReadOnlyList<DeliveryRoute>> AllRoutesAsync() => await Fleet.Routes.GetAllAsync();

    public async Task<Vehicle> AddVanAsync(string plate, double capacityKg = 10, VehicleStatus status = VehicleStatus.Available)
    {
        var van = new Vehicle { LicensePlate = plate, Type = VehicleType.Van, CapacityKg = capacityKg, Status = status };
        van.UpdateLocation(0, 0);
        return await Fleet.Vehicles.AddAsync(van);
    }

    public static CreateDeliveryRouteRequest NewRouteRequest(string name = "Ruta Norte", Guid? vehicleId = null)
        => new(name, 0, 0, 0, 0.6, null, null, vehicleId, null, null);

    /// <summary>Entregas de 6 kg a 0,1° y 0,2° del punto de salida: con furgonetas de 10 kg, cada una lleva solo una.</summary>
    public static DispatchPlanRequest TwoHeavyDeliveries()
        => new(0, 0, new List<DispatchDeliveryRequest>
        {
            new("Entrega 1", 6, 0, null, 0, 0.1),
            new("Entrega 2", 6, 0, null, 0, 0.2)
        });
}
