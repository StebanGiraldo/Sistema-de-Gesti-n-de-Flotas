using FleetManagement.Application.DTOs;
using FleetManagement.Application.Factories;
using FleetManagement.Application.Services;
using FleetManagement.Infrastructure.Logging;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Factories;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN DEL FACTORY METHOD a través del caso de uso completo
/// (VehicleService.CreateVehicleAsync), usando las implementaciones reales en
/// memoria de Infrastructure (sin mocks) para ejercitar la cadena completa:
/// VehicleService -&gt; VehicleFactoryProvider -&gt; fábrica concreta -&gt; Vehicle
/// -&gt; repositorio -&gt; VehicleDto. Esto es lo mismo que hace FleetController
/// al recibir un POST /api/fleet/vehicles.
///
/// Nota sobre el Singleton: FleetAuditLogger.Instance es una única instancia
/// compartida durante toda la ejecución de pruebas (es, literalmente, el
/// patrón Singleton funcionando). No se hacen aserciones sobre su contenido
/// aquí para no acoplar estas pruebas al orden de ejecución, pero se reutiliza
/// deliberadamente para demostrar que VehicleService lo invoca sin errores
/// como parte del flujo de creación.
/// </summary>
public class VehicleServiceFactoryMethodTests
{
    private static VehicleService CreateService()
    {
        return new VehicleService(
            new InMemoryVehicleRepository(),
            new InMemoryDriverRepository(),
            new VehicleFactoryProvider(),
            new FleetOnboardingFactoryProvider(),
            FleetAuditLogger.Instance);
    }

    [Theory]
    [InlineData("Truck", 8000)]
    [InlineData("Van", 1500)]
    [InlineData("Car", 400)]
    [InlineData("Motorcycle", 30)]
    public async Task CreateVehicleAsync_DelegatesToFactoryMethod_AndReturnsCategoryCapacity(string type, double expectedCapacityKg)
    {
        var service = CreateService();
        var request = new CreateVehicleRequest($"TEST-{type.ToUpperInvariant()}", "MarcaX", "ModeloX", 2023, type, 7.0, -73.0);

        var result = await service.CreateVehicleAsync(request);

        Assert.Equal(expectedCapacityKg, result.CapacityKg);
        Assert.Equal(type, result.Type);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task CreateVehicleAsync_WithInvalidType_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = new CreateVehicleRequest("TEST-INVALID", "MarcaX", "ModeloX", 2023, "Helicoptero", 7.0, -73.0);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateVehicleAsync(request));
    }

    [Fact]
    public async Task CreateVehicleAsync_WithDuplicateLicensePlate_ThrowsArgumentException()
    {
        var service = CreateService();
        var request = new CreateVehicleRequest("DUP-001", "MarcaX", "ModeloX", 2023, "Truck", 7.0, -73.0);
        await service.CreateVehicleAsync(request);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateVehicleAsync(request));
    }
}
