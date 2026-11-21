using FleetManagement.Domain.Enums;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>
/// Un servicio ya realizado. Los valores son relativos a hoy y al kilometraje del vehículo, para que los datos de
/// demostración no «caduquen» con el tiempo: <paramref name="KmSinceService"/> es lo recorrido desde el servicio y
/// <paramref name="KmUntilDue"/> lo que falta (negativo = ya pasó) para el próximo; igual con <paramref name="DueInDays"/>.
/// </summary>
public sealed record DemoMaintenance(
    string Plate,
    MaintenanceType Type,
    int MonthsAgo,
    int DueInDays,
    double KmSinceService,
    double KmUntilDue,
    string Notes);

/// <summary>
/// Historial de mantenimiento de la flota nueva. Los vehículos en viaje o con ruta están al día, salvo dos casos
/// «próximos a vencer» (VAN-017 y VAN-019). Los vehículos no listados aquí no tienen historial (la regla de
/// intérprete los trata como «sin dato»). CAR-028 y CAR-029 tienen el mantenimiento vencido a propósito.
/// </summary>
public static class DemoMaintenanceHistory
{
    public static IReadOnlyList<DemoMaintenance> All { get; } = new DemoMaintenance[]
    {
        new("TRK-006", MaintenanceType.OilChange, 2, 120, 3200, 6800, "Cambio de aceite y filtros."),
        new("TRK-007", MaintenanceType.BrakeInspection, 1, 150, 1500, 13500, "Pastillas y tambores en buen estado."),
        new("TRK-008", MaintenanceType.TireRotation, 3, 60, 7000, 3000, "Rotación y balanceo de llantas."),
        new("VAN-015", MaintenanceType.OilChange, 1, 90, 900, 9100, "Cambio de aceite sintético."),
        new("VAN-017", MaintenanceType.GeneralInspection, 5, 25, 12000, 2000, "Revisión general; próxima en pocas semanas."),
        new("VAN-019", MaintenanceType.BrakeInspection, 4, 40, 9000, 1000, "Frenos con desgaste medio; próxima por kilometraje."),
        new("VAN-022", MaintenanceType.OilChange, 5, 25, 8600, 1400, "Cambio de aceite; ya recorrió más de 8000 km desde entonces."),
        new("TRK-011", MaintenanceType.OilChange, 3, 70, 5000, 5000, "Cambio de aceite y filtro de combustible."),
        new("TRK-014", MaintenanceType.GeneralInspection, 2, 100, 2100, 12900, "Revisión general sin novedades."),
        new("MOT-030", MaintenanceType.Other, 1, 60, 800, 4200, "Kit de arrastre (cadena y piñones) y ajuste de frenos."),
        new("CAR-025", MaintenanceType.TireRotation, 2, 80, 3000, 7000, "Rotación de llantas."),
        new("CAR-028", MaintenanceType.OilChange, 7, -30, 14000, -800, "Servicio vencido por fecha y por kilometraje: el vehículo está en el taller."),
        new("CAR-029", MaintenanceType.BrakeInspection, 8, -20, 16500, -1200, "Inspección de frenos vencida: no se puede asignar a rutas hasta registrarla.")
    };
}
