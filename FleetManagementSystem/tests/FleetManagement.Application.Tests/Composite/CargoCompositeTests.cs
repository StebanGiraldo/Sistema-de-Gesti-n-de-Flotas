using FleetManagement.Domain.Cargo;
using FleetManagement.Domain.Entities;
using Xunit;

namespace FleetManagement.Application.Tests.Composite;

/// <summary>
/// PRUEBAS DEL PATRÓN COMPOSITE sobre la estructura jerárquica de carga
/// (CargoItemComponent = hoja, CargoGroup = compuesto). Cubren: elemento
/// individual, grupo de elementos, grupo con subgrupos, cálculo de propiedades
/// agregadas, agregar/eliminar componentes, invariantes del árbol y recorrido.
/// </summary>
public class CargoCompositeTests
{
    private static CargoItemComponent Leaf(string description, double kg, double m3)
        => new(new CargoItem { Description = description, WeightKg = kg, VolumeM3 = m3 });

    // ---------- 1. Elemento individual ----------

    [Fact]
    public void Leaf_ExposesTheValuesOfItsCargoItem()
    {
        var leaf = Leaf("Cajas de repuestos", 12.5, 0.3);

        Assert.Equal("Cajas de repuestos", leaf.Name);
        Assert.Equal(12.5, leaf.TotalWeightKg);
        Assert.Equal(0.3, leaf.TotalVolumeM3);
        Assert.Equal(1, leaf.ItemCount);
        Assert.Empty(leaf.Children);
    }

