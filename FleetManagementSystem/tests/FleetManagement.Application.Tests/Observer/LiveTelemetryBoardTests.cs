using FleetManagement.Application.Monitoring;
using Xunit;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// OBSERVADOR CONCRETO #2: el tablero en vivo es un modelo de lectura alimentado por los eventos. Estas pruebas lo alimentan a mano
/// (sin publicador ni simulador) y comprueban qué conserva, qué reemplaza, qué redondea y qué ignora.
/// </summary>
public class LiveTelemetryBoardTests
{
    private static readonly Guid Truck = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Van = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid FirstTrip = new("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid SecondTrip = new("cccccccc-0000-0000-0000-000000000002");

    private readonly LiveTelemetryBoard _board = new();

    [Fact]
    public void ABoardWithoutEvents_IsEmpty()
    {
        Assert.Empty(_board.GetAll());
        Assert.Null(_board.Get(Truck));
    }

    [Fact]
    public void TheFirstMovement_CreatesTheEntryOfThatVehicle()
    {
        _board.OnTelemetry(Telemetry.Moved(plate: "TRK-001", vehicleId: Truck, routeId: FirstTrip, latitude: 7.1, longitude: -73.1, mileageKm: 1234.5, percent: 40, nextStop: "Segunda", remainingKm: 40, etaMinutes: 24, at: Telemetry.At));

        var status = _board.Get(Truck)!;

        Assert.Equal(Truck, status.VehicleId);
        Assert.Equal("TRK-001", status.LicensePlate);
        Assert.Equal(FirstTrip, status.RouteId);
        Assert.Equal(7.1, status.Latitude);
        Assert.Equal(-73.1, status.Longitude);
        Assert.Equal(1234.5, status.MileageKm);
        Assert.Equal(40, status.Percent);
        Assert.Equal("Segunda", status.NextStopLabel);
        Assert.Null(status.LastStopReached);
        Assert.Equal(40, status.RemainingKm);
        Assert.Equal(24, status.EtaMinutes);
        Assert.False(status.Arrived);
        Assert.Equal(Telemetry.At, status.UpdatedAt);
    }

    [Fact]
    public void TheValues_AreRoundedLikeTheProgressQuery_SoTheTwoNeverDisagree()
    {
        _board.OnTelemetry(Telemetry.Moved(
            vehicleId: Truck, latitude: 7.123456789, longitude: -73.987654321, mileageKm: 1000.123456, percent: 33.33333, remainingKm: 4.56789, etaMinutes: 12.3456));

        var status = _board.Get(Truck)!;

        Assert.Equal(7.123457, status.Latitude);       // 6 decimales (≈ 0,1 m)
        Assert.Equal(-73.987654, status.Longitude);
        Assert.Equal(1000.123, status.MileageKm);      // 3 decimales (metros)
        Assert.Equal(33.3, status.Percent);            // 1 decimal
        Assert.Equal(4.568, status.RemainingKm);       // 3 decimales
        Assert.Equal(12.3, status.EtaMinutes);         // 1 decimal
    }

    [Fact]
    public void ASecondMovement_ReplacesTheEntry_ThereIsOneEntryPerVehicle()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, mileageKm: 1000, percent: 10));
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, mileageKm: 1010, percent: 20));

        var status = Assert.Single(_board.GetAll());

        Assert.Equal(1010, status.MileageKm);
        Assert.Equal(20, status.Percent);
    }

    [Fact]
    public void AReachedStop_IsRemembered_UntilTheTripChanges()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, routeId: FirstTrip, percent: 10));
        _board.OnTelemetry(Telemetry.Stop(label: "Primera", vehicleId: Truck, routeId: FirstTrip));

        Assert.Equal("Primera", _board.Get(Truck)!.LastStopReached);

        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, routeId: FirstTrip, percent: 30));   // sigue el mismo viaje
        Assert.Equal("Primera", _board.Get(Truck)!.LastStopReached);

        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, routeId: SecondTrip, percent: 5));    // empezó otro viaje
        Assert.Null(_board.Get(Truck)!.LastStopReached);
    }

    [Fact]
    public void TheLatestStop_ReplacesThePreviousOne()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck));
        _board.OnTelemetry(Telemetry.Stop(label: "Primera", vehicleId: Truck));
        _board.OnTelemetry(Telemetry.Stop(label: "Segunda", vehicleId: Truck));

        Assert.Equal("Segunda", _board.Get(Truck)!.LastStopReached);
    }

    [Fact]
    public void AStop_UpdatesTheTimestamp_ButNotTheRestOfThePosition()
    {
        var later = Telemetry.At.AddSeconds(30);
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, latitude: 1, longitude: 2, percent: 10, at: Telemetry.At));
        _board.OnTelemetry(Telemetry.Stop(vehicleId: Truck, at: later));

        var status = _board.Get(Truck)!;

        Assert.Equal(later, status.UpdatedAt);
        Assert.Equal(1, status.Latitude);
        Assert.Equal(2, status.Longitude);
        Assert.Equal(10, status.Percent);
    }

    [Fact]
    public void Arrival_MarksTheVehicleAsArrived_AndKeepsItsLastKnownData()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, plate: "TRK-001", percent: 100, remainingKm: 0, etaMinutes: 0, nextStop: "Destino"));
        _board.OnTelemetry(Telemetry.Arrived(vehicleId: Truck, at: Telemetry.At.AddMinutes(1)));

        var status = _board.Get(Truck)!;

        Assert.True(status.Arrived);
        Assert.Equal(100, status.Percent);
        Assert.Equal("Destino", status.NextStopLabel);
        Assert.Equal(Telemetry.At.AddMinutes(1), status.UpdatedAt);
    }

    [Fact]
    public void ANewTripAfterArrival_ClearsTheArrivedFlag()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, routeId: FirstTrip));
        _board.OnTelemetry(Telemetry.Arrived(vehicleId: Truck, routeId: FirstTrip));

        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, routeId: SecondTrip, percent: 3));

        var status = _board.Get(Truck)!;
        Assert.False(status.Arrived);
        Assert.Equal(SecondTrip, status.RouteId);
    }

    [Fact]
    public void AStopOrArrival_OfAnUnknownVehicle_IsIgnored_TheBoardDoesNotInventAPosition()
    {
        _board.OnTelemetry(Telemetry.Stop(vehicleId: Van));
        _board.OnTelemetry(Telemetry.Arrived(vehicleId: Van));

        Assert.Empty(_board.GetAll());
        Assert.Null(_board.Get(Van));
    }

    [Fact]
    public void GetAll_ListsEveryVehicle_OrderedByPlate()
    {
        _board.OnTelemetry(Telemetry.Moved(plate: "VAN-002", vehicleId: Van));
        _board.OnTelemetry(Telemetry.Moved(plate: "TRK-001", vehicleId: Truck));

        Assert.Equal(new[] { "TRK-001", "VAN-002" }, _board.GetAll().Select(status => status.LicensePlate));
    }

    [Fact]
    public void Get_AnswersOnlyForTheVehicleAsked()
    {
        _board.OnTelemetry(Telemetry.Moved(plate: "TRK-001", vehicleId: Truck));

        Assert.Equal("TRK-001", _board.Get(Truck)!.LicensePlate);
        Assert.Null(_board.Get(Van));
        Assert.Null(_board.Get(Guid.Empty));
    }

    [Fact]
    public void GetAll_ReturnsASnapshot_ThatLaterEventsDoNotChange()
    {
        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, percent: 10));
        var snapshot = _board.GetAll();

        _board.OnTelemetry(Telemetry.Moved(vehicleId: Truck, percent: 90));
        _board.OnTelemetry(Telemetry.Moved(plate: "VAN-002", vehicleId: Van));

        Assert.Single(snapshot);
        Assert.Equal(10, snapshot[0].Percent);
        Assert.Equal(2, _board.GetAll().Count);
    }

    [Fact]
    public void AnEventOfAnotherKind_IsIgnored_WithoutFailing()
    {
        _board.OnTelemetry(new UnknownKindEvent(Truck, "TRK-001", FirstTrip, Telemetry.At));

        Assert.Empty(_board.GetAll());
    }

    [Fact]
    public void TheBoard_IsAnObserverAndAReadModel_AtTheSameTime()
    {
        Assert.IsAssignableFrom<ITelemetryObserver>(_board);
        Assert.IsAssignableFrom<ILiveTelemetryBoard>(_board);
    }

    [Fact]
    public async Task ManyWritersAndReaders_NeverSeeAVehicleHalfUpdated_AndEndWithOneEntryEach()
    {
        const int vehicles = 8;
        const int movesPerVehicle = 300;
        var ids = Enumerable.Range(0, vehicles).Select(_ => Guid.NewGuid()).ToArray();
        var finished = false;

        var readers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            while (!Volatile.Read(ref finished))
            {
                foreach (var status in _board.GetAll())
                {
                    // Cada fila sale de un solo evento: kilometraje y porcentaje siempre van en la misma proporción (kilometraje = 1000 + porcentaje).
                    Assert.Equal(status.MileageKm - 1000, status.Percent);
                }
            }
        })).ToArray();

        await Task.WhenAll(ids.Select((id, index) => Task.Run(() =>
        {
            for (var move = 1; move <= movesPerVehicle; move++)
            {
                _board.OnTelemetry(Telemetry.Moved(plate: $"V-{index:00}", vehicleId: id, mileageKm: 1000 + move, percent: move));
                if (move % 50 == 0)
                    _board.OnTelemetry(Telemetry.Stop(vehicleId: id, label: $"Parada {move}"));
            }
        })));
        Volatile.Write(ref finished, true);
        await Task.WhenAll(readers);

        var all = _board.GetAll();
        Assert.Equal(vehicles, all.Count);
        Assert.All(all, status => Assert.Equal(movesPerVehicle, status.Percent));
        Assert.All(all, status => Assert.Equal("Parada 300", status.LastStopReached));
    }

    private sealed record UnknownKindEvent(Guid VehicleId, string LicensePlate, Guid RouteId, DateTimeOffset OccurredAt)
        : TelemetryEvent(VehicleId, LicensePlate, RouteId, OccurredAt);
}
