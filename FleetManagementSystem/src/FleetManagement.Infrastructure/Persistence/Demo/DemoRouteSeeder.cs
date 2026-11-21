using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Da de alta las rutas de demostración con sus paradas y su carga.</summary>
public sealed class DemoRouteSeeder
{
    private readonly IDeliveryRouteRepository _routes;
    private readonly DemoLookup _lookup;

    public DemoRouteSeeder(IDeliveryRouteRepository routes, DemoLookup lookup)
    {
        _routes = routes;
        _lookup = lookup;
    }

    public async Task SeedAsync(IEnumerable<DemoRoute> routes)
    {
        foreach (var spec in routes)
            await _routes.AddAsync(await BuildAsync(spec));
    }

    private async Task<DeliveryRoute> BuildAsync(DemoRoute spec)
    {
        var route = new DeliveryRoute
        {
            Name = spec.Name,
            Origin = DemoPlaces.Of(spec.Origin).ToPoint(),
            Destination = DemoPlaces.Of(spec.Destination).ToPoint(),
            AssignedVehicleId = await _lookup.VehicleIdAsync(spec.Plate),
            AssignedDriverId = await _lookup.DriverIdAsync(spec.DriverUsername),
            Status = spec.Status,
            EstimatedDistanceKm = spec.DistanceKm,
            EstimatedDurationMinutes = Math.Round(TripEstimate.Minutes(spec.DistanceKm, TripEstimate.DefaultSpeedKmh)),
            ScheduledDate = DateTime.UtcNow.AddDays(spec.DaysFromToday),
            DelayMinutes = spec.DelayMinutes
        };
        route.Waypoints.AddRange(spec.Via.Select((place, index) => ToWaypoint(place, index)));
        route.CargoItems.AddRange(spec.Cargo.Select(ToCargoItem));
        return route;
    }

    private static Waypoint ToWaypoint(DemoPlace place, int index)
    {
        var coordinates = DemoPlaces.Of(place);
        return new Waypoint { Order = index, Label = coordinates.Name, Location = coordinates.ToPoint() };
    }

    private static CargoItem ToCargoItem(DemoCargo cargo)
        => new() { Description = cargo.Description, WeightKg = cargo.WeightKg, VolumeM3 = cargo.VolumeM3, Priority = cargo.Priority };
}
