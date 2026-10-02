using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Composite;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN del Composite a través del caso de uso completo
/// (CargoManifestService), usando el repositorio real en memoria y sin mocks:
/// repositorio -> CargoManifest (Composite) -> CargoManifestDto.
/// </summary>
public class CargoManifestServiceTests
{
    private static async Task<(CargoManifestService Service, DeliveryRoute Route)> CreateAsync(params CargoItem[] items)
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = new DeliveryRoute { Name = "Ruta Centro" };
        route.CargoItems.AddRange(items);
        await repository.AddAsync(route);
        return (new CargoManifestService(repository), route);
    }

    [Fact]
    public async Task GetManifestAsync_ForAnExistingRoute_ReturnsTheHierarchyAsDto()
    {
        var (service, route) = await CreateAsync(
            new CargoItem { Description = "Repuestos industriales", WeightKg = 320, VolumeM3 = 1.8, Priority = CargoPriority.High },
            new CargoItem { Description = "Insumos médicos", WeightKg = 45, VolumeM3 = 0.4, Priority = CargoPriority.Urgent },
            new CargoItem { Description = "Documentos", WeightKg = 5, VolumeM3 = 0.05, Priority = CargoPriority.Urgent });

        var dto = await service.GetManifestAsync(route.Id);

        Assert.NotNull(dto);
        Assert.Equal(route.Id, dto!.RouteId);
        Assert.Equal("Group", dto.Manifest.Kind);
        Assert.Equal("Ruta Centro", dto.Manifest.Name);
        Assert.Equal(370.0, dto.Manifest.TotalWeightKg);
        Assert.Equal(2.25, dto.Manifest.TotalVolumeM3);
        Assert.Equal(3, dto.Manifest.ItemCount);

        Assert.Equal(new[] { "Prioridad High", "Prioridad Urgent" }, dto.Manifest.Children.Select(c => c.Name).ToArray());

        var urgent = dto.Manifest.Children[1];
        Assert.Equal(50.0, urgent.TotalWeightKg);
        Assert.Equal(2, urgent.ItemCount);

        // Una hoja y un grupo comparten la misma forma; la hoja sólo tiene Kind = Item y no tiene hijos.
        var leaf = urgent.Children[0];
        Assert.Equal("Item", leaf.Kind);
        Assert.Equal(1, leaf.ItemCount);
        Assert.Empty(leaf.Children);
    }

    [Fact]
    public async Task GetManifestAsync_ForAnUnknownRoute_ReturnsNull()
    {
        var (service, _) = await CreateAsync();

        Assert.Null(await service.GetManifestAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetManifestAsync_RoundsTotalsToThreeDecimals_ToAvoidFloatingPointNoise()
    {
        var (service, route) = await CreateAsync(
            new CargoItem { Description = "A", WeightKg = 1, VolumeM3 = 0.1 },
            new CargoItem { Description = "B", WeightKg = 1, VolumeM3 = 0.2 });

        var dto = await service.GetManifestAsync(route.Id);

        // 0.1 + 0.2 = 0.30000000000000004 en doble precisión.
        Assert.Equal(0.3, dto!.Manifest.TotalVolumeM3);
    }
}
