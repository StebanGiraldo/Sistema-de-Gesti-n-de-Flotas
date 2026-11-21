using FleetManagement.Domain.Enums;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Un artículo de carga de una ruta de demostración.</summary>
public sealed record DemoCargo(string Description, double WeightKg, double VolumeM3, CargoPriority Priority);

/// <summary>
/// Una ruta de demostración con su vehículo, conductor y carga. <paramref name="DaysFromToday"/> es negativo para las
/// ya completadas y positivo para las planificadas. <paramref name="Plate"/> y <paramref name="DriverUsername"/> se
/// resuelven al sembrar (placa del vehículo y usuario del operador).
/// </summary>
public sealed record DemoRoute(
    string Name,
    DemoPlace Origin,
    IReadOnlyList<DemoPlace> Via,
    DemoPlace Destination,
    double DistanceKm,
    DeliveryRouteStatus Status,
    int DaysFromToday,
    string Plate,
    string DriverUsername,
    IReadOnlyList<DemoCargo> Cargo,
    int DelayMinutes = 0)
{
    public double TotalKg => Cargo.Sum(item => item.WeightKg);

    public double TotalM3 => Cargo.Sum(item => item.VolumeM3);
}

/// <summary>Las 22 rutas nuevas: 10 activas, 6 planificadas y 6 completadas (más la ruta original de VAN-002: 11 viajes en curso).</summary>
public static class DemoRoutes
{
    public static IReadOnlyList<DemoRoute> All { get; } = DemoActiveRoutes.All
        .Concat(DemoPlannedRoutes.All)
        .Concat(DemoCompletedRoutes.All)
        .ToList();
}

/// <summary>Viajes en curso hoy: ocho «En progreso» y dos «Con retraso». Cada uno con vehículo En ruta y operador propios.</summary>
public static class DemoActiveRoutes
{
    private const DeliveryRouteStatus Going = DeliveryRouteStatus.InProgress;
    private const DeliveryRouteStatus Late = DeliveryRouteStatus.Delayed;

    public static IReadOnlyList<DemoRoute> All { get; } = new DemoRoute[]
    {
        new("Ruta Bucaramanga - Barrancabermeja", DemoPlace.Bucaramanga, new[] { DemoPlace.Lebrija }, DemoPlace.Barrancabermeja, 115, Going, 0, "TRK-006", "operador3",
            new DemoCargo[] { new("Cemento", 4500, 6, CargoPriority.Standard), new("Varilla de construcción", 3200, 3, CargoPriority.Standard) }),
        new("Ruta Bogotá - Villavicencio", DemoPlace.Bogota, new[] { DemoPlace.Chipaque }, DemoPlace.Villavicencio, 120, Going, 0, "TRK-007", "operador4",
            new DemoCargo[] { new("Electrodomésticos", 3800, 22, CargoPriority.High), new("Textiles", 1200, 15, CargoPriority.Standard) }),
        new("Ruta Barranquilla - Santa Marta", DemoPlace.Barranquilla, new[] { DemoPlace.Cienaga }, DemoPlace.SantaMarta, 93, Going, 0, "TRK-008", "operador5",
            new DemoCargo[] { new("Alimentos perecederos", 5200, 18, CargoPriority.Fragile), new("Bebidas", 2800, 10, CargoPriority.Standard) }),
        new("Ruta Cali - Palmira", DemoPlace.Cali, new[] { DemoPlace.Yumbo }, DemoPlace.Palmira, 32, Going, 0, "VAN-015", "operador6",
            new DemoCargo[] { new("Paquetería de comercio electrónico", 620, 5.5, CargoPriority.Standard), new("Medicamentos", 180, 1.2, CargoPriority.Urgent) }),
        new("Ruta Medellín - Rionegro", DemoPlace.Medellin, new[] { DemoPlace.Envigado }, DemoPlace.Rionegro, 38, Going, 0, "VAN-016", "operador7",
            new DemoCargo[] { new("Insumos hospitalarios", 450, 3, CargoPriority.Urgent), new("Papelería", 300, 4, CargoPriority.Standard) }),
        new("Ruta Pereira - Manizales", DemoPlace.Pereira, new[] { DemoPlace.Chinchina }, DemoPlace.Manizales, 55, Late, 0, "VAN-017", "operador8",
            new DemoCargo[] { new("Café empacado", 900, 6, CargoPriority.High), new("Etiquetas y empaques", 120, 1, CargoPriority.Standard) }, DelayMinutes: 25),
        new("Ruta Cúcuta - Pamplona", DemoPlace.Cucuta, new[] { DemoPlace.LosPatios }, DemoPlace.Pamplona, 75, Going, 0, "VAN-018", "operador9",
            new DemoCargo[] { new("Repuestos automotrices", 700, 3, CargoPriority.High), new("Calzado", 350, 6, CargoPriority.Standard) }),
        new("Ruta Ibagué - Bogotá", DemoPlace.Ibague, new[] { DemoPlace.Girardot }, DemoPlace.Bogota, 200, Late, 0, "VAN-019", "operador10",
            new DemoCargo[] { new("Frutas frescas", 1100, 8, CargoPriority.Fragile), new("Hortalizas", 250, 3, CargoPriority.Standard) }, DelayMinutes: 40),
        new("Mensajería Bucaramanga - Floridablanca", DemoPlace.Bucaramanga, new[] { DemoPlace.Canaveral }, DemoPlace.Floridablanca, 12, Going, 0, "MOT-030", "operador11",
            new DemoCargo[] { new("Documentos", 8, 0.1, CargoPriority.Urgent), new("Muestras de laboratorio", 12, 0.15, CargoPriority.Fragile) }),
        new("Ruta Bucaramanga - Piedecuesta", DemoPlace.Bucaramanga, new[] { DemoPlace.Ruitoque }, DemoPlace.Piedecuesta, 22, Going, 0, "CAR-025", "operador12",
            new DemoCargo[] { new("Equipos de cómputo", 150, 1.5, CargoPriority.Fragile), new("Material de oficina", 90, 1, CargoPriority.Standard) })
    };
}

