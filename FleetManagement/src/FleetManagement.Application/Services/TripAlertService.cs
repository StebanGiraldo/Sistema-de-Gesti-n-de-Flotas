using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;

namespace FleetManagement.Application.Services;

/// <summary>
/// Caso de uso de Alertas de viaje: permite a un operador reportar una
/// complicación (retraso, avería, tráfico, accidente, clima) durante su
/// recorrido, y refleja automáticamente el retraso en la ruta afectada.
/// </summary>
public class TripAlertService : ITripAlertService
{
    private readonly ITripAlertRepository _alertRepository;
    private readonly IDeliveryRouteRepository _routeRepository;
    private readonly IFleetAuditLogger _auditLogger;

    public TripAlertService(ITripAlertRepository alertRepository, IDeliveryRouteRepository routeRepository, IFleetAuditLogger auditLogger)
    {
        _alertRepository = alertRepository;
        _routeRepository = routeRepository;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<TripAlertDto>> GetAllAsync()
    {
        var alerts = await _alertRepository.GetAllAsync();
        var routes = await _routeRepository.GetAllAsync();
        return alerts.OrderByDescending(a => a.CreatedAt).Select(a => MapToDto(a, routes)).ToList();
    }

    public async Task<IReadOnlyList<TripAlertDto>> GetByRouteAsync(Guid routeId)
    {
        var alerts = await _alertRepository.GetByRouteIdAsync(routeId);
        var routes = await _routeRepository.GetAllAsync();
        return alerts.OrderByDescending(a => a.CreatedAt).Select(a => MapToDto(a, routes)).ToList();
    }

    public async Task<TripAlertDto> CreateAlertAsync(CreateTripAlertRequest request)
    {
        var route = await _routeRepository.GetByIdAsync(request.RouteId)
            ?? throw new KeyNotFoundException("Ruta no encontrada.");

        if (!Enum.TryParse<AlertType>(request.Type, true, out var type))
            throw new ArgumentException($"Tipo de alerta inválido: '{request.Type}'.");

        var alert = new TripAlert
        {
            RouteId = route.Id,
            VehicleId = route.AssignedVehicleId,
            DriverId = route.AssignedDriverId,
            Type = type,
            Description = request.Description,
            DelayMinutes = Math.Max(0, request.DelayMinutes)
        };
        var saved = await _alertRepository.AddAsync(alert);

        if (alert.DelayMinutes > 0)
        {
            route.DelayMinutes += alert.DelayMinutes;
            route.Status = DeliveryRouteStatus.Delayed;
            await _routeRepository.UpdateAsync(route);
        }

        _auditLogger.LogEvent("Alerta", $"Nueva alerta '{type}' en la ruta '{route.Name}': {request.Description}");

        return MapToDto(saved, new List<DeliveryRoute> { route });
    }

    public async Task ResolveAlertAsync(Guid id)
    {
        var alert = await _alertRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Alerta no encontrada.");

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        await _alertRepository.UpdateAsync(alert);

        _auditLogger.LogEvent("Alerta", $"Alerta {alert.Id} marcada como resuelta.");
    }

    private static TripAlertDto MapToDto(TripAlert a, IReadOnlyList<DeliveryRoute> routes)
    {
        var routeName = routes.FirstOrDefault(r => r.Id == a.RouteId)?.Name ?? "(ruta desconocida)";
        return new TripAlertDto(a.Id, a.RouteId, routeName, a.VehicleId, a.DriverId, a.Type.ToString(), a.Description, a.DelayMinutes, a.Status.ToString(), a.CreatedAt, a.ResolvedAt);
    }
}
