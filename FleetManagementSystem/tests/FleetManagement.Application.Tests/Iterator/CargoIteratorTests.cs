using FleetManagement.Domain.Cargo;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using Xunit;

namespace FleetManagement.Application.Tests.Iterator;

/// <summary>
/// PRUEBAS DEL PATRÓN ITERATOR sobre el árbol COMPOSITE de carga: recorrido completo, estructuras anidadas, orden,
/// hojas sueltas, estructuras vacías, ausencia de duplicados y compatibilidad con el <c>Traverse()</c> que ya existía.
/// Trabajan directamente sobre Domain, sin repositorios ni servicios.
/// </summary>
public class CargoIteratorTests
{
    private static CargoItemComponent Leaf(string name, double kg = 1, double m3 = 0.1)
        => new(new CargoItem { Description = name, WeightKg = kg, VolumeM3 = m3 });

    private static List<ICargoComponent> Drain(ICargoIterator iterator)
    {
        var all = new List<ICargoComponent>();
        while (iterator.HasNext())
            all.Add(iterator.Next());
        return all;
    }

    private static List<string> Names(ICargoIterator iterator) => Drain(iterator).Select(c => c.Name).ToList();

    /// <summary>
    /// Raíz
    ///  ├─ A            (grupo)
    ///  │   ├─ a1
    ///  │   └─ a2
    ///  ├─ B            (grupo)
    ///  │   ├─ b1
    ///  │   └─ Sub      (grupo anidado)
    ///  │       └─ s1
    ///  └─ c1
    /// </summary>
    private static CargoGroup SampleTree()
    {
        var a = new CargoGroup("A");
        a.Add(Leaf("a1"));
        a.Add(Leaf("a2"));

        var sub = new CargoGroup("Sub");
        sub.Add(Leaf("s1"));

        var b = new CargoGroup("B");
        b.Add(Leaf("b1"));
        b.Add(sub);

        var root = new CargoGroup("Raíz");
        root.Add(a);
        root.Add(b);
        root.Add(Leaf("c1"));
        return root;
    }

    // ---------- Recorrido completo, anidado y en orden ----------

    [Fact]
    public void CreateIterator_VisitsEveryGroupAndItem_DepthFirstInPreOrder()
    {
        var names = Names(SampleTree().CreateIterator());

        Assert.Equal(new[] { "Raíz", "A", "a1", "a2", "B", "b1", "Sub", "s1", "c1" }, names);
    }

    [Fact]
    public void CreateItemIterator_VisitsOnlyTheIndividualItems_InTheOrderOfTheTree()
    {
        var names = Names(SampleTree().CreateItemIterator());

        Assert.Equal(new[] { "a1", "a2", "b1", "s1", "c1" }, names);
    }

    [Fact]
    public void Iterator_KeepsTheOrderOfTheChildren_EvenWhenItIsNotAlphabetical()
    {
        var root = new CargoGroup("Raíz");
        foreach (var name in new[] { "z", "m", "a", "k" })
            root.Add(Leaf(name));

        Assert.Equal(new[] { "z", "m", "a", "k" }, Names(root.CreateItemIterator()));
    }

    [Fact]
    public void Iterator_GoesDownTheFirstBranchCompletely_BeforeTheNextSibling()
    {
        // En profundidad: todo el subárbol de "izq" sale antes que "der" (no por niveles).
        var left = new CargoGroup("izq");
        var leftSub = new CargoGroup("izq-sub");
        leftSub.Add(Leaf("izq-profunda"));
        left.Add(leftSub);
        left.Add(Leaf("izq-hoja"));

        var root = new CargoGroup("Raíz");
        root.Add(left);
        root.Add(Leaf("der"));

        Assert.Equal(new[] { "Raíz", "izq", "izq-sub", "izq-profunda", "izq-hoja", "der" }, Names(root.CreateIterator()));
    }

    // ---------- Hojas individuales y estructuras vacías ----------

    [Fact]
    public void Iterator_OverASingleLeaf_DeliversOnlyThatLeaf()
    {
        var leaf = Leaf("suelto");

        var all = Drain(leaf.CreateIterator());
        var items = Drain(leaf.CreateItemIterator());

        Assert.Same(leaf, Assert.Single(all));
        Assert.Same(leaf, Assert.Single(items));
    }

    [Fact]
    public void Iterator_OverAnEmptyGroup_DeliversOnlyTheGroupItself()
    {
        var empty = new CargoGroup("Vacío");

        var all = Drain(empty.CreateIterator());

        Assert.Same(empty, Assert.Single(all));
    }

    [Fact]
    public void ItemIterator_OverAnEmptyGroup_HasNothingToDeliver()
    {
        var iterator = new CargoGroup("Vacío").CreateItemIterator();

        Assert.False(iterator.HasNext());
        Assert.Throws<InvalidOperationException>(() => iterator.Next());
    }

