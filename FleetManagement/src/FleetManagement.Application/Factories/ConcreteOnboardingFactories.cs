using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Factories;

/// <summary>Familia de objetos (Abstract Factory) para camiones de carga.</summary>
public class TruckOnboardingFactory : IFleetOnboardingAbstractFactory
{
    public MaintenancePlan CreateMaintenancePlan() => new()
    {
        ApplicableVehicleType = VehicleType.Truck,
        Tasks = new List<MaintenanceTaskDefinition>
        {
            new() { TaskName = "Cambio de aceite y filtro", Type = MaintenanceType.OilChange, IntervalDays = 60, IntervalKm = 10000 },
            new() { TaskName = "Inspección de frenos", Type = MaintenanceType.BrakeInspection, IntervalDays = 90, IntervalKm = 15000 },
            new() { TaskName = "Rotación de neumáticos", Type = MaintenanceType.TireRotation, IntervalDays = 120, IntervalKm = 20000 }
        }
    };

    public NavigationProfile CreateNavigationProfile() => new()
    {
        ProfileName = "Perfil Camión de Carga",
        AvoidHighways = false,
        AvoidTolls = false,
        AvoidLowBridges = true,
        MaxSpeedKmh = 80
    };
}

/// <summary>Familia de objetos (Abstract Factory) para furgonetas de reparto.</summary>
public class VanOnboardingFactory : IFleetOnboardingAbstractFactory
{
    public MaintenancePlan CreateMaintenancePlan() => new()
    {
        ApplicableVehicleType = VehicleType.Van,
        Tasks = new List<MaintenanceTaskDefinition>
        {
            new() { TaskName = "Cambio de aceite y filtro", Type = MaintenanceType.OilChange, IntervalDays = 90, IntervalKm = 12000 },
            new() { TaskName = "Revisión general", Type = MaintenanceType.GeneralInspection, IntervalDays = 180, IntervalKm = 20000 }
        }
    };

    public NavigationProfile CreateNavigationProfile() => new()
    {
        ProfileName = "Perfil Furgoneta de Reparto",
        AvoidHighways = false,
        AvoidTolls = false,
        AvoidLowBridges = false,
        MaxSpeedKmh = 100
    };
}

/// <summary>Familia de objetos (Abstract Factory) para automóviles/camionetas livianas.</summary>
public class CarOnboardingFactory : IFleetOnboardingAbstractFactory
{
    public MaintenancePlan CreateMaintenancePlan() => new()
    {
        ApplicableVehicleType = VehicleType.Car,
        Tasks = new List<MaintenanceTaskDefinition>
        {
            new() { TaskName = "Cambio de aceite y filtro", Type = MaintenanceType.OilChange, IntervalDays = 120, IntervalKm = 10000 },
            new() { TaskName = "Revisión general", Type = MaintenanceType.GeneralInspection, IntervalDays = 180, IntervalKm = 15000 }
        }
    };

    public NavigationProfile CreateNavigationProfile() => new()
    {
        ProfileName = "Perfil Vehículo Liviano",
        AvoidHighways = false,
        AvoidTolls = false,
        AvoidLowBridges = false,
        MaxSpeedKmh = 120
    };
}

/// <summary>Familia de objetos (Abstract Factory) para motocicletas de mensajería.</summary>
public class MotorcycleOnboardingFactory : IFleetOnboardingAbstractFactory
{
    public MaintenancePlan CreateMaintenancePlan() => new()
    {
        ApplicableVehicleType = VehicleType.Motorcycle,
        Tasks = new List<MaintenanceTaskDefinition>
        {
            new() { TaskName = "Cambio de aceite", Type = MaintenanceType.OilChange, IntervalDays = 45, IntervalKm = 3000 },
            new() { TaskName = "Revisión de cadena y frenos", Type = MaintenanceType.BrakeInspection, IntervalDays = 30, IntervalKm = 2000 }
        }
    };

    public NavigationProfile CreateNavigationProfile() => new()
    {
        ProfileName = "Perfil Motocicleta de Mensajería",
        AvoidHighways = true,
        AvoidTolls = true,
        AvoidLowBridges = false,
        MaxSpeedKmh = 80
    };
}
