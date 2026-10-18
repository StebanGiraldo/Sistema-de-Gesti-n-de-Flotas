using FleetManagement.Application.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.BackgroundServices;

/// <summary>
/// Reloj de la simulación (IHostedService): cada <see cref="SimulationOptions.TickSeconds"/> segundos le avisa al
/// <see cref="IVehicleMovementSimulator"/> cuánto tiempo pasó. No decide nada del movimiento: esa lógica vive en
/// Application, donde se prueba sin esperar y sin depender del host (SRP). El frontend consulta la API por polling y aquí
/// es donde la posición "avanza" entre una consulta y la siguiente, tal como pide el enunciado.
///
/// Crea un scope por paso con IServiceScopeFactory porque este servicio es Singleton y el simulador depende de servicios
/// Scoped (p. ej. IDeliveryRouteService): es la práctica recomendada por Microsoft para servicios de larga duración.
/// Un fallo en un paso se registra y NO detiene el reloj; de lo contrario un error pasajero congelaría la simulación
/// hasta reiniciar la aplicación. El paso dura siempre lo mismo (<see cref="SimulationOptions.TickSeconds"/>): así el
/// avance no depende de la hora del sistema ni de cuánto tardó el paso anterior.
/// </summary>
public sealed class VehicleSimulationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<VehicleSimulationBackgroundService> _logger;

    public VehicleSimulationBackgroundService(
        IServiceScopeFactory scopeFactory,
        SimulationOptions options,
        ILogger<VehicleSimulationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tick = TimeSpan.FromSeconds(_options.TickSeconds);
        using var timer = new PeriodicTimer(tick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunTickAsync(tick, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // El host pidió detenerse mientras se esperaba el siguiente paso: es la salida normal del bucle, no un error.
            _logger.LogInformation("Simulación de movimiento detenida.");
        }
    }

    private async Task RunTickAsync(TimeSpan elapsed, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var simulator = scope.ServiceProvider.GetRequiredService<IVehicleMovementSimulator>();
            await simulator.AdvanceAsync(elapsed, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error al simular el movimiento de los vehículos.");
        }
    }
}
