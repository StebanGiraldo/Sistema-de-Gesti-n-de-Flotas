using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Interpreter;

/// <summary>Un vehículo junto con el contexto desde el que se le evalúa una regla.</summary>
public sealed record RuleSubject(Vehicle Vehicle, IRuleContext Context);

/// <summary>
/// Un caso de uso de las reglas: qué variables se pueden nombrar (<see cref="Vocabulary"/>) y sobre qué vehículos, cada uno con
/// su contexto, se evalúa. Agregar un caso de uso nuevo es una clase nueva; el servicio y el parser no cambian.
/// </summary>
public interface IRuleScope
{
    RuleVocabulary Vocabulary { get; }

    Task<IReadOnlyList<RuleSubject>> LoadSubjectsAsync();
}

/// <summary>Alertas de mantenimiento predictivo: cada vehículo con su historial de mantenimientos y el reloj de la aplicación.</summary>
public sealed class MaintenanceRuleScope : IRuleScope
{
    private readonly IVehicleRepository _vehicles;
    private readonly IMaintenanceRepository _records;
    private readonly TimeProvider _clock;

    public MaintenanceRuleScope(IVehicleRepository vehicles, IMaintenanceRepository records, TimeProvider clock)
    {
        _vehicles = vehicles;
        _records = records;
        _clock = clock;
    }

    public RuleVocabulary Vocabulary => MaintenanceRuleContext.Vocabulary;

    public async Task<IReadOnlyList<RuleSubject>> LoadSubjectsAsync()
    {
        var vehicles = await _vehicles.GetAllAsync();
        var recordsByVehicle = (await _records.GetAllAsync()).ToLookup(record => record.VehicleId);

        return vehicles
            .Select(vehicle => new RuleSubject(vehicle, new MaintenanceRuleContext(vehicle, recordsByVehicle[vehicle.Id], _clock)))
            .ToList();
    }
}

/// <summary>Filtros de monitoreo: cada vehículo con sus datos actuales.</summary>
public sealed class MonitoringRuleScope : IRuleScope
{
    private readonly IVehicleRepository _vehicles;

    public MonitoringRuleScope(IVehicleRepository vehicles)
    {
        _vehicles = vehicles;
    }

    public RuleVocabulary Vocabulary => VehicleMonitoringContext.Vocabulary;

    public async Task<IReadOnlyList<RuleSubject>> LoadSubjectsAsync()
        => (await _vehicles.GetAllAsync())
            .Select(vehicle => new RuleSubject(vehicle, new VehicleMonitoringContext(vehicle)))
            .ToList();
}
