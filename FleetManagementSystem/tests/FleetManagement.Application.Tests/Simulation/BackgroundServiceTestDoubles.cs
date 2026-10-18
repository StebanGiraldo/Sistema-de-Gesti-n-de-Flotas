using System.Collections.Concurrent;
using FleetManagement.Application.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Simulador de prueba para el servicio de fondo: cuenta cuántas veces lo llaman, avisa cuando llega a las llamadas que se
/// esperaban (así la prueba espera un hecho, no un tiempo fijo) y puede fallar en una llamada o quedarse esperando hasta que
/// lo cancelen. Lo llama otro hilo, por eso los contadores y las colas son seguros entre hilos.
/// </summary>
public sealed class SignalingSimulator : IVehicleMovementSimulator
{
    private readonly int _targetCalls;
    private readonly TaskCompletionSource _reachedTarget = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<TimeSpan> _elapsed = new();
    private int _calls;

    public SignalingSimulator(int targetCalls)
    {
        _targetCalls = targetCalls;
    }

    /// <summary>Número de llamada (empezando en 1) en la que el simulador lanza un error.</summary>
    public int? FailOnCall { get; init; }

    /// <summary>Si es verdadero, cada llamada espera hasta que la cancelen (simula un paso que tarda).</summary>
    public bool BlockUntilCancelled { get; init; }

    public int Calls => Volatile.Read(ref _calls);

    public IReadOnlyCollection<TimeSpan> ElapsedValues => _elapsed;

    /// <summary>Se completa cuando el simulador ya recibió las llamadas esperadas.</summary>
    public Task ReachedTarget => _reachedTarget.Task;

    public async Task AdvanceAsync(TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        _elapsed.Enqueue(elapsed);
        var call = Interlocked.Increment(ref _calls);
        if (call >= _targetCalls)
            _reachedTarget.TrySetResult();

        if (BlockUntilCancelled)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        if (call == FailOnCall)
            throw new InvalidOperationException("Fallo de prueba en el simulador.");
    }
}

/// <summary>
/// Fábrica de scopes de prueba escrita a mano (sin depender del contenedor real de DI): entrega siempre el simulador dado y
/// cuenta cuántos scopes se crearon y cuántos se liberaron, para comprobar que el servicio de fondo no deja scopes abiertos.
/// </summary>
public sealed class FakeScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;
    private int _created;
    private int _disposed;

    public FakeScopeFactory(IVehicleMovementSimulator simulator)
    {
        _provider = new SingleSimulatorProvider(simulator);
    }

    public int ScopesCreated => Volatile.Read(ref _created);

    public int ScopesDisposed => Volatile.Read(ref _disposed);

    public IServiceScope CreateScope()
    {
        Interlocked.Increment(ref _created);
        return new FakeScope(_provider, () => Interlocked.Increment(ref _disposed));
    }

    private sealed class FakeScope : IServiceScope
    {
        private readonly Action _onDispose;

        public FakeScope(IServiceProvider provider, Action onDispose)
        {
            ServiceProvider = provider;
            _onDispose = onDispose;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose() => _onDispose();
    }

    private sealed class SingleSimulatorProvider : IServiceProvider
    {
        private readonly IVehicleMovementSimulator _simulator;

        public SingleSimulatorProvider(IVehicleMovementSimulator simulator)
        {
            _simulator = simulator;
        }

        public object? GetService(Type serviceType) => serviceType == typeof(IVehicleMovementSimulator) ? _simulator : null;
    }
}

/// <summary>Mensaje que recibió el logger de prueba.</summary>
public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>Logger de prueba que guarda lo que recibe (cola segura entre hilos, porque el servicio de fondo escribe desde otro).</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
}
