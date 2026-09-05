using FleetManagement.Application.Builders;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Rutas de reparto: creación (Builder), duplicación
/// (Prototype), consulta y transición de estados. También contiene la
/// heurística de estimación de distancia/tiempo que sustenta el módulo de
/// "optimización de rutas" del prototipo.
/// </summary>
public class DeliveryRouteService : IDeliveryRouteService
{
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IDeliveryRouteBuilder _routeBuilder; // BUILDER (Transient: una instancia nueva por cada request)
    private readonly DeliveryRouteDirector _routeDirector;
    private readonly IFleetAuditLogger _auditLogger;

    public DeliveryRouteService(
        IDeliveryRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IDeliveryRouteBuilder routeBuilder,
        DeliveryRouteDirector routeDirector,
        IFleetAuditLogger auditLogger)
    {
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _routeBuilder = routeBuilder;
        _routeDirector = routeDirector;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync()
    {
        var routes = await _routeRepository.GetAllAsync();
        return await MapManyToDtoAsync(routes);
    }

    public async Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id)
    {
        var route = await _routeRepository.GetByIdAsync(id);
        if (route is null) return null;
        var (vehicles, drivers) = await LoadLookupsAsync();
        return MapToDto(route, vehicles, drivers);
    }

    public async Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId)
    {
        var routes = await _routeRepository.GetByDriverIdAsync(driverId);
        var ordered = routes.OrderByDescending(r => r.ScheduledDate ?? r.CreatedAt).ToList();
        return await MapManyToDtoAsync(ordered);
    }

    /// <summary>Creación de una ruta general usando el patrón BUILDER paso a paso.</summary>
    public async Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request)
    {
        IDeliveryRouteBuilder builder = _routeBuilder
            .WithName(request.Name)
            .WithOrigin(request.OriginLat, request.OriginLng)
            .WithDestination(request.DestinationLat, request.DestinationLng);

        foreach (var wp in (request.Waypoints ?? new List<WaypointDto>()).OrderBy(w => w.Order))
            builder = builder.AddWaypoint(wp.Latitude, wp.Longitude, wp.Label);

        foreach (var cargo in request.CargoItems ?? new List<CargoItemDto>())
        {
            if (!Enum.TryParse<CargoPriority>(cargo.Priority, true, out var priority))
                priority = CargoPriority.Standard;
            builder = builder.AddCargoItem(cargo.Description, cargo.WeightKg, cargo.VolumeM3, priority);
        }

        if (request.AssignedVehicleId is { } vehicleId) builder = builder.AssignVehicle(vehicleId);
        if (request.AssignedDriverId is { } driverId) builder = builder.AssignDriver(driverId);
        if (request.ScheduledDate is { } date) builder = builder.ScheduleFor(date);

        var (distanceKm, minutes) = EstimateTrip(
            request.OriginLat, request.OriginLng,
            request.DestinationLat, request.DestinationLng,
            request.Waypoints);
        builder = builder.WithEstimatedTrip(distanceKm, minutes);

        var route = builder.Build();
        var saved = await _routeRepository.AddAsync(route);

        _auditLogger.LogEvent(
            "Ruta",
            $"Ruta '{saved.Name}' creada con {saved.Waypoints.Count} parada(s) y {saved.CargoItems.Count} artículo(s) de carga (patrón Builder). Distancia estimada: {distanceKm} km.");

        var (vehicles, drivers) = await LoadLookupsAsync();
        return MapToDto(saved, vehicles, drivers);
    }

    /// <summary>Creación rápida de una ruta "express" usando el Director del Builder.</summary>
    public async Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request)
    {
        var (distanceKm, minutes) = EstimateTrip(request.OriginLat, request.OriginLng, request.DestinationLat, request.DestinationLng, null);

        var route = _routeDirector.BuildExpressRoute(
            _routeBuilder,
            request.Name,
            request.OriginLat, request.OriginLng,
            request.DestinationLat, request.DestinationLng,
            request.AssignedVehicleId, request.AssignedDriverId,
            distanceKm, minutes);

        var saved = await _routeRepository.AddAsync(route);
        _auditLogger.LogEvent("Ruta", $"Ruta express '{saved.Name}' creada mediante DeliveryRouteDirector (Builder + Director).");

        var (vehicles, drivers) = await LoadLookupsAsync();
        return MapToDto(saved, vehicles, drivers);
    }

    /// <summary>Duplica una ruta existente (por ejemplo, para rutas recurrentes) usando PROTOTYPE.</summary>
    public async Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request)
    {
        var original = await _routeRepository.GetByIdAsync(routeId)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        var clone = original.Clone(); // PROTOTYPE
        clone.ScheduledDate = request.NewScheduledDate ?? DateTime.UtcNow.AddDays(1);

        var saved = await _routeRepository.AddAsync(clone);

        _auditLogger.LogEvent(
            "Ruta",
            $"Ruta '{saved.Name}' creada duplicando la ruta '{original.Name}' (patrón Prototype), programada para {saved.ScheduledDate:yyyy-MM-dd}.");

        var (vehicles, drivers) = await LoadLookupsAsync();
        return MapToDto(saved, vehicles, drivers);
    }

    public async Task UpdateRouteStatusAsync(Guid id, string status)
    {
        var route = await _routeRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        if (!Enum.TryParse<DeliveryRouteStatus>(status, true, out var newStatus))
            throw new ArgumentException($"Estado de ruta inválido: '{status}'.");

        route.Status = newStatus;
        await _routeRepository.UpdateAsync(route);

        // Mantiene coherente el estado del vehículo asignado con el estado de la ruta.
        if (route.AssignedVehicleId is { } vehicleId)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
            if (vehicle is not null)
            {
                vehicle.Status = newStatus switch
                {
                    DeliveryRouteStatus.InProgress or DeliveryRouteStatus.Delayed => VehicleStatus.EnRoute,
                    DeliveryRouteStatus.Completed or DeliveryRouteStatus.Cancelled => VehicleStatus.Available,
                    _ => vehicle.Status
                };
                await _vehicleRepository.UpdateAsync(vehicle);
            }
        }

        _auditLogger.LogEvent("Ruta", $"Ruta '{route.Name}' cambió su estado a {newStatus}.");
    }

    // ------------------------------------------------------------------
    // Estimación de distancia/tiempo (heurística de optimización de rutas)
    // ------------------------------------------------------------------

    private static (double distanceKm, double minutes) EstimateTrip(
        double originLat, double originLng,
        double destinationLat, double destinationLng,
        List<WaypointDto>? waypoints)
    {
        var points = new List<(double lat, double lng)> { (originLat, originLng) };
        if (waypoints is not null)
            points.AddRange(waypoints.OrderBy(w => w.Order).Select(w => (w.Latitude, w.Longitude)));
        points.Add((destinationLat, destinationLng));

        double totalKm = 0;
        for (var i = 0; i < points.Count - 1; i++)
            totalKm += HaversineDistanceKm(points[i], points[i + 1]);

        const double avgSpeedKmh = 55.0; // velocidad promedio asumida para vías mixtas urbanas/interurbanas
        var minutes = totalKm / avgSpeedKmh * 60;

        return (Math.Round(totalKm, 2), Math.Round(minutes, 1));
    }

    /// <summary>Distancia en línea recta entre dos coordenadas usando la fórmula de Haversine (radio terrestre 6371 km).</summary>
    private static double HaversineDistanceKm((double lat, double lng) a, (double lat, double lng) b)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(b.lat - a.lat);
        var dLng = ToRadians(b.lng - a.lng);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(a.lat)) * Math.Cos(ToRadians(b.lat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    // ------------------------------------------------------------------
    // Mapeo a DTOs
    // ------------------------------------------------------------------

    private async Task<(IReadOnlyList<Vehicle> vehicles, IReadOnlyList<Driver> drivers)> LoadLookupsAsync()
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var drivers = await _driverRepository.GetAllAsync();
        return (vehicles, drivers);
    }

    private async Task<IReadOnlyList<DeliveryRouteDto>> MapManyToDtoAsync(IReadOnlyList<DeliveryRoute> routes)
    {
        var (vehicles, drivers) = await LoadLookupsAsync();
        return routes.Select(r => MapToDto(r, vehicles, drivers)).ToList();
    }

    private static DeliveryRouteDto MapToDto(DeliveryRoute r, IReadOnlyList<Vehicle> vehicles, IReadOnlyList<Driver> drivers)
    {
        var vehicle = r.AssignedVehicleId is null ? null : vehicles.FirstOrDefault(v => v.Id == r.AssignedVehicleId);
        var driver = r.AssignedDriverId is null ? null : drivers.FirstOrDefault(d => d.Id == r.AssignedDriverId);

        return new DeliveryRouteDto(
            r.Id, r.Name,
            r.Origin.Y, r.Origin.X,
            r.Destination.Y, r.Destination.X,
            r.Waypoints.OrderBy(w => w.Order).Select(w => new WaypointDto(w.Location.Y, w.Location.X, w.Label, w.Order)).ToList(),
            r.CargoItems.Select(c => new CargoItemDto(c.Description, c.WeightKg, c.VolumeM3, c.Priority.ToString())).ToList(),
            r.AssignedVehicleId, vehicle?.LicensePlate,
            r.AssignedDriverId, driver?.FullName,
            r.Status.ToString(), r.EstimatedDistanceKm, r.EstimatedDurationMinutes,
            r.CreatedAt, r.ScheduledDate, r.DelayMinutes);
    }
}
