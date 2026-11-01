using FleetManagement.Application.Geo;
using FleetManagement.Application.Optimization;
using FleetManagement.Domain.Enums;
using Xunit;
using static FleetManagement.Application.Tests.Optimization.OptimizationTestData;

namespace FleetManagement.Application.Tests.Optimization;

public class PendingDeliveryTests
{
    [Fact]
    public void PriorityRank_PutsUrgentBeforeHighBeforeFragileBeforeStandard()
    {
        Assert.True(Delivery("u", 1, CargoPriority.Urgent).PriorityRank > Delivery("h", 1, CargoPriority.High).PriorityRank);
        Assert.True(Delivery("h", 1, CargoPriority.High).PriorityRank > Delivery("f", 1, CargoPriority.Fragile).PriorityRank);
        Assert.True(Delivery("f", 1, CargoPriority.Fragile).PriorityRank > Delivery("s", 1, CargoPriority.Standard).PriorityRank);
    }
}

public class FirstFitInOrderStrategyTests
{
    private readonly FirstFitInOrderStrategy _strategy = new();
    private static readonly GeoCoordinate Depot = OnEquator(0);

    [Fact]
    public void GivesEachDeliveryToTheFirstVehicleOfTheListThatHasRoom_EvenIfItIsTheBiggest()
    {
        var truck = Vehicle("TRK-001", 8000);
        var moto = Vehicle("MOT-004", 30);

        var result = _strategy.Assign(new[] { Delivery("sobre", 2) }, new[] { truck, moto }, Depot);

        var load = Assert.Single(result.Loads);
        Assert.Equal("TRK-001", load.Vehicle.LicensePlate);   // un camión para un sobre: justamente lo que se quiere evitar
    }

    [Fact]
    public void ProcessesDeliveriesInTheOrderReceived_SoALargeOneMayFindNoRoomLeft()
    {
        // Dos vehículos de 10 kg. En orden: 4 y 4 llenan el primero (quedan 2); 6 va al segundo; el último 6 ya no cabe en ninguno.
        var vehicles = new[] { Vehicle("A", 10), Vehicle("B", 10) };
        var deliveries = new[] { Delivery("d1", 4), Delivery("d2", 4), Delivery("d3", 6), Delivery("d4", 6) };

        var result = _strategy.Assign(deliveries, vehicles, Depot);

        Assert.Equal("d4", Assert.Single(result.Unassigned).Delivery.Description);
        Assert.Equal(new[] { "d1", "d2" }, result.Loads.Single(l => l.Vehicle.LicensePlate == "A").Deliveries.Select(d => d.Description));
    }

    [Fact]
    public void ExplainsWhyADeliveryWasLeftOut()
    {
        var result = _strategy.Assign(new[] { Delivery("enorme", 9999) }, new[] { Vehicle("A", 100) }, Depot);

        Assert.Contains("Excede la capacidad", Assert.Single(result.Unassigned).Reason);
    }

    [Fact]
    public void WithoutVehicles_EverythingIsUnassigned()
    {
        var result = _strategy.Assign(new[] { Delivery("d1", 1), Delivery("d2", 1) }, new List<CandidateVehicle>(), Depot);

        Assert.Empty(result.Loads);
        Assert.All(result.Unassigned, item => Assert.Equal("No hay vehículos disponibles.", item.Reason));
    }
}

public class PriorityBestFitStrategyTests
{
    private readonly PriorityBestFitStrategy _strategy = new();
    private static readonly GeoCoordinate Depot = OnEquator(0);

    private static List<(string Description, string Plate)> Placements(CargoAssignment result)
        => result.Loads.SelectMany(load => load.Deliveries.Select(d => (d.Description, load.Vehicle.LicensePlate))).ToList();

