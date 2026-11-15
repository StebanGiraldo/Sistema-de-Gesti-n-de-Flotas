using System.Reflection;
using FleetManagement.Application.Analysis;
using FleetManagement.Application.Assignment;
using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Interfaces.Services;
using FleetManagement.Application.Mediators;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;
using Xunit;

namespace FleetManagement.Application.Tests.State;

/// <summary>
/// Pruebas de DISEÑO del patrón State: cuántos estados hay, de qué dependen, quién los usa (siempre por el resolutor, nunca por una
/// clase concreta) y que los constructores existentes de los componentes que lo adoptaron siguen siendo compatibles.
/// </summary>
public class VehicleStateDesignTests
{
    private const string States = "FleetManagement.Domain.States";

    private static readonly Assembly Domain = typeof(Vehicle).Assembly;
    private static readonly Assembly Application = typeof(VehicleService).Assembly;

    private static readonly Type[] ConcreteStates =
    {
        typeof(AvailableState), typeof(EnRouteState), typeof(MaintenanceState), typeof(OutOfServiceState)
    };

    [Fact]
    public void ThereAreExactlyFourStates_OnePerVehicleStatus_AllDerivedFromTheSameBase()
    {
        var found = Domain.GetTypes().Where(type => type.IsClass && !type.IsAbstract && typeof(VehicleState).IsAssignableFrom(type)).ToList();

        Assert.Equal(ConcreteStates.Select(type => type.Name).Order(), found.Select(type => type.Name).Order());
        Assert.Equal(Enum.GetValues<VehicleStatus>().Length, found.Count);
    }

    [Fact]
    public void TheStatesNamespace_OnlyKnowsTheDomain()
    {
        var used = SignatureDependencies.TypesIn(Domain, States)
            .SelectMany(SignatureDependencies.Of)
            .Select(dependency => dependency.Namespace!)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(new[] { States, "FleetManagement.Domain.Entities", "FleetManagement.Domain.Enums" }.Order().ToArray(), used);
    }

    [Fact]
    public void TheComponentsThatUseTheStates_AskTheResolver_NeverAConcreteState()
    {
        var consumers = new[]
        {
            typeof(VehicleColleague), typeof(VehicleService), typeof(RouteAssignmentValidator), typeof(FleetAnalysisService),
            typeof(VehicleUtilizationVisitor), typeof(OperationalInsightsVisitor)
        };

        foreach (var consumer in consumers)
        {
            var dependencies = SignatureDependencies.Of(consumer);
            Assert.Contains(typeof(IVehicleStateResolver), dependencies);
            Assert.DoesNotContain(dependencies, dependency => ConcreteStates.Contains(dependency));
        }
    }

    [Fact]
    public void ARouteOrADriverService_DoesNotKnowTheStates_TheMediatorDelegatesToTheVehicleColleague()
    {
        // La regla "al empezar un viaje el vehículo queda En ruta" ya no vive en DeliveryRouteService: sigue en el Mediator + el participante de vehículos.
        Assert.DoesNotContain(typeof(IVehicleStateResolver), SignatureDependencies.Of(typeof(DeliveryRouteService)));
        Assert.DoesNotContain(typeof(IVehicleStateResolver), SignatureDependencies.Of(typeof(FleetLifecycleMediator)));
        Assert.DoesNotContain(typeof(IVehicleStateResolver), SignatureDependencies.Of(typeof(RouteColleague)));
    }

    // ---------------- Compatibilidad de los constructores existentes ----------------

    [Fact]
    public void TheVehicleColleague_KeepsItsConstructor_AndTheResolverIsOptional()
    {
        var parameters = Assert.Single(typeof(VehicleColleague).GetConstructors()).GetParameters();

        Assert.Equal(new[] { typeof(IVehicleRepository), typeof(IVehicleStateResolver) }, parameters.Select(parameter => parameter.ParameterType));
        Assert.False(parameters[0].IsOptional);
        Assert.True(parameters[1].IsOptional);
    }

    [Fact]
    public void TheVehicleService_KeepsItsSixOriginalParameters_AndTheResolverIsAnOptionalSeventh()
    {
        var parameters = Assert.Single(typeof(VehicleService).GetConstructors()).GetParameters();

        Assert.Equal(7, parameters.Length);
        Assert.All(parameters.Take(6), parameter => Assert.False(parameter.IsOptional));
        Assert.Equal(typeof(IVehicleStateResolver), parameters[6].ParameterType);
        Assert.True(parameters[6].IsOptional);
    }

    [Fact]
    public void TheAssignmentValidator_KeepsItsFourOriginalParameters_AndTheResolverIsAnOptionalFifth()
    {
        var parameters = Assert.Single(typeof(RouteAssignmentValidator).GetConstructors()).GetParameters();

        Assert.Equal(
            new[] { typeof(IVehicleRepository), typeof(IDriverRepository), typeof(IDeliveryRouteRepository), typeof(IMaintenanceService), typeof(IVehicleStateResolver) },
            parameters.Select(parameter => parameter.ParameterType));
        Assert.All(parameters.Take(4), parameter => Assert.False(parameter.IsOptional));
        Assert.True(parameters[4].IsOptional);
    }

    // ---------------- Lo que no se tocó ----------------

    [Fact]
    public void TheVehicleEntity_StillHasItsStatusAsAnEnum_TheStatesAreNotAParallelCopy()
    {
        var property = typeof(Vehicle).GetProperty(nameof(Vehicle.Status))!;

        Assert.Equal(typeof(VehicleStatus), property.PropertyType);
        Assert.True(property.CanRead && property.CanWrite);
        Assert.Empty(typeof(Vehicle).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(field => typeof(IVehicleState).IsAssignableFrom(field.FieldType)));
    }

    [Fact]
    public void TheDomainEntities_DoNotKnowTheStatesEither()
    {
        foreach (var entity in new[] { typeof(Vehicle), typeof(DeliveryRoute), typeof(MaintenanceRecord) })
            Assert.DoesNotContain(SignatureDependencies.Of(entity), dependency => dependency.Namespace == States);
    }

    [Fact]
    public void OnlyTheDomainCanChangeTheStatusThroughATransition_TheContextHidesTheWrite()
    {
        var publicMethods = typeof(VehicleStateContext).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "ChangeTo", "CompleteMaintenance", "FinishTrip", "OperationBlockReason", "StartTrip" }, publicMethods);
    }

    [Fact]
    public void TheApplicationAssembly_DoesNotContainAnyConcreteState()
    {
        Assert.DoesNotContain(Application.GetTypes(), type => typeof(IVehicleState).IsAssignableFrom(type) && type.IsClass);
    }
}
