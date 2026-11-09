using System.Reflection;
using FleetManagement.Application.Commands;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;
using Xunit;

namespace FleetManagement.Application.Tests.Memento;

/// <summary>
/// PRUEBAS DEL PATRÓN MEMENTO SOLO (sin Command ni invocador): el Originator (<see cref="RouteSnapshotService"/>) crea el
/// recuerdo (<see cref="RouteSnapshot"/>) del orden de las paradas y de las estimaciones de una ruta, y es el único que sabe
/// restaurarlo. Se prueba con el repositorio real en memoria: captura, independencia de los cambios posteriores,
/// inmutabilidad, restauración completa y rechazo de las restauraciones que no corresponden.
/// </summary>
public class RouteMementoTests
{
    private readonly InMemoryDeliveryRouteRepository _routes = new();

    private RouteSnapshotService Originator() => new(_routes);

    /// <summary>Una ruta planificada con las paradas en el orden dado (0, 1, 2...), 155,7 km y 117 min de estimación.</summary>
    private async Task<DeliveryRoute> PlannedRouteAsync(params string[] stops)
    {
        var route = new DeliveryRoute { Name = "Ruta de prueba", Status = DeliveryRouteStatus.Planned, EstimatedDistanceKm = 155.7, EstimatedDurationMinutes = 117 };
        for (var index = 0; index < stops.Length; index++)
            route.Waypoints.Add(new Waypoint { Order = index, Label = stops[index] });
        return await _routes.AddAsync(route);
    }

    private static void Reorder(DeliveryRoute route, params string[] labels)
    {
        for (var index = 0; index < labels.Length; index++)
            route.Waypoints.Single(waypoint => waypoint.Label == labels[index]).Order = index;
        route.Waypoints = route.Waypoints.OrderBy(waypoint => waypoint.Order).ToList();
    }

