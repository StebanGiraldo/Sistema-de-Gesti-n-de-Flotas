using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence;

public class InMemoryMaintenanceRepository : IMaintenanceRepository
{
    private readonly ConcurrentDictionary<Guid, MaintenanceRecord> _records = new();

    public Task<IReadOnlyList<MaintenanceRecord>> GetAllAsync()
        => Task.FromResult((IReadOnlyList<MaintenanceRecord>)_records.Values.ToList());

    public Task<IReadOnlyList<MaintenanceRecord>> GetByVehicleIdAsync(Guid vehicleId)
        => Task.FromResult((IReadOnlyList<MaintenanceRecord>)_records.Values.Where(r => r.VehicleId == vehicleId).ToList());

    public Task<MaintenanceRecord> AddAsync(MaintenanceRecord record)
    {
        _records[record.Id] = record;
        return Task.FromResult(record);
    }
}