    [Fact]
    public void Leaf_WithNullItem_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CargoItemComponent(null!));
    }

    // ---------- 2. Grupo de elementos ----------

    [Fact]
    public void Group_OfItems_SumsWeightVolumeAndItemCount()
    {
        var pallet = new CargoGroup("Pallet A");
        pallet.Add(Leaf("Cajas", 10, 0.1));
        pallet.Add(Leaf("Sacos", 20, 0.2));
        pallet.Add(Leaf("Tubos", 30, 0.3));

        Assert.Equal(60.0, pallet.TotalWeightKg);
        Assert.Equal(0.6, pallet.TotalVolumeM3, 3);
        Assert.Equal(3, pallet.ItemCount);
        Assert.Equal(3, pallet.Children.Count);
    }

    [Fact]
    public void EmptyGroup_HasZeroTotals()
    {
        var group = new CargoGroup("Vacío");

        Assert.Equal(0.0, group.TotalWeightKg);
        Assert.Equal(0.0, group.TotalVolumeM3);
        Assert.Equal(0, group.ItemCount);
        Assert.Empty(group.Children);
    }

    // ---------- 3. Grupo con subgrupos ----------

    [Fact]
    public void Group_WithSubgroups_AggregatesRecursively()
    {
        var root = new CargoGroup("Contenedor");
        var palletA = new CargoGroup("Pallet A");
        var box = new CargoGroup("Caja consolidada");

        root.Add(Leaf("Bolsa suelta", 5, 0.05));
        root.Add(palletA);
        palletA.Add(Leaf("Cajas", 10, 0.1));
        palletA.Add(box);
        box.Add(Leaf("Repuesto", 20, 0.2));

        Assert.Equal(35.0, root.TotalWeightKg);
        Assert.Equal(0.35, root.TotalVolumeM3, 3);
        Assert.Equal(3, root.ItemCount);

        Assert.Equal(30.0, palletA.TotalWeightKg);
        Assert.Equal(2, palletA.ItemCount);

        Assert.Equal(20.0, box.TotalWeightKg);
        Assert.Equal(1, box.ItemCount);
    }

    // ---------- 4. Cálculo de propiedades agregadas ----------

    [Fact]
    public void LeavesAndGroups_CanBeTreatedUniformly_ThroughTheComponentInterface()
    {
        var group = new CargoGroup("Grupo");
        group.Add(Leaf("A", 1, 0.1));
        group.Add(Leaf("B", 2, 0.2));
        ICargoComponent[] components = { Leaf("Suelto", 4, 0.4), group };

        // Mismo código para una hoja y para un grupo: no se pregunta por el tipo concreto.
        Assert.Equal(7.0, components.Sum(c => c.TotalWeightKg));
        Assert.Equal(3, components.Sum(c => c.ItemCount));
    }

    [Fact]
    public void GroupTotals_ReflectChangesMadeToTheUnderlyingCargoItem()
    {
        var item = new CargoItem { Description = "Motor", WeightKg = 100, VolumeM3 = 1 };
        var group = new CargoGroup("Grupo");
        group.Add(new CargoItemComponent(item));
        Assert.Equal(100.0, group.TotalWeightKg);

        item.WeightKg = 250;

        Assert.Equal(250.0, group.TotalWeightKg);
    }

    // ---------- 5. Agregar y eliminar componentes ----------

    [Fact]
    public void Add_IncreasesTheTotals_AndRemove_DecreasesThem()
    {
        var group = new CargoGroup("Grupo");
        var heavy = Leaf("Pesado", 100, 1);
        var light = Leaf("Liviano", 10, 0.1);

        group.Add(heavy);
        group.Add(light);
        Assert.Equal(110.0, group.TotalWeightKg);
        Assert.Equal(2, group.ItemCount);

        var removed = group.Remove(heavy);

        Assert.True(removed);
        Assert.Equal(10.0, group.TotalWeightKg);
        Assert.Equal(1, group.ItemCount);
        Assert.DoesNotContain(heavy, group.Children);
    }

    [Fact]
    public void Remove_OfASubgroup_RemovesItsWholeSubtree()
    {
        var root = new CargoGroup("Raíz");
        var sub = new CargoGroup("Sub");
        sub.Add(Leaf("A", 10, 0.1));
        sub.Add(Leaf("B", 20, 0.2));
        root.Add(Leaf("C", 5, 0.05));
        root.Add(sub);
        Assert.Equal(35.0, root.TotalWeightKg);

        Assert.True(root.Remove(sub));

        Assert.Equal(5.0, root.TotalWeightKg);
        Assert.Equal(1, root.ItemCount);
    }

    [Fact]
    public void Remove_OfAComponentThatIsNotAChild_ReturnsFalse()
    {
        var group = new CargoGroup("Grupo");
        group.Add(Leaf("A", 1, 0.1));

        Assert.False(group.Remove(Leaf("Otro", 1, 0.1)));
        Assert.Equal(1, group.ItemCount);
    }

    // ---------- Invariantes del árbol ----------

    [Fact]
    public void Add_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CargoGroup("G").Add(null!));
    }

    [Fact]
    public void Add_TheSameComponentTwice_ThrowsInvalidOperationException()
    {
        var group = new CargoGroup("G");
        var leaf = Leaf("A", 1, 0.1);
        group.Add(leaf);

        Assert.Throws<InvalidOperationException>(() => group.Add(leaf));
    }

    [Fact]
    public void Add_AGroupToItself_ThrowsInvalidOperationException()
    {
        var group = new CargoGroup("G");

        Assert.Throws<InvalidOperationException>(() => group.Add(group));
    }

    [Fact]
    public void Add_AnAncestorInsideItsDescendant_Throws_ToAvoidCycles()
    {
        var root = new CargoGroup("Raíz");
        var child = new CargoGroup("Hijo");
        root.Add(child);

        Assert.Throws<InvalidOperationException>(() => child.Add(root));
    }

    [Fact]
    public void Group_WithBlankName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CargoGroup("   "));
    }

    // ---------- Recorrido de la estructura ----------

    [Fact]
    public void Traverse_VisitsTheTreeDepthFirst_ParentBeforeItsChildren()
    {
        var root = new CargoGroup("Raíz");
        var sub = new CargoGroup("Sub");
        root.Add(Leaf("Uno", 1, 0.1));
        root.Add(sub);
        sub.Add(Leaf("Dos", 2, 0.2));
        sub.Add(Leaf("Tres", 3, 0.3));
        root.Add(Leaf("Cuatro", 4, 0.4));

        var names = root.Traverse().Select(c => c.Name).ToArray();

        Assert.Equal(new[] { "Raíz", "Uno", "Sub", "Dos", "Tres", "Cuatro" }, names);
    }

    [Fact]
    public void Traverse_OfALeaf_ReturnsOnlyTheLeaf()
    {
        var leaf = Leaf("Solo", 1, 0.1);

        Assert.Single(leaf.Traverse());
    }
}
