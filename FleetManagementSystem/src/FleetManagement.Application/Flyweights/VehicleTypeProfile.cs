using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Flyweights;

/// <summary>Plantilla inmutable de una tarea de mantenimiento preventivo (copia de <c>MaintenanceTaskDefinition</c>).</summary>
public sealed record MaintenanceTaskTemplate(string TaskName, MaintenanceType Type, int IntervalDays, double IntervalKm);

/// <summary>
/// PATRÓN FLYWEIGHT (estructural): perfil que comparten TODOS los vehículos de un mismo tipo.
///
/// Es el ESTADO INTRÍNSECO: lo que es igual para cualquier camión (nombre del perfil, velocidad máxima, restricciones de
/// navegación, plantillas de mantenimiento). Por eso existe una sola instancia por <see cref="VehicleType"/>, que
/// entrega <see cref="IVehicleTypeProfileFactory"/>. El ESTADO EXTRÍNSECO —placa, posición, estado, progreso del viaje—
/// sigue en <c>Vehicle</c> y <c>TripProgress</c>: cambia por vehículo y no se comparte.
///
/// Un Flyweight solo es seguro si es inmutable (varios consumidores y varios hilos lo comparten). Por eso es un record sin
/// setters y la lista de tareas se copia a una colección de solo lectura al construirlo: ningún consumidor puede alterar
/// el perfil que ven los demás. Por la misma razón NUNCA se comparten <c>MaintenancePlan</c> ni <c>NavigationProfile</c>
/// (clases mutables del Abstract Factory): sus valores se copian aquí.
/// </summary>
public sealed record VehicleTypeProfile(
    VehicleType Type,
    string DisplayName,
    double MaxSpeedKmh,
    bool AvoidHighways,
    bool AvoidTolls,
    bool AvoidLowBridges,
    IReadOnlyList<MaintenanceTaskTemplate> MaintenanceTasks)
{
    /// <summary>Plantillas de mantenimiento del tipo. Copia de solo lectura: cambiar la lista original no afecta al perfil.</summary>
    public IReadOnlyList<MaintenanceTaskTemplate> MaintenanceTasks { get; } = Array.AsReadOnly(MaintenanceTasks.ToArray());
}
