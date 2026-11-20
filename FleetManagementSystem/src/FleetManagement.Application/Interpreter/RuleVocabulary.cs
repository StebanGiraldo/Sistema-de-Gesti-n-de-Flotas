namespace FleetManagement.Application.Interpreter;

/// <summary>
/// Lista blanca de las variables que una regla puede nombrar (no distingue mayúsculas). El parser rechaza cualquier otro
/// identificador antes de construir el árbol: no hay forma de llegar a un dato que el contexto no haya ofrecido.
/// </summary>
public sealed class RuleVocabulary
{
    private readonly HashSet<string> _variables;

    public RuleVocabulary(IEnumerable<string> variables)
    {
        _variables = new HashSet<string>(variables, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names => _variables;

    public bool Contains(string name) => _variables.Contains(name);

    public string Describe() => string.Join(", ", _variables.OrderBy(name => name, StringComparer.Ordinal));
}
