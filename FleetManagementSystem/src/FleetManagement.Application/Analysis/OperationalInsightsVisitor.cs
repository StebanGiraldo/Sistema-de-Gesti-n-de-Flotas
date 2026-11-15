using System.Globalization;
using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;

namespace FleetManagement.Application.Analysis;

/// <summary>
/// CONCRETE VISITOR #4 (patrón VISITOR): información relevante para decidir en la operación. Visita los tres tipos de elemento
/// y cruza sus datos para señalar lo que un operador debería atender: rutas en curso que no pueden avanzar o cuyo vehículo no las
/// sigue, rutas planificadas que no podrán empezar, vehículos "En ruta" que no tienen ruta, retrasos importantes y vehículos
/// que llevan demasiados kilómetros sin mantenimiento. Los avisos salen del más al menos urgente (ver <see cref="InsightCodes"/>).
///
/// Para saber si un vehículo puede operar, o por qué no, pregunta a su estado (patrón STATE: <c>CanOperate</c>,
/// <c>CanTrackTrip</c>, <c>OperationBlockReason</c>): no repite esas reglas ni sus mensajes. Los umbrales vienen de
/// <see cref="InsightThresholds"/>.
/// </summary>
public sealed class OperationalInsightsVisitor : FleetVisitor<IReadOnlyList<OperationalInsightDto>>
{
    private readonly IVehicleStateResolver _states;
    private readonly InsightThresholds _thresholds;
    private readonly Dictionary<Guid, VehicleElement> _vehicles = new();
    private readonly Dictionary<Guid, RouteElement> _routes = new();
    private readonly Dictionary<Guid, MaintenanceRecordElement> _records = new();

    public OperationalInsightsVisitor(IVehicleStateResolver? states = null, InsightThresholds? thresholds = null)
    {
        _states = states ?? VehicleStateResolver.Default;
        _thresholds = thresholds ?? new InsightThresholds();
    }

    public override void VisitVehicle(VehicleElement vehicle) => _vehicles[vehicle.Id] = vehicle;

    public override void VisitRoute(RouteElement route) => _routes[route.Id] = route;

    public override void VisitMaintenanceRecord(MaintenanceRecordElement record) => _records[record.Id] = record;

    public override IReadOnlyList<OperationalInsightDto> Result
    {
        get
        {
            var insights = ActiveRouteInsights()
                .Concat(PlannedRouteInsights())
                .Concat(VehicleInsights())
                .ToList();

            return insights
                .OrderBy(insight => insight.Severity)
                .ThenBy(insight => insight.Code, StringComparer.Ordinal)
                .ThenBy(insight => insight.Subject, StringComparer.Ordinal)
                .Select(insight => new OperationalInsightDto(insight.Code, insight.Severity.ToString(), insight.Subject, insight.Message))
                .ToList();
        }
    }

    private sealed record Insight(string Code, InsightSeverity Severity, string Subject, string Message);

    private IEnumerable<Insight> ActiveRouteInsights()
    {
        foreach (var route in _routes.Values.Where(route => route.IsActive))
        {
            var blocked = NotOperableReason(route);
            if (blocked is not null)
                yield return new Insight(InsightCodes.ActiveRouteWithoutOperableVehicle, InsightSeverity.Critical, route.Name,
                    $"La ruta '{route.Name}' está en curso pero no puede avanzar. {blocked}");
            else if (VehicleNotFollowingTrip(route) is { } idle)
                yield return new Insight(InsightCodes.ActiveRouteVehicleNotEnRoute, InsightSeverity.Warning, route.Name,
                    $"La ruta '{route.Name}' está en curso pero su vehículo {idle.LicensePlate} no está En ruta (estado: {idle.Status}): la simulación no la mueve.");

            if (route.DelayMinutes >= _thresholds.DelayAlertMinutes)
                yield return new Insight(InsightCodes.ActiveRouteDelayed, InsightSeverity.Warning, route.Name,
                    $"La ruta '{route.Name}' acumula {route.DelayMinutes} min de retraso (umbral: {_thresholds.DelayAlertMinutes} min).");
        }
    }

