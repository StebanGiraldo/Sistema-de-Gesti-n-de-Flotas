using FleetManagement.Application.Interpreter;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Interpreter;

/// <summary>
/// Los dos contextos reales: lo que una regla puede preguntar de un vehículo. El «hoy» sale de un reloj manual
/// (<see cref="ManualTimeProvider"/>, 17 de octubre de 2026 a las 12:00 UTC), así que cada cifra se comprueba exacta.
/// </summary>
public class MaintenanceRuleContextTests
{
    private readonly ManualTimeProvider _clock = new();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private MaintenanceRuleContext ContextFor(Vehicle vehicle, params MaintenanceRecord[] records)
        => new(vehicle, records, _clock);

    private static MaintenanceRecord Record(DateTime performedAt, double mileageAtServiceKm)
        => new() { Type = MaintenanceType.OilChange, PerformedAt = performedAt, MileageAtServiceKm = mileageAtServiceKm };

    [Fact]
    public void TheVehicleFields_AreOfferedAsTheyAre_AndTheAgeIsTheCurrentYearMinusTheVehicleYear()
    {
        var context = ContextFor(RuleTestData.Vehicle(type: VehicleType.Van, status: VehicleStatus.Maintenance, mileageKm: 60000, year: 2020));

        Assert.Equal(RuleValue.FromNumber(60000), context.Resolve(RuleVariables.MileageKm));
        Assert.Equal(RuleValue.FromText("Van"), context.Resolve(RuleVariables.Type));
        Assert.Equal(RuleValue.FromText("Maintenance"), context.Resolve(RuleVariables.Status));
        Assert.Equal(RuleValue.FromNumber(6), context.Resolve(RuleVariables.VehicleAgeYears));
    }

    [Fact]
    public void AVehicleFromTheFuture_NeverHasANegativeAge()
        => Assert.Equal(RuleValue.FromNumber(0), ContextFor(RuleTestData.Vehicle(year: 2030)).Resolve(RuleVariables.VehicleAgeYears));

    [Fact]
    public void TheLastService_IsTheMostRecentByDate_NotTheOneWithTheMostKilometers()
    {
        var context = ContextFor(
            RuleTestData.Vehicle(mileageKm: 60000),
            Record(Now.AddDays(-200), mileageAtServiceKm: 40000),
            Record(Now.AddDays(-30), mileageAtServiceKm: 52000));

        Assert.Equal(RuleValue.FromNumber(8000), context.Resolve(RuleVariables.KmSinceLastService));
        Assert.Equal(RuleValue.FromNumber(30), context.Resolve(RuleVariables.DaysSinceLastService));
    }

    [Fact]
    public void WithTheSameDate_TheServiceWithMoreKilometersCountsAsTheLast()
    {
        var sameDay = Now.AddDays(-10);

        var context = ContextFor(RuleTestData.Vehicle(mileageKm: 60000), Record(sameDay, 50000), Record(sameDay, 55000));

        Assert.Equal(RuleValue.FromNumber(5000), context.Resolve(RuleVariables.KmSinceLastService));
    }

    [Fact]
    public void TheDays_AreWholeDays_RoundedDown()
    {
        var context = ContextFor(RuleTestData.Vehicle(), Record(Now.AddDays(-29).AddHours(-23), 0));

        Assert.Equal(RuleValue.FromNumber(29), context.Resolve(RuleVariables.DaysSinceLastService));
    }

    [Fact]
    public void ARecordFromTheFutureOrWithMoreKilometersThanTheVehicle_NeverGivesNegativeNumbers()
    {
        var context = ContextFor(RuleTestData.Vehicle(mileageKm: 1000), Record(Now.AddDays(3), mileageAtServiceKm: 5000));

        Assert.Equal(RuleValue.FromNumber(0), context.Resolve(RuleVariables.DaysSinceLastService));
        Assert.Equal(RuleValue.FromNumber(0), context.Resolve(RuleVariables.KmSinceLastService));
    }