    [Fact]
    public void PlacesBigDeliveriesFirst_SoEverythingFitsWhereTheNaiveOrderFails()
    {
        // La misma situación en la que la línea base dejó una entrega fuera: ahora caben las cuatro.
        var vehicles = new[] { Vehicle("A", 10), Vehicle("B", 10) };
        var deliveries = new[] { Delivery("d1", 4), Delivery("d2", 4), Delivery("d3", 6), Delivery("d4", 6) };

        var result = _strategy.Assign(deliveries, vehicles, Depot);

        Assert.Empty(result.Unassigned);
        Assert.Equal(2, result.Loads.Count);
        Assert.All(result.Loads, load => Assert.Equal(10, load.TotalWeightKg));
    }

    [Fact]
    public void UrgentDeliveriesAreServedFirst_WhenSpaceIsScarce()
    {
        var vehicles = new[] { Vehicle("A", 10) };
        var deliveries = new[] { Delivery("estándar", 8), Delivery("urgente", 5, CargoPriority.Urgent), Delivery("alta", 4, CargoPriority.High) };

        var result = _strategy.Assign(deliveries, vehicles, Depot);

        Assert.Equal("estándar", Assert.Single(result.Unassigned).Delivery.Description);
        Assert.Equal(new[] { "urgente", "alta" }, Assert.Single(result.Loads).Deliveries.Select(d => d.Description));
    }

    [Fact]
    public void ConsolidatesIntoAVehicleAlreadyLoaded_InsteadOfOpeningAnother()
    {
        // La moto está más cerca del origen, pero la segunda entrega cabe en el camión que ya salió: no se abre un segundo vehículo.
        var truck = Vehicle("TRK-001", 1000, longitude: 0.5);
        var moto = Vehicle("MOT-004", 100, longitude: 0.0);
        var deliveries = new[] { Delivery("grande", 600), Delivery("pequeña", 20) };

        var result = _strategy.Assign(deliveries, new[] { truck, moto }, Depot);

        var load = Assert.Single(result.Loads);
        Assert.Equal("TRK-001", load.Vehicle.LicensePlate);
        Assert.Equal(2, load.Deliveries.Count);
    }

    [Fact]
    public void AmongLoadedVehicles_ChoosesTheOneThatFitsTightest()
    {
        var big = Vehicle("A-BIG", 500);
        var small = Vehicle("B-SMALL", 100);
        // 450 solo cabe en A-BIG (quedan 50); 90 ya no cabe ahí y abre B-SMALL (quedan 10). Los 8 kg caben en los dos:
        // deben ir donde queda menos hueco (B-SMALL: 10 → 2) y no donde sobra más (A-BIG: 50 → 42).
        var deliveries = new[] { Delivery("d450", 450), Delivery("d90", 90), Delivery("d8", 8) };

        var placements = Placements(_strategy.Assign(deliveries, new[] { big, small }, Depot));

        Assert.Contains(("d450", "A-BIG"), placements);
        Assert.Contains(("d90", "B-SMALL"), placements);
        Assert.Contains(("d8", "B-SMALL"), placements);
    }

    [Fact]
    public void OpeningANewVehicle_PrefersTheOneThatReachesTheDepotSooner()
    {
        var near = Vehicle("NEAR", 100, longitude: 0.01);
        var far = Vehicle("FAR", 100, longitude: 3.0);

        var result = _strategy.Assign(new[] { Delivery("d1", 10) }, new[] { far, near }, Depot);

        Assert.Equal("NEAR", Assert.Single(result.Loads).Vehicle.LicensePlate);
    }

    [Fact]
    public void ReachingTheDepotSooner_TakesTheSpeedOfTheVehicleIntoAccount()
    {
        var slowButCloser = Vehicle("SLOW", 100, longitude: 0.5, speedKmh: 20);   // 55,6 km a 20 km/h: 2,8 h
        var fastButFarther = Vehicle("FAST", 100, longitude: 1.0, speedKmh: 120); // 111 km a 120 km/h: 0,9 h

        var result = _strategy.Assign(new[] { Delivery("d1", 10) }, new[] { slowButCloser, fastButFarther }, Depot);

        Assert.Equal("FAST", Assert.Single(result.Loads).Vehicle.LicensePlate);
    }