    [Fact]
    public void ItemIterator_SkipsEmptyGroupsNestedAtAnyLevel()
    {
        var root = new CargoGroup("Raíz");
        var emptyOuter = new CargoGroup("vacío-externo");
        emptyOuter.Add(new CargoGroup("vacío-interno"));
        root.Add(emptyOuter);
        root.Add(Leaf("único"));
        root.Add(new CargoGroup("otro-vacío"));

        Assert.Equal(new[] { "único" }, Names(root.CreateItemIterator()));
    }

    [Fact]
    public void Iterator_OverARouteWithoutCargo_DeliversTheEmptyRootAndNoItems()
    {
        var manifest = CargoManifest.ForRoute(new DeliveryRoute { Name = "Sin carga" });

        Assert.Equal(new[] { "Sin carga" }, Names(manifest.CreateIterator()));
        Assert.False(manifest.CreateItemIterator().HasNext());
    }

    // ---------- Contrato del iterador: HasNext no consume, Next avanza, el final se señala ----------

    [Fact]
    public void HasNext_DoesNotConsumeAnything_NoMatterHowManyTimesItIsCalled()
    {
        var iterator = SampleTree().CreateItemIterator();

        Assert.True(iterator.HasNext());
        Assert.True(iterator.HasNext());
        Assert.True(iterator.HasNext());

        Assert.Equal("a1", iterator.Next().Name);
        Assert.Equal("a2", iterator.Next().Name);
    }

    [Fact]
    public void Next_AfterTheLastComponent_ThrowsInvalidOperationException_AndHasNextStaysFalse()
    {
        var iterator = Leaf("único").CreateIterator();
        iterator.Next();

        Assert.False(iterator.HasNext());
        Assert.Throws<InvalidOperationException>(() => iterator.Next());
        Assert.False(iterator.HasNext());
    }

    [Fact]
    public void Next_WithoutCallingHasNextFirst_StillDeliversInOrder()
    {
        var iterator = SampleTree().CreateItemIterator();

        Assert.Equal("a1", iterator.Next().Name);
        Assert.Equal("a2", iterator.Next().Name);
        Assert.Equal("b1", iterator.Next().Name);
    }

    [Fact]
    public void TwoIteratorsOverTheSameTree_AdvanceIndependently()
    {
        var tree = SampleTree();
        var first = tree.CreateItemIterator();
        var second = tree.CreateItemIterator();

        first.Next();
        first.Next();

        Assert.Equal("a1", second.Next().Name);
        Assert.Equal("b1", first.Next().Name);
        Assert.Equal("a2", second.Next().Name);
    }

