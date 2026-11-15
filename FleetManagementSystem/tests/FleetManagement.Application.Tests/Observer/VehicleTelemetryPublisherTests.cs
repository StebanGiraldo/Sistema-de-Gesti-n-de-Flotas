using FleetManagement.Application.Monitoring;
using FleetManagement.Application.Tests.Decorator;
using Xunit;

namespace FleetManagement.Application.Tests.Observer;

/// <summary>
/// Pruebas del SUJETO del patrón Observer (<see cref="VehicleTelemetryPublisher"/>) sin simulador ni repositorios: suscripción sin
/// duplicados, cancelación, entrega a todos en orden, aislamiento de los fallos de un observador, notificaciones que no se
/// vuelven recursivas ni cíclicas y entrega serializada cuando varios hilos publican a la vez.
/// </summary>
public class VehicleTelemetryPublisherTests
{
    private readonly RecordingAuditLogger _audit = new();

    private VehicleTelemetryPublisher Publisher(params ITelemetryObserver[] observers) => new(observers, _audit);

    // ---------------- Suscripción ----------------

    [Fact]
    public void ANewPublisher_HasNoObservers_AndPublishingDoesNothing()
    {
        var publisher = Publisher();

        publisher.Publish(Telemetry.Moved());   // no debe lanzar ni dejar rastro

        Assert.Equal(0, publisher.ObserverCount);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void ASubscribedObserver_ReceivesWhatIsPublished()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();
        publisher.Subscribe(observer);
        var moved = Telemetry.Moved();

        publisher.Publish(moved);

        Assert.Equal(1, publisher.ObserverCount);
        Assert.Same(moved, Assert.Single(observer.Received));
    }

    [Fact]
    public void TheSameObserver_SubscribedTwice_IsRegisteredOnce_AndReceivesEachEventOnce()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();

        publisher.Subscribe(observer);
        publisher.Subscribe(observer);
        publisher.Publish(Telemetry.Moved());

