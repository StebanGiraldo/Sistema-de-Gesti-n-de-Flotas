namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Base de los contextos de variables: una tabla nombre → valor, de solo lectura y sin distinguir mayúsculas. Lo que no está
/// en la tabla se resuelve como <see cref="RuleValue.Missing"/>. Cada contexto concreto solo decide qué variables ofrece y
/// cómo se calculan a partir de los datos reales.
/// </summary>
public abstract class VariableContext : IRuleContext
{
    private readonly IReadOnlyDictionary<string, RuleValue> _values;

    protected VariableContext(IReadOnlyDictionary<string, RuleValue> values)
    {
        _values = values;
    }

    public RuleValue Resolve(string variable) => _values.GetValueOrDefault(variable, RuleValue.Missing);

    protected static IReadOnlyDictionary<string, RuleValue> Table(params (string Name, RuleValue Value)[] variables)
        => variables.ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.OrdinalIgnoreCase);
}