    [Fact]
    public void Creating_WithANullRoot_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((ICargoComponent)null!).CreateIterator());
        Assert.Throws<ArgumentNullException>(() => ((ICargoComponent)null!).CreateItemIterator());
        Assert.Throws<ArgumentNullException>(() => new CargoDepthFirstIterator(null!));
    }

    // ---------- Sin duplicados ----------

    [Fact]
    public void ASharedComponent_ReachableThroughTwoGroups_IsDeliveredOnce_WhileTraverseRepeatsIt()
    {
        // CargoGroup.Add sólo impide repetir un componente dentro del MISMO grupo, de modo que uno puede colgar de dos.
        var shared = Leaf("compartido");
        var left = new CargoGroup("izq");
        left.Add(shared);
        var right = new CargoGroup("der");
        right.Add(shared);
        var root = new CargoGroup("Raíz");
        root.Add(left);
        root.Add(right);

        var delivered = Drain(root.CreateIterator());

        Assert.Equal(new[] { "Raíz", "izq", "compartido", "der" }, delivered.Select(c => c.Name).ToArray());
        Assert.Equal(delivered.Count, delivered.Distinct().Count());
        // Traverse() no cambió: sigue recorriendo por caminos, así que repite el compartido. Es el comportamiento que ya tenía.
        Assert.Equal(5, root.Traverse().Count());
    }

    [Fact]
    public void TwoDifferentComponentsWithTheSameName_AreBothDelivered_BecauseTheyAreDifferentObjects()
    {
        var root = new CargoGroup("Raíz");
        root.Add(Leaf("caja"));
        root.Add(Leaf("caja"));

        Assert.Equal(new[] { "caja", "caja" }, Names(root.CreateItemIterator()));
    }

    [Fact]
    public void ACustomComponentThatFormsACycle_DoesNotMakeTheIteratorLoopForever()
    {
        // CargoGroup.Add impide los ciclos; una implementación propia de ICargoComponent podría no hacerlo.
        var loop = new LoopingComponent("bucle");

        var delivered = Drain(loop.CreateIterator());

        Assert.Same(loop, Assert.Single(delivered));
    }

    // ---------- Sólo lee: no modifica el árbol ----------

    [Fact]
    public void Iterating_DoesNotChangeTheTree()
    {
        var tree = SampleTree();
        var before = tree.Traverse().ToList();
        var weight = tree.TotalWeightKg;
        var volume = tree.TotalVolumeM3;
        var items = tree.ItemCount;

        Drain(tree.CreateIterator());
        Drain(tree.CreateItemIterator());

        Assert.Equal(before.Count, tree.Traverse().Count());
        Assert.True(before.SequenceEqual(tree.Traverse()));
        Assert.Equal(weight, tree.TotalWeightKg);
        Assert.Equal(volume, tree.TotalVolumeM3);
        Assert.Equal(items, tree.ItemCount);
    }

    // ---------- Filtro ----------

    [Fact]
    public void AFilter_KeepsTheOrder_AndStillDescendsThroughRejectedGroups()
    {
        // Con el filtro "empieza con s" se rechazan Raíz, A, B... pero sus descendientes siguen visitándose.
        var iterator = new CargoDepthFirstIterator(SampleTree(), c => c.Name.StartsWith("s", StringComparison.Ordinal));

        Assert.Equal(new[] { "s1" }, Names(iterator));
    }

    [Fact]
    public void AFilterThatRejectsEverything_ProducesAnEmptyRecorrido()
    {
        var iterator = new CargoDepthFirstIterator(SampleTree(), _ => false);

        Assert.False(iterator.HasNext());
    }

    // ---------- Árboles muy profundos ----------

    [Fact]
    public void AVeryDeepTree_IsTraversedWithoutRecursion_SoItCannotOverflowTheCallStack()
    {
        const int depth = 20_000;
        var root = new CargoGroup("n0");
        var current = root;
        for (var i = 1; i <= depth; i++)
        {
            var next = new CargoGroup($"n{i}");
            current.Add(next);
            current = next;
        }
        current.Add(Leaf("fondo"));

        // Sólo se pide el artículo: no se leen los totales del árbol (esos sí son recursivos, por ser del Composite).
        var items = Drain(root.CreateItemIterator());
        var everything = Drain(root.CreateIterator());

        Assert.Equal("fondo", Assert.Single(items).Name);
        Assert.Equal(depth + 2, everything.Count);
        Assert.Equal("fondo", everything[^1].Name);
    }

    // ---------- Compatibilidad con Composite y con Traverse() ----------

    [Fact]
    public void Iterator_DeliversTheSameComponentsAndOrderAsTraverse_WhenNothingIsShared()
    {
        var tree = SampleTree();

        var byTraverse = tree.Traverse().ToList();
        var byIterator = Drain(tree.CreateIterator());

        Assert.Equal(byTraverse.Count, byIterator.Count);
        for (var i = 0; i < byTraverse.Count; i++)
            Assert.Same(byTraverse[i], byIterator[i]);
    }

    [Fact]
    public void ItemIterator_DeliversTheSameLeavesAsFilteringTraverse()
    {
        var tree = SampleTree();

        var byTraverse = tree.Traverse().Where(c => c is CargoItemComponent).ToList();
        var byIterator = Drain(tree.CreateItemIterator());

        Assert.True(byTraverse.SequenceEqual(byIterator));
    }

    [Fact]
    public void ItemIterator_ItemsAddUpToTheTotalsOfTheComposite()
    {
        var tree = SampleTree();

        var items = Drain(tree.CreateItemIterator());

        Assert.Equal(tree.ItemCount, items.Count);
        Assert.Equal(tree.TotalWeightKg, items.Sum(c => c.TotalWeightKg));
        Assert.Equal(tree.TotalVolumeM3, items.Sum(c => c.TotalVolumeM3), 6);
    }

    [Fact]
    public void ForRoute_Manifest_IsIteratedGroupByPriorityInTheOrderOfTheEnum()
    {
        var route = new DeliveryRoute { Name = "Ruta Centro" };
        route.CargoItems.Add(new CargoItem { Description = "frágil-1", Priority = CargoPriority.Fragile });
        route.CargoItems.Add(new CargoItem { Description = "normal-1", Priority = CargoPriority.Standard });
        route.CargoItems.Add(new CargoItem { Description = "urgente-1", Priority = CargoPriority.Urgent });
        route.CargoItems.Add(new CargoItem { Description = "normal-2", Priority = CargoPriority.Standard });

        var names = Names(CargoManifest.ForRoute(route).CreateIterator());

        Assert.Equal(
            new[] { "Ruta Centro", "Prioridad Standard", "normal-1", "normal-2", "Prioridad Urgent", "urgente-1", "Prioridad Fragile", "frágil-1" },
            names);
    }

    /// <summary>Componente de prueba que se tiene a sí mismo como hijo (un ciclo que CargoGroup jamás permitiría).</summary>
    private sealed class LoopingComponent : ICargoComponent
    {
        public LoopingComponent(string name) => Name = name;

        public string Name { get; }
        public double TotalWeightKg => 0;
        public double TotalVolumeM3 => 0;
        public int ItemCount => 0;
        public IReadOnlyList<ICargoComponent> Children => new ICargoComponent[] { this };
    }
}
