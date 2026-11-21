using FleetManagement.Domain.Enums;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Un vehículo de demostración. <paramref name="DriverUsername"/> solo se llena si va en viaje con ese operador.</summary>
public sealed record DemoVehicle(
    string Plate,
    string Brand,
    string Model,
    int Year,
    VehicleType Type,
    double CapacityKg,
    double CapacityM3,
    VehicleStatus Status,
    double MileageKm,
    DemoPlace Place,
    string? DriverUsername = null);

/// <summary>
/// Treinta vehículos nuevos (TRK-006 … MOT-035) que se suman a los cinco originales: 35 en total.
///  - 10 en viaje (En ruta) con una ruta activa y su operador.
///  - 12 con una ruta planificada (6) o ya completada (6), asignados a ella con su carga.
///  - 8 sin ruta, para poder asignarles rutas nuevas desde el panel (uno en mantenimiento, uno fuera de servicio
///    y otro con mantenimiento vencido, para ver cómo las reglas los rechazan).
/// </summary>
public static class DemoVehicles
{
    private const VehicleStatus Road = VehicleStatus.EnRoute;
    private const VehicleStatus Free = VehicleStatus.Available;

    private static IReadOnlyList<DemoVehicle> OnTheRoad { get; } = new DemoVehicle[]
    {
        new("TRK-006", "Kenworth", "T800", 2021, VehicleType.Truck, 10000, 45, Road, 78400, DemoPlace.Bucaramanga, "operador3"),
        new("TRK-007", "Freightliner", "M2 106", 2022, VehicleType.Truck, 12000, 50, Road, 52150, DemoPlace.Bogota, "operador4"),
        new("TRK-008", "Hino", "500 GH", 2020, VehicleType.Truck, 9000, 40, Road, 96300, DemoPlace.Barranquilla, "operador5"),
        new("VAN-015", "Renault", "Master", 2023, VehicleType.Van, 1500, 10, Road, 21800, DemoPlace.Cali, "operador6"),
        new("VAN-016", "Ford", "Transit", 2022, VehicleType.Van, 1600, 11, Road, 33400, DemoPlace.Medellin, "operador7"),
        new("VAN-017", "Hyundai", "H350", 2021, VehicleType.Van, 1700, 11, Road, 61200, DemoPlace.Pereira, "operador8"),
        new("VAN-018", "Mercedes-Benz", "Sprinter", 2022, VehicleType.Van, 1800, 13, Road, 44900, DemoPlace.Cucuta, "operador9"),
        new("VAN-019", "Iveco", "Daily", 2020, VehicleType.Van, 1800, 12, Road, 87600, DemoPlace.Ibague, "operador10"),
        new("MOT-030", "Honda", "XR 190L", 2023, VehicleType.Motorcycle, 30, 0.3, Road, 12700, DemoPlace.Bucaramanga, "operador11"),
        new("CAR-025", "Renault", "Kangoo Express", 2022, VehicleType.Car, 500, 4, Road, 38900, DemoPlace.Bucaramanga, "operador12")
    };

    private static IReadOnlyList<DemoVehicle> WithRoute { get; } = new DemoVehicle[]
    {
        // Con ruta planificada (disponibles hasta que salgan).
        new("TRK-009", "Chevrolet", "NPR", 2022, VehicleType.Truck, 9000, 40, Free, 41200, DemoPlace.Bogota),
        new("TRK-010", "Volvo", "FMX", 2023, VehicleType.Truck, 10000, 45, Free, 18700, DemoPlace.Medellin),
        new("VAN-020", "Renault", "Master", 2022, VehicleType.Van, 1500, 10, Free, 29500, DemoPlace.Cali),
        new("VAN-021", "Peugeot", "Boxer", 2021, VehicleType.Van, 1500, 9, Free, 57800, DemoPlace.Cartagena),
        new("MOT-031", "Yamaha", "FZ 150", 2023, VehicleType.Motorcycle, 30, 0.3, Free, 8400, DemoPlace.Medellin),
        new("CAR-026", "Chevrolet", "N300", 2022, VehicleType.Car, 500, 4, Free, 27300, DemoPlace.Villavicencio),
        // Con ruta ya completada (de vuelta en su destino, disponibles).
        new("TRK-011", "Hino", "300", 2021, VehicleType.Truck, 8000, 36, Free, 69100, DemoPlace.SanGil),
        new("TRK-012", "Isuzu", "FVR", 2022, VehicleType.Truck, 9500, 42, Free, 47600, DemoPlace.Bucaramanga),
        new("VAN-022", "Ford", "Transit", 2021, VehicleType.Van, 1600, 11, Free, 72300, DemoPlace.Cali),
        new("VAN-023", "Renault", "Master", 2023, VehicleType.Van, 1500, 10, Free, 15900, DemoPlace.Cartagena),
        new("MOT-032", "AKT", "NKD 125", 2023, VehicleType.Motorcycle, 30, 0.3, Free, 6200, DemoPlace.Bucaramanga),
        new("CAR-027", "Chevrolet", "N300", 2021, VehicleType.Car, 500, 4, Free, 51400, DemoPlace.Ibague)
    };

    private static IReadOnlyList<DemoVehicle> Spare { get; } = new DemoVehicle[]
    {
        new("TRK-013", "Volvo", "FH 460", 2023, VehicleType.Truck, 11000, 48, Free, 9800, DemoPlace.Bucaramanga),
        new("TRK-014", "Kenworth", "T370", 2022, VehicleType.Truck, 9500, 42, Free, 36500, DemoPlace.Bogota),
        new("VAN-024", "Hyundai", "H100", 2022, VehicleType.Van, 1200, 8, Free, 24600, DemoPlace.Medellin),
        new("CAR-028", "Suzuki", "Carry", 2020, VehicleType.Car, 600, 4, VehicleStatus.Maintenance, 90500, DemoPlace.Bogota),
        new("CAR-029", "Chevrolet", "N300", 2020, VehicleType.Car, 500, 4, Free, 84700, DemoPlace.Cucuta),
        new("MOT-033", "Honda", "CB 125F", 2023, VehicleType.Motorcycle, 35, 0.35, Free, 5300, DemoPlace.Cali),
        new("MOT-034", "Bajaj", "Boxer CT 100", 2022, VehicleType.Motorcycle, 35, 0.35, Free, 14100, DemoPlace.Barranquilla),
        new("MOT-035", "AKT", "NKD 125", 2020, VehicleType.Motorcycle, 30, 0.3, VehicleStatus.OutOfService, 47800, DemoPlace.Pereira)
    };

    // Va al final a propósito: los inicializadores estáticos corren en orden de texto y esta lista usa las tres de arriba.
    public static IReadOnlyList<DemoVehicle> All { get; } = OnTheRoad.Concat(WithRoute).Concat(Spare).ToList();
}
