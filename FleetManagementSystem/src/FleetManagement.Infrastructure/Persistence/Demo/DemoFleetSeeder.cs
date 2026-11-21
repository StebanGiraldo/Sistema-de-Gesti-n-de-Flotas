using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>
/// Amplía los datos de demostración sin tocar los originales: más operadores, más vehículos y más rutas con carga.
/// El orden importa (operadores → vehículos → rutas → mantenimiento) porque cada paso busca lo que creó el anterior.
/// Cada tabla de datos vive en su propio archivo; aquí solo se ordena la carga.
/// </summary>
public static class DemoFleetSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var vehicles = services.GetRequiredService<IVehicleRepository>();
        var users = services.GetRequiredService<IUserRepository>();
        var drivers = services.GetRequiredService<IDriverRepository>();
        var lookup = new DemoLookup(vehicles, users, drivers);

        await new DemoOperatorSeeder(users, drivers, services.GetRequiredService<IPasswordHasher>()).SeedAsync(DemoOperators.All);
        await new DemoVehicleSeeder(vehicles, lookup).SeedAsync(DemoVehicles.All);
        await new DemoRouteSeeder(services.GetRequiredService<IDeliveryRouteRepository>(), lookup).SeedAsync(DemoRoutes.All);
        await new DemoMaintenanceSeeder(services.GetRequiredService<IMaintenanceRepository>(), vehicles).SeedAsync(DemoMaintenanceHistory.All);
    }
}
