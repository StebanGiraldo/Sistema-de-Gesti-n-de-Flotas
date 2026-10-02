using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Domain.Cargo;

/// <summary>
/// Construye el árbol COMPOSITE de la carga de una <see cref="DeliveryRoute"/>
/// (su "manifiesto de carga") a partir de los datos reales del dominio, sin
/// agregar propiedades nuevas a ninguna entidad:
///
///   ruta (CargoGroup raíz)
///    └─ "Prioridad X" (CargoGroup, uno por cada <see cref="CargoPriority"/> con artículos)
///        └─ artículo (CargoItemComponent, envuelve cada CargoItem de la ruta)
///
/// Los grupos vacíos se omiten y aparecen en el orden en que se declara el
/// enum <see cref="CargoPriority"/>. Sólo LEE la ruta: no la modifica, por lo
/// que convive sin efectos con Builder (que la construye) y Prototype (que la
/// clona).
/// </summary>
public static class CargoManifest
{
    public static CargoGroup ForRoute(DeliveryRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        var root = new CargoGroup(string.IsNullOrWhiteSpace(route.Name) ? "Ruta sin nombre" : route.Name);

        foreach (var priority in Enum.GetValues<CargoPriority>())
        {
            var items = route.CargoItems.Where(c => c.Priority == priority).ToList();
            if (items.Count == 0)
                continue;

            var group = new CargoGroup($"Prioridad {priority}");
            foreach (var item in items)
                group.Add(new CargoItemComponent(item));

            root.Add(group);
        }

        return root;
    }
}
