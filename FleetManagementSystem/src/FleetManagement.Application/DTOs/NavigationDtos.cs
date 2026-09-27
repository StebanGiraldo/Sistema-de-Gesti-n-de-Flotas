namespace FleetManagement.Application.DTOs;

/// <summary>Enlace de navegación externa generado para que el conductor siga la ruta desde su teléfono.</summary>
public record NavigationLinkDto(Guid RouteId, string ExternalMapsUrl, List<WaypointDto> OrderedStops);
