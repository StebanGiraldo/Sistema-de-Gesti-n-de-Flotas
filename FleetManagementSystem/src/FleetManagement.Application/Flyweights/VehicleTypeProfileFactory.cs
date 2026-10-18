using System.Collections.Concurrent;
using FleetManagement.Application.Factories;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Flyweights;

/// <summary>
/// Fábrica del patrón FLYWEIGHT: construye cada <see cref="VehicleTypeProfile"/> una sola vez y después devuelve siempre
/// la misma instancia.
///
/// Fuente única de verdad: los valores NO se repiten aquí; se leen de <see cref="FleetOnboardingFactoryProvider"/> (el
/// Abstract Factory existente, que no se modifica) y se COPIAN a records inmutables. Los productos de esa fábrica
/// (<c>NavigationProfile</c>, <c>MaintenancePlan</c>) son mutables, así que se descartan después de copiarlos: compartirlos
/// rompería el patrón.
///
/// Concurrencia: <c>ConcurrentDictionary.GetOrAdd</c> puede ejecutar su delegado más de una vez si dos hilos llegan a la
/// vez; por eso el valor es un <see cref="Lazy{T}"/> (modo por defecto, ExecutionAndPublication): solo un hilo construye
/// el perfil y todos reciben la misma instancia.
/// </summary>
public sealed class VehicleTypeProfileFactory : IVehicleTypeProfileFactory
{
    private readonly FleetOnboardingFactoryProvider _onboardingFactories;
    private readonly ConcurrentDictionary<VehicleType, Lazy<VehicleTypeProfile>> _profiles = new();

    public VehicleTypeProfileFactory(FleetOnboardingFactoryProvider onboardingFactories)
    {
        _onboardingFactories = onboardingFactories;
    }

    public VehicleTypeProfile GetProfile(VehicleType type)
        => _profiles.GetOrAdd(type, key => new Lazy<VehicleTypeProfile>(() => Build(key))).Value;

    private VehicleTypeProfile Build(VehicleType type)
    {
        var family = _onboardingFactories.GetFactory(type);
        var navigation = family.CreateNavigationProfile();
        var maintenance = family.CreateMaintenancePlan();

        return new VehicleTypeProfile(
            type,
            navigation.ProfileName,
            navigation.MaxSpeedKmh,
            navigation.AvoidHighways,
            navigation.AvoidTolls,
            navigation.AvoidLowBridges,
            maintenance.Tasks.Select(ToTemplate).ToList());
    }

    private static MaintenanceTaskTemplate ToTemplate(MaintenanceTaskDefinition task)
        => new(task.TaskName, task.Type, task.IntervalDays, task.IntervalKm);
}
