using FleetManagement.Application.Analysis;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Visitor.FleetData;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// VISITANTE #3: historial de mantenimiento. Cruza dos tipos de elemento (vehículos y registros). Flota de la prueba: TRK-001
/// (45 210 km) con dos servicios, CAR-003 (62 870 km) con uno y MOT-004 (9 120 km) sin ninguno.
/// </summary>
public class MaintenanceHistoryVisitorTests
{
    private const double Tolerance = 1e-9;

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static (FleetElementStructure Structure, Vehicle Truck, Vehicle Car, Vehicle Moto) Scenario()
    {
        var truck = Vehicle("TRK-001", VehicleType.Truck, mileageKm: 45210);
        var car = Vehicle("CAR-003", VehicleType.Car, mileageKm: 62870);
        var moto = Vehicle("MOT-004", VehicleType.Motorcycle, mileageKm: 9120);
        var records = new[]
        {
            Record(truck, MaintenanceType.OilChange, Day(1, 10), 30000),
            Record(truck, MaintenanceType.BrakeInspection, Day(5, 20), 40000),
            Record(car, MaintenanceType.OilChange, Day(2, 1), 60000)
        };
        return (Structure(new[] { truck, car, moto }, records: records), truck, car, moto);
    }

    private static MaintenanceHistoryAnalysisDto Analyze(FleetElementStructure structure) => structure.Analyze(new MaintenanceHistoryVisitor());

    [Fact]
    public void TheTotals_CountTheRecordsAndTheVehiclesThatHaveThem()
    {
        var result = Analyze(Scenario().Structure);

        Assert.Equal(3, result.TotalRecords);
        Assert.Equal(2, result.VehiclesWithRecords);
        Assert.Equal(new[] { "MOT-004" }, result.VehiclesWithoutRecords);
    }

    [Fact]
    public void TheBreakdownByType_ListsEveryTypeInTheOrderOfTheEnum()
    {
        var byType = Analyze(Scenario().Structure).ByType;

        Assert.Equal(new[] { "OilChange", "TireRotation", "BrakeInspection", "GeneralInspection", "Repair", "Other" }, byType.Select(type => type.Type));
        Assert.Equal(new[] { 2, 0, 1, 0, 0, 0 }, byType.Select(type => type.Records));
    }

    [Fact]
    public void EachVehicleWithRecords_ShowsItsLatestServiceAndTheKilometersSinceThen()
    {
        var vehicles = Analyze(Scenario().Structure).Vehicles;

        Assert.Equal(new[] { "CAR-003", "TRK-001" }, vehicles.Select(vehicle => vehicle.LicensePlate));   // por placa

        var truck = vehicles.Single(vehicle => vehicle.LicensePlate == "TRK-001");
        Assert.Equal(2, truck.Records);
        Assert.Equal(Day(5, 20), truck.LastServiceAt);
        Assert.Equal("BrakeInspection", truck.LastServiceType);
        Assert.Equal(45210, truck.MileageKm);
        Assert.Equal(5210, truck.KmSinceLastService, Tolerance);    // 45 210 − 40 000

        var car = vehicles.Single(vehicle => vehicle.LicensePlate == "CAR-003");
        Assert.Equal(1, car.Records);
        Assert.Equal("OilChange", car.LastServiceType);
        Assert.Equal(2870, car.KmSinceLastService, Tolerance);      // 62 870 − 60 000
    }

    [Fact]
    public void TheAverageDistanceBetweenServices_UsesTheConsecutiveServicesOfEachVehicle()
    {
        var (structure, _, _, _) = Scenario();

        Assert.Equal(10000, Analyze(structure).AverageKmBetweenServices!.Value, Tolerance);   // solo TRK-001 tiene dos servicios: 40 000 − 30 000
    }

    [Fact]
    public void TheAverage_MixesTheIntervalsOfAllTheVehicles()
    {
        var (_, truck, car, moto) = Scenario();
        var records = new[]
        {
            Record(truck, MaintenanceType.OilChange, Day(1, 10), 30000),
            Record(truck, MaintenanceType.BrakeInspection, Day(5, 20), 40000),   // intervalo 10 000
            Record(car, MaintenanceType.OilChange, Day(2, 1), 60000),
            Record(car, MaintenanceType.TireRotation, Day(8, 1), 62000)          // intervalo 2 000
        };

        var result = Analyze(Structure(new[] { truck, car, moto }, records: records));

        Assert.Equal(6000, result.AverageKmBetweenServices!.Value, Tolerance);   // (10 000 + 2 000) / 2
    }

    [Fact]
    public void WithOneServicePerVehicle_ThereIsNoAverage()
    {
        var car = Vehicle("CAR-003", mileageKm: 100);
        var result = Analyze(Structure(new[] { car }, records: new[] { Record(car, MaintenanceType.OilChange, Day(1, 1), 50) }));

        Assert.Null(result.AverageKmBetweenServices);
    }

