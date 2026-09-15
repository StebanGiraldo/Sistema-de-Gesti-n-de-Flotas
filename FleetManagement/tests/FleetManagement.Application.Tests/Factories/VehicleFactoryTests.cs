using FleetManagement.Application.Factories;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Factories;

/// <summary>
/// PRUEBAS DEL PATRÓN FACTORY METHOD (nivel unitario, una fábrica a la vez).
///
/// Cada fábrica concreta (TruckFactory, VanFactory, CarFactory,
/// MotorcycleFactory) debe construir un Vehicle con el Type y la CapacityKg
/// correctos para su categoría, sin que el código que la invoca conozca esos
/// detalles. Estas pruebas verifican exactamente eso, fábrica por fábrica.
/// </summary>
public class VehicleFactoryTests
{
    [Fact]
    public void TruckFactory_CreatesVehicle_WithTruckTypeAndCapacity()
    {
        var factory = new TruckFactory();

        var vehicle = factory.CreateVehicle("TRK-100", "Volvo", "FH 460", 2023, 7.1193, -73.1227);

        Assert.Equal(VehicleType.Truck, vehicle.Type);
        Assert.Equal(8000, vehicle.CapacityKg);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal("TRK-100", vehicle.LicensePlate);
    }

    [Fact]
    public void VanFactory_CreatesVehicle_WithVanTypeAndCapacity()
    {
        var factory = new VanFactory();

        var vehicle = factory.CreateVehicle("VAN-100", "Renault", "Master", 2023, 7.0806, -73.1716);

        Assert.Equal(VehicleType.Van, vehicle.Type);
        Assert.Equal(1500, vehicle.CapacityKg);
    }

    [Fact]
    public void CarFactory_CreatesVehicle_WithCarTypeAndCapacity()
    {
        var factory = new CarFactory();

        var vehicle = factory.CreateVehicle("CAR-100", "Chevrolet", "N300", 2023, 4.7110, -74.0721);

        Assert.Equal(VehicleType.Car, vehicle.Type);
        Assert.Equal(400, vehicle.CapacityKg);
    }

    [Fact]
    public void MotorcycleFactory_CreatesVehicle_WithMotorcycleTypeAndCapacity()
    {
        var factory = new MotorcycleFactory();

        var vehicle = factory.CreateVehicle("MOT-100", "AKT", "NKD 125", 2023, 6.2442, -75.5812);

        Assert.Equal(VehicleType.Motorcycle, vehicle.Type);
        Assert.Equal(30, vehicle.CapacityKg);
    }

    [Fact]
    public void CreateVehicle_SetsCurrentLocation_FromLatitudeAndLongitude()
    {
        var factory = new TruckFactory();

        var vehicle = factory.CreateVehicle("TRK-101", "Volvo", "FH 460", 2023, latitude: 7.12, longitude: -73.15);

        // Point.Y = Latitud, Point.X = Longitud (convención GeoJSON/WGS84 usada en todo el dominio).
        Assert.Equal(7.12, vehicle.CurrentLocation.Y, 4);
        Assert.Equal(-73.15, vehicle.CurrentLocation.X, 4);
    }
}