    [Fact]
    public void WhenArrivalTimeTies_UsesTheSmallerVehicle()
    {
        var truck = Vehicle("TRK-001", 8000);
        var moto = Vehicle("MOT-004", 30);

        var result = _strategy.Assign(new[] { Delivery("sobre", 2) }, new[] { truck, moto }, Depot);

        Assert.Equal("MOT-004", Assert.Single(result.Loads).Vehicle.LicensePlate);
    }

    [Fact]
    public void ChecksVolumeToo_WhenTheVehicleDeclaresIt()
    {
        var tight = Vehicle("TIGHT", 1000, capacityM3: 1.0);
        var roomy = Vehicle("ROOMY", 1000, capacityM3: 5.0, longitude: 0.5);

        var result = _strategy.Assign(new[] { Delivery("voluminosa", 10, m3: 3.0) }, new[] { tight, roomy }, Depot);

        Assert.Equal("ROOMY", Assert.Single(result.Loads).Vehicle.LicensePlate);
    }

    [Fact]
    public void WithoutDeclaredVolume_OnlyWeightLimitsTheLoad()
    {
        var result = _strategy.Assign(new[] { Delivery("voluminosa", 10, m3: 9999) }, new[] { Vehicle("A", 100, capacityM3: null) }, Depot);

        Assert.Empty(result.Unassigned);
    }

    [Fact]
    public void ExplainsWhenThereIsNoRoomLeft_AsDifferentFromTooBig()
    {
        var result = _strategy.Assign(new[] { Delivery("d1", 8), Delivery("d2", 8) }, new[] { Vehicle("A", 10) }, Depot);

        var left = Assert.Single(result.Unassigned);
        Assert.Contains("ya no tienen espacio libre", left.Reason);
    }

    [Fact]
    public void NeverExceedsTheCapacityOfAVehicle_AndPlacesEveryDeliveryExactlyOnce()
    {
        var random = new Random(31);
        for (var run = 0; run < 100; run++)
        {
            var vehicles = Enumerable.Range(0, random.Next(1, 5))
                .Select(i => Vehicle($"V-{i}", random.Next(20, 200), random.Next(0, 2) == 0 ? null : random.Next(1, 8), random.NextDouble())).ToList();
            var deliveries = Enumerable.Range(0, random.Next(1, 15))
                .Select(i => Delivery($"d{i}", random.Next(1, 120), (CargoPriority)random.Next(0, 4), random.NextDouble() * 3, random.NextDouble())).ToList();

            var result = _strategy.Assign(deliveries, vehicles, Depot);

            Assert.All(result.Loads, load =>
            {
                Assert.True(load.TotalWeightKg <= load.Vehicle.CapacityKg + 1e-9);
                if (load.Vehicle.CapacityM3 is { } m3) Assert.True(load.TotalVolumeM3 <= m3 + 1e-9);
            });
            var everyDelivery = result.Loads.SelectMany(l => l.Deliveries).Concat(result.Unassigned.Select(u => u.Delivery));
            Assert.Equal(deliveries.Select(d => d.Description).OrderBy(x => x), everyDelivery.Select(d => d.Description).OrderBy(x => x));
        }
    }

    [Fact]
    public void WhenTheFleetCannotCarryEverything_FavorsTheHeavyDeliveries_EvenIfFewerDeliveriesGoOut()
    {
        // Regla de negocio, no un defecto: con una sola furgoneta de 55 kg se lleva la entrega de 54 kg y quedan las demás;
        // el orden simple llevaría 4 entregas pequeñas (39 kg). Lo que no se lleva queda listado con su motivo.
        var vehicles = new[] { Vehicle("V-0", 55) };
        var deliveries = new[] { 11, 54, 20, 26, 42, 4, 47, 35, 4 }.Select((kg, index) => Delivery($"d{index}", kg)).ToList();

        var optimized = _strategy.Assign(deliveries, vehicles, Depot);
        var naive = new FirstFitInOrderStrategy().Assign(deliveries, vehicles, Depot);

        Assert.Equal(54, optimized.Loads.Sum(load => load.TotalWeightKg));
        Assert.Equal(8, optimized.Unassigned.Count);
        Assert.Equal(39, naive.Loads.Sum(load => load.TotalWeightKg));
        Assert.Equal(5, naive.Unassigned.Count);
        Assert.All(optimized.Unassigned, left => Assert.False(string.IsNullOrWhiteSpace(left.Reason)));
    }

