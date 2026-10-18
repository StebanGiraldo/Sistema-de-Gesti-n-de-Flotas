using FleetManagement.Application.Simulation;
using FleetManagement.Infrastructure.BackgroundServices;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FleetManagement.Application.Tests.Simulation;

/// <summary>
/// Pruebas del reloj de la simulación (<see cref="VehicleSimulationBackgroundService"/>): solo debe llamar al simulador en
/// cada paso, sobrevivir a un fallo y detenerse al cancelar, también si el paso está en curso. Usa dobles escritos a mano
/// para el contenedor de DI (no depende del contenedor real) y un paso de 20 ms; en vez de dormir una cantidad fija,
/// espera la señal del simulador, con un límite holgado solo para que una falla no deje la prueba colgada.
/// </summary>
public class VehicleSimulationBackgroundServiceTests
{
    private const double TickSeconds = 0.02;

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task EveryTick_AsksTheSimulatorToAdvanceByTheTickDuration_InsideAFreshScope()
    {
        var simulator = new SignalingSimulator(targetCalls: 3);
        var scopes = new FakeScopeFactory(simulator);
        using var service = CreateService(scopes, new ListLogger<VehicleSimulationBackgroundService>());

        await service.StartAsync(CancellationToken.None);
        await simulator.ReachedTarget.WaitAsync(WaitLimit);
        await service.StopAsync(CancellationToken.None);

        Assert.True(simulator.Calls >= 3);
        Assert.All(simulator.ElapsedValues, elapsed => Assert.Equal(TimeSpan.FromSeconds(TickSeconds), elapsed));
        Assert.Equal(simulator.Calls, scopes.ScopesCreated);
        Assert.Equal(scopes.ScopesCreated, scopes.ScopesDisposed);
    }

    [Fact]
    public async Task AFailingTick_IsLoggedAsAnError_AndTheClockKeepsRunning()
    {
        var simulator = new SignalingSimulator(targetCalls: 3) { FailOnCall = 1 };
        var logger = new ListLogger<VehicleSimulationBackgroundService>();
        using var service = CreateService(new FakeScopeFactory(simulator), logger);

        await service.StartAsync(CancellationToken.None);
        await simulator.ReachedTarget.WaitAsync(WaitLimit); // tras el fallo del paso 1 siguen los pasos 2 y 3
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task Stop_EndsTheLoopCleanly_AndNoMoreTicksHappenAfterwards()
    {
        var simulator = new SignalingSimulator(targetCalls: 1);
        var logger = new ListLogger<VehicleSimulationBackgroundService>();
        using var service = CreateService(new FakeScopeFactory(simulator), logger);
        await service.StartAsync(CancellationToken.None);
        await simulator.ReachedTarget.WaitAsync(WaitLimit);

        await service.StopAsync(CancellationToken.None);
        var callsAtStop = simulator.Calls;
        await Task.Delay(150); // varios periodos de 20 ms: si el reloj siguiera vivo, habría más llamadas

        Assert.Equal(callsAtStop, simulator.Calls);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Stop_WhileATickIsRunning_CancelsItAndEndsWithoutLoggingAnError()
    {
        var simulator = new SignalingSimulator(targetCalls: 1) { BlockUntilCancelled = true };
        var scopes = new FakeScopeFactory(simulator);
        var logger = new ListLogger<VehicleSimulationBackgroundService>();
        using var service = CreateService(scopes, logger);
        await service.StartAsync(CancellationToken.None);
        await simulator.ReachedTarget.WaitAsync(WaitLimit); // el paso está en curso y no termina solo

        await service.StopAsync(CancellationToken.None);

        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(1, simulator.Calls);
        Assert.Equal(scopes.ScopesCreated, scopes.ScopesDisposed);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private static VehicleSimulationBackgroundService CreateService(FakeScopeFactory scopes, ListLogger<VehicleSimulationBackgroundService> logger)
        => new(scopes, new SimulationOptions { TickSeconds = TickSeconds }, logger);
}
