using NetTopologySuite.Geometries;

namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Lugares de Colombia que usan las rutas de demostración (origen, paradas y destino).</summary>
public enum DemoPlace
{
    Bucaramanga, Giron, Floridablanca, Piedecuesta, Lebrija, Barrancabermeja, Canaveral, Ruitoque, LosSantos, SanGil,
    Bogota, Zipaquira, Tunja, Chipaque, Villavicencio, Guayabetal, Fusagasuga, Girardot, Ibague,
    Medellin, Envigado, Rionegro, Laureles, Poblado, Honda,
    Cali, Yumbo, Palmira, Tulua, Pereira, Chinchina, Manizales,
    Barranquilla, Cienaga, SantaMarta, Cartagena, Turbaco,
    Cucuta, LosPatios, Pamplona
}

/// <summary>Nombre y coordenadas (WGS84) de un lugar de demostración.</summary>
public sealed record DemoCoordinates(string Name, double Latitude, double Longitude)
{
    /// <summary>NetTopologySuite usa X = longitud e Y = latitud, igual que el resto del sistema.</summary>
    public Point ToPoint() => new(Longitude, Latitude) { SRID = 4326 };
}

/// <summary>Catálogo de lugares: una sola tabla, sin lógica repetida en las rutas.</summary>
public static class DemoPlaces
{
    private static readonly IReadOnlyDictionary<DemoPlace, DemoCoordinates> Catalog = new Dictionary<DemoPlace, DemoCoordinates>
    {
        [DemoPlace.Bucaramanga] = new("Bucaramanga", 7.1193, -73.1227),
        [DemoPlace.Giron] = new("Girón", 7.0806, -73.1716),
        [DemoPlace.Floridablanca] = new("Floridablanca", 7.0622, -73.0864),
        [DemoPlace.Piedecuesta] = new("Piedecuesta", 6.9886, -73.0507),
        [DemoPlace.Lebrija] = new("Lebrija", 7.1167, -73.2167),
        [DemoPlace.Barrancabermeja] = new("Barrancabermeja", 7.0653, -73.8547),
        [DemoPlace.Canaveral] = new("Cañaveral", 7.0925, -73.1000),
        [DemoPlace.Ruitoque] = new("Ruitoque", 7.0230, -73.0700),
        [DemoPlace.LosSantos] = new("Los Santos", 6.7500, -73.1000),
        [DemoPlace.SanGil] = new("San Gil", 6.5559, -73.1337),
        [DemoPlace.Bogota] = new("Bogotá", 4.7110, -74.0721),
        [DemoPlace.Zipaquira] = new("Zipaquirá", 5.0225, -74.0058),
        [DemoPlace.Tunja] = new("Tunja", 5.5353, -73.3678),
        [DemoPlace.Chipaque] = new("Chipaque", 4.4417, -74.0422),
        [DemoPlace.Villavicencio] = new("Villavicencio", 4.1420, -73.6266),
        [DemoPlace.Guayabetal] = new("Guayabetal", 4.2158, -73.8128),
        [DemoPlace.Fusagasuga] = new("Fusagasugá", 4.3369, -74.3644),
        [DemoPlace.Girardot] = new("Girardot", 4.3030, -74.8030),
        [DemoPlace.Ibague] = new("Ibagué", 4.4389, -75.2322),
        [DemoPlace.Medellin] = new("Medellín", 6.2442, -75.5812),
        [DemoPlace.Envigado] = new("Envigado", 6.1759, -75.5917),
        [DemoPlace.Rionegro] = new("Rionegro", 6.1553, -75.3737),
        [DemoPlace.Laureles] = new("Laureles", 6.2476, -75.5900),
        [DemoPlace.Poblado] = new("El Poblado", 6.2088, -75.5670),
        [DemoPlace.Honda] = new("Honda", 5.2086, -74.7364),
        [DemoPlace.Cali] = new("Cali", 3.4516, -76.5320),
        [DemoPlace.Yumbo] = new("Yumbo", 3.5853, -76.4939),
        [DemoPlace.Palmira] = new("Palmira", 3.5394, -76.3036),
        [DemoPlace.Tulua] = new("Tuluá", 4.0847, -76.1954),
        [DemoPlace.Pereira] = new("Pereira", 4.8133, -75.6961),
        [DemoPlace.Chinchina] = new("Chinchiná", 4.9833, -75.6039),
        [DemoPlace.Manizales] = new("Manizales", 5.0703, -75.5138),
        [DemoPlace.Barranquilla] = new("Barranquilla", 10.9685, -74.7813),
        [DemoPlace.Cienaga] = new("Ciénaga", 11.0069, -74.2478),
        [DemoPlace.SantaMarta] = new("Santa Marta", 11.2408, -74.1990),
        [DemoPlace.Cartagena] = new("Cartagena", 10.3910, -75.4794),
        [DemoPlace.Turbaco] = new("Turbaco", 10.3333, -75.4167),
        [DemoPlace.Cucuta] = new("Cúcuta", 7.8939, -72.5078),
        [DemoPlace.LosPatios] = new("Los Patios", 7.8333, -72.5167),
        [DemoPlace.Pamplona] = new("Pamplona", 7.3750, -72.6475)
    };

    public static DemoCoordinates Of(DemoPlace place) => Catalog[place];
}
