using System.Collections.Concurrent;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Monitoring;

/// <summary>
/// CONCRETE SUBJECT (patrón OBSERVER): mantiene el registro de observadores y les distribuye cada evento de telemetría que
/// publica el simulador de movimiento. Es lo único que esta clase hace: no decide qué hace cada observador (eso es de ellos),
/// no envía notificaciones por canales (eso es del patrón Bridge) y no coordina vehículos con rutas (eso es del Mediator).
///
/// Garantías, pensadas para que el patrón no genere los problemas típicos:
/// <list type="bullet">
/// <item><b>Registro seguro entre hilos.</b> La lista de observadores es una copia inmutable que se reemplaza completa al
/// registrar o retirar (copy-on-write): quien publica recorre la copia que leyó, sin candados, y un observador puede
/// registrar o retirar a otro (o a sí mismo) mientras recibe un evento sin romper la entrega en curso.</item>
/// <item><b>Sin duplicados.</b> Un observador queda registrado una sola vez (por referencia): registrarlo de nuevo no le
/// duplica los eventos.</item>
/// <item><b>Sin recursión.</b> Los eventos pasan por una cola y los entrega un solo hilo a la vez. Si un observador publica
/// mientras recibe un evento, el nuevo evento espera en la cola y se entrega al terminar el actual (en orden): la pila no
/// crece. Como medida de seguridad, si los observadores se publican eventos entre sí más de
/// <see cref="MaxReentrantPublishes"/> veces en una misma entrega (un ciclo), se descarta el exceso y se deja constancia.</item>
/// <item><b>Sin entregas simultáneas.</b> Al haber un solo hilo entregando, ningún observador recibe dos eventos a la vez ni los
/// recibe desordenados, aunque publiquen varios hilos; por eso no tienen que sincronizar su propio estado. Si otro hilo publica
/// mientras uno entrega, su evento lo entrega el que ya está entregando, antes de que ese hilo termine su llamada.</item>
/// <item><b>Aislamiento de fallos.</b> Si un observador lanza una excepción, los demás reciben igual el evento, la simulación
/// no se detiene y el fallo queda en la auditoría (categoría <see cref="FailureAuditCategory"/>).</item>
/// </list>
///
/// Se registra como Singleton: el registro de observadores y la cola deben ser únicos y sobrevivir a cada paso de la simulación
/// (que se ejecuta en un ámbito nuevo por paso). Por eso solo depende de servicios Singleton.
/// </summary>
public sealed class VehicleTelemetryPublisher : ITelemetryPublisher, ITelemetrySubject
{
    /// <summary>Tope de eventos que los observadores pueden publicar, como reacción a otros, dentro de una misma entrega.</summary>
    public const int MaxReentrantPublishes = 1000;

    /// <summary>Categoría de auditoría de los fallos de entrega (un observador que lanza, un ciclo cortado).</summary>
    public const string FailureAuditCategory = "Monitoreo";

    private readonly object _registryLock = new();
    private readonly ConcurrentQueue<TelemetryEvent> _pending = new();
    private readonly IFleetAuditLogger? _audit;

    private ITelemetryObserver[] _observers = Array.Empty<ITelemetryObserver>();

    // Id del hilo que está entregando eventos (0 = nadie). Es la señal "hay una entrega en curso" y a la vez permite reconocer
    // una publicación hecha por un observador: solo el hilo que entrega ejecuta observadores.
    private int _delivererThreadId;

    // Eventos que los observadores publicaron durante la entrega actual. Solo lo toca el hilo que entrega.
    private int _reentrantPublishes;

    /// <param name="observers">Observadores que quedan registrados desde el inicio (el contenedor de DI entrega todos los registrados).</param>
    /// <param name="audit">Donde se deja constancia de los fallos de entrega; si es <c>null</c>, los fallos se aíslan sin registro.</param>
    public VehicleTelemetryPublisher(IEnumerable<ITelemetryObserver>? observers = null, IFleetAuditLogger? audit = null)
    {
        _audit = audit;
        foreach (var observer in observers ?? Enumerable.Empty<ITelemetryObserver>())
            Subscribe(observer);
    }

    public int ObserverCount => Volatile.Read(ref _observers).Length;

    public IDisposable Subscribe(ITelemetryObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_registryLock)
        {
            if (!IsRegistered(_observers, observer))
                Volatile.Write(ref _observers, _observers.Append(observer).ToArray());
        }

        return new Subscription(this, observer);
    }

    public bool Unsubscribe(ITelemetryObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_registryLock)
        {
            if (!IsRegistered(_observers, observer))
                return false;

            Volatile.Write(ref _observers, _observers.Where(registered => !ReferenceEquals(registered, observer)).ToArray());
            return true;
        }
    }

    public void Publish(TelemetryEvent telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (IsCalledByAnObserver() && ++_reentrantPublishes > MaxReentrantPublishes)
        {
            ReportFailure(
                $"Se descartó un evento {telemetry.GetType().Name} del vehículo {telemetry.LicensePlate}: los observadores publicaron " +
                $"más de {MaxReentrantPublishes} eventos como reacción a otros (posible ciclo de notificaciones).");
            return;
        }

        _pending.Enqueue(telemetry);
        DeliverPending();
    }

    private bool IsCalledByAnObserver() => Volatile.Read(ref _delivererThreadId) == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Entrega lo que haya en la cola si nadie más lo está entregando. Si ya hay un hilo entregando (otro hilo, o este mismo si lo
    /// llamó un observador), no hace nada: ese hilo recogerá el evento. Después de soltar el turno vuelve a mirar la cola, para
    /// no dejar un evento sin entregar si llegó justo en ese instante.
    /// </summary>
    private void DeliverPending()
    {
        var thisThread = Environment.CurrentManagedThreadId;

        while (!_pending.IsEmpty)
        {
            if (Interlocked.CompareExchange(ref _delivererThreadId, thisThread, 0) != 0)
                return;

            try
            {
                while (_pending.TryDequeue(out var next))
                    Deliver(next);
            }
            finally
            {
                _reentrantPublishes = 0;
                Volatile.Write(ref _delivererThreadId, 0);
            }
        }
    }

    private void Deliver(TelemetryEvent telemetry)
    {
        // Se recorre la copia que había al empezar: registrar o retirar observadores durante la entrega no la altera.
        foreach (var observer in Volatile.Read(ref _observers))
        {
            try
            {
                observer.OnTelemetry(telemetry);
            }
            catch (Exception failure)
            {
                ReportFailure($"El observador {observer.GetType().Name} falló al recibir {telemetry.GetType().Name} del vehículo {telemetry.LicensePlate}: {failure.Message}");
            }
        }
    }

    private void ReportFailure(string message)
    {
        try
        {
            _audit?.LogEvent(FailureAuditCategory, message);
        }
        catch
        {
            // Si ni siquiera se puede auditar el fallo, se descarta: informar un error no debe detener la entrega a los demás
            // observadores ni la simulación.
        }
    }

    private static bool IsRegistered(ITelemetryObserver[] registered, ITelemetryObserver observer)
        => registered.Any(candidate => ReferenceEquals(candidate, observer));

    /// <summary>Token de una suscripción: al liberarse retira al observador, una sola vez.</summary>
    private sealed class Subscription : IDisposable
    {
        private readonly VehicleTelemetryPublisher _subject;
        private readonly ITelemetryObserver _observer;
        private int _disposed;

        public Subscription(VehicleTelemetryPublisher subject, ITelemetryObserver observer)
        {
            _subject = subject;
            _observer = observer;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _subject.Unsubscribe(_observer);
        }
    }
}
