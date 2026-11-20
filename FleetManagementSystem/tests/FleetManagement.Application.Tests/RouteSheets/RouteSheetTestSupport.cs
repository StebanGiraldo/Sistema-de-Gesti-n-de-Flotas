using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.RouteSheets;
using FleetManagement.Application.Services;
using FleetManagement.Application.Simulation;
using FleetManagement.Application.Tests.Proxy;
using FleetManagement.Application.Tests.Simulation;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Simulation;

namespace FleetManagement.Application.Tests.RouteSheets;

/// <summary>Bitácora compartida: cada doble anota aquí qué paso se ejecutó, para comprobar el orden exacto del esqueleto.</summary>
public sealed class CallLog
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry) => _entries.Add(entry);
}

public sealed class FakeSourceLoader : IRouteSheetSourceLoader
{
    private readonly CallLog _log;
    private readonly RouteSheetSource? _source;
    private readonly Exception? _failure;

    public FakeSourceLoader(CallLog log, RouteSheetSource? source = null, Exception? failure = null)
    {
        _log = log;
        _source = source;
        _failure = failure;
    }

    public Task<RouteSheetSource> LoadAsync(Guid routeId)
    {
        _log.Add("load");
        return _failure is null ? Task.FromResult(_source!) : Task.FromException<RouteSheetSource>(_failure);
    }
}

public sealed class FakeTripProgress : ITripProgressService
{
    private readonly CallLog _log;

    public FakeTripProgress(CallLog log) => _log = log;

    public List<TripProgressDto> Trips { get; } = new();

    public Task<IReadOnlyList<TripProgressDto>> GetActiveTripsAsync()
    {
        _log.Add("trips");
        return Task.FromResult<IReadOnlyList<TripProgressDto>>(Trips.ToList());
    }
}

public sealed class FakeNavigation : INavigationService
{
    public const string Url = "https://navegacion.test/ruta";

    private readonly CallLog _log;

    public FakeNavigation(CallLog log) => _log = log;

    public int Calls { get; private set; }

    public Task<NavigationLinkDto> GenerateExternalNavigationLinkAsync(Guid routeId)
    {
        Calls++;
        _log.Add("navigation-link");
        return Task.FromResult(new NavigationLinkDto(routeId, Url, new List<WaypointDto>()));
    }
}

/// <summary>Variante de prueba que anota cada paso variable que el esqueleto le pide y devuelve algo reconocible.</summary>
public sealed class SpyTemplate : RouteSheetTemplate
{
    public const string NavigationUrl = "spy-url";

    private readonly CallLog _log;

    public SpyTemplate(CallLog log, RouteSheetCollaborators collaborators)
        : base(RouteSheetAudience.Driver, collaborators)
    {
        _log = log;
    }

    public RouteSheetSource? Seen { get; private set; }

    protected override RouteSheetHeader BuildHeader(RouteSheetSource source)
    {
        Seen = source;
        _log.Add("header");
        return RouteSheetHeader.From(source, "spy");
    }

    protected override IReadOnlyList<TripStopDto> BuildStops(RouteSheetSource source)
    {
        _log.Add("stops");
        return new[] { new TripStopDto("S", 1, 2) };
    }

    protected override RouteSheetCargo? BuildCargo(RouteSheetSource source)
    {
        _log.Add("cargo");
        return new RouteSheetCargo(1, 2, 3, 4, 5, null, false);
    }

    protected override RouteSheetProgress? BuildProgress(RouteSheetSource source)
    {
        _log.Add("progress");
        return new RouteSheetProgress(Percent: 50);
    }

    protected override Task<string?> BuildNavigationAsync(RouteSheetSource source)
    {
        _log.Add("navigation");
        return Task.FromResult<string?>(NavigationUrl);
    }
}

/// <summary>Variante mínima: solo decide lo que es obligatorio (encabezado y avance). Todo lo demás lo deja en su valor por defecto.</summary>
public sealed class MinimalTemplate : RouteSheetTemplate
{
    public MinimalTemplate(RouteSheetCollaborators collaborators)
        : base(RouteSheetAudience.Dispatcher, collaborators)
    {
    }

    protected override RouteSheetHeader BuildHeader(RouteSheetSource source) => RouteSheetHeader.From(source, "mínima");

    protected override RouteSheetProgress? BuildProgress(RouteSheetSource source) => null;
}

/// <summary>Datos de prueba de las hojas de ruta.</summary>
public static class SheetData
{
    public static Vehicle Vehicle(
        string plate = "VAN-100",
        double capacityKg = 1500,
        double? capacityM3 = null,
        VehicleStatus status = VehicleStatus.EnRoute)
        => new() { LicensePlate = plate, Type = VehicleType.Van, Status = status, CapacityKg = capacityKg, CapacityM3 = capacityM3, MileageKm = 1000 };

