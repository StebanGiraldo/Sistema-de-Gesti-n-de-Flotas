namespace FleetManagement.Application.Geo;

/// <summary>
/// Coordenada geográfica en grados decimales (WGS84). Es un value object: reemplaza el par suelto
/// <c>(double lat, double lng)</c> para que latitud y longitud no se puedan intercambiar por error (evita la obsesión
/// por los tipos primitivos). Vive en Application y no usa <c>NetTopologySuite.Point</c> a propósito: así la geometría
/// del cálculo no depende del tipo con el que Domain guarda las posiciones (X = longitud, Y = latitud).
/// </summary>
public readonly record struct GeoCoordinate(double Latitude, double Longitude);
