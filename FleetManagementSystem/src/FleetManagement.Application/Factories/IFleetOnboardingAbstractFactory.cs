using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.Factories;

/// <summary>
/// PATRÓN ABSTRACT FACTORY (creacional).
///
/// A diferencia de IVehicleFactory (Factory Method, que crea UN solo
/// producto: el Vehicle), esta fábrica crea una FAMILIA de objetos
/// relacionados que deben ser coherentes entre sí para un mismo tipo de
/// vehículo: el Plan de Mantenimiento (MaintenancePlan) y el Perfil de
/// Navegación (NavigationProfile).
///
/// Beneficio: garantiza que, por ejemplo, un camión nunca termine con un
/// plan de mantenimiento o un perfil de navegación pensado para una
/// motocicleta. Si un producto de la familia cambia, sólo se toca la
/// fábrica concreta correspondiente (Open/Closed).
/// </summary>
public interface IFleetOnboardingAbstractFactory
{
    MaintenancePlan CreateMaintenancePlan();
    NavigationProfile CreateNavigationProfile();
}
