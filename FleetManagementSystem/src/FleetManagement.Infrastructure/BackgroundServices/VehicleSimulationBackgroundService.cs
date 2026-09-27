using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.BackgroundServices;

/// <summary>
/// Servicio en segundo plano (IHostedService) que simula el movimiento de
/// los vehículos en estado "EnRoute" para que el dashboard muestre
/// actividad en tiempo real simulado, tal como pide el enunciado: el
/// frontend consume la API vía fetch() periódicamente (polling) y aquí es
/// donde esa posición "se mueve" entre una llamada y la siguiente.
///
/// Usa IServiceScopeFactory para crear un scope propio en cada ciclo, lo
/// cual es la práctica recomendada por Microsoft para que un servicio de
/// larga duración (Singleton) resuelva dependencias sin importar si esas
/// dependencias están registradas como Singleton o Scoped.
/// </summary>
public class VehicleSimulationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleSimulationBackgroundService> _logger;
    private readonly Random _random = new();

    public VehicleSimulationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<VehicleSimulationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var vehicleRepository = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
                var vehicles = await vehicleRepository.GetAllAsync();

                foreach (var vehicle in vehicles.Where(v => v.Status == VehicleStatus.EnRoute))
                {
                    var latJitter = (_random.NextDouble() - 0.5) * 0.006;
                    var lngJitter = (_random.NextDouble() - 0.5) * 0.006;
                    vehicle.UpdateLocation(vehicle.CurrentLocation.Y + latJitter, vehicle.CurrentLocation.X + lngJitter);
                    vehicle.MileageKm += _random.NextDouble() * 1.5;
                    await vehicleRepository.UpdateAsync(vehicle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al simular el movimiento de los vehículos.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Cierre solicitado por el host durante la espera: se sale del bucle de forma controlada.
                break;
            }
        }
    }
}
