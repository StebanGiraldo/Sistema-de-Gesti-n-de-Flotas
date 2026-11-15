using System.Reflection;
using System.Runtime.CompilerServices;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Herramienta de las pruebas de diseño: qué tipos NOMBRA la firma de un tipo (clase base, interfaces, campos, propiedades,
/// constructores y métodos con sus parámetros y retornos). Sirve para comprobar quién depende de quién en una capa sin leer
/// código fuente. Es una lectura de FIRMAS: no ve lo que un método usa solo por dentro; para eso están las pruebas de comportamiento.
/// </summary>
public static class SignatureDependencies
{
    private const BindingFlags Everything =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Tipos propios del proyecto (no de .NET) que aparecen en la firma de <paramref name="type"/>.</summary>
    public static IReadOnlySet<Type> Of(Type type)
    {
        var found = new HashSet<Type>();

        Add(type.BaseType, found);
        foreach (var contract in type.GetInterfaces())
            Add(contract, found);
        foreach (var field in type.GetFields(Everything))
            Add(field.FieldType, found);
        foreach (var property in type.GetProperties(Everything))
            Add(property.PropertyType, found);
        foreach (var constructor in type.GetConstructors(Everything))
            AddParameters(constructor.GetParameters(), found);
        foreach (var method in type.GetMethods(Everything))
        {
            Add(method.ReturnType, found);
            AddParameters(method.GetParameters(), found);
        }

        found.Remove(type);
        return found.Where(IsFromTheProject).ToHashSet();
    }

    /// <summary>Tipos "de primer nivel" escritos a mano de un espacio de nombres (sin los que genera el compilador).</summary>
    public static IEnumerable<Type> TypesIn(Assembly assembly, string @namespace)
        => assembly.GetTypes().Where(type => type.Namespace == @namespace && !type.IsNested && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    /// <summary>Tipos "de primer nivel" escritos a mano que NO están en ninguno de los espacios de nombres dados.</summary>
    public static IEnumerable<Type> TypesOutside(Assembly assembly, params string[] namespaces)
        => assembly.GetTypes().Where(type => !type.IsNested
                                             && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                                             && !namespaces.Contains(type.Namespace));

    private static void AddParameters(IEnumerable<ParameterInfo> parameters, ISet<Type> found)
    {
        foreach (var parameter in parameters)
            Add(parameter.ParameterType, found);
    }

    private static void Add(Type? type, ISet<Type> found)
    {
        if (type is null)
            return;

        if (type.HasElementType)
        {
            Add(type.GetElementType(), found);
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                Add(argument, found);
            type = type.GetGenericTypeDefinition();
        }

        found.Add(type);
    }

    private static bool IsFromTheProject(Type type) => type.Namespace?.StartsWith("FleetManagement.", StringComparison.Ordinal) == true;
}
