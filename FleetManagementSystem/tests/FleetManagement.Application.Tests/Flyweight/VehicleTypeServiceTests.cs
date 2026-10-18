using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Flyweight;

/// <summary>Pruebas de la consulta de perfiles por tipo: es un consumidor del Flyweight y no debe reconstruirlos.</summary>
public class VehicleTypeServiceTests
{
    /// <summary>Fábrica de prueba escrita a mano: cuenta cuántas veces se le pide un perfil.</summary>
    private sealed class CountingProfileFactory : IVehicleTypeProfileFactory
    {
        private readonly IVehicleTypeProfileFactory _real = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());

        public List<VehicleType> Requested { get; } = new();

        public VehicleTypeProfile GetProfile(VehicleType type)
        {
            Requested.Add(type);
            return _real.GetProfile(type);
        }
    }

    [Fact]
    public void GetAllProfiles_ReturnsOneProfilePerVehicleType_InDeclarationOrder()
    {
        var service = new VehicleTypeService(new CountingProfileFactory());

        var profiles = service.GetAllProfiles();

        Assert.Equal(new[] { "Truck", "Van", "Car", "Motorcycle" }, profiles.Select(p => p.Type).ToArray());
    }

    [Fact]
    public void GetAllProfiles_ShowsTheValuesOfTheSharedProfile()
    {
        var service = new VehicleTypeService(new CountingProfileFactory());

        var truck = service.GetAllProfiles().Single(p => p.Type == "Truck");

        Assert.Equal("Perfil Camión de Carga", truck.DisplayName);
        Assert.Equal(80, truck.MaxSpeedKmh);
        Assert.True(truck.AvoidLowBridges);
        Assert.False(truck.AvoidHighways);
        Assert.Contains(truck.MaintenanceTasks, task => task.Type == "OilChange" && task.IntervalKm == 10000);
        Assert.Equal(3, truck.MaintenanceTasks.Count);
    }

    [Fact]
    public void GetAllProfiles_AsksTheFlyweightFactoryForEachType_InsteadOfBuildingProfilesItself()
    {
        var factory = new CountingProfileFactory();
        var service = new VehicleTypeService(factory);

        service.GetAllProfiles();
        service.GetAllProfiles();

        Assert.Equal(8, factory.Requested.Count);   // 4 tipos x 2 consultas, siempre a través de la fábrica
        Assert.Equal(Enum.GetValues<VehicleType>(), factory.Requested.Take(4).ToArray());
    }
}
