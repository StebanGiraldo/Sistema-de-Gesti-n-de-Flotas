namespace FleetManagement.Application.Interpreter;

/// <summary>
/// La regla escrita no se puede interpretar (carácter inesperado, falta un operando, variable desconocida, regla demasiado
/// larga o anidada…). El mensaje está en español y dice en qué carácter ocurrió (<see cref="Position"/>, empezando en 1);
/// la API lo devuelve tal cual en un 400.
/// </summary>
public sealed class RuleSyntaxException : Exception
{
    public RuleSyntaxException(string description, int position)
        : base($"{description} (posición {position})")
    {
        Position = position;
    }

    /// <summary>Número del carácter donde se detectó el problema (el primero es el 1).</summary>
    public int Position { get; }
}
