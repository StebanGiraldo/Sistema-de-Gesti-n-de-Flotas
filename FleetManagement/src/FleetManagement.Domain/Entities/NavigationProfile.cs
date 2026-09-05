namespace FleetManagement.Domain.Entities;

/// <summary>
/// Restricciones de conducción/navegación apropiadas para un tipo de
/// vehículo (por ejemplo, un camión debe evitar puentes bajos). Es el
/// segundo producto de la familia creada por IFleetOnboardingAbstractFactory
/// (patrón ABSTRACT FACTORY), junto con MaintenancePlan.
/// </summary>
public class NavigationProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProfileName { get; set; } = string.Empty;
    public bool AvoidHighways { get; set; }
    public bool AvoidTolls { get; set; }
    public bool AvoidLowBridges { get; set; }
    public double MaxSpeedKmh { get; set; }
}
