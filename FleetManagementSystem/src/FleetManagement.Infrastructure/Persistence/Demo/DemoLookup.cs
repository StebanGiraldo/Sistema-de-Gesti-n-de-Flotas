using FleetManagement.Application.Interfaces.Repositories;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Busca los identificadores que necesitan las rutas y el mantenimiento: vehículo por placa y conductor por usuario.</summary>
public sealed class DemoLookup
{
    private readonly IVehicleRepository _vehicles;
    private readonly IUserRepository _users;
    private readonly IDriverRepository _drivers;

    public DemoLookup(IVehicleRepository vehicles, IUserRepository users, IDriverRepository drivers)
    {
        _vehicles = vehicles;
        _users = users;
        _drivers = drivers;
    }

    public async Task<Guid> VehicleIdAsync(string plate)
    {
        var vehicle = await _vehicles.GetByLicensePlateAsync(plate)
            ?? throw new InvalidOperationException($"Dato de demostración inconsistente: no existe el vehículo '{plate}'.");
        return vehicle.Id;
    }

    public async Task<Guid> DriverIdAsync(string username)
    {
        var user = await _users.GetByUsernameAsync(username)
            ?? throw new InvalidOperationException($"Dato de demostración inconsistente: no existe el usuario '{username}'.");
        var driver = await _drivers.GetByUserIdAsync(user.Id)
            ?? throw new InvalidOperationException($"Dato de demostración inconsistente: '{username}' no tiene conductor.");
        return driver.Id;
    }
}
