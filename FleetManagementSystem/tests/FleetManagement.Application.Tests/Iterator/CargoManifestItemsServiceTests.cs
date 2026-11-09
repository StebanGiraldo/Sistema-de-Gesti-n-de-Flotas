using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Iterator;

/// <summary>
/// PRUEBAS DE INTEGRACIÓN del Iterator a través del caso de uso completo (CargoManifestService), con el repositorio
/// real en memoria y sin mocks: repositorio → CargoManifest (Composite) → iterador → lista plana de artículos.
/// Comprueban también que el manifiesto jerárquico que ya existía sigue igual.
/// </summary>
public class CargoManifestItemsServiceTests
{
    private static async Task<(CargoManifestService Service, DeliveryRoute Route, InMemoryDeliveryRouteRepository Repository)> CreateAsync(
        params CargoItem[] items)
    {
        var repository = new InMemoryDeliveryRouteRepository();
        var route = new DeliveryRoute { Name = "Ruta Centro" };
        route.CargoItems.AddRange(items);
        await repository.AddAsync(route);
        return (new CargoManifestService(repository), route, repository);
    }

    private static CargoItem Item(string description, double kg, double m3, CargoPriority priority)
        => new() { Description = description, WeightKg = kg, VolumeM3 = m3, Priority = priority };

    /// <summary>Recorre el DTO jerárquico de hojas a raíz sin conocer el servicio: sirve para comparar ambos endpoints.</summary>
    private static IEnumerable<CargoManifestNodeDto> LeavesOf(CargoManifestNodeDto node)
        => node.Kind == "Item" ? new[] { node } : node.Children.SelectMany(LeavesOf);

    [Fact]
    public async Task GetCargoItemsAsync_ReturnsTheFlatItems_InTheDepthFirstOrderOfTheManifest()
    {
        var (service, route, _) = await CreateAsync(
            Item("Frágil", 2, 0.02, CargoPriority.Fragile),
            Item("Documentos", 5, 0.05, CargoPriority.Standard),
            Item("Insumos médicos", 45, 0.4, CargoPriority.Urgent),
            Item("Repuestos", 320, 1.8, CargoPriority.Standard));

        var dto = await service.GetCargoItemsAsync(route.Id);

        Assert.NotNull(dto);
        Assert.Equal(route.Id, dto!.RouteId);
        // Grupos en el orden del enum (Standard, High, Urgent, Fragile); dentro de cada grupo, el orden de la ruta.
        Assert.Equal(new[] { "Documentos", "Repuestos", "Insumos médicos", "Frágil" }, dto.Items.Select(i => i.Description).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, dto.Items.Select(i => i.Position).ToArray());
        Assert.Equal(new[] { "Standard", "Standard", "Urgent", "Fragile" }, dto.Items.Select(i => i.Priority).ToArray());
    }

    [Fact]
    public async Task GetCargoItemsAsync_ReturnsTheDataOfEachItem()
    {
        var (service, route, _) = await CreateAsync(Item("Repuestos industriales", 320, 1.8, CargoPriority.High));

        var item = Assert.Single((await service.GetCargoItemsAsync(route.Id))!.Items);

        Assert.Equal("Repuestos industriales", item.Description);
        Assert.Equal(320.0, item.WeightKg);
        Assert.Equal(1.8, item.VolumeM3);
        Assert.Equal("High", item.Priority);
        Assert.Equal(1, item.Position);
    }

    [Fact]
    public async Task GetCargoItemsAsync_ItemsAreTheLeavesOfTheHierarchicalManifest_InTheSameOrder()
    {
        var (service, route, _) = await CreateAsync(
            Item("A", 10, 0.1, CargoPriority.Urgent),
            Item("B", 20, 0.2, CargoPriority.Standard),
            Item("C", 30, 0.3, CargoPriority.Urgent),
            Item("D", 40, 0.4, CargoPriority.High));

        var flat = (await service.GetCargoItemsAsync(route.Id))!;
        var leaves = LeavesOf((await service.GetManifestAsync(route.Id))!.Manifest).ToList();

        Assert.Equal(leaves.Select(l => l.Name), flat.Items.Select(i => i.Description));
        Assert.Equal(leaves.Select(l => l.TotalWeightKg), flat.Items.Select(i => i.WeightKg));
        Assert.Equal(leaves.Select(l => l.TotalVolumeM3), flat.Items.Select(i => i.VolumeM3));
    }