    [Fact]
    public void TheRecordsAreOrderedByDate_NotByTheOrderTheyWereRegistered()
    {
        var truck = Vehicle("TRK-001", mileageKm: 50000);
        var inReverse = new[]
        {
            Record(truck, MaintenanceType.BrakeInspection, Day(5, 20), 40000),
            Record(truck, MaintenanceType.OilChange, Day(1, 10), 30000)
        };

        var result = Analyze(Structure(new[] { truck }, records: inReverse));

        Assert.Equal(10000, result.AverageKmBetweenServices!.Value, Tolerance);   // 40 000 − 30 000, no −10 000
        Assert.Equal("BrakeInspection", result.Vehicles.Single().LastServiceType);
    }

    [Fact]
    public void AnIntervalThatWouldBeNegative_IsDiscarded_ItIsADataEntryMistakeNotADistance()
    {
        var truck = Vehicle("TRK-001", mileageKm: 50000);
        var records = new[]
        {
            Record(truck, MaintenanceType.OilChange, Day(1, 10), 40000),
            Record(truck, MaintenanceType.Repair, Day(5, 20), 35000)   // más reciente pero con menos kilometraje
        };

        var result = Analyze(Structure(new[] { truck }, records: records));

        Assert.Null(result.AverageKmBetweenServices);
    }

    [Fact]
    public void TwoServicesOnTheSameDay_TheOneWithMoreMileageIsTheLatest()
    {
        var truck = Vehicle("TRK-001", mileageKm: 50000);
        var records = new[]
        {
            Record(truck, MaintenanceType.OilChange, Day(3, 3), 40000),
            Record(truck, MaintenanceType.BrakeInspection, Day(3, 3), 40100)
        };

        var summary = Analyze(Structure(new[] { truck }, records: records)).Vehicles.Single();

        Assert.Equal("BrakeInspection", summary.LastServiceType);
        Assert.Equal(9900, summary.KmSinceLastService, Tolerance);
    }

    [Fact]
    public void TheKilometersSinceTheLastService_NeverGoNegative()
    {
        var truck = Vehicle("TRK-001", mileageKm: 1000);   // el odómetro marca menos que el servicio registrado
        var summary = Analyze(Structure(new[] { truck }, records: new[] { Record(truck, MaintenanceType.Repair, Day(1, 1), 5000) })).Vehicles.Single();

        Assert.Equal(0, summary.KmSinceLastService);
    }

    [Fact]
    public void WithoutRecords_EveryVehicleIsListedAsWithoutMaintenance()
    {
        var result = Analyze(Structure(new[] { Vehicle("VAN-002"), Vehicle("TRK-001") }));

        Assert.Equal(0, result.TotalRecords);
        Assert.Equal(0, result.VehiclesWithRecords);
        Assert.Equal(new[] { "TRK-001", "VAN-002" }, result.VehiclesWithoutRecords);   // por placa
        Assert.Null(result.AverageKmBetweenServices);
        Assert.Empty(result.Vehicles);
        Assert.Equal(6, result.ByType.Count);
    }

    [Fact]
    public void ARecordOfAnUnknownVehicle_IsCounted_ButBelongsToNoVehicleSummary()
    {
        var known = Vehicle("TRK-001");
        var unknown = Vehicle("GHOST-1");   // no se incluye en la flota
        var records = new[] { Record(known, MaintenanceType.OilChange, Day(1, 1), 10), Record(unknown, MaintenanceType.Repair, Day(1, 2), 20) };

        var result = Analyze(Structure(new[] { known }, records: records));

        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(1, result.ByType.Single(type => type.Type == "Repair").Records);
        Assert.Equal("TRK-001", Assert.Single(result.Vehicles).LicensePlate);
    }

    [Fact]
    public void TheAverage_IsRoundedToThreeDecimals()
    {
        var truck = Vehicle("TRK-001", mileageKm: 99999);
        var records = new[] { Record(truck, MaintenanceType.OilChange, Day(1, 1), 1000), Record(truck, MaintenanceType.OilChange, Day(2, 1), 11000.12345) };

        Assert.Equal(10000.123, Analyze(Structure(new[] { truck }, records: records)).AverageKmBetweenServices!.Value);
    }

    [Fact]
    public void VisitingTheSameRecordTwice_DoesNotCountItTwice_AndTheOrderOfVisitsDoesNotMatter()
    {
        var (structure, _, _, _) = Scenario();
        var once = Analyze(structure);
        var shuffled = new MaintenanceHistoryVisitor();

        foreach (var element in structure.Elements.Reverse().Concat(structure.Elements))
            element.Accept(shuffled);

        Assert.Equal(Json.Of(once), Json.Of(shuffled.Result));
    }
}