    public static DeliveryRoute Route(Vehicle? vehicle = null, Guid? driverId = null, params CargoItem[] cargo)
    {
        var route = new DeliveryRoute
        {
            Name = "Ruta de prueba",
            Status = DeliveryRouteStatus.InProgress,
            AssignedVehicleId = vehicle?.Id,
            AssignedDriverId = driverId,
            ScheduledDate = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc)
        };
        route.CargoItems.AddRange(cargo);
        return route;
    }

    public static CargoItem Cargo(double weightKg, double volumeM3 = 0)
        => new() { Description = "Carga de prueba", WeightKg = weightKg, VolumeM3 = volumeM3 };

    public static Driver Driver(string name = "Ana Pérez") => new() { FullName = name, LicenseNumber = "LIC-1", Phone = "300" };

    public static RouteSheetSource Source(Vehicle vehicle, DeliveryRoute route, Driver? driver = null, TripProgressDto? trip = null)
        => new(route, vehicle, driver, trip);

    public static TripProgressDto Trip(Guid routeId, double percent = 40, double remainingKm = 20, string nextStop = "Segunda", double etaMinutes = 12)
        => new(
            RouteId: routeId, RouteName: "Ruta de prueba", VehicleId: Guid.NewGuid(), LicensePlate: "VAN-100", RouteStatus: "InProgress",
            DistanceCoveredKm: 10, TotalKm: 30, Percent: percent, Latitude: 0, Longitude: 0, NextStopLabel: nextStop,
            RemainingKm: remainingKm, EtaMinutes: etaMinutes, Arrived: false, Stops: Array.Empty<TripStopDto>());

    /// <summary>Colaboradores de doble para probar el esqueleto sin repositorios.</summary>
    public static RouteSheetCollaborators Doubles(CallLog log, RouteSheetSource? source = null, Exception? failure = null)
        => new(new FakeSourceLoader(log, source, failure), new FakeNavigation(log), new FakeTripProgress(log));
}

/// <summary>
/// Mundo con los servicios REALES (navegación con el proveedor de conteo, avance de viajes, repositorios en memoria): sirve para
/// comprobar que las hojas muestran exactamente lo que esos servicios entregan.
/// </summary>
public sealed class RouteSheetWorld
{
    public RouteSheetWorld()
    {
        Navigation = new NavigationService(Fleet.RouteRepository, NavigationProvider);
        Progress = new TripProgressService(
            Fleet.RouteRepository,
            Fleet.VehicleRepository,
            new InMemoryTripProgressTracker(),
            new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider()),
            new SimulationOptions());
        Collaborators = new RouteSheetCollaborators(
            new RouteSheetSourceLoader(Fleet.RouteRepository, Fleet.VehicleRepository, Fleet.DriverRepository),
            Navigation,
            Progress);
        Sheets = new RouteSheetProvider(AllTemplates());
    }

    public RealFleet Fleet { get; } = new();
    public CountingNavigationProvider NavigationProvider { get; } = new();
    public NavigationService Navigation { get; }
    public TripProgressService Progress { get; }
    public RouteSheetCollaborators Collaborators { get; }
    public RouteSheetProvider Sheets { get; }

    public IReadOnlyList<RouteSheetTemplate> AllTemplates()
        => new RouteSheetTemplate[] { new DriverRouteSheet(Collaborators), new DispatcherRouteSheet(Collaborators) };

    /// <summary>Un vehículo y la ruta del ecuador asignada a él (origen, «Primera», «Segunda», destino).</summary>
    public async Task<(Vehicle Vehicle, DeliveryRoute Route)> AddEquatorTripAsync(
        VehicleStatus vehicleStatus = VehicleStatus.EnRoute,
        DeliveryRouteStatus routeStatus = DeliveryRouteStatus.InProgress,
        Driver? driver = null,
        int delayMinutes = 0)
    {
        var vehicle = SimulationTestData.EnRouteVehicle();
        vehicle.Status = vehicleStatus;
        vehicle.CapacityKg = 1500;
        vehicle.CapacityM3 = 10;
        await Fleet.VehicleRepository.AddAsync(vehicle);

        var route = SimulationTestData.EquatorRoute(routeStatus, vehicle.Id);
        route.DelayMinutes = delayMinutes;
        if (driver is not null)
        {
            await Fleet.DriverRepository.AddAsync(driver);
            route.AssignedDriverId = driver.Id;
        }

        route.CargoItems.Add(SheetData.Cargo(weightKg: 320, volumeM3: 1.8));
        route.CargoItems.Add(SheetData.Cargo(weightKg: 45, volumeM3: 0.4));
        await Fleet.RouteRepository.AddAsync(route);
        return (vehicle, route);
    }
}
