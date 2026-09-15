using FleetManagement.Application.Factories;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Factories;

/// <summary>
/// PRUEBAS DEL PATRÓN ABSTRACT FACTORY (nivel unitario, una fábrica concreta
/// a la vez).
///
/// A diferencia del Factory Method (IVehicleFactory, que crea UN producto:
/// el Vehicle), cada IFleetOnboardingAbstractFactory crea una FAMILIA de DOS
/// productos relacionados —MaintenancePlan y NavigationProfile— que deben
/// ser coherentes entre sí para un mismo VehicleType. Estas pruebas verifican,
/// fábrica por fábrica, que ambos productos de la familia correspondan
/// siempre a la misma categoría de vehículo.
/// </summary>
public class FleetOnboardingAbstractFactoryTests
{
    [Fact]
    public void TruckOnboardingFactory_CreatesMaintenancePlan_ForTruckWithExpectedTasks()
    {
        var factory = new TruckOnboardingFactory();

        var plan = factory.CreateMaintenancePlan();

        Assert.Equal(VehicleType.Truck, plan.ApplicableVehicleType);
        Assert.Equal(3, plan.Tasks.Count);
        Assert.Contains(plan.Tasks, t => t.Type == MaintenanceType.BrakeInspection);
    }

    [Fact]
    public void TruckOnboardingFactory_CreatesNavigationProfile_AvoidingLowBridges()
    {
        var factory = new TruckOnboardingFactory();

        var profile = factory.CreateNavigationProfile();

        // Un camión de carga sí debe evitar puentes bajos; ningún otro perfil lo hace.
        Assert.True(profile.AvoidLowBridges);
        Assert.Equal(80, profile.MaxSpeedKmh);
    }

    [Fact]
    public void VanOnboardingFactory_CreatesMaintenancePlan_ForVanWithExpectedTasks()
    {
        var factory = new VanOnboardingFactory();

        var plan = factory.CreateMaintenancePlan();

        Assert.Equal(VehicleType.Van, plan.ApplicableVehicleType);
        Assert.Equal(2, plan.Tasks.Count);
    }

    [Fact]
    public void VanOnboardingFactory_CreatesNavigationProfile_WithoutLowBridgeRestriction()
    {
        var factory = new VanOnboardingFactory();

        var profile = factory.CreateNavigationProfile();

        Assert.False(profile.AvoidLowBridges);
        Assert.Equal(100, profile.MaxSpeedKmh);
    }

    [Fact]
    public void CarOnboardingFactory_CreatesMaintenancePlan_ForCar()
    {
        var factory = new CarOnboardingFactory();

        var plan = factory.CreateMaintenancePlan();

        Assert.Equal(VehicleType.Car, plan.ApplicableVehicleType);
        Assert.Contains(plan.Tasks, t => t.Type == MaintenanceType.GeneralInspection);
    }

    [Fact]
    public void CarOnboardingFactory_CreatesNavigationProfile_WithHighestMaxSpeed()
    {
        var factory = new CarOnboardingFactory();

        var profile = factory.CreateNavigationProfile();

        Assert.Equal(120, profile.MaxSpeedKmh);
    }

    [Fact]
    public void MotorcycleOnboardingFactory_CreatesMaintenancePlan_WithShortestIntervals()
    {
        var factory = new MotorcycleOnboardingFactory();

        var plan = factory.CreateMaintenancePlan();

        Assert.Equal(VehicleType.Motorcycle, plan.ApplicableVehicleType);
        Assert.All(plan.Tasks, t => Assert.True(t.IntervalKm <= 3000));
    }

    [Fact]
    public void MotorcycleOnboardingFactory_CreatesNavigationProfile_AvoidingHighwaysAndTolls()
    {
        var factory = new MotorcycleOnboardingFactory();

        var profile = factory.CreateNavigationProfile();

        // Coherencia de familia: una motocicleta de mensajería evita autopistas y peajes.
        Assert.True(profile.AvoidHighways);
        Assert.True(profile.AvoidTolls);
    }

    [Fact]
    public void EachConcreteFactory_ProducesConsistentFamily_MatchingVehicleType()
    {
        // Demuestra la garantía central del Abstract Factory: dentro de UNA
        // misma fábrica concreta, ambos productos "pertenecen" al mismo tipo,
        // sin que el código cliente tenga que verificarlo manualmente.
        IFleetOnboardingAbstractFactory factory = new TruckOnboardingFactory();

        var plan = factory.CreateMaintenancePlan();
        var profile = factory.CreateNavigationProfile();

        Assert.Equal(VehicleType.Truck, plan.ApplicableVehicleType);
        Assert.Equal("Perfil Camión de Carga", profile.ProfileName);
    }
}