    // Instancias al azar con semilla fija (siempre las mismas). Las dos pruebas siguientes son COMPROBACIONES EMPÍRICAS sobre
    // ese lote, no garantías generales: empaquetar es un problema difícil y ninguna heurística voraz le gana a otra en todas
    // las instancias posibles.
    private static (List<CandidateVehicle> Vehicles, List<PendingDelivery> Deliveries) RandomInstance(Random random)
    {
        var vehicles = Enumerable.Range(0, random.Next(1, 4)).Select(i => Vehicle($"V-{i}", random.Next(50, 150))).ToList();
        var deliveries = Enumerable.Range(0, random.Next(1, 10)).Select(i => Delivery($"d{i}", random.Next(1, 60))).ToList();
        return (vehicles, deliveries);
    }

    [Fact]
    public void WhenTheNaiveOrderPlacesEverything_SoDoesThisStrategy_InTheSeededBatch()
    {
        var naive = new FirstFitInOrderStrategy();
        var random = new Random(43);
        var compared = 0;
        for (var run = 0; run < 100; run++)
        {
            var (vehicles, deliveries) = RandomInstance(random);
            if (naive.Assign(deliveries, vehicles, Depot).Unassigned.Count > 0)
                continue;

            var optimized = _strategy.Assign(deliveries, vehicles, Depot);
            compared++;

            Assert.True(optimized.Unassigned.Count == 0, $"corrida {run}: el orden simple lo asignó todo y esta estrategia dejó {optimized.Unassigned.Count} sin asignar");
        }

        Assert.True(compared >= 30, $"solo {compared} instancias fueron comparables; el lote no ejercita la comprobación");
    }

    [Fact]
    public void OverTheSeededBatch_CarriesMoreTotalWeightThanTheNaiveOrder()
    {
        // Medida agregada: instancia por instancia puede llevar menos (en este lote, 3 de 100), pero en conjunto aprovecha mejor la flota.
        var naive = new FirstFitInOrderStrategy();
        var random = new Random(43);
        double optimizedKg = 0, naiveKg = 0;
        for (var run = 0; run < 100; run++)
        {
            var (vehicles, deliveries) = RandomInstance(random);
            optimizedKg += _strategy.Assign(deliveries, vehicles, Depot).Loads.Sum(load => load.TotalWeightKg);
            naiveKg += naive.Assign(deliveries, vehicles, Depot).Loads.Sum(load => load.TotalWeightKg);
        }

        Assert.True(optimizedKg > naiveKg, $"{optimizedKg} kg frente a {naiveKg} kg del orden simple");
    }

    [Fact]
    public void GivesTheSameAnswerForTheSameData()
    {
        var vehicles = new[] { Vehicle("A", 100, longitude: 0.2), Vehicle("B", 100, longitude: 0.2) };   // empate total entre A y B
        var deliveries = new[] { Delivery("d1", 10), Delivery("d2", 10), Delivery("d3", 10) };

        Assert.Equal(Placements(_strategy.Assign(deliveries, vehicles, Depot)), Placements(_strategy.Assign(deliveries, vehicles, Depot)));
    }

    [Fact]
    public void DoesNotModifyItsInputs()
    {
        var vehicles = new[] { Vehicle("A", 100) };
        var deliveries = new[] { Delivery("d1", 10), Delivery("d2", 20) };

        _strategy.Assign(deliveries, vehicles, Depot);

        Assert.Equal(new[] { "d1", "d2" }, deliveries.Select(d => d.Description));
        Assert.Single(vehicles);
    }
}
