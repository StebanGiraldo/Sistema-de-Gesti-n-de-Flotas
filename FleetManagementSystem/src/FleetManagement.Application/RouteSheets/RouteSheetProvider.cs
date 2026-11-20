namespace FleetManagement.Application.RouteSheets;

/// <summary>Entrega la plantilla de la audiencia pedida (mismo papel que <c>VehicleFactoryProvider</c> con las fábricas).</summary>
public interface IRouteSheetProvider
{
    /// <exception cref="ArgumentException">La audiencia falta o no es de ninguna plantilla registrada; el mensaje lista las válidas.</exception>
    RouteSheetTemplate For(string? audience);
}

/// <summary>
/// Recibe TODAS las plantillas registradas y las indexa por la audiencia que cada una expone: no hay <c>switch</c> ni preguntas
/// por tipo. Agregar una variante es una clase nueva y una línea de registro; este proveedor no cambia. Los valores válidos que
/// informa son los de las plantillas que de verdad están registradas.
/// </summary>
public sealed class RouteSheetProvider : IRouteSheetProvider
{
    private readonly IReadOnlyDictionary<string, RouteSheetTemplate> _templates;

    public RouteSheetProvider(IEnumerable<RouteSheetTemplate> templates)
    {
        _templates = templates.ToDictionary(template => template.Audience.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    public RouteSheetTemplate For(string? audience)
    {
        if (audience is not null && _templates.TryGetValue(audience.Trim(), out var template))
            return template;

        throw new ArgumentException($"{Describe(audience)} Valores válidos: {string.Join(", ", _templates.Keys.Order(StringComparer.Ordinal))}.");
    }

    private static string Describe(string? audience)
        => string.IsNullOrWhiteSpace(audience) ? "Falta la audiencia." : $"Audiencia desconocida: '{audience}'.";
}
