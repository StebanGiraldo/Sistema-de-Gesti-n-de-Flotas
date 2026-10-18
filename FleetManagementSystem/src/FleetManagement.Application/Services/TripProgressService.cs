using FleetManagement.Application.DTOs;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Simulation;

namespace FleetManagement.Application.Services;

/// <summary>
/// Informa el avance de los viajes activos. Hace dos lecturas (rutas y vehículos) sin importar cuántos viajes haya: el
/// frontend consulta cada pocos segundos y pedir cada viaje por separado sería el problema N+1.
///
/// Es de solo lectura: lo que hace avanzar los viajes es el simulador. Para que lo que se muestra coincida con lo que
/// ocurre, usa las mismas reglas que él: qué es un viaje activo (<see cref="ActiveTrips"/>) y a qué velocidad avanza
/// (<see cref="TripSpeed"/>), de modo que la ETA es el tiempo que realmente tardará la simulación en llegar.
/// </summary>
public class TripProgressService : ITripProgressService
{
    private const int PercentDecimals = 1;
    private const int DistanceDecimals = 3;
    private const int MinutesDecimals = 1;
    private const int CoordinateDecimals = 6;

    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITripProgressTracker _tracker;
    private readonly IVehicleTypeProfileFactory _profiles;
    private readonly SimulationOptions _options;

    public TripProgressService(
        IDeliveryRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        ITripProgressTracker tracker,
        IVehicleTypeProfileFactory profiles,
        SimulationOptions options)
    {
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _tracker = tracker;
        _profiles = profiles;
        _options = options;
    }

    public async Task<IReadOnlyList<TripProgressDto>> GetActiveTripsAsync()
    {
        var routes = await _routeRepository.GetAllAsync();
        var vehicles = await _vehicleRepository.GetAllAsync();

        return ActiveTrips.Find(routes, vehicles).Select(ToDto).ToList();
    }

    private TripProgressDto ToDto(ActiveTrip trip)
    {
        var path = RoutePath.From(trip.Route);
        return ToRoundedDto(trip, ProgressOf(trip, path), path.Stops);
    }

    // Mismas reglas que el simulador: los kilómetros recorridos salen del tracker y la velocidad de TripSpeed.
    private TripProgress ProgressOf(ActiveTrip trip, RoutePath path)
    {
        var coveredKm = _tracker.TryGetCoveredKm(trip.Route.Id, out var tracked) ? tracked : 0;
        var speedKmh = TripSpeed.KmPerHour(_profiles.GetProfile(trip.Vehicle.Type), trip.Route.Status, _options);
        return path.ProgressAt(trip.Vehicle.Id, coveredKm, speedKmh);
    }

    // Solo redondea a la precisión que tiene sentido mostrar (metros, décimas de minuto); no calcula nada nuevo.
    private static TripProgressDto ToRoundedDto(ActiveTrip trip, TripProgress progress, IEnumerable<RouteStop> stops)
        => new(
            trip.Route.Id,
            trip.Route.Name,
            trip.Vehicle.Id,
            trip.Vehicle.LicensePlate,
            trip.Route.Status.ToString(),
            Math.Round(progress.DistanceCoveredKm, DistanceDecimals),
            Math.Round(progress.TotalKm, DistanceDecimals),
            Math.Round(progress.Percent, PercentDecimals),
            Math.Round(progress.Latitude, CoordinateDecimals),
            Math.Round(progress.Longitude, CoordinateDecimals),
            progress.NextStopLabel,
            Math.Round(progress.RemainingKm, DistanceDecimals),
            Math.Round(progress.EtaMinutes, MinutesDecimals),
            progress.Arrived,
            stops.Select(ToStopDto).ToList());

    private static TripStopDto ToStopDto(RouteStop stop)
        => new(stop.Label, stop.Location.Latitude, stop.Location.Longitude);
}
