namespace FleetManagement.Application.Analysis;

/// <summary>Gravedad de un hallazgo operativo, de más a menos urgente (el orden del enum es el orden en que se listan).</summary>
public enum InsightSeverity
{
    Critical,
    Warning,
    Info
}

/// <summary>Códigos estables de los hallazgos, para que quien consuma el análisis (o una prueba) no dependa del texto del mensaje.</summary>
public static class InsightCodes
{
    public const string ActiveRouteWithoutOperableVehicle = "ActiveRouteWithoutOperableVehicle";
    public const string ActiveRouteVehicleNotEnRoute = "ActiveRouteVehicleNotEnRoute";
    public const string PlannedRouteVehicleNotOperable = "PlannedRouteVehicleNotOperable";
    public const string EnRouteVehicleWithoutActiveRoute = "EnRouteVehicleWithoutActiveRoute";
    public const string ActiveRouteDelayed = "ActiveRouteDelayed";
    public const string ServiceDistanceExceeded = "ServiceDistanceExceeded";
    public const string PlannedRouteWithoutVehicle = "PlannedRouteWithoutVehicle";
}

/// <summary>
/// Umbrales con los que <see cref="OperationalInsightsVisitor"/> decide qué merece un aviso. Tienen nombre y valor por defecto en
/// vez de números sueltos dentro de las reglas, y se pueden cambiar sin tocar el visitante (por ejemplo, en una prueba).
/// </summary>
/// <param name="DelayAlertMinutes">Minutos de retraso, en una ruta en curso, a partir de los cuales se avisa.</param>
/// <param name="ServiceDistanceAlertKm">Kilómetros recorridos desde el último mantenimiento (o desde siempre, si nunca tuvo uno) a partir de los cuales se avisa.</param>
public sealed record InsightThresholds(int DelayAlertMinutes = 30, double ServiceDistanceAlertKm = 10_000);