    [Fact]
    public async Task GetCargoItemsAsync_TotalsAndCount_MatchTheRootOfTheHierarchicalManifest()
    {
        var (service, route, _) = await CreateAsync(
            Item("A", 320, 1.8, CargoPriority.High),
            Item("B", 45, 0.4, CargoPriority.Urgent),
            Item("C", 5, 0.05, CargoPriority.Urgent));

        var flat = (await service.GetCargoItemsAsync(route.Id))!;
        var root = (await service.GetManifestAsync(route.Id))!.Manifest;

        Assert.Equal(root.ItemCount, flat.ItemCount);
        Assert.Equal(flat.Items.Count, flat.ItemCount);
        Assert.Equal(root.TotalWeightKg, flat.TotalWeightKg);
        Assert.Equal(root.TotalVolumeM3, flat.TotalVolumeM3);
        Assert.Equal(370.0, flat.TotalWeightKg);
        Assert.Equal(2.25, flat.TotalVolumeM3);
    }

    [Fact]
    public async Task GetCargoItemsAsync_ForARouteWithoutCargo_ReturnsAnEmptyListWithZeroTotals()
    {
        var (service, route, _) = await CreateAsync();

        var dto = await service.GetCargoItemsAsync(route.Id);

        Assert.NotNull(dto);
        Assert.Empty(dto!.Items);
        Assert.Equal(0, dto.ItemCount);
        Assert.Equal(0.0, dto.TotalWeightKg);
        Assert.Equal(0.0, dto.TotalVolumeM3);
    }

    [Fact]
    public async Task GetCargoItemsAsync_ForAnUnknownRoute_ReturnsNull()
    {
        var (service, _, _) = await CreateAsync();

        Assert.Null(await service.GetCargoItemsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetCargoItemsAsync_RoundsToThreeDecimals_LikeTheHierarchicalManifest()
    {
        var (service, route, _) = await CreateAsync(
            Item("A", 1, 0.1, CargoPriority.Standard),
            Item("B", 1, 0.2, CargoPriority.Standard));

        var dto = (await service.GetCargoItemsAsync(route.Id))!;

        // 0.1 + 0.2 = 0.30000000000000004 en doble precisión.
        Assert.Equal(0.3, dto.TotalVolumeM3);
    }

    [Fact]
    public async Task GetCargoItemsAsync_RoundsEachItemToThreeDecimals_AndMatchesTheLeafOfTheManifest()
    {
        // Con más de 3 decimales el redondeo se nota: 1.23456 kg → 1.235 y 0.98765 m³ → 0.988, igual que la hoja del manifiesto.
        var (service, route, _) = await CreateAsync(Item("Pieza fina", 1.23456, 0.98765, CargoPriority.Standard));

        var item = Assert.Single((await service.GetCargoItemsAsync(route.Id))!.Items);
        var leaf = Assert.Single(LeavesOf((await service.GetManifestAsync(route.Id))!.Manifest));

        Assert.Equal(1.235, item.WeightKg);
        Assert.Equal(0.988, item.VolumeM3);
        Assert.Equal(leaf.TotalWeightKg, item.WeightKg);
        Assert.Equal(leaf.TotalVolumeM3, item.VolumeM3);
    }

    [Fact]
    public async Task GetCargoItemsAsync_RepeatedItemsOfTheRoute_AreReportedAsManyTimesAsTheRouteHasThem()
    {
        // Un artículo no es "duplicado" por llamarse igual: el iterador sólo evita repetir EL MISMO componente.
        var (service, route, _) = await CreateAsync(
            Item("Caja", 10, 0.1, CargoPriority.Standard),
            Item("Caja", 10, 0.1, CargoPriority.Standard));

        var dto = (await service.GetCargoItemsAsync(route.Id))!;

        Assert.Equal(2, dto.ItemCount);
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(20.0, dto.TotalWeightKg);
    }

    [Fact]
    public async Task GetCargoItemsAsync_DoesNotModifyTheRoute()
    {
        var (service, route, repository) = await CreateAsync(
            Item("A", 10, 0.1, CargoPriority.High),
            Item("B", 20, 0.2, CargoPriority.Urgent));

        await service.GetCargoItemsAsync(route.Id);
        await service.GetCargoItemsAsync(route.Id);

        var stored = (await repository.GetByIdAsync(route.Id))!;
        Assert.Equal(new[] { "A", "B" }, stored.CargoItems.Select(c => c.Description).ToArray());
        Assert.Equal(new[] { 10.0, 20.0 }, stored.CargoItems.Select(c => c.WeightKg).ToArray());
    }

    [Fact]
    public async Task GetManifestAsync_KeepsReturningTheHierarchy_AfterAddingTheIterator()
    {
        var (service, route, _) = await CreateAsync(
            Item("Repuestos", 320, 1.8, CargoPriority.High),
            Item("Insumos", 45, 0.4, CargoPriority.Urgent));

        var manifest = (await service.GetManifestAsync(route.Id))!;

        Assert.Equal("Group", manifest.Manifest.Kind);
        Assert.Equal("Ruta Centro", manifest.Manifest.Name);
        Assert.Equal(new[] { "Prioridad High", "Prioridad Urgent" }, manifest.Manifest.Children.Select(c => c.Name).ToArray());
        Assert.Equal("Item", manifest.Manifest.Children[0].Children[0].Kind);
    }
}