    private async Task<string[]> StopsAsync(Guid routeId)
        => (await _routes.GetByIdAsync(routeId))!.Waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => waypoint.Label).ToArray();

    /// <summary>Todo lo que el Originator podría tocar, en una línea: sirve para comprobar que un rechazo no cambió nada.</summary>
    private async Task<string> FingerprintAsync(Guid routeId)
    {
        var route = (await _routes.GetByIdAsync(routeId))!;
        return $"{route.Status}|{route.EstimatedDistanceKm}|{route.EstimatedDurationMinutes}|"
            + string.Join(",", route.Waypoints.Select(waypoint => $"{waypoint.Label}:{waypoint.Order}"));
    }

    /// <summary>Un recuerdo que NO creó el Originator: cumple la interfaz estrecha pero no trae paradas ni estimaciones.</summary>
    private sealed class ForeignMemento : IRouteMemento
    {
        public ForeignMemento(Guid routeId) => RouteId = routeId;
        public Guid RouteId { get; }
        public string RouteName => "Ajeno";
    }

    // ---------- 1. El Originator crea el recuerdo con el estado original ----------

    [Fact]
    public async Task TheOriginator_CreatesAMemento_WithTheRoute_TheStopsInTheirOrder_AndTheEstimates()
    {
        var route = await PlannedRouteAsync("A", "B", "C");

        var memento = await Originator().CaptureAsync(route.Id);

        Assert.Equal(route.Id, memento.RouteId);
        Assert.Equal("Ruta de prueba", memento.RouteName);
        Assert.Equal(route.Waypoints.Select(waypoint => waypoint.Id).ToArray(), memento.Stops.Select(stop => stop.WaypointId).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, memento.Stops.Select(stop => stop.Order).ToArray());
        Assert.Equal(155.7, memento.EstimatedDistanceKm);
        Assert.Equal(117, memento.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task TheMemento_RecordsTheOrderTheStopsReallyHad_NotTheirPositionInTheList()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        Reorder(route, "C", "A", "B");

        var memento = await Originator().CaptureAsync(route.Id);

        var labelById = route.Waypoints.ToDictionary(waypoint => waypoint.Id, waypoint => waypoint.Label);
        var recorded = memento.Stops.OrderBy(stop => stop.Order).Select(stop => labelById[stop.WaypointId]).ToArray();
        Assert.Equal(new[] { "C", "A", "B" }, recorded);
    }

    [Fact]
    public void TheMemento_KeepsOnlyWhatIsNeededToGoBack()
    {
        // Evitar conservar información innecesaria: ni la entidad, ni coordenadas, ni etiquetas, ni la carga ni el estado.
        string[] Names(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(new[] { "EstimatedDistanceKm", "EstimatedDurationMinutes", "RouteId", "RouteName", "Stops" }, Names(typeof(RouteSnapshot)));
        Assert.Equal(new[] { "Order", "WaypointId" }, Names(typeof(WaypointPosition)));

        var held = typeof(RouteSnapshot).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(field => field.FieldType).Concat(typeof(RouteSnapshot).GetProperties().Select(property => property.PropertyType));
        Assert.DoesNotContain(held, type => type == typeof(DeliveryRoute) || type == typeof(Waypoint));
    }

    // ---------- 2. El recuerdo es independiente de los cambios posteriores ----------

    [Fact]
    public async Task TheMemento_IsNotAffectedByLaterChangesToTheRoute()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var memento = await Originator().CaptureAsync(route.Id);
        var idsBefore = memento.Stops.Select(stop => stop.WaypointId).ToArray();

        Reorder(route, "C", "B", "A");
        route.Waypoints.Add(new Waypoint { Order = 3, Label = "D" });
        route.Waypoints.RemoveAll(waypoint => waypoint.Label == "A");
        route.EstimatedDistanceKm = 1;
        route.EstimatedDurationMinutes = 2;
        route.Name = "Otro nombre";
        route.Status = DeliveryRouteStatus.Completed;

        Assert.Equal(new[] { 0, 1, 2 }, memento.Stops.Select(stop => stop.Order).ToArray());
        Assert.Equal(idsBefore, memento.Stops.Select(stop => stop.WaypointId).ToArray());
        Assert.Equal(155.7, memento.EstimatedDistanceKm);
        Assert.Equal(117, memento.EstimatedDurationMinutes);
        Assert.Equal("Ruta de prueba", memento.RouteName);
    }

    [Fact]
    public async Task TwoMementosOfTheSameRoute_AreIndependentOfEachOther()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var first = await originator.CaptureAsync(route.Id);
        Reorder(route, "C", "A", "B");
        route.EstimatedDistanceKm = 50;
        var second = await originator.CaptureAsync(route.Id);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Stops, second.Stops);
        Assert.Equal(155.7, first.EstimatedDistanceKm);
        Assert.Equal(50, second.EstimatedDistanceKm);

        await originator.RestoreStopOrderAsync(first);
        Assert.Equal(new[] { "A", "B", "C" }, await StopsAsync(route.Id));

        await originator.RestoreStopOrderAsync(second);
        Assert.Equal(new[] { "C", "A", "B" }, await StopsAsync(route.Id));
        Assert.Equal(50, (await _routes.GetByIdAsync(route.Id))!.EstimatedDistanceKm);
    }

    // ---------- 3. El recuerdo no se puede alterar ni fabricar ----------

    [Fact]
    public async Task TheStopsOfTheMemento_CannotBeChangedThroughTheList()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var memento = await Originator().CaptureAsync(route.Id);

        Assert.False(memento.Stops is List<WaypointPosition>);
        var asList = Assert.IsAssignableFrom<IList<WaypointPosition>>(memento.Stops);
        Assert.True(asList.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asList.Add(new WaypointPosition(Guid.NewGuid(), 9)));
        Assert.Throws<NotSupportedException>(() => asList[0] = new WaypointPosition(Guid.NewGuid(), 9));
        Assert.Throws<NotSupportedException>(() => asList.Clear());
        Assert.Equal(3, memento.Stops.Count);
    }

    [Fact]
    public void TheMemento_HasNoSettersAndCannotBeBuiltFromOutside_OnlyTheOriginatorCreatesIt()
    {
        var properties = typeof(RouteSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.All(properties, property => Assert.True(property.SetMethod is null, $"{property.Name} tiene setter"));
        Assert.Empty(typeof(RouteSnapshot).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(typeof(RouteSnapshot).IsSealed);
    }

    [Fact]
    public void TheNarrowInterface_ExposesOnlyTheIdentityOfTheRoute_NeitherStopsNorEstimates()
    {
        var members = typeof(IRouteMemento).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        Assert.Equal(new[] { "RouteId", "RouteName" }, members);
        Assert.Empty(typeof(IRouteMemento).GetMethods().Where(method => !method.IsSpecialName));
        Assert.True(typeof(IRouteMemento).IsAssignableFrom(typeof(RouteSnapshot)));
    }

    // ---------- 4. El Originator restaura el estado completo ----------

    [Fact]
    public async Task Restore_PutsBackTheOriginalOrderOfEveryStop_EvenWhenTheyWereAllShuffled()
    {
        var route = await PlannedRouteAsync("A", "B", "C", "D", "E");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        Reorder(route, "E", "C", "A", "D", "B");

        await originator.RestoreStopOrderAsync(memento);

        var restored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, restored.Waypoints.Select(waypoint => waypoint.Label).ToArray()); // la lista, no solo el número
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, restored.Waypoints.Select(waypoint => waypoint.Order).ToArray());
    }

    [Fact]
    public async Task Restore_PutsBackTheDistanceAndTheDuration()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        route.EstimatedDistanceKm = 12.5;
        route.EstimatedDurationMinutes = 30;

        await originator.RestoreStopOrderAsync(memento);

        var restored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal(155.7, restored.EstimatedDistanceKm);
        Assert.Equal(117, restored.EstimatedDurationMinutes);
    }

    [Fact]
    public async Task Restore_LeavesEverythingElseAsItIs()
    {
        var route = await PlannedRouteAsync("A", "B");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        Reorder(route, "B", "A");
        route.Name = "Renombrada";
        route.DelayMinutes = 7;

        await originator.RestoreStopOrderAsync(memento);

        var restored = (await _routes.GetByIdAsync(route.Id))!;
        Assert.Equal("Renombrada", restored.Name);
        Assert.Equal(7, restored.DelayMinutes);
        Assert.Equal(DeliveryRouteStatus.Planned, restored.Status);
    }

    [Fact]
    public async Task ARestore_CanBeRepeated_TheMementoIsNotConsumed()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);

        Reorder(route, "C", "B", "A");
        await originator.RestoreStopOrderAsync(memento);
        var firstRestore = await FingerprintAsync(route.Id);

        Reorder(route, "B", "C", "A");
        route.EstimatedDistanceKm = 3;
        await originator.RestoreStopOrderAsync(memento);

        Assert.Equal(firstRestore, await FingerprintAsync(route.Id));
        Assert.Equal(new[] { "A", "B", "C" }, await StopsAsync(route.Id));
    }

    // ---------- 5. Rechazo de las restauraciones que no corresponden (y sin cambiar nada) ----------

    [Theory]
    [InlineData(DeliveryRouteStatus.InProgress)]
    [InlineData(DeliveryRouteStatus.Delayed)]
    [InlineData(DeliveryRouteStatus.Completed)]
    [InlineData(DeliveryRouteStatus.Cancelled)]
    public async Task Restore_IsRefused_WhenTheRouteIsNoLongerPlanned_AndNothingChanges(DeliveryRouteStatus status)
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        Reorder(route, "C", "A", "B");
        route.EstimatedDistanceKm = 1;
        route.Status = status;
        var before = await FingerprintAsync(route.Id);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => originator.RestoreStopOrderAsync(memento));

        Assert.Equal(before, await FingerprintAsync(route.Id));
    }

    [Fact]
    public async Task Restore_IsRefused_WhenAStopWasAdded_AndNothingChanges()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        Reorder(route, "C", "A", "B");
        route.Waypoints.Add(new Waypoint { Order = 3, Label = "D" });
        var before = await FingerprintAsync(route.Id);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => originator.RestoreStopOrderAsync(memento));

        Assert.Equal(before, await FingerprintAsync(route.Id));
    }

    [Fact]
    public async Task Restore_IsRefused_WhenAStopWasRemoved_AndNothingChanges()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        route.Waypoints.RemoveAll(waypoint => waypoint.Label == "B");
        var before = await FingerprintAsync(route.Id);

        await Assert.ThrowsAsync<CommandNotUndoableException>(() => originator.RestoreStopOrderAsync(memento));

        Assert.Equal(before, await FingerprintAsync(route.Id));
    }

    [Fact]
    public async Task Restore_IsRefused_WhenTheRouteNoLongerExists_WithItsNameInTheMessage()
    {
        var route = await PlannedRouteAsync("A", "B");
        var originator = Originator();
        var memento = await originator.CaptureAsync(route.Id);
        await _routes.DeleteAsync(route.Id);

        var error = await Assert.ThrowsAsync<CommandNotUndoableException>(() => originator.RestoreStopOrderAsync(memento));

        Assert.Contains("'Ruta de prueba' ya no existe", error.Message);
    }

    [Fact]
    public async Task Restore_IsRefused_WithAMementoThatTheOriginatorDidNotCreate_AndNothingChanges()
    {
        var route = await PlannedRouteAsync("A", "B", "C");
        Reorder(route, "C", "A", "B");
        var before = await FingerprintAsync(route.Id);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => Originator().RestoreStopOrderAsync(new ForeignMemento(route.Id)));

        Assert.Equal("memento", error.ParamName);
        Assert.Equal(before, await FingerprintAsync(route.Id));
    }

    [Fact]
    public async Task Restore_IsRefused_WithoutAMemento()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Originator().RestoreStopOrderAsync(null!));

    [Fact]
    public async Task Capture_OfARouteThatDoesNotExist_IsNotFound()
        => await Assert.ThrowsAsync<KeyNotFoundException>(() => Originator().CaptureAsync(Guid.NewGuid()));
}
