namespace FleetManagement.Application.AlertHandling;

/// <summary>Arma una cadena a partir de una lista, en el orden dado, y devuelve su primer eslabón. Así el orden se lee de arriba abajo en un solo lugar (<c>Program.cs</c>).</summary>
public static class AlertHandlerChain
{
    public static IAlertHandler Of(IAlertHandler first, params IAlertHandler[] rest)
    {
        ArgumentNullException.ThrowIfNull(first);

        var last = first;
        foreach (var handler in rest)
            last = last.SetNext(handler);

        return first;
    }
}
