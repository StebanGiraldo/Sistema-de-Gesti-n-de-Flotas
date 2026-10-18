using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Simulation;

/// <summary>
/// Mueve los vehículos EnRoute a lo largo de la poligonal de su ruta activa (Origen → paradas → Destino).
///
/// Reglas: un vehículo EnRoute sin ruta activa NO se mueve (no hay movimiento aleatorio); una ruta retrasada avanza más
/// despacio; el kilometraje suma la distancia realmente recorrida sobre la ruta; al llegar, el vehículo queda en el
/// destino exacto y la ruta se completa a través de <see cref="IDeliveryRouteService"/>, que ya libera el vehículo y deja
/// la auditoría (esa lógica no se duplica aquí).
///
/// Cada paso solo recuerda un dato por viaje —los kilómetros recorridos, en <see cref="ITripProgressTracker"/>— y todo lo
/// demás (posición, siguiente parada) se calcula con <see cref="RoutePath"/>. Si el primer paso de una ruta encuentra al
/// vehículo lejos del origen, se le coloca en el origen y avanza desde ahí.
/// </summary>
public sealed class VehicleMovementSimulator : IVehicleMovementSimulator
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDeliveryRouteService _routeService;
    private readonly ITripProgressTracker _tracker;
    private readonly IVehicleTypeProfileFactory _profiles;
    private readonly SimulationOptions _options;

    public VehicleMovementSimulator(
        IDeliveryRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        IDeliveryRouteService routeService,
        ITripProgressTracker tracker,
        IVehicleTypeProfileFactory profiles,
        SimulationOptions options)
    {
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _routeService = routeService;
        _tracker = tracker;
        _profiles = profiles;
        _options = options;
    }

    public async Task AdvanceAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var routes = await _routeRepository.GetAllAsync();
        var vehicles = await _vehicleRepository.GetAllAsync();
        var trips = ActiveTrips.Find(routes, vehicles);

        // Si una ruta dejó de estar activa por un cambio manual de estado, su avance ya no tiene sentido.
        _tracker.ClearExcept(trips.Select(trip => trip.Route.Id).ToHashSet());

        foreach (var trip in trips)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AdvanceTripAsync(trip, elapsed);
        }
    }

    private async Task AdvanceTripAsync(ActiveTrip trip, TimeSpan elapsed)
    {
        var path = RoutePath.From(trip.Route);
        var speedKmh = TripSpeed.KmPerHour(_profiles.GetProfile(trip.Vehicle.Type), trip.Route.Status, _options);
        var previousKm = _tracker.TryGetCoveredKm(trip.Route.Id, out var covered) ? covered : 0;
        var currentKm = Math.Min(previousKm + speedKmh * elapsed.TotalHours, path.TotalKm);

        // Primero se guarda la nueva posición y después se completa la ruta: completarla relee el vehículo para liberarlo.
        await MoveVehicleAsync(trip.Vehicle, path.PointAt(currentKm), currentKm - previousKm);

        if (currentKm >= path.TotalKm)
            await CompleteTripAsync(trip.Route.Id);
        else
            _tracker.SetCoveredKm(trip.Route.Id, currentKm);
    }

    private async Task MoveVehicleAsync(Vehicle vehicle, GeoCoordinate position, double distanceKm)
    {
        vehicle.UpdateLocation(position.Latitude, position.Longitude);
        vehicle.MileageKm += distanceKm;
        await _vehicleRepository.UpdateAsync(vehicle);
    }

    private async Task CompleteTripAsync(Guid routeId)
    {
        await _routeService.UpdateRouteStatusAsync(routeId, nameof(DeliveryRouteStatus.Completed));
        _tracker.Clear(routeId);
    }
}
