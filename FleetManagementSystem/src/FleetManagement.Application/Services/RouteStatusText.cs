using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>Cómo se nombra cada estado de una ruta dentro de un mensaje para el usuario ("la ruta está en progreso"). Un solo lugar para todos los servicios.</summary>
public static class RouteStatusText
{
    public static string Describe(DeliveryRouteStatus status) => status switch
    {
        DeliveryRouteStatus.Planned => "planificada",
        DeliveryRouteStatus.InProgress => "en progreso",
        DeliveryRouteStatus.Delayed => "retrasada",
        DeliveryRouteStatus.Completed => "completada",
        DeliveryRouteStatus.Cancelled => "cancelada",
        _ => status.ToString()
    };
}
