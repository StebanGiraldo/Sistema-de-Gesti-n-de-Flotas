using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Entities;

namespace FleetManagement.Application.RouteSheets;

/// <summary>A quién va dirigida la hoja de ruta. Cada valor lo atiende exactamente una plantilla registrada.</summary>
public enum RouteSheetAudience
{
    Driver,
    Dispatcher
}

/// <summary>
/// Lo que la parte fija de la plantilla ya cargó y validó: la ruta, su vehículo, su conductor (si tiene) y el avance del viaje
/// (solo si el viaje está en curso). Los pasos variables lo reciben ya validado y solo eligen qué contar.
/// </summary>
public sealed record RouteSheetSource(DeliveryRoute Route, Vehicle Vehicle, Driver? Driver, TripProgressDto? Trip = null);

public sealed record RouteSheetHeader(
    string Title,
    string RouteName,
    string Status,
    string LicensePlate,
    DateTime? ScheduledDate,
    string? DriverName = null)
{
    /// <summary>El encabezado común (sin conductor); cada variante lo completa si le hace falta.</summary>
    public static RouteSheetHeader From(RouteSheetSource source, string title)
        => new(title, source.Route.Name, source.Route.Status.ToString(), source.Vehicle.LicensePlate, source.Route.ScheduledDate);
}

/// <summary>Resumen de la carga frente a lo que el vehículo puede llevar. <c>CapacityM3</c> es nulo si el vehículo no la declara.</summary>
public sealed record RouteSheetCargo(
    int Items,
    double TotalWeightKg,
    double CapacityKg,
    double WeightUsagePercent,
    double TotalVolumeM3,
    double? CapacityM3,
    bool ExceedsCapacity);

/// <summary>Lo que cada audiencia necesita saber del avance. Los datos que no aplican (o no existen) quedan nulos y no se muestran.</summary>
public sealed record RouteSheetProgress(
    double? Percent = null,
    double? RemainingKm = null,
    string? NextStop = null,
    double? EtaMinutes = null,
    int? DelayMinutes = null);

/// <summary>
/// La hoja de ruta ya armada. Las partes que una audiencia no necesita quedan vacías o nulas (y la API no las escribe): paradas
/// vacías, <c>Cargo</c>, <c>Progress</c> o <c>NavigationUrl</c> nulos.
/// </summary>
public sealed record RouteSheetDto(
    string Audience,
    RouteSheetHeader Header,
    IReadOnlyList<TripStopDto> Stops,
    RouteSheetCargo? Cargo,
    RouteSheetProgress? Progress,
    string? NavigationUrl);
