using System.Text.Json;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Reports;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Servicios de aplicación de prueba (test doubles escritos a mano, sin librerías de mocking, igual que el resto
/// del proyecto) para probar el reporte (Decorator) y el resumen del dashboard (Facade) SIN los servicios reales:
/// devuelven lo que la prueba les pone, cuentan cuántas veces los consultan y pueden fallar a propósito. Solo
/// implementan la consulta que usan esos componentes; el resto de operaciones lanzan NotSupportedException para
/// que cualquier uso inesperado haga fallar la prueba.
/// </summary>
public sealed class FakeVehicleService : IVehicleService
{
    public List<VehicleDto> Vehicles { get; } = new();
    public Exception? Failure { get; set; }
    public int GetAllCalls { get; private set; }

    public Task<IReadOnlyList<VehicleDto>> GetAllVehiclesAsync()
    {
        GetAllCalls++;
        if (Failure is not null) throw Failure;
        return Task.FromResult<IReadOnlyList<VehicleDto>>(Vehicles.ToList());
    }

    public Task<VehicleDto?> GetVehicleByIdAsync(Guid id) => throw new NotSupportedException();
    public Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request) => throw new NotSupportedException();
    public Task<VehicleDto> CloneVehicleAsync(Guid templateVehicleId, CloneVehicleRequest request) => throw new NotSupportedException();
    public Task UpdateVehicleStatusAsync(Guid id, UpdateVehicleStatusRequest request) => throw new NotSupportedException();
}

public sealed class FakeDeliveryRouteService : IDeliveryRouteService
{
    public List<DeliveryRouteDto> Routes { get; } = new();
    public Exception? Failure { get; set; }
    public int GetAllCalls { get; private set; }

    public Task<IReadOnlyList<DeliveryRouteDto>> GetAllRoutesAsync()
    {
        GetAllCalls++;
        if (Failure is not null) throw Failure;
        return Task.FromResult<IReadOnlyList<DeliveryRouteDto>>(Routes.ToList());
    }

    public Task<DeliveryRouteDto?> GetRouteByIdAsync(Guid id) => throw new NotSupportedException();
    public Task<IReadOnlyList<DeliveryRouteDto>> GetRoutesByDriverAsync(Guid driverId) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> CreateRouteAsync(CreateDeliveryRouteRequest request) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> CreateExpressRouteAsync(CreateExpressRouteRequest request) => throw new NotSupportedException();
    public Task<DeliveryRouteDto> DuplicateRouteAsync(Guid routeId, DuplicateRouteRequest request) => throw new NotSupportedException();
    public Task UpdateRouteStatusAsync(Guid id, string status) => throw new NotSupportedException();
}

public sealed class FakeMaintenanceService : IMaintenanceService
{
    public List<MaintenanceDueDto> Due { get; } = new();
    public Exception? Failure { get; set; }
    public int DueCalls { get; private set; }

    public Task<IReadOnlyList<MaintenanceDueDto>> GetVehiclesDueForMaintenanceAsync()
    {
        DueCalls++;
        if (Failure is not null) throw Failure;
        return Task.FromResult<IReadOnlyList<MaintenanceDueDto>>(Due.ToList());
    }

    public Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync() => throw new NotSupportedException();
    public Task<IReadOnlyList<MaintenanceRecordDto>> GetByVehicleAsync(Guid vehicleId) => throw new NotSupportedException();
    public Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request) => throw new NotSupportedException();
}

public sealed class FakeTripAlertService : ITripAlertService
{
    public List<TripAlertDto> Alerts { get; } = new();
    public Exception? Failure { get; set; }
    public int GetAllCalls { get; private set; }

    public Task<IReadOnlyList<TripAlertDto>> GetAllAsync()
    {
        GetAllCalls++;
        if (Failure is not null) throw Failure;
        return Task.FromResult<IReadOnlyList<TripAlertDto>>(Alerts.ToList());
    }

    public Task<IReadOnlyList<TripAlertDto>> GetByRouteAsync(Guid routeId) => throw new NotSupportedException();
    public Task<TripAlertDto> CreateAlertAsync(CreateTripAlertRequest request) => throw new NotSupportedException();
    public Task ResolveAlertAsync(Guid id) => throw new NotSupportedException();
}

/// <summary>
/// "Componente" mínimo para probar un decorador AISLADO del generador real y de los servicios: devuelve el
/// reporte que se le entrega y cuenta cuántas veces se le pidió.
/// </summary>
public sealed class StubReportGenerator : IFleetReportGenerator
{
    private readonly FleetReportDto _report;

    public StubReportGenerator(FleetReportDto report)
    {
        _report = report;
    }

    public int GenerateCalls { get; private set; }

    public Task<FleetReportDto> GenerateAsync()
    {
        GenerateCalls++;
        return Task.FromResult(_report);
    }
}

/// <summary>Constructores de DTO con valores razonables, para que cada prueba muestre solo lo que le importa.</summary>
public static class TestData
{
    public static VehicleDto Vehicle(string plate, string status = "Available", double mileageKm = 0, double capacityKg = 0)
        => new(Guid.NewGuid(), plate, "Marca", "Modelo", 2024, "Truck", status, capacityKg, mileageKm, 7.1, -73.1, null, null, DateTime.UtcNow, null);

    public static CargoItemDto Cargo(string description, double weightKg, double volumeM3, string priority = "Standard")
        => new(description, weightKg, volumeM3, priority);

    public static DeliveryRouteDto Route(string name, string status = "Planned", int delayMinutes = 0, IEnumerable<CargoItemDto>? cargo = null)
        => new(Guid.NewGuid(), name, 7.1, -73.1, 7.2, -73.2, new List<WaypointDto>(), (cargo ?? Enumerable.Empty<CargoItemDto>()).ToList(),
               null, null, null, null, status, 10, 20, DateTime.UtcNow, null, delayMinutes);

    public static MaintenanceDueDto Due(string plate, string task = "OilChange", bool isOverdue = true)
        => new(Guid.NewGuid(), plate, task, DateTime.UtcNow.AddDays(-1), null, isOverdue);

    public static TripAlertDto Alert(string status)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Ruta", null, null, "Delay", "Descripción", 5, status, DateTime.UtcNow, null);

    /// <summary>Reporte base con los vehículos y rutas dados, como lo entregaría <see cref="FleetReportGenerator"/>.</summary>
    public static FleetReportDto BaseReport(IEnumerable<VehicleDto>? vehicles = null, IEnumerable<DeliveryRouteDto>? routes = null)
        => new(
            new ReportMetadataDto("Fleet", DateTime.UtcNow, new[] { "Vehicles", "Routes" }),
            (vehicles ?? Enumerable.Empty<VehicleDto>()).ToList(),
            (routes ?? Enumerable.Empty<DeliveryRouteDto>()).ToList());
}

/// <summary>
/// Comparación estructural de DTO que contienen colecciones o diccionarios: la igualdad de los <c>record</c> compara
/// esas colecciones por referencia, así que dos cálculos idénticos no serían "iguales". Se comparan serializados.
/// </summary>
public static class Json
{
    public static string Of(object? value) => JsonSerializer.Serialize(value);
}
