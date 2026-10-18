using System.ComponentModel.DataAnnotations;

namespace FleetManagement.Application.Simulation;

/// <summary>
/// Parámetros de la simulación de movimiento. Existen para que no haya números mágicos en el código de la simulación:
/// cada valor tiene nombre, valor por defecto y una regla de validez.
///
/// Se leen de la sección <c>"Simulation"</c> de la configuración con el patrón Options (<c>IOptions&lt;T&gt;</c>), que
/// enlaza y valida <c>Program.cs</c> al arrancar (un valor inválido detiene el arranque con un mensaje claro). Las clases de
/// Application reciben este objeto ya resuelto en vez de <c>IOptions&lt;T&gt;</c> para que la capa no dependa de
/// paquetes de configuración (ver README, sección Simulación de tiempo real).
/// </summary>
public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    /// <summary>Cada cuántos segundos reales avanza la simulación un paso.</summary>
    [Range(0.0, double.MaxValue, MinimumIsExclusive = true, ErrorMessage = "Simulation:TickSeconds debe ser mayor que 0.")]
    public double TickSeconds { get; set; } = 1.0;

    /// <summary>
    /// Cuántas veces más rápido que en la realidad se mueven los vehículos. Con 1 un vehículo que va a 100 km/h recorre
    /// 100 km en una hora real; con 10 los recorre en 6 minutos (útil para demostraciones).
    /// </summary>
    [Range(0.0, double.MaxValue, MinimumIsExclusive = true, ErrorMessage = "Simulation:TimeAcceleration debe ser mayor que 0.")]
    public double TimeAcceleration { get; set; } = 1.0;

    /// <summary>Fracción de la velocidad normal con la que avanza una ruta retrasada (0 &lt; factor ≤ 1).</summary>
    [Range(0.0, 1.0, MinimumIsExclusive = true, ErrorMessage = "Simulation:DelayedSpeedFactor debe estar entre 0 (sin incluir) y 1.")]
    public double DelayedSpeedFactor { get; set; } = 0.5;
}
