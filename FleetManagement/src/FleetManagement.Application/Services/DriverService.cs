using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;

namespace FleetManagement.Application.Services;

/// <summary>Caso de uso de Conductores: listado simple usado por los selectores del dashboard y el portal de operador.</summary>
public class DriverService : IDriverService
{
    private readonly IDriverRepository _driverRepository;
    private readonly IVehicleRepository _vehicleRepository;

    public DriverService(IDriverRepository driverRepository, IVehicleRepository vehicleRepository)
    {
        _driverRepository = driverRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<IReadOnlyList<DriverDto>> GetAllDriversAsync()
    {
        var drivers = await _driverRepository.GetAllAsync();
        var vehicles = await _vehicleRepository.GetAllAsync();

        return drivers.Select(d =>
        {
            var vehicle = d.AssignedVehicleId is null ? null : vehicles.FirstOrDefault(v => v.Id == d.AssignedVehicleId);
            return new DriverDto(d.Id, d.FullName, d.LicenseNumber, d.Phone, d.AssignedVehicleId, vehicle?.LicensePlate);
        }).ToList();
    }
}
