using FleetManagement.Application.Builders;
using FleetManagement.Domain.Cargo;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Composite;

/// <summary>
/// PRUEBAS de CargoManifest, que arma el árbol Composite a partir de los
/// datos reales de una DeliveryRoute (agrupando por CargoPriority). Incluye
/// pruebas de coexistencia con Builder y Prototype para demostrar que el
/// Composite sólo LEE la ruta y no interfiere con los patrones existentes.
/// </summary>
public class CargoManifestTests
{
    private static CargoItem Item(string description, double kg, double m3, CargoPriority priority)
        => new() { Description = description, WeightKg = kg, VolumeM3 = m3, Priority = priority };

    private static DeliveryRoute RouteWith(params CargoItem[] items)
    {
        var route = new DeliveryRoute { Name = "Ruta Norte" };
        route.CargoItems.AddRange(items);
        return route;
    }

    [Fact]
    public void ForRoute_GroupsItemsByPriority_OmittingEmptyGroups_InEnumOrder()
    {
        var route = RouteWith(
            Item("Sobre", 1, 0.01, CargoPriority.Urgent),
            Item("Cajas", 10, 0.2, CargoPriority.Standard),
            Item("Vidrio", 5, 0.1, CargoPriority.Fragile),
            Item("Tornillos", 2, 0.02, CargoPriority.Standard));

        var manifest = CargoManifest.ForRoute(route);

        Assert.Equal("Ruta Norte", manifest.Name);
        Assert.Equal(
            new[] { "Prioridad Standard", "Prioridad Urgent", "Prioridad Fragile" },
            manifest.Children.Select(c => c.Name).ToArray());
        Assert.Equal(2, manifest.Children[0].ItemCount);
    }

    [Fact]
    public void ForRoute_RootTotals_EqualTheSumOfTheRouteCargoItems()
    {
        var route = RouteWith(
            Item("A", 320, 1.8, CargoPriority.High),
            Item("B", 45, 0.4, CargoPriority.Urgent),
            Item("C", 5, 0.05, CargoPriority.Urgent));

        var manifest = CargoManifest.ForRoute(route);

        Assert.Equal(route.CargoItems.Sum(c => c.WeightKg), manifest.TotalWeightKg);
        Assert.Equal(route.CargoItems.Sum(c => c.VolumeM3), manifest.TotalVolumeM3, 6);
        Assert.Equal(route.CargoItems.Count, manifest.ItemCount);
    }

    [Fact]
    public void ForRoute_WithoutCargo_ReturnsAnEmptyRoot()
    {
        var manifest = CargoManifest.ForRoute(RouteWith());

        Assert.Empty(manifest.Children);
        Assert.Equal(0, manifest.ItemCount);
        Assert.Equal(0.0, manifest.TotalWeightKg);
    }

    [Fact]
    public void ForRoute_WithBlankRouteName_UsesADefaultRootName()
    {
        var route = new DeliveryRoute { Name = "  " };

        Assert.Equal("Ruta sin nombre", CargoManifest.ForRoute(route).Name);
    }

    [Fact]
    public void ForRoute_WithNullRoute_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CargoManifest.ForRoute(null!));
    }

    [Fact]
    public void ForRoute_Leaves_WrapTheSameCargoItemInstancesOfTheRoute()
    {
        var route = RouteWith(
            Item("A", 1, 0.1, CargoPriority.Standard),
            Item("B", 2, 0.2, CargoPriority.High));

        var wrapped = CargoManifest.ForRoute(route).Traverse().OfType<CargoItemComponent>().Select(l => l.Item).ToList();

        Assert.Equal(2, wrapped.Count);
        foreach (var item in route.CargoItems)
            Assert.Contains(item, wrapped);
    }

    // ---------- Coexistencia con los patrones existentes ----------

    [Fact]
    public void Composite_CoexistsWithBuilder_ManifestOfARouteBuiltByTheDirector()
    {
        var route = new DeliveryRouteDirector().BuildExpressRoute(
            new DeliveryRouteBuilder(), "Express", 7.11, -73.12, 7.13, -73.10, Guid.NewGuid(), Guid.NewGuid(), 12, 25);

        var manifest = CargoManifest.ForRoute(route);

        Assert.Equal(50.0, manifest.TotalWeightKg);
        Assert.Equal(0.2, manifest.TotalVolumeM3, 3);
        Assert.Equal(1, manifest.ItemCount);
        Assert.Equal("Prioridad Urgent", manifest.Children.Single().Name);
    }

    [Fact]
    public void Composite_CoexistsWithPrototype_ManifestOfAClonedRouteIsIndependent()
    {
        var route = RouteWith(
            Item("Cajas", 10, 0.2, CargoPriority.Standard),
            Item("Sobre", 1, 0.01, CargoPriority.Urgent));
        var clone = route.Clone();

        var original = CargoManifest.ForRoute(route);
        var copy = CargoManifest.ForRoute(clone);

        Assert.Equal(original.TotalWeightKg, copy.TotalWeightKg);
        Assert.Equal(original.ItemCount, copy.ItemCount);

        // Prototype hizo copias profundas: cambiar la copia no altera el manifiesto del original.
        clone.CargoItems[0].WeightKg = 999;

        Assert.Equal(11.0, original.TotalWeightKg);
        Assert.Equal(1000.0, CargoManifest.ForRoute(clone).TotalWeightKg);
    }
}