    [Fact]
    public void WithoutServices_TheServiceVariablesHaveNoData_NotZero()
    {
        var context = ContextFor(RuleTestData.Vehicle());

        Assert.Same(RuleValue.Missing, context.Resolve(RuleVariables.KmSinceLastService));
        Assert.Same(RuleValue.Missing, context.Resolve(RuleVariables.DaysSinceLastService));
    }

    [Theory]
    [InlineData("kmSinceLastService >= 0", false)]
    [InlineData("daysSinceLastService >= 0", false)]
    [InlineData("kmSinceLastService < 1", false)]
    [InlineData("kmSinceLastService != 5", false)]
    [InlineData("kmSinceLastService >= 8000 OR daysSinceLastService > 180", false)]
    [InlineData("NOT kmSinceLastService > 5", true)]       // consecuencia documentada: sin dato la comparación es falsa, y NOT la invierte
    [InlineData("type = \"Truck\"", true)]                 // lo que sí tiene dato sigue funcionando
    public void ARuleOverServiceDataNeverMatchesAVehicleWithoutServices(string rule, bool expected)
    {
        var context = ContextFor(RuleTestData.Vehicle(type: VehicleType.Truck));

        Assert.Equal(expected, new RuleParser().Parse(rule, MaintenanceRuleContext.Vocabulary).Interpret(context));
    }

    [Theory]
    [InlineData(8000, 10, true)]
    [InlineData(7999, 180, false)]
    [InlineData(7999, 181, true)]
    [InlineData(10000, 0, true)]
    [InlineData(0, 0, false)]
    public void ThePredictiveRuleOfTheProject_UsesTheExactThresholds(double kmSince, int daysSince, bool expected)
    {
        var vehicle = RuleTestData.Vehicle(mileageKm: 50000);
        var context = ContextFor(vehicle, Record(Now.AddDays(-daysSince), mileageAtServiceKm: 50000 - kmSince));

        var matches = new RuleParser()
            .Parse("kmSinceLastService >= 8000 OR daysSinceLastService > 180", MaintenanceRuleContext.Vocabulary)
            .Interpret(context);

        Assert.Equal(expected, matches);
    }

    [Fact]
    public void TheVocabulary_IsExactlyWhatTheContextOffers()
    {
        var expected = new[]
        {
            RuleVariables.MileageKm, RuleVariables.VehicleAgeYears, RuleVariables.DaysSinceLastService,
            RuleVariables.KmSinceLastService, RuleVariables.Type, RuleVariables.Status
        };
        var context = ContextFor(RuleTestData.Vehicle(), Record(Now.AddDays(-5), 100));

        Assert.Equal(expected.Order(StringComparer.Ordinal), MaintenanceRuleContext.Vocabulary.Names.Order(StringComparer.Ordinal));
        Assert.All(MaintenanceRuleContext.Vocabulary.Names, name => Assert.NotSame(RuleValue.Missing, context.Resolve(name)));
    }

    [Fact]
    public void AnUnknownVariable_HasNoData_AndTheNamesDoNotDistinguishCase()
    {
        var context = ContextFor(RuleTestData.Vehicle(mileageKm: 777));

        Assert.Same(RuleValue.Missing, context.Resolve("fuelLevel"));
        Assert.Equal(context.Resolve("mileageKm"), context.Resolve("MILEAGEKM"));
    }

    [Fact]
    public void TheClockIsReadWhenTheContextIsCreated_SoAnOldContextDoesNotMove()
    {
        var vehicle = RuleTestData.Vehicle();
        var record = Record(Now.AddDays(-10), 0);
        var before = ContextFor(vehicle, record);

        _clock.Advance(TimeSpan.FromDays(5));
        var after = ContextFor(vehicle, record);

        Assert.Equal(RuleValue.FromNumber(10), before.Resolve(RuleVariables.DaysSinceLastService));
        Assert.Equal(RuleValue.FromNumber(15), after.Resolve(RuleVariables.DaysSinceLastService));
    }

