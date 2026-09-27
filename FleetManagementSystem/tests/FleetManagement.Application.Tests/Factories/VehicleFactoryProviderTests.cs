using FleetManagement.Application.Factories;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Factories;

/// <summary>
/// PRUEBAS DEL PATRÓN FACTORY METHOD — foco en el "Creator" (VehicleFactoryProvider).
///
/// Estas pruebas demuestran la parte central del patrón: el código cliente
/// pide "una fábrica para este VehicleType" y recibe polimórficamente la
/// implementación concreta correcta —sin ningún switch/if en el llamador— y
/// cada una produce vehículos con los valores por defecto de su categoría.
/// </summary>
public class VehicleFactoryProviderTests
{
    private readonly VehicleFactoryProvider _provider = new();

    [Theory]
    [InlineData(VehicleType.Truck, typeof(TruckFactory))]
    [InlineData(VehicleType.Van, typeof(VanFactory))]
    [InlineData(VehicleType.Car, typeof(CarFactory))]
    [InlineData(VehicleType.Motorcycle, typeof(MotorcycleFactory))]
    public void GetFactory_ReturnsCorrectConcreteFactory_ForEachVehicleType(VehicleType type, Type expectedFactoryType)
    {
        var factory = _provider.GetFactory(type);

        Assert.IsType(expectedFactoryType, factory);
        Assert.IsAssignableFrom<IVehicleFactory>(factory);
    }

    [Theory]
    [InlineData(VehicleType.Truck, 8000)]
    [InlineData(VehicleType.Van, 1500)]
    [InlineData(VehicleType.Car, 400)]
    [InlineData(VehicleType.Motorcycle, 30)]
    public void GetFactory_ProducesVehicleWithCategoryCapacity(VehicleType type, double expectedCapacityKg)
    {
        var factory = _provider.GetFactory(type);

        var vehicle = factory.CreateVehicle("TEST-001", "MarcaX", "ModeloX", 2023, 0, 0);

        Assert.Equal(expectedCapacityKg, vehicle.CapacityKg);
        Assert.Equal(type, vehicle.Type);
    }

    [Fact]
    public void GetFactory_WithUnregisteredVehicleType_ThrowsNotSupportedException()
    {
        // Simula un valor de enum fuera de rango (por ejemplo, si alguien agrega
        // un nuevo VehicleType en el futuro y olvida registrar su fábrica).
        var unregisteredType = (VehicleType)999;

        Assert.Throws<NotSupportedException>(() => _provider.GetFactory(unregisteredType));
    }
}
