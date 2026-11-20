namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Valor con el que trabaja una comparación: un número, un texto o «sin dato». Cada valor sabe compararse con otro por doble
/// despacho, así que ni las expresiones ni el parser preguntan «de qué tipo es» con un <c>if</c> o un <c>switch</c>.
/// <see cref="CompareTo"/> devuelve el signo de (este − otro) o <c>null</c> si no se pueden comparar (tipos distintos o sin
/// dato); en ese caso la comparación completa da falso.
/// </summary>
public abstract record RuleValue
{
    /// <summary>La variable no tiene dato (p. ej. un vehículo sin mantenimientos). No es un cero: no se compara con nada.</summary>
    public static readonly RuleValue Missing = new MissingValue();

    public static RuleValue FromNumber(double number) => new NumberValue(number);

    public static RuleValue FromText(string text) => new TextValue(text);

    public static RuleValue FromOptional(double? number) => number is { } value ? new NumberValue(value) : Missing;

    public abstract int? CompareTo(RuleValue other);

    // Segundo despacho: «yo soy el lado derecho y el izquierdo es un número / un texto». Por defecto, no comparables.
    internal virtual int? CompareFromNumber(double left) => null;

    internal virtual int? CompareFromText(string left) => null;

    private sealed record MissingValue : RuleValue
    {
        public override int? CompareTo(RuleValue other) => null;
    }
}

public sealed record NumberValue(double Number) : RuleValue
{
    public override int? CompareTo(RuleValue other) => other.CompareFromNumber(Number);

    internal override int? CompareFromNumber(double left) => left.CompareTo(Number);
}

/// <summary>Los textos se comparan sin distinguir mayúsculas: <c>status = "available"</c> encuentra los <c>Available</c>.</summary>
public sealed record TextValue(string Text) : RuleValue
{
    public override int? CompareTo(RuleValue other) => other.CompareFromText(Text);

    internal override int? CompareFromText(string left) => string.Compare(left, Text, StringComparison.OrdinalIgnoreCase);
}
