namespace FleetManagement.Infrastructure.Persistence.Demo;

/// <summary>Cuenta de operador de demostración: usuario de acceso y conductor vinculado a él.</summary>
public sealed record DemoOperator(string Username, string FullName, string LicenseNumber, string Phone);

/// <summary>
/// Diez operadores nuevos (operador3 … operador12). Se suman a operador1 y operador2 del sembrador original, para
/// poder probar el portal del operador con varios conductores que tienen viajes distintos al mismo tiempo.
/// </summary>
public static class DemoOperators
{
    /// <summary>Contraseña común de todas las cuentas de operador de demostración (la misma de operador1 y operador2).</summary>
    public const string Password = "Operador123!";

    public static IReadOnlyList<DemoOperator> All { get; } = new DemoOperator[]
    {
        new("operador3", "Andrés Felipe Torres", "SAN-61045", "+57 300 555 0103"),
        new("operador4", "María Fernanda Rojas", "SAN-61046", "+57 300 555 0104"),
        new("operador5", "Julián David Pardo", "SAN-61047", "+57 300 555 0105"),
        new("operador6", "Diana Carolina Mejía", "SAN-61048", "+57 300 555 0106"),
        new("operador7", "Sebastián Ortiz", "SAN-61049", "+57 300 555 0107"),
        new("operador8", "Camila Andrea Vargas", "SAN-61050", "+57 300 555 0108"),
        new("operador9", "Jhon Alexander Peña", "SAN-61051", "+57 300 555 0109"),
        new("operador10", "Natalia Cárdenas", "SAN-61052", "+57 300 555 0110"),
        new("operador11", "Óscar Eduardo Duarte", "SAN-61053", "+57 300 555 0111"),
        new("operador12", "Valentina Herrera", "SAN-61054", "+57 300 555 0112")
    };
}
