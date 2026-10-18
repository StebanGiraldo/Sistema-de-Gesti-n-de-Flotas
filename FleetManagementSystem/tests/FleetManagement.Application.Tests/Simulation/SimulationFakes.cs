using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Simulation;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Simulation;
using static FleetManagement.Application.Tests.Simulation.SimulationTestData;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Dobles de prueba (escritos a mano, sin librerías de mocking, igual que el resto del proyecto) para probar el simulador
/// de movimiento y el servicio de progreso SIN depender de los servicios reales ni de esperar tiempo real.
/// </summary>
public sealed class EventLog
{
    private readonly List<string> _events = new();

    public IReadOnlyList<string> Events => _events;

    public void Add(string @event) => _events.Add(@event);
}

/// <summary>Estado de un vehículo en el instante en que se guardó: la entidad es mutable, así que sin esta copia no se vería el recorrido.</summary>
public sealed record VehicleSnapshot(Guid VehicleId, double Latitude, double Longitude, double MileageKm, VehicleStatus Status);

public sealed class FakeVehicleRepository : IVehicleRepository
{
    private readonly Dictionary<Guid, Vehicle> _vehicles = new();
    private readonly EventLog _log;

    public FakeVehicleRepository(EventLog log)
    {
        _log = log;
    }

    /// <summary>Una copia del vehículo por cada vez que el simulador lo guardó, en orden.</summary>
    public List<VehicleSnapshot> Updates { get; } = new();

    /// <summary>Cuántas veces se leyó toda la lista y cuántas un vehículo por id: así se comprueba que una consulta no hace N+1.</summary>
    public int GetAllCalls { get; private set; }
    public int GetByIdCalls { get; private set; }

    public Task<IReadOnlyList<Vehicle>> GetAllAsync()
    {
        GetAllCalls++;
        return Task.FromResult<IReadOnlyList<Vehicle>>(_vehicles.Values.ToList());
    }

    public Task<Vehicle?> GetByIdAsync(Guid id)
    {
        GetByIdCalls++;
        return Task.FromResult(_vehicles.GetValueOrDefault(id));
    }

    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlate) => throw new NotSupportedException();

    public Task<Vehicle> AddAsync(Vehicle vehicle)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.FromResult(vehicle);
    }

    public Task UpdateAsync(Vehicle vehicle)
    {
        _vehicles[vehicle.Id] = vehicle;
        Updates.Add(new VehicleSnapshot(vehicle.Id, vehicle.CurrentLocation.Y, vehicle.CurrentLocation.X, vehicle.MileageKm, vehicle.Status));
        _log.Add("vehicle.update");
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id) => throw new NotSupportedException();
}

public sealed class FakeRouteRepository : IDeliveryRouteRepository
{
    private readonly Dictionary<Guid, DeliveryRoute> _routes = new();

    public int GetAllCalls { get; private set; }
    public int GetByIdCalls { get; private set; }

    public Task<IReadOnlyList<DeliveryRoute>> GetAllAsync()
    {
        GetAllCalls++;
        return Task.FromResult<IReadOnlyList<DeliveryRoute>>(_routes.Values.ToList());
    }

    public Task<DeliveryRoute?> GetByIdAsync(Guid id)
    {
        GetByIdCalls++;
        return Task.FromResult(_routes.GetValueOrDefault(id));
    }

    public Task<IReadOnlyList<DeliveryRoute>> GetByDriverIdAsync(Guid driverId) => throw new NotSupportedException();

    public Task<DeliveryRoute> AddAsync(DeliveryRoute route)
    {
        _routes[route.Id] = route;
        return Task.FromResult(route);
    }

    public Task UpdateAsync(DeliveryRoute route)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Servicio de rutas que solo registra qué cambios de estado se le pidieron. El simulador no debe duplicar la lógica de
/// "completar una ruta" (liberar el vehículo, auditar): solo debe pedírsela a este servicio, y eso es lo que se comprueba.
/// </summary>
public sealed class RecordingRouteService : IDeliveryRouteService
{
    private readonly EventLog _log;

    public RecordingRouteService(EventLog log)
    {
        _log = log;
    }

    public List<(Guid RouteId, string Status)> StatusChanges { get; } = new();

