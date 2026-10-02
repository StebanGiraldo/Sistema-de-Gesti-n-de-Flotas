using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Cargo;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso que expone la carga de una ruta como un árbol (patrón
/// COMPOSITE, ver FleetManagement.Domain.Cargo). Sólo LEE la ruta a través de
/// su repositorio; no modifica DeliveryRouteService ni las entidades.
///
/// El mapeo a DTO trata hojas y grupos de forma uniforme a través de
/// <see cref="ICargoComponent"/>: es una única función recursiva, sin ramas
/// distintas según el tipo (salvo la etiqueta "Kind" que sólo informa al
/// cliente si el nodo es un grupo o un artículo).
/// </summary>
public class CargoManifestService : ICargoManifestService
{
    private readonly IDeliveryRouteRepository _routeRepository;

    public CargoManifestService(IDeliveryRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<CargoManifestDto?> GetManifestAsync(Guid routeId)
    {
        var route = await _routeRepository.GetByIdAsync(routeId);
        if (route is null)
            return null;

        var root = CargoManifest.ForRoute(route);
        return new CargoManifestDto(route.Id, ToNode(root));
    }

    private static CargoManifestNodeDto ToNode(ICargoComponent component) => new(
        component.Name,
        component is CargoGroup ? "Group" : "Item",
        Math.Round(component.TotalWeightKg, 3),
        Math.Round(component.TotalVolumeM3, 3),
        component.ItemCount,
        component.Children.Select(ToNode).ToList());
}
