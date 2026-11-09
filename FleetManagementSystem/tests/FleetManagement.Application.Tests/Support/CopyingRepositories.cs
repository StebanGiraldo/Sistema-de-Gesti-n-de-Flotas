using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Repositorio de vehículos de prueba que se comporta como una base de datos real: guarda y entrega COPIAS. Lo que una prueba o
/// un servicio cambie en el objeto que leyó solo queda guardado si luego llama a <see cref="UpdateAsync"/>. El repositorio
/// en memoria del proyecto entrega siempre la misma instancia, así que con él una escritura olvidada pasa inadvertida (el cambio
/// ya está hecho sobre el objeto compartido); con este doble, no.
/// </summary>
public sealed class CopyingVehicleRepository : IVehicleRepository
{
    private readonly Dictionary<Guid, Vehicle> _stored = new();

    public Task<IReadOnlyList<Vehicle>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<Vehicle>>(_stored.Values.Select(Copy).ToList());

    public Task<Vehicle?> GetByIdAsync(Guid id)
        => Task.FromResult<Vehicle?>(_stored.TryGetValue(id, out var vehicle) ? Copy(vehicle) : null);

    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlate)
    {
        var found = _stored.Values.FirstOrDefault(vehicle => vehicle.LicensePlate == licensePlate);
        return Task.FromResult<Vehicle?>(found is null ? null : Copy(found));
    }

    public Task<Vehicle> AddAsync(Vehicle vehicle)
    {
        _stored[vehicle.Id] = Copy(vehicle);
        return Task.FromResult(Copy(vehicle));
    }

    public Task UpdateAsync(Vehicle vehicle)
    {
        _stored[vehicle.Id] = Copy(vehicle);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _stored.Remove(id);
        return Task.CompletedTask;
    }

    private static Vehicle Copy(Vehicle vehicle) => new()
    {
        Id = vehicle.Id,
        LicensePlate = vehicle.LicensePlate,
        Brand = vehicle.Brand,
        Model = vehicle.Model,
        Year = vehicle.Year,
        Type = vehicle.Type,
        Status = vehicle.Status,
        CapacityKg = vehicle.CapacityKg,
        CapacityM3 = vehicle.CapacityM3,
        MileageKm = vehicle.MileageKm,
        CurrentLocation = vehicle.CurrentLocation,
        AssignedDriverId = vehicle.AssignedDriverId,
        RegisteredAt = vehicle.RegisteredAt,
        LastMaintenanceDate = vehicle.LastMaintenanceDate
    };
}

/// <summary>
/// Repositorio de rutas de prueba que guarda y entrega copias (paradas incluidas), como lo haría una base de datos real. Sirve
/// para comprobar que un componente realmente GUARDA lo que cambia y no solo lo modifica en el objeto que leyó.
/// </summary>
public sealed class CopyingRouteRepository : IDeliveryRouteRepository
{
    private readonly Dictionary<Guid, DeliveryRoute> _stored = new();

    public Task<IReadOnlyList<DeliveryRoute>> GetAllAsync()
        => Task.FromResult<IReadOnlyList<DeliveryRoute>>(_stored.Values.Select(Copy).ToList());

    public Task<DeliveryRoute?> GetByIdAsync(Guid id)
        => Task.FromResult<DeliveryRoute?>(_stored.TryGetValue(id, out var route) ? Copy(route) : null);

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverIdAsync(Guid driverId)
        => Task.FromResult<IReadOnlyList<DeliveryRoute>>(_stored.Values.Where(route => route.AssignedDriverId == driverId).Select(Copy).ToList());

    public Task<DeliveryRoute> AddAsync(DeliveryRoute route)
    {
        _stored[route.Id] = Copy(route);
        return Task.FromResult(Copy(route));
    }

    public Task UpdateAsync(DeliveryRoute route)
    {
        _stored[route.Id] = Copy(route);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _stored.Remove(id);
        return Task.CompletedTask;
    }

    private static DeliveryRoute Copy(DeliveryRoute route) => new()
    {
        Id = route.Id,
        Name = route.Name,
        Origin = route.Origin,
        Destination = route.Destination,
        Waypoints = route.Waypoints.Select(waypoint => new Waypoint { Id = waypoint.Id, Order = waypoint.Order, Label = waypoint.Label, Location = waypoint.Location }).ToList(),
        CargoItems = route.CargoItems.ToList(),
        AssignedVehicleId = route.AssignedVehicleId,
        AssignedDriverId = route.AssignedDriverId,
        Status = route.Status,
        EstimatedDistanceKm = route.EstimatedDistanceKm,
        EstimatedDurationMinutes = route.EstimatedDurationMinutes,
        CreatedAt = route.CreatedAt,
        ScheduledDate = route.ScheduledDate,
        DelayMinutes = route.DelayMinutes
    };
}
