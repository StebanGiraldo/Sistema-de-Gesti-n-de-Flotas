using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Factories;

/// <summary>Fábrica concreta (Factory Method) para vehículos tipo camión de carga.</summary>
public class TruckFactory : IVehicleFactory
{
    public Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = licensePlate,
            Brand = brand,
            Model = model,
            Year = year,
            Type = VehicleType.Truck,
            CapacityKg = 8000,
            Status = VehicleStatus.Available
        };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }
}

/// <summary>Fábrica concreta (Factory Method) para furgonetas de reparto.</summary>
public class VanFactory : IVehicleFactory
{
    public Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = licensePlate,
            Brand = brand,
            Model = model,
            Year = year,
            Type = VehicleType.Van,
            CapacityKg = 1500,
            Status = VehicleStatus.Available
        };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }
}

/// <summary>Fábrica concreta (Factory Method) para automóviles/camionetas livianas.</summary>
public class CarFactory : IVehicleFactory
{
    public Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = licensePlate,
            Brand = brand,
            Model = model,
            Year = year,
            Type = VehicleType.Car,
            CapacityKg = 400,
            Status = VehicleStatus.Available
        };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }
}

/// <summary>Fábrica concreta (Factory Method) para motocicletas de mensajería.</summary>
public class MotorcycleFactory : IVehicleFactory
{
    public Vehicle CreateVehicle(string licensePlate, string brand, string model, int year, double latitude, double longitude)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = licensePlate,
            Brand = brand,
            Model = model,
            Year = year,
            Type = VehicleType.Motorcycle,
            CapacityKg = 30,
            Status = VehicleStatus.Available
        };
        vehicle.UpdateLocation(latitude, longitude);
        return vehicle;
    }
}