    private IEnumerable<Insight> PlannedRouteInsights()
    {
        foreach (var route in _routes.Values.Where(route => route.Status == DeliveryRouteStatus.Planned))
        {
            if (route.AssignedVehicleId is null)
            {
                yield return new Insight(InsightCodes.PlannedRouteWithoutVehicle, InsightSeverity.Info, route.Name,
                    $"La ruta planificada '{route.Name}' todavía no tiene vehículo asignado.");
                continue;
            }

            var blocked = NotOperableReason(route);
            if (blocked is not null)
                yield return new Insight(InsightCodes.PlannedRouteVehicleNotOperable, InsightSeverity.Warning, route.Name,
                    $"La ruta planificada '{route.Name}' no podrá iniciarse así. {blocked}");
        }
    }

    private IEnumerable<Insight> VehicleInsights()
    {
        var vehiclesWithActiveRoute = ActiveRouteVehicles.Of(_routes.Values);
        var recordsByVehicle = _records.Values.ToLookup(record => record.VehicleId);

        foreach (var vehicle in _vehicles.Values)
        {
            if (_states.For(vehicle.Status).CanTrackTrip && !vehiclesWithActiveRoute.Contains(vehicle.Id))
                yield return new Insight(InsightCodes.EnRouteVehicleWithoutActiveRoute, InsightSeverity.Warning, vehicle.LicensePlate,
                    $"El vehículo {vehicle.LicensePlate} figura En ruta pero no tiene ninguna ruta en curso: la simulación no lo mueve.");

            var serviceInsight = ServiceDistanceInsight(vehicle, MaintenanceMileage.Latest(recordsByVehicle[vehicle.Id]));
            if (serviceInsight is not null)
                yield return serviceInsight;
        }
    }

    private Insight? ServiceDistanceInsight(VehicleElement vehicle, MaintenanceRecordElement? latest)
    {
        var kmSince = MaintenanceMileage.KmSince(vehicle, latest) ?? vehicle.MileageKm;
        if (kmSince < _thresholds.ServiceDistanceAlertKm)
            return null;

        var km = kmSince.ToString("0.###", CultureInfo.InvariantCulture);
        var threshold = _thresholds.ServiceDistanceAlertKm.ToString("0.###", CultureInfo.InvariantCulture);
        var message = latest is null
            ? $"El vehículo {vehicle.LicensePlate} no tiene ningún mantenimiento registrado y lleva {km} km (umbral: {threshold} km)."
            : $"El vehículo {vehicle.LicensePlate} lleva {km} km desde su último mantenimiento (umbral: {threshold} km).";

        return new Insight(InsightCodes.ServiceDistanceExceeded, InsightSeverity.Warning, vehicle.LicensePlate, message);
    }

    /// <summary>
    /// El vehículo de una ruta en curso cuyo estado NO sigue el viaje (por ejemplo, alguien lo dejó Disponible a mano mientras la
    /// ruta seguía en marcha): la ruta no avanza y nada lo avisaba. Es <c>null</c> si el vehículo sí sigue el viaje o la ruta no
    /// tiene vehículo. Quien además no puede operar ya sale como hallazgo crítico (<see cref="NotOperableReason"/>).
    /// </summary>
    private VehicleElement? VehicleNotFollowingTrip(RouteElement route)
        => route.AssignedVehicleId is { } vehicleId
           && _vehicles.TryGetValue(vehicleId, out var vehicle)
           && !_states.For(vehicle.Status).CanTrackTrip
            ? vehicle
            : null;

    /// <summary>Por qué el vehículo de la ruta no puede operar (lo dice su estado), o <c>null</c> si puede o la ruta aún no tiene vehículo.</summary>
    private string? NotOperableReason(RouteElement route)
    {
        if (route.AssignedVehicleId is not { } vehicleId)
            return route.IsActive ? "No tiene vehículo asignado: nadie la recorre." : null;

        if (!_vehicles.TryGetValue(vehicleId, out var vehicle))
            return "Su vehículo asignado ya no existe.";

        return _states.For(vehicle.Status).OperationBlockReason(vehicle.LicensePlate);
    }
}
