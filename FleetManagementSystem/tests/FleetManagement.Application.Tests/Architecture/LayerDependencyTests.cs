using System.Reflection;
using FleetManagement.Application.Simulation;
using FleetManagement.Domain.Entities;
using FleetManagement.Infrastructure.Navigation;
using Xunit;

namespace FleetManagement.Application.Tests.Architecture;

/// <summary>
/// Prueba de arquitectura sencilla: las dependencias entre capas de la Clean Architecture solo pueden apuntar hacia
/// adentro (Api → Infrastructure → Application → Domain). Se leen las referencias que realmente tiene cada ensamblado
/// compilado: si alguien usa en Application un tipo de Infrastructure, la compilación del proyecto cambia y esta prueba falla.
/// </summary>
public class LayerDependencyTests
{
    private const string ApiLayer = "FleetManagement.Api";
    private const string InfrastructureLayer = "FleetManagement.Infrastructure";
    private const string ApplicationLayer = "FleetManagement.Application";

    private static readonly Assembly Domain = typeof(Vehicle).Assembly;
    private static readonly Assembly Application = typeof(IVehicleMovementSimulator).Assembly;
    private static readonly Assembly Infrastructure = typeof(GoogleMapsAdapter).Assembly;

    [Fact]
    public void Application_DoesNotReferenceInfrastructureNorTheApi()
        => AssertDoesNotReference(Application, InfrastructureLayer, ApiLayer);

    [Fact]
    public void Domain_DoesNotReferenceAnyOtherLayer()
        => AssertDoesNotReference(Domain, ApplicationLayer, InfrastructureLayer, ApiLayer);

    [Fact]
    public void Infrastructure_DoesNotReferenceTheApi()
        => AssertDoesNotReference(Infrastructure, ApiLayer);

    [Fact]
    public void Application_DoesNotDependOnWebOrDatabaseFrameworks()
    {
        // La capa de aplicación debe poder usarse sin ASP.NET Core ni EF Core: eso es lo que la hace reemplazable e probable.
        var referenced = Application.GetReferencedAssemblies().Select(name => name.Name!).ToList();

        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    private static void AssertDoesNotReference(Assembly layer, params string[] forbiddenLayers)
    {
        var referenced = layer.GetReferencedAssemblies().Select(name => name.Name).ToList();

        foreach (var forbidden in forbiddenLayers)
            Assert.DoesNotContain(forbidden, referenced);
    }
}
