using System.Globalization;
using FleetManagement.Application.DTOs;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Application.Tests.Proxy;

/// <summary>
/// Proveedor de navegación de prueba que hace de "sujeto real" detrás del proxy: cuenta cuántas veces se le llamó y
/// puede fallar a propósito. El número de llamada va dentro de la URL que devuelve: así una prueba distingue una URL
/// guardada (mismo número) de una pedida de nuevo (otro número). Lo llaman varios hilos, por eso el contador es atómico.
/// </summary>
public sealed class CountingNavigationProvider : INavigationProvider
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Si tiene valor, la PRÓXIMA llamada lanza esta excepción (una sola vez).</summary>
    public Exception? FailureOnNextCall { get; set; }

    public Task<string> GenerateRouteUrlAsync(
        double originLat, double originLng, double destinationLat, double destinationLng, IReadOnlyList<WaypointDto> waypoints)
    {
        var call = Interlocked.Increment(ref _calls);
        if (FailureOnNextCall is { } failure)
        {
            FailureOnNextCall = null;
            throw failure;
        }

        return Task.FromResult(string.Create(CultureInfo.InvariantCulture,
            $"https://proveedor.test/ruta?o={originLat},{originLng}&d={destinationLat},{destinationLng}&paradas={waypoints.Count}&llamada={call}"));
    }
}