        Assert.Equal(1, publisher.ObserverCount);
        Assert.Single(observer.Received);
    }

    [Fact]
    public void TwoDifferentInstances_OfTheSameType_AreBothRegistered()
    {
        var publisher = Publisher();
        var first = new RecordingObserver();
        var second = new RecordingObserver();

        publisher.Subscribe(first);
        publisher.Subscribe(second);
        publisher.Publish(Telemetry.Moved());

        Assert.Equal(2, publisher.ObserverCount);   // la identidad es la instancia, no el tipo
        Assert.Single(first.Received);
        Assert.Single(second.Received);
    }

    [Fact]
    public void TheObserversGivenToTheConstructor_AreSubscribed_WithoutDuplicates()
    {
        var observer = new RecordingObserver();
        var other = new RecordingObserver();

        var publisher = new VehicleTelemetryPublisher(new ITelemetryObserver[] { observer, other, observer });   // lo que entrega el contenedor de dependencias

        Assert.Equal(2, publisher.ObserverCount);
    }

    [Fact]
    public void ThePublisher_CanBeCreatedWithoutObserversOrAuditLogger()
    {
        var publisher = new VehicleTelemetryPublisher();
        var observer = new RecordingObserver();
        publisher.Subscribe(observer);

        publisher.Publish(Telemetry.Moved());

        Assert.Single(observer.Received);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        var publisher = Publisher();

        Assert.Throws<ArgumentNullException>(() => publisher.Subscribe(null!));
        Assert.Throws<ArgumentNullException>(() => publisher.Unsubscribe(null!));
        Assert.Throws<ArgumentNullException>(() => publisher.Publish(null!));
    }

    // ---------------- Cancelar la suscripción ----------------

    [Fact]
    public void Unsubscribe_StopsTheDelivery_AndReportsWhetherItWasRegistered()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();
        publisher.Subscribe(observer);

        Assert.True(publisher.Unsubscribe(observer));
        Assert.False(publisher.Unsubscribe(observer));                  // ya no estaba
        Assert.False(publisher.Unsubscribe(new RecordingObserver()));   // nunca estuvo

        publisher.Publish(Telemetry.Moved());
        Assert.Empty(observer.Received);
        Assert.Equal(0, publisher.ObserverCount);
    }

    [Fact]
    public void DisposingTheSubscription_Unsubscribes_AndDisposingAgainIsHarmless()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();
        var subscription = publisher.Subscribe(observer);

        subscription.Dispose();
        subscription.Dispose();

        publisher.Publish(Telemetry.Moved());
        Assert.Empty(observer.Received);
        Assert.Equal(0, publisher.ObserverCount);
    }

    [Fact]
    public void AnObserver_CanSubscribeAgain_AfterBeingRemoved()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();
        publisher.Subscribe(observer).Dispose();

        publisher.Subscribe(observer);
        publisher.Publish(Telemetry.Moved());

        Assert.Single(observer.Received);
    }

    [Fact]
    public void DisposingAnOldSubscription_DoesNotRemoveTheObserverThatSubscribedAgain()
    {
        var publisher = Publisher();
        var observer = new RecordingObserver();
        var first = publisher.Subscribe(observer);
        first.Dispose();
        publisher.Subscribe(observer);

        first.Dispose();   // el token viejo ya estaba cancelado: no debe afectar a la suscripción nueva

        publisher.Publish(Telemetry.Moved());
        Assert.Single(observer.Received);
    }

    // ---------------- Entrega ----------------

    [Fact]
    public void EveryObserver_ReceivesEveryEvent_InTheOrderTheyWerePublished()
    {
        var first = new RecordingObserver();
        var second = new RecordingObserver();
        var third = new RecordingObserver();
        var publisher = Publisher(first, second, third);
        var events = new TelemetryEvent[] { Telemetry.Moved(stepKm: 1), Telemetry.Stop(), Telemetry.Moved(stepKm: 2), Telemetry.Arrived() };

        foreach (var telemetry in events)
            publisher.Publish(telemetry);

        Assert.Equal(events, first.Received);
        Assert.Equal(events, second.Received);
        Assert.Equal(events, third.Received);
    }

    [Fact]
    public void TheObservers_AreNotifiedInTheOrderTheySubscribed()
    {
        var order = new List<string>();
        var publisher = Publisher(
            new DelegateObserver(_ => order.Add("primero")),
            new DelegateObserver(_ => order.Add("segundo")),
            new DelegateObserver(_ => order.Add("tercero")));

        publisher.Publish(Telemetry.Moved());

        Assert.Equal(new[] { "primero", "segundo", "tercero" }, order);
    }

    // ---------------- Un observador que falla no afecta a nadie más ----------------

    [Fact]
    public void AFailingObserver_DoesNotStopTheOthers_NorTheCaller()
    {
        var before = new RecordingObserver();
        var failing = new ThrowingObserver();
        var after = new RecordingObserver();
        var publisher = Publisher(before, failing, after);

        publisher.Publish(Telemetry.Moved());   // no debe lanzar

        Assert.Single(before.Received);
        Assert.Single(after.Received);          // el observador que viene DESPUÉS del que falló recibe el evento igual
        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public void AFailingObserver_KeepsReceivingTheNextEvents()
    {
        var failing = new ThrowingObserver();
        var healthy = new RecordingObserver();
        var publisher = Publisher(failing, healthy);

        publisher.Publish(Telemetry.Moved(stepKm: 1));
        publisher.Publish(Telemetry.Moved(stepKm: 2));

        Assert.Equal(2, failing.Calls);
        Assert.Equal(2, healthy.Received.Count);
    }

    [Fact]
    public void TheFailureOfAnObserver_IsAudited_NamingTheObserverTheEventAndTheVehicle()
    {
        var publisher = Publisher(new ThrowingObserver());

        publisher.Publish(Telemetry.Moved(plate: "TRK-777"));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(VehicleTelemetryPublisher.FailureAuditCategory, entry.Category);
        Assert.Equal("Monitoreo", entry.Category);
        Assert.Contains(nameof(ThrowingObserver), entry.Message);
        Assert.Contains(nameof(VehicleMovedEvent), entry.Message);
        Assert.Contains("TRK-777", entry.Message);
        Assert.Contains(ThrowingObserver.FailureMessage, entry.Message);
    }

    [Fact]
    public void AFailure_IsReported_EvenWithoutAnAuditLogger()
    {
        var publisher = new VehicleTelemetryPublisher(new ITelemetryObserver[] { new ThrowingObserver() });

        publisher.Publish(Telemetry.Moved());   // sin bitácora, el fallo simplemente se descarta
    }

    [Fact]
    public void IfTheAuditLoggerFailsToo_TheDeliveryStillGoesOn()
    {
        var healthy = new RecordingObserver();
        var publisher = new VehicleTelemetryPublisher(new ITelemetryObserver[] { new ThrowingObserver(), healthy }, new ThrowingAuditLogger());

        publisher.Publish(Telemetry.Moved());

        Assert.Single(healthy.Received);
    }

    // ---------------- Notificaciones que no se vuelven recursivas ----------------

    [Fact]
    public void AnEventPublishedByAnObserver_IsQueued_AndDeliveredAfterTheCurrentOneReachedEveryone()
    {
        var log = new List<string>();
        VehicleTelemetryPublisher? publisher = null;
        var reactor = new DelegateObserver(telemetry =>
        {
            var step = ((VehicleMovedEvent)telemetry).StepKm;
            log.Add($"reactor:{step}");
            if (step == 1)
                publisher!.Publish(Telemetry.Moved(stepKm: 2));   // reacciona a un evento publicando otro
        });
        var witness = new DelegateObserver(telemetry => log.Add($"testigo:{((VehicleMovedEvent)telemetry).StepKm}"));
        publisher = Publisher(reactor, witness);

        publisher.Publish(Telemetry.Moved(stepKm: 1));

        // El evento 2 NO se entrega en medio del 1 (eso sería recursión): el testigo ve el 1 antes de que nadie vea el 2.
        Assert.Equal(new[] { "reactor:1", "testigo:1", "reactor:2", "testigo:2" }, log);
    }

    [Fact]
    public void NotificationsNeverNest_EvenWhenObserversKeepPublishing()
    {
        var depth = 0;
        var maxDepth = 0;
        var received = 0;
        VehicleTelemetryPublisher? publisher = null;
        var observer = new DelegateObserver(telemetry =>
        {
            depth++;
            maxDepth = Math.Max(maxDepth, depth);
            received++;
            if (((VehicleMovedEvent)telemetry).StepKm < 50)
                publisher!.Publish(Telemetry.Moved(stepKm: ((VehicleMovedEvent)telemetry).StepKm + 1));
            depth--;
        });
        publisher = Publisher(observer);

        publisher.Publish(Telemetry.Moved(stepKm: 1));

        Assert.Equal(50, received);
        Assert.Equal(1, maxDepth);   // la pila nunca crece: cada evento se entrega cuando el anterior terminó
    }

    // ---------------- Ciclos ----------------

    [Fact]
    public void ACycleOfNotifications_IsCut_AfterTheLimit_AndAudited()
    {
        var looping = new RecordingLoopingObserver();
        var publisher = Publisher(looping);
        looping.Publisher = publisher;

        publisher.Publish(Telemetry.Moved());   // debe terminar: sin el límite, esto no acabaría nunca

        Assert.Equal(1 + VehicleTelemetryPublisher.MaxReentrantPublishes, looping.Received);   // el original + los que el límite dejó pasar
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(VehicleTelemetryPublisher.FailureAuditCategory, entry.Category);
        Assert.Contains("ciclo", entry.Message);
        Assert.Contains(nameof(VehicleMovedEvent), entry.Message);
    }

    [Fact]
    public void AfterACycleWasCut_ThePublisherWorksNormally_AndTheLimitStartsAgain()
    {
        var looping = new RecordingLoopingObserver();
        var publisher = Publisher(looping);
        looping.Publisher = publisher;

        publisher.Publish(Telemetry.Moved());
        var afterFirstCycle = looping.Received;
        publisher.Publish(Telemetry.Moved());

        // Si el contador no se reiniciara, el segundo ciclo se cortaría de inmediato en lugar de repetir el mismo recorrido.
        Assert.Equal(2 * (1 + VehicleTelemetryPublisher.MaxReentrantPublishes), looping.Received);
        Assert.Equal(1 + VehicleTelemetryPublisher.MaxReentrantPublishes, afterFirstCycle);

        publisher.Unsubscribe(looping);
        var healthy = new RecordingObserver();
        publisher.Subscribe(healthy);
        publisher.Publish(Telemetry.Moved());
        Assert.Single(healthy.Received);
    }

    [Fact]
    public void ThePublishesOfAnObserver_UnderTheLimit_AreNotAffectedByIt()
    {
        var publisher = Publisher();
        var counter = new RecordingObserver();
        var burst = new DelegateObserver(telemetry =>
        {
            if (((VehicleMovedEvent)telemetry).StepKm == 0)
                for (var i = 1; i <= 25; i++)
                    publisher.Publish(Telemetry.Moved(stepKm: i));
        });
        publisher.Subscribe(burst);
        publisher.Subscribe(counter);

        publisher.Publish(Telemetry.Moved(stepKm: 0));

        Assert.Equal(26, counter.Received.Count);
        Assert.Empty(_audit.Entries);
    }

    // ---------------- Cambios del registro durante una entrega ----------------

    [Fact]
    public void ChangingTheObservers_WhileDelivering_DoesNotAlterTheDeliveryInProgress()
    {
        var publisher = Publisher();
        var removed = new RecordingObserver();
        var added = new RecordingObserver();
        var changer = new DelegateObserver(_ =>
        {
            publisher.Unsubscribe(removed);
            publisher.Subscribe(added);
        });
        publisher.Subscribe(changer);
        publisher.Subscribe(removed);

        publisher.Publish(Telemetry.Moved(stepKm: 1));
        publisher.Publish(Telemetry.Moved(stepKm: 2));

        // El evento 1 se entrega a quienes estaban al empezar (el retirado lo recibe, el nuevo no); el 2, a los de ahora.
        Assert.Equal(new[] { 1.0 }, removed.Received.OfType<VehicleMovedEvent>().Select(e => e.StepKm));
        Assert.Equal(new[] { 2.0 }, added.Received.OfType<VehicleMovedEvent>().Select(e => e.StepKm));
    }

    // ---------------- Varios hilos ----------------

    [Fact]
    public async Task ManyThreadsPublishing_EveryEventArrivesOnce_OneAtATime_InEachProducersOrder()
    {
        const int producers = 8;
        const int eventsPerProducer = 400;
        var observer = new ExclusiveObserver();
        var publisher = Publisher(observer);
        var vehicles = Enumerable.Range(0, producers).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(vehicles.Select(vehicle => Task.Run(() =>
        {
            for (var sequence = 1; sequence <= eventsPerProducer; sequence++)
                publisher.Publish(Telemetry.Moved(vehicleId: vehicle, mileageKm: sequence));
        })));

        Assert.Equal(producers * eventsPerProducer, observer.Received.Count);
        Assert.Equal(1, observer.MaxConcurrency);   // el observador nunca tuvo dos hilos dentro a la vez
        var expected = Enumerable.Range(1, eventsPerProducer).Select(n => (double)n).ToArray();
        foreach (var vehicle in vehicles)
        {
            var seen = observer.Received.OfType<VehicleMovedEvent>().Where(e => e.VehicleId == vehicle).Select(e => e.MileageKm).ToArray();
            Assert.Equal(expected, seen);           // y cada productor ve sus eventos en el orden en que los publicó
        }
    }

    [Fact]
    public void ManyThreadsSubscribing_DistinctObserversAreAllRegistered_AndTheSameOneOnlyOnce()
    {
        var publisher = Publisher();
        var distinct = Enumerable.Range(0, 60).Select(_ => new RecordingObserver()).ToArray();
        var shared = new RecordingObserver();

        Parallel.For(0, distinct.Length, index =>
        {
            publisher.Subscribe(distinct[index]);
            publisher.Subscribe(shared);
        });

        Assert.Equal(distinct.Length + 1, publisher.ObserverCount);
        publisher.Publish(Telemetry.Moved());
        Assert.Single(shared.Received);
        Assert.All(distinct, observer => Assert.Single(observer.Received));
    }

    [Fact]
    public async Task SubscribingAndUnsubscribing_WhilePublishing_NeverBreaksTheDelivery()
    {
        var publisher = Publisher();
        var stable = new RecordingObserver();
        publisher.Subscribe(stable);
        const int events = 300;

        var publishing = Task.Run(() =>
        {
            for (var i = 0; i < events; i++)
                publisher.Publish(Telemetry.Moved(stepKm: i));
        });
        var churning = Task.Run(() =>
        {
            for (var i = 0; i < 300; i++)
                publisher.Subscribe(new RecordingObserver()).Dispose();
        });
        await Task.WhenAll(publishing, churning);

        Assert.Equal(events, stable.Received.Count);   // el observador estable no perdió ni repitió ningún evento
        Assert.Equal(1, publisher.ObserverCount);
        Assert.Empty(_audit.Entries);
    }

    /// <summary>Observador que reacciona a cada evento publicando otro igual: un ciclo de notificaciones, para probar el límite.</summary>
    private sealed class RecordingLoopingObserver : ITelemetryObserver
    {
        public ITelemetryPublisher? Publisher { get; set; }

        public int Received { get; private set; }

        public void OnTelemetry(TelemetryEvent telemetry)
        {
            Received++;
            Publisher!.Publish(Telemetry.Moved());
        }
    }
}
