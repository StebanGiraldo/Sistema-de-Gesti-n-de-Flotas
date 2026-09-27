using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// ESPECIALIZACIÓN / REFINED ABSTRACTION (patrón BRIDGE).
///
/// Notificación de un cambio de estado de un vehículo (se dispara en
/// <c>VehicleService.UpdateVehicleStatusAsync</c>, por ejemplo al marcar un
/// vehículo "Fuera de servicio" o "En mantenimiento"). El destinatario es el
/// conductor asignado al vehículo cuando existe.
/// </summary>
public class VehicleNotification : FleetNotification
{
    private readonly string _recipient;
    private readonly string _vehiclePlate;
    private readonly string _previousStatus;
    private readonly string _newStatus;

    public VehicleNotification(
        INotificationChannel channel,
        string recipient,
        string vehiclePlate,
        string previousStatus,
        string newStatus)
        : base(channel)
    {
        _recipient = recipient;
        _vehiclePlate = vehiclePlate;
        _previousStatus = previousStatus;
        _newStatus = newStatus;
    }

    public override Task SendAsync()
    {
        var subject = $"Cambio de estado - vehículo {_vehiclePlate}";
        var message = $"El vehículo {_vehiclePlate} cambió de estado: {_previousStatus} -> {_newStatus}.";

        return Channel.SendAsync(_recipient, subject, message);
    }
}
