using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// Repositorio Mock en memoria para Vehicle. Se registra como Singleton en
/// el contenedor de DI para que los datos persistan durante la vida del
/// proceso (si se registrara como Scoped/Transient, cada petición HTTP
/// vería una colección vacía nueva). Queda preparado para sustituirse por
/// una implementación EfVehicleRepository basada en FleetDbContext sin que
/// ninguna otra capa deba cambiar (DIP).
/// </summary>
public class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly ConcurrentDictionary<Guid, Vehicle> _vehicles = new();

    public Task<IReadOnlyList<Vehicle>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<Vehicle>)_vehicles.Values.OrderBy(v => v.LicensePlate).ToList());

    public Task<Vehicle?> GetByIdAsync(Guid id)
        => Task.FromResult(_vehicles.TryGetValue(id, out var vehicle) ? vehicle : null);

    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlate)
        => Task.FromResult(_vehicles.Values.FirstOrDefault(v => string.Equals(v.LicensePlate, licensePlate, StringComparison.OrdinalIgnoreCase)));

    public Task<Vehicle> AddAsync(Vehicle vehicle)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.FromResult(vehicle);
    }

    public Task UpdateAsync(Vehicle vehicle)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _vehicles.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
