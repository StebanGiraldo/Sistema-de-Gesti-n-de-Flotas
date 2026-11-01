using FleetManagement.Application.AlertHandling;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Bridge;
using FleetManagement.Application.Tests.Decorator;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Infrastructure.Persistence;

namespace FleetManagement.Application.Tests.AlertHandling;

/// <summary>
/// El servicio de alertas REAL (con su Bridge y sus Decorators de notificación) detrás de la cadena REAL de producción
/// (<see cref="DefaultAlertHandlerChain"/>), con repositorios en memoria y un canal que guarda lo que se envía.
/// </summary>
public sealed class AlertHandlingKit
{
    public const string OperationsDesk = "operaciones@fleetcontrol.demo";

    public AlertHandlingKit()
    {
        Inner = new TripAlertService(Alerts, Routes, Audit, Channel);
        Chain = DefaultAlertHandlerChain.Create(Options, Inner, Channel, Audit);
        Service = new AlertHandlingTripAlertService(Inner, Chain);
    }

    public RecordingAuditLogger Audit { get; } = new();
    public RecordingNotificationChannel Channel { get; } = new();
    public InMemoryTripAlertRepository Alerts { get; } = new();
    public InMemoryDeliveryRouteRepository Routes { get; } = new();
    public AlertHandlingOptions Options { get; } = new();

    public TripAlertService Inner { get; }
    public IAlertHandler Chain { get; private set; }
    public AlertHandlingTripAlertService Service { get; private set; }

    /// <summary>Vuelve a armar la cadena después de cambiar <see cref="Options"/> (las reglas se leen al armarla).</summary>
    public void Rebuild()
    {
        Chain = DefaultAlertHandlerChain.Create(Options, Inner, Channel, Audit);
        Service = new AlertHandlingTripAlertService(Inner, Chain);
    }

    public async Task<DeliveryRoute> AddRouteAsync(string name = "Ruta Norte")
        => await Routes.AddAsync(new DeliveryRoute { Name = name, Status = DeliveryRouteStatus.InProgress });

    public Task<TripAlertDto> ReportAsync(DeliveryRoute route, string type, int delayMinutes = 10, string description = "Reporte del conductor")
        => Service.CreateAlertAsync(new CreateTripAlertRequest(route.Id, type, description, delayMinutes));

    public IEnumerable<string> Recipients => Channel.SentMessages.Select(message => message.Recipient);

    /// <summary>Lo que la auditoría de notificaciones anotó (una línea por envío), en orden: ahí se ve la prioridad de cada uno.</summary>
    public IEnumerable<string> NotificationAudits => Audit.Entries.Where(entry => entry.Category == "Notificación").Select(entry => entry.Message);
}
