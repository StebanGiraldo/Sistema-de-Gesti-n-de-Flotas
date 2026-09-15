using FleetManagement.Application.Factories;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Factories;

/// <summary>
/// PRUEBAS DEL PATRÓN ABSTRACT FACTORY — foco en el "Creator"
/// (FleetOnboardingFactoryProvider).
///
/// Estas pruebas demuestran que el código cliente puede pedir "la familia de
/// onboarding para este VehicleType" y recibir polimórficamente la fábrica
/// concreta correcta, sin ningún switch/if en el llamador.
/// </summary>
public class FleetOnboardingFactoryProviderTests
{
    private readonly FleetOnboardingFactoryProvider _provider = new();

    [Theory]
    [InlineData(VehicleType.Truck, typeof(TruckOnboardingFactory))]
    [InlineData(VehicleType.Van, typeof(VanOnboardingFactory))]
    [InlineData(VehicleType.Car, typeof(CarOnboardingFactory))]
    [InlineData(VehicleType.Motorcycle, typeof(MotorcycleOnboardingFactory))]
    public void GetFactory_ReturnsCorrectConcreteFactory_ForEachVehicleType(VehicleType type, Type expectedFactoryType)
    {
        var factory = _provider.GetFactory(type);

        Assert.IsType(expectedFactoryType, factory);
        Assert.IsAssignableFrom<IFleetOnboardingAbstractFactory>(factory);
    }

    [Theory]
    [InlineData(VehicleType.Truck)]
    [InlineData(VehicleType.Van)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public void GetFactory_ProducesFamilyMatchingRequestedVehicleType(VehicleType type)
    {
        var factory = _provider.GetFactory(type);

        var plan = factory.CreateMaintenancePlan();

        Assert.Equal(type, plan.ApplicableVehicleType);
    }

    [Fact]
    public void GetFactory_WithUnregisteredVehicleType_ThrowsNotSupportedException()
    {
        // Simula un valor de enum fuera de rango (por ejemplo, si alguien
        // agrega un nuevo VehicleType en el futuro y olvida registrar su
        // fábrica de onboarding correspondiente).
        var unregisteredType = (VehicleType)999;

        Assert.Throws<NotSupportedException>(() => _provider.GetFactory(unregisteredType));
    }
}
