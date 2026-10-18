using System.Globalization;
using FleetManagement.Application.DTOs;

namespace FleetManagement.Infrastructure.Navigation;

/// <summary>
/// Clave de caché de una URL de navegación: identifica una ruta por su origen, su destino y sus paradas en el ORDEN en que
/// se visitan. Es un tipo propio (y no un <c>string</c> suelto) para que nadie arme claves a mano con otro formato: la única
/// forma de crearla es <see cref="From"/>, que aplica siempre las mismas reglas.
///
/// Reglas que decide la clave (todas para que dos pedidos "iguales" para el proveedor compartan entrada):
/// <list type="bullet">
///   <item>Las coordenadas se redondean a 6 decimales (~11 cm): mucho más fino que un GPS, y evita que el ruido de coma
///   flotante (7.1 contra 7.1000000001) haga fallar el caché. El costo: dos puntos a menos de ~10 cm comparten URL.</item>
///   <item>Se formatean con la cultura invariante: con "es-CO" la coma decimal produciría claves distintas según el equipo.</item>
///   <item>Las paradas se ordenan por <see cref="WaypointDto.Order"/> (igual que el proveedor real). Cambia la clave cambiar
///   el orden de visita; NO la cambian la etiqueta de la parada ni los valores absolutos de <c>Order</c>, que no afectan la URL.</item>
/// </list>
/// </summary>
public readonly record struct NavigationCacheKey(string Value)
{
    private const int CoordinateDecimals = 6;
    private const string CoordinateFormat = "F6"; // debe coincidir con CoordinateDecimals
    private const char PointSeparator = '>';

    public static NavigationCacheKey From(
        double originLat,
        double originLng,
        double destinationLat,
        double destinationLng,
        IReadOnlyList<WaypointDto> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);

        // Origen, paradas en orden y destino forman una sola cadena: de la cadena se recupera sin ambigüedad cuál es cuál
        // (el primero es el origen, el último el destino y los de en medio las paradas).
        var points = new List<string> { Format(originLat, originLng) };
        points.AddRange(waypoints.OrderBy(waypoint => waypoint.Order).Select(waypoint => Format(waypoint.Latitude, waypoint.Longitude)));
        points.Add(Format(destinationLat, destinationLng));

        return new NavigationCacheKey(string.Join(PointSeparator, points));
    }

    private static string Format(double latitude, double longitude)
        => $"{Normalize(latitude).ToString(CoordinateFormat, CultureInfo.InvariantCulture)},{Normalize(longitude).ToString(CoordinateFormat, CultureInfo.InvariantCulture)}";

    // "+ 0.0" convierte el cero negativo en cero: -0,0000001 se redondea a -0, que se escribiría "-0.000000" y no
    // coincidiría con la clave de 0.
    private static double Normalize(double degrees) => Math.Round(degrees, CoordinateDecimals) + 0.0;
}