    public Task UpdateRouteStatusAsync(Guid id, string status)
    {
        StatusChanges.Add((id, status));
        _log.Add($"route.status:{status}");
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync() => throw new NotSupportedException();
    public Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id) => throw new NotSupportedException();
    public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) => throw new NotSupportedException();
}

/// <summary>Fábrica de perfiles (Flyweight) de prueba: todos los tipos viajan a la misma velocidad y anota qué tipos le pidieron.</summary>
public sealed class StubProfileFactory : IVehicleTypeProfileFactory
{
    private readonly double _speedKmh;

    public StubProfileFactory(double speedKmh)
    {
        _speedKmh = speedKmh;
    }

    public List<VehicleType> Requested { get; } = new();

    public VehicleTypeProfile GetProfile(VehicleType type)
    {
        Requested.Add(type);
        return new VehicleTypeProfile(type, type.ToString(), _speedKmh, false, false, false, Array.Empty<MaintenanceTaskTemplate>());
    }
}

/// <summary>
/// Simulador armado con dobles de prueba y la configuración por defecto, más atajos para dejar un viaje listo. Usa la
/// fábrica de perfiles REAL (con los valores de la Abstract Factory) salvo que la prueba entregue otra. Las pruebas del
/// simulador y las del servicio de progreso lo comparten para que ambos vean exactamente el mismo mundo.
/// </summary>
public sealed class SimulatorRig
{
    public SimulatorRig(IVehicleTypeProfileFactory? profiles = null)
    {
        Profiles = profiles ?? new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());
        Vehicles = new FakeVehicleRepository(Log);
        RouteService = new RecordingRouteService(Log);
        Simulator = new VehicleMovementSimulator(Routes, Vehicles, RouteService, Tracker, Profiles, Options);
    }

    public EventLog Log { get; } = new();
    public FakeVehicleRepository Vehicles { get; }
    public FakeRouteRepository Routes { get; } = new();
    public RecordingRouteService RouteService { get; }
    public InMemoryTripProgressTracker Tracker { get; } = new();
    public IVehicleTypeProfileFactory Profiles { get; }

    /// <summary>Configuración de la simulación; una prueba puede cambiarla antes de avanzar (el simulador la lee en cada paso).</summary>
    public SimulationOptions Options { get; } = new();

    public VehicleMovementSimulator Simulator { get; }

    /// <summary>Deja registrado el vehículo y la ruta dados, y asigna la ruta al vehículo.</summary>
    public async Task<ActiveTrip> AddTripAsync(Vehicle vehicle, DeliveryRoute route)
    {
        route.AssignedVehicleId = vehicle.Id;
        await Vehicles.AddAsync(vehicle);
        await Routes.AddAsync(route);
        return new ActiveTrip(route, vehicle);
    }

    /// <summary>
    /// Crea un vehículo del tipo dado (en recorrido, en el origen del ecuador) y le asigna la ruta indicada o, si no se da
    /// ninguna, la ruta del ecuador. El estado de la ruta y del vehículo se pueden fijar para armar casos que NO son viajes activos.
    /// </summary>
    public Task<ActiveTrip> AddTripAsync(
        VehicleType type,
        DeliveryRoute? route = null,
        DeliveryRouteStatus routeStatus = DeliveryRouteStatus.InProgress,
        VehicleStatus vehicleStatus = VehicleStatus.EnRoute)
    {
        var vehicle = EnRouteVehicle(type);
        vehicle.Status = vehicleStatus;
        var assigned = route ?? EquatorRoute();
        assigned.Status = routeStatus;
        return AddTripAsync(vehicle, assigned);
    }

    public Task AdvanceAsync(TimeSpan elapsed) => Simulator.AdvanceAsync(elapsed);

    /// <summary>Kilómetros que lleva recorridos la ruta según el tracker (falla si todavía no tiene avance).</summary>
    public double CoveredKm(DeliveryRoute route)
        => Tracker.TryGetCoveredKm(route.Id, out var coveredKm)
            ? coveredKm
            : throw new InvalidOperationException("La ruta no tiene avance registrado.");

    public double CoveredKm(ActiveTrip trip) => CoveredKm(trip.Route);
}