    [Fact]
    public void TheContext_DoesNotModifyTheVehicleNorItsHistory()
    {
        var vehicle = RuleTestData.Vehicle(status: VehicleStatus.Maintenance, mileageKm: 4321);
        var record = Record(Now.AddDays(-10), 4000);

        var context = ContextFor(vehicle, record);
        foreach (var name in MaintenanceRuleContext.Vocabulary.Names) context.Resolve(name);

        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
        Assert.Equal(4321, vehicle.MileageKm);
        Assert.Null(vehicle.LastMaintenanceDate);
        Assert.Equal(4000, record.MileageAtServiceKm);
    }
}

public class VehicleMonitoringContextTests
{
    [Fact]
    public void TheVariables_ComeFromTheRealFieldsOfTheVehicle()
    {
        var context = new VehicleMonitoringContext(RuleTestData.Vehicle(type: VehicleType.Motorcycle, status: VehicleStatus.EnRoute, mileageKm: 9120, capacityKg: 30));

        Assert.Equal(RuleValue.FromText("EnRoute"), context.Resolve(RuleVariables.Status));
        Assert.Equal(RuleValue.FromText("Motorcycle"), context.Resolve(RuleVariables.Type));
        Assert.Equal(RuleValue.FromNumber(9120), context.Resolve(RuleVariables.MileageKm));
        Assert.Equal(RuleValue.FromNumber(30), context.Resolve(RuleVariables.CapacityKg));
    }

    [Fact]
    public void HasDriver_IsOneWithADriverAndZeroWithout_BecauseTheGrammarHasNoBooleans()
    {
        var with = new VehicleMonitoringContext(RuleTestData.Vehicle(driverId: Guid.NewGuid()));
        var without = new VehicleMonitoringContext(RuleTestData.Vehicle(driverId: null));

        Assert.Equal(RuleValue.FromNumber(1), with.Resolve(RuleVariables.HasDriver));
        Assert.Equal(RuleValue.FromNumber(0), without.Resolve(RuleVariables.HasDriver));
    }

    [Fact]
    public void TheVocabulary_IsExactlyWhatTheContextOffers()
    {
        var expected = new[] { RuleVariables.Status, RuleVariables.Type, RuleVariables.MileageKm, RuleVariables.CapacityKg, RuleVariables.HasDriver };
        var context = new VehicleMonitoringContext(RuleTestData.Vehicle());

        Assert.Equal(expected.Order(StringComparer.Ordinal), VehicleMonitoringContext.Vocabulary.Names.Order(StringComparer.Ordinal));
        Assert.All(VehicleMonitoringContext.Vocabulary.Names, name => Assert.NotSame(RuleValue.Missing, context.Resolve(name)));
    }

    [Fact]
    public void WhatTheMonitoringContextDoesNotOffer_HasNoData_EvenIfAnotherContextOffersIt()
    {
        var context = new VehicleMonitoringContext(RuleTestData.Vehicle());

        Assert.Same(RuleValue.Missing, context.Resolve(RuleVariables.KmSinceLastService));
        Assert.Same(RuleValue.Missing, context.Resolve(RuleVariables.VehicleAgeYears));
    }

    [Theory]
    [InlineData("status = \"Available\" AND type = \"Truck\"", true)]
    [InlineData("status = \"available\" AND type = \"truck\"", true)]
    [InlineData("status != \"Available\"", false)]
    [InlineData("hasDriver = 0", true)]
    [InlineData("mileageKm > 5000 AND mileageKm <= 10000", true)]
    [InlineData("capacityKg >= 9000", false)]
    public void TheMonitoringFilters_OfTheProject(string rule, bool expected)
    {
        var context = new VehicleMonitoringContext(RuleTestData.Vehicle(type: VehicleType.Truck, status: VehicleStatus.Available, mileageKm: 10000, capacityKg: 8000));

        Assert.Equal(expected, new RuleParser().Parse(rule, VehicleMonitoringContext.Vocabulary).Interpret(context));
    }
}
