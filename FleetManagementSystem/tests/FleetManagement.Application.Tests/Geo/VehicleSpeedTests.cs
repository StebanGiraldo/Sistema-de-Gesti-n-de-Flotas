using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Application.Geo;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Geo;

public class VehicleSpeedTests
{
    private readonly IVehicleTypeProfileFactory _profiles = new VehicleTypeProfileFactory(new FleetOnboardingFactoryProvider());

    [Fact]
    public void WithoutAVehicle_UsesTheDefaultSpeed()
        => Assert.Equal(TripEstimate.DefaultSpeedKmh, VehicleSpeed.KmPerHour(null, _profiles));

    [Fact]
    public void WithoutProfiles_UsesTheDefaultSpeed()
        => Assert.Equal(TripEstimate.DefaultSpeedKmh, VehicleSpeed.KmPerHour(new Vehicle { Type = VehicleType.Truck }, null));

    [Theory]
    [InlineData(VehicleType.Truck)]
    [InlineData(VehicleType.Van)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public void WithAVehicle_UsesTheSpeedOfItsTypeProfile(VehicleType type)
        => Assert.Equal(_profiles.GetProfile(type).MaxSpeedKmh, VehicleSpeed.KmPerHour(new Vehicle { Type = type }, _profiles));

    [Fact]
    public void TheSpeedReallyDependsOnTheType()
        => Assert.NotEqual(
            VehicleSpeed.KmPerHour(new Vehicle { Type = VehicleType.Truck }, _profiles),
            VehicleSpeed.KmPerHour(new Vehicle { Type = VehicleType.Car }, _profiles));
}
