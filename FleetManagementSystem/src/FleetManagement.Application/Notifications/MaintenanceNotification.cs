using System.Globalization;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// ESPECIALIZACIÓN / REFINED ABSTRACTION (patrón BRIDGE).
///
/// Notificación de un evento de mantenimiento (se dispara al registrar un
/// mantenimiento en <c>MaintenanceService.CreateAsync</c>). Define el
/// contenido a partir de los datos del mantenimiento; el canal (heredado de
/// <see cref="FleetNotification"/>) sólo se encarga de la entrega, sin saber
/// nada sobre mantenimiento de vehículos.
/// </summary>
public class MaintenanceNotification : FleetNotification
{
    private readonly string _recipient;
    private readonly string _vehiclePlate;
    private readonly string _maintenanceType;
    private readonly DateTime? _nextDueDate;
    private readonly double? _nextDueMileageKm;

    public MaintenanceNotification(
        INotificationChannel channel,
        string recipient,
        string vehiclePlate,
        string maintenanceType,
        DateTime? nextDueDate,
        double? nextDueMileageKm)
        : base(channel)
    {
        _recipient = recipient;
        _vehiclePlate = vehiclePlate;
        _maintenanceType = maintenanceType;
        _nextDueDate = nextDueDate;
        _nextDueMileageKm = nextDueMileageKm;
    }

    public override Task SendAsync()
    {
        var subject = $"Mantenimiento registrado - vehículo {_vehiclePlate}";
        var message = $"Se registró un mantenimiento de tipo '{_maintenanceType}' para el vehículo {_vehiclePlate}. " +
                      $"Próximo vencimiento: {FormatNextDue()}.";

        return Channel.SendAsync(_recipient, subject, message);
    }

    private string FormatNextDue()
    {
        // InvariantCulture explícita (igual que el resto del proyecto, p. ej.
        // el formateo de coordenadas en GoogleMapsApi): el resultado no debe
        // depender de la configuración regional del servidor donde se
        // despliegue la aplicación.
        if (_nextDueDate.HasValue && _nextDueMileageKm.HasValue)
            return $"{_nextDueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} o {FormatMileage(_nextDueMileageKm.Value)} (lo que ocurra primero)";
        if (_nextDueDate.HasValue)
            return _nextDueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_nextDueMileageKm.HasValue)
            return FormatMileage(_nextDueMileageKm.Value);
        return "sin definir";
    }

    private static string FormatMileage(double km) => $"{km.ToString("N0", CultureInfo.InvariantCulture)} km";
}
