using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Da de alta el historial de mantenimiento de demostración.</summary>
public sealed class DemoMaintenanceSeeder
{
    private readonly IMaintenanceRepository _maintenance;
    private readonly IVehicleRepository _vehicles;

    public DemoMaintenanceSeeder(IMaintenanceRepository maintenance, IVehicleRepository vehicles)
    {
        _maintenance = maintenance;
        _vehicles = vehicles;
    }

    public async Task SeedAsync(IEnumerable<DemoMaintenance> history)
    {
        foreach (var spec in history)
            await _maintenance.AddAsync(await BuildAsync(spec));
    }

    private async Task<MaintenanceRecord> BuildAsync(DemoMaintenance spec)
    {
        var vehicle = await _vehicles.GetByLicensePlateAsync(spec.Plate)
            ?? throw new InvalidOperationException($"Dato de demostración inconsistente: no existe el vehículo '{spec.Plate}'.");
        return new MaintenanceRecord
        {
            VehicleId = vehicle.Id,
            Type = spec.Type,
            PerformedAt = DateTime.UtcNow.AddMonths(-spec.MonthsAgo),
            NextDueDate = DateTime.UtcNow.AddDays(spec.DueInDays),
            NextDueMileageKm = vehicle.MileageKm + spec.KmUntilDue,
            MileageAtServiceKm = vehicle.MileageKm - spec.KmSinceService,
            Notes = spec.Notes
        };
    }
}
