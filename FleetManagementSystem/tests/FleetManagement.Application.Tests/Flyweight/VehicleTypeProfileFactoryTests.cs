using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Flyweights;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Flyweight;

/// <summary>
/// PRUEBAS DEL PATRÓN FLYWEIGHT: la fábrica entrega siempre la misma instancia por tipo de vehículo, los perfiles de tipos
/// distintos son instancias distintas, son inmutables y copian (no comparten) los valores del Abstract Factory.
/// </summary>
public class VehicleTypeProfileFactoryTests
{
    private static VehicleTypeProfileFactory CreateFactory() => new(new FleetOnboardingFactoryProvider());

    [Theory]
    [InlineData(VehicleType.Truck)]
    [InlineData(VehicleType.Van)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public void GetProfile_ForTheSameType_ReturnsTheVeryInstanceEveryTime(VehicleType type)
    {
        var factory = CreateFactory();

        var first = factory.GetProfile(type);
        var second = factory.GetProfile(type);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetProfile_ForDifferentTypes_ReturnsDifferentInstances()
    {
        var factory = CreateFactory();
        var profiles = Enum.GetValues<VehicleType>().Select(factory.GetProfile).ToList();

        Assert.Equal(profiles.Count, profiles.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(Enum.GetValues<VehicleType>(), profiles.Select(p => p.Type).ToArray());
    }

    [Theory]
    [InlineData(VehicleType.Truck)]
    [InlineData(VehicleType.Van)]
    [InlineData(VehicleType.Car)]
    [InlineData(VehicleType.Motorcycle)]
    public void TheProfile_CopiesTheValuesOfTheAbstractFactory_WithoutDuplicatingThemInTheCode(VehicleType type)
    {
        var family = new FleetOnboardingFactoryProvider().GetFactory(type);
        var navigation = family.CreateNavigationProfile();
        var maintenance = family.CreateMaintenancePlan();

        var profile = CreateFactory().GetProfile(type);

        Assert.Equal(type, profile.Type);
        Assert.Equal(navigation.ProfileName, profile.DisplayName);
        Assert.Equal(navigation.MaxSpeedKmh, profile.MaxSpeedKmh);
        Assert.Equal(navigation.AvoidHighways, profile.AvoidHighways);
        Assert.Equal(navigation.AvoidTolls, profile.AvoidTolls);
        Assert.Equal(navigation.AvoidLowBridges, profile.AvoidLowBridges);
        Assert.Equal(
            maintenance.Tasks.Select(t => (t.TaskName, t.Type, t.IntervalDays, t.IntervalKm)).ToArray(),
            profile.MaintenanceTasks.Select(t => (t.TaskName, t.Type, t.IntervalDays, t.IntervalKm)).ToArray());
    }

    [Fact]
    public void TheProfiles_ExposeTheSpeedOfEachTypeThatTheSimulationUses()
    {
        var factory = CreateFactory();

        Assert.Equal(80, factory.GetProfile(VehicleType.Truck).MaxSpeedKmh);
        Assert.Equal(100, factory.GetProfile(VehicleType.Van).MaxSpeedKmh);
        Assert.Equal(120, factory.GetProfile(VehicleType.Car).MaxSpeedKmh);
        Assert.Equal(80, factory.GetProfile(VehicleType.Motorcycle).MaxSpeedKmh);
    }

    [Fact]
    public void TheProfile_HasNoSetters_ItIsImmutable()
    {
        var withSetter = typeof(VehicleTypeProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is not null && !IsInitOnly(p.SetMethod))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(withSetter);
    }

    [Fact]
    public void ACopyMadeWithAnExpression_NeverChangesTheSharedInstance()
    {
        var shared = CreateFactory().GetProfile(VehicleType.Truck);

        var modifiedCopy = shared with { MaxSpeedKmh = 1 };

        Assert.NotSame(shared, modifiedCopy);
        Assert.Equal(80, shared.MaxSpeedKmh);
    }

    [Fact]
    public void TheMaintenanceTasks_AreAReadOnlyList_ThatCannotBeChangedByAConsumer()
    {
        var profile = CreateFactory().GetProfile(VehicleType.Truck);

        var tasks = (IList<MaintenanceTaskTemplate>)profile.MaintenanceTasks;

        Assert.Throws<NotSupportedException>(() => tasks.Add(new MaintenanceTaskTemplate("Intrusa", MaintenanceType.Other, 1, 1)));
        Assert.Throws<NotSupportedException>(() => tasks.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => tasks[0] = new MaintenanceTaskTemplate("Intrusa", MaintenanceType.Other, 1, 1));
    }

    [Fact]
    public void AProfileBuiltFromAMutableList_KeepsItsOwnCopy_SoChangingTheListLaterDoesNotAffectIt()
    {
        var tasks = new List<MaintenanceTaskTemplate> { new("Cambio de aceite", MaintenanceType.OilChange, 90, 10000) };
        var profile = new VehicleTypeProfile(VehicleType.Car, "Perfil de prueba", 90, false, false, false, tasks);

        tasks.Add(new MaintenanceTaskTemplate("Agregada después", MaintenanceType.Other, 1, 1));
        tasks.Clear();

        var kept = Assert.Single(profile.MaintenanceTasks);
        Assert.Equal("Cambio de aceite", kept.TaskName);
    }

    [Fact]
    public void TheProfile_DoesNotHoldTheMutableProductsOfTheAbstractFactory()
    {
        var mutableTypes = new[] { typeof(NavigationProfile), typeof(MaintenancePlan), typeof(MaintenanceTaskDefinition) };

        var profileMembers = typeof(VehicleTypeProfile).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.PropertyType)
            .ToList();

        Assert.DoesNotContain(profileMembers, type => mutableTypes.Contains(type));
        Assert.Equal(typeof(IReadOnlyList<MaintenanceTaskTemplate>), typeof(VehicleTypeProfile).GetProperty(nameof(VehicleTypeProfile.MaintenanceTasks))!.PropertyType);
    }

    [Fact]
    public void GetProfile_FromManyThreadsAtOnce_AlwaysReturnsTheSingleInstanceOfEachType()
    {
        var factory = CreateFactory();
        var types = Enum.GetValues<VehicleType>();
        var results = new ConcurrentBag<VehicleTypeProfile>();

        Parallel.For(0, 20_000, new ParallelOptions { MaxDegreeOfParallelism = 16 },
            i => results.Add(factory.GetProfile(types[i % types.Length])));

        Assert.Equal(20_000, results.Count);
        foreach (var group in results.GroupBy(profile => profile.Type))
            Assert.Single(group.Distinct(ReferenceEqualityComparer.Instance));
        Assert.Equal(types.Length, results.Select(profile => profile.Type).Distinct().Count());
    }

    [Fact]
    public void GetProfile_ForATypeWithoutAnOnboardingFactory_FailsClearlyInsteadOfInventingAProfile()
    {
        var factory = CreateFactory();

        Assert.Throws<NotSupportedException>(() => factory.GetProfile((VehicleType)99));
    }

    private static bool IsInitOnly(MethodInfo setter)
        => setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
}