/// <summary>Rutas programadas para los próximos días: el «siguiente viaje» de varios operadores que hoy ya están en la calle.</summary>
public static class DemoPlannedRoutes
{
    private const DeliveryRouteStatus Next = DeliveryRouteStatus.Planned;

    public static IReadOnlyList<DemoRoute> All { get; } = new DemoRoute[]
    {
        new("Ruta Bogotá - Tunja", DemoPlace.Bogota, new[] { DemoPlace.Zipaquira }, DemoPlace.Tunja, 150, Next, 1, "TRK-009", "operador3",
            new DemoCargo[] { new("Maquinaria liviana", 3600, 14, CargoPriority.High), new("Insumos agrícolas", 2000, 8, CargoPriority.Standard) }),
        new("Ruta Medellín - Bogotá", DemoPlace.Medellin, new[] { DemoPlace.Honda }, DemoPlace.Bogota, 415, Next, 1, "TRK-010", "operador4",
            new DemoCargo[] { new("Textiles", 4800, 30, CargoPriority.Standard), new("Repuestos de automóviles", 2500, 9, CargoPriority.High) }),
        new("Ruta Cali - Pereira", DemoPlace.Cali, new[] { DemoPlace.Tulua }, DemoPlace.Pereira, 220, Next, 2, "VAN-020", "operador5",
            new DemoCargo[] { new("Cosméticos", 600, 6, CargoPriority.Fragile), new("Bebidas", 500, 3, CargoPriority.Standard) }),
        new("Ruta Cartagena - Barranquilla", DemoPlace.Cartagena, new[] { DemoPlace.Turbaco }, DemoPlace.Barranquilla, 120, Next, 2, "VAN-021", "operador6",
            new DemoCargo[] { new("Equipos médicos", 800, 5, CargoPriority.Urgent), new("Mercancía general", 400, 4, CargoPriority.Standard) }),
        new("Mensajería Medellín - El Poblado", DemoPlace.Medellin, new[] { DemoPlace.Laureles }, DemoPlace.Poblado, 8, Next, 1, "MOT-031", "operador7",
            new DemoCargo[] { new("Documentos notariales", 6, 0.08, CargoPriority.Urgent), new("Llaves y tarjetas", 4, 0.05, CargoPriority.Fragile) }),
        new("Ruta Villavicencio - Bogotá", DemoPlace.Villavicencio, new[] { DemoPlace.Guayabetal }, DemoPlace.Bogota, 120, Next, 3, "CAR-026", "operador8",
            new DemoCargo[] { new("Repuestos eléctricos", 200, 2, CargoPriority.Standard), new("Instrumental de laboratorio", 120, 1.2, CargoPriority.Fragile) })
    };
}

/// <summary>Viajes ya terminados en los últimos días (historial de la flota).</summary>
public static class DemoCompletedRoutes
{
    private const DeliveryRouteStatus Done = DeliveryRouteStatus.Completed;

    public static IReadOnlyList<DemoRoute> All { get; } = new DemoRoute[]
    {
        new("Ruta Bucaramanga - San Gil", DemoPlace.Bucaramanga, new[] { DemoPlace.LosSantos }, DemoPlace.SanGil, 95, Done, -1, "TRK-011", "operador9",
            new DemoCargo[] { new("Cemento", 5000, 5, CargoPriority.Standard), new("Ladrillo", 3000, 8, CargoPriority.Standard) }),
        new("Ruta Cúcuta - Bucaramanga", DemoPlace.Cucuta, new[] { DemoPlace.Pamplona }, DemoPlace.Bucaramanga, 200, Done, -2, "TRK-012", "operador10",
            new DemoCargo[] { new("Arroz", 6000, 12, CargoPriority.Standard), new("Aceites comestibles", 1800, 6, CargoPriority.Standard) }),
        new("Ruta Pereira - Cali", DemoPlace.Pereira, new[] { DemoPlace.Tulua }, DemoPlace.Cali, 215, Done, -2, "VAN-022", "operador11",
            new DemoCargo[] { new("Muebles", 900, 8, CargoPriority.Standard), new("Menaje de cocina", 300, 2.5, CargoPriority.Fragile) }),
        new("Ruta Barranquilla - Cartagena", DemoPlace.Barranquilla, new[] { DemoPlace.Turbaco }, DemoPlace.Cartagena, 120, Done, -3, "VAN-023", "operador12",
            new DemoCargo[] { new("Bebidas", 1000, 7, CargoPriority.Standard), new("Snacks", 200, 2, CargoPriority.Standard) }),
        new("Mensajería Girón - Bucaramanga", DemoPlace.Giron, new[] { DemoPlace.Canaveral }, DemoPlace.Bucaramanga, 14, Done, -1, "MOT-032", "operador1",
            new DemoCargo[] { new("Paquetes pequeños", 10, 0.2, CargoPriority.Standard), new("Sobres certificados", 3, 0.02, CargoPriority.High) }),
        new("Ruta Bogotá - Ibagué", DemoPlace.Bogota, new[] { DemoPlace.Fusagasuga }, DemoPlace.Ibague, 200, Done, -4, "CAR-027", "operador2",
            new DemoCargo[] { new("Insumos de oficina", 180, 2, CargoPriority.Standard), new("Libros", 120, 1.5, CargoPriority.Standard) })
    };
}
