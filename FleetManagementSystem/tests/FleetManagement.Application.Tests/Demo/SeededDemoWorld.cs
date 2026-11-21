using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Assignment;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Infrastructure.Persistence;
using FleetManagement.Infrastructure.Security;

namespace FleetManagement.Application.Tests.Demo;

/// <summary>Proveedor de servicios mínimo: entrega justo lo que necesita el sembrador, sin montar el contenedor de la aplicación.</summary>
public sealed class DictionaryServices : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public DictionaryServices Add<T>(T service) where T : notnull
    {
        _services[typeof(T)] = service;
        return this;
    }

    public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);
}

/// <summary>
/// Los datos de demostración REALES de la aplicación (sembrador original + ampliación) cargados en repositorios en memoria,
/// con los servicios reales de mantenimiento y de reglas de asignación encima.
/// </summary>
public sealed class SeededDemoWorld
{
    private SeededDemoWorld()
    {
        Maintenance = new MaintenanceService(MaintenanceRepository, VehicleRepository, DriverRepository, new RecordingAuditLogger(), new RecordingNotificationChannel());
        Validator = new RouteAssignmentValidator(VehicleRepository, DriverRepository, RouteRepository, Maintenance);
    }

    public InMemoryVehicleRepository VehicleRepository { get; } = new();
    public InMemoryDriverRepository DriverRepository { get; } = new();
    public InMemoryDeliveryRouteRepository RouteRepository { get; } = new();
    public InMemoryUserRepository UserRepository { get; } = new();
    public InMemoryMaintenanceRepository MaintenanceRepository { get; } = new();
    public PasswordHasher Hasher { get; } = new();
    public MaintenanceService Maintenance { get; }
    public RouteAssignmentValidator Validator { get; }

    public static async Task<SeededDemoWorld> CreateAsync()
    {
        var world = new SeededDemoWorld();
        var services = new DictionaryServices()
            .Add<IVehicleRepository>(world.VehicleRepository)
            .Add<IDriverRepository>(world.DriverRepository)
            .Add<IDeliveryRouteRepository>(world.RouteRepository)
            .Add<IUserRepository>(world.UserRepository)
            .Add<IMaintenanceRepository>(world.MaintenanceRepository)
            .Add<IPasswordHasher>(world.Hasher);

        await InMemoryDataSeeder.SeedAsync(services);
        return world;
    }
}
