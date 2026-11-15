using System.Reflection;
using FleetManagement.Application.Analysis;
using FleetManagement.Application.Reports;
using FleetManagement.Application.Services;
using FleetManagement.Application.Tests.Support;
using FleetManagement.Domain.Entities;
using Xunit;

namespace FleetManagement.Application.Tests.Visitor;

/// <summary>
/// Pruebas de DISEÑO del patrón Visitor: que no se convierta en otro Decorator, que los visitantes solo vean los adaptadores (nunca
/// las entidades), que el conjunto de elementos sea cerrado y a propósito, y que quien los use cree un visitante nuevo por análisis
/// (guardan datos mientras recorren, así que no se comparten).
/// </summary>
public class VisitorDesignTests
{
    private const string Analysis = "FleetManagement.Application.Analysis";
    private const string Reports = "FleetManagement.Application.Reports";

    private static readonly Assembly Application = typeof(IFleetVisitor).Assembly;

    private static readonly Type[] ConcreteVisitors =
    {
        typeof(VehicleUtilizationVisitor), typeof(RouteDistanceVisitor), typeof(MaintenanceHistoryVisitor), typeof(OperationalInsightsVisitor)
    };

    private static readonly Type[] EntityTypes = { typeof(Vehicle), typeof(DeliveryRoute), typeof(MaintenanceRecord), typeof(Waypoint) };

    // ---------------- Quién conoce a quién ----------------

    [Fact]
    public void Analysis_DependsOnlyOnTheDomainTheStatesAndTheResultDtos()
    {
        var used = SignatureDependencies.TypesIn(Application, Analysis)
            .SelectMany(SignatureDependencies.Of)
            .Select(dependency => dependency.Namespace!)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                Analysis, "FleetManagement.Application.DTOs",
                "FleetManagement.Domain.Entities", "FleetManagement.Domain.Enums", "FleetManagement.Domain.States"
            }.Order().ToArray(),
            used);
    }

    [Fact]
    public void TheVisitors_NeverNameAnEntity_TheyOnlySeeTheAdapters()
    {
        var visitorSide = ConcreteVisitors.Concat(new[] { typeof(IFleetVisitor), typeof(FleetVisitor<int>) });

        foreach (var type in visitorSide)
            Assert.DoesNotContain(SignatureDependencies.Of(type), dependency => EntityTypes.Contains(dependency));
    }

    [Fact]
    public void OnlyTheAdaptersAndTheStructure_KnowTheEntities()
    {
        var knowing = SignatureDependencies.TypesIn(Application, Analysis)
            .Where(type => SignatureDependencies.Of(type).Any(dependency => EntityTypes.Contains(dependency)))
            .Select(type => type.Name)
            .Order()
            .ToArray();

        Assert.Equal(new[] { nameof(FleetElementStructure), nameof(MaintenanceRecordElement), nameof(RouteElement), nameof(VehicleElement) }, knowing);
    }

    [Fact]
    public void TheVisitor_IsNotAnotherDecorator_ItDoesNotWrapNorTouchTheReports()
    {
        var analysisTypes = SignatureDependencies.TypesIn(Application, Analysis).ToList();
        var reportTypes = SignatureDependencies.TypesIn(Application, Reports)
            .Concat(SignatureDependencies.TypesIn(Application, "FleetManagement.Application.Reports.Decorators"))
            .ToList();

        Assert.NotEmpty(reportTypes);
        Assert.DoesNotContain(analysisTypes.SelectMany(SignatureDependencies.Of), dependency => dependency.Namespace?.StartsWith(Reports, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(reportTypes.SelectMany(SignatureDependencies.Of), dependency => dependency.Namespace == Analysis);
        Assert.DoesNotContain(analysisTypes, type => typeof(IFleetReportGenerator).IsAssignableFrom(type));
    }

    // ---------------- Los contratos ----------------

    [Fact]
    public void TheVisitorContract_HasOneMethodPerKindOfElement()
    {
        var methods = typeof(IFleetVisitor).GetMethods().Select(method => method.Name).Order().ToArray();

        Assert.Equal(new[] { "VisitMaintenanceRecord", "VisitRoute", "VisitVehicle" }, methods);
        Assert.Equal(new[] { "Accept" }, typeof(IFleetElement).GetMethods().Select(method => method.Name));
        Assert.Equal(new[] { "Result" }, typeof(IFleetVisitor<>).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void TheSetOfElements_IsClosedAndDeliberate_ThreeKinds()
    {
        // El costo conocido del Visitor: agregar una CLASE de elemento obliga a cambiar la interfaz del visitante. Está fijado aquí para
        // que ocurra a propósito y no por accidente; agregar un ANÁLISIS nuevo (otro visitante) no cuesta nada, y esa es la ventaja.
        var elements = Application.GetTypes().Where(type => type.IsClass && !type.IsAbstract && typeof(IFleetElement).IsAssignableFrom(type)).Select(type => type.Name).Order().ToArray();

        Assert.Equal(new[] { nameof(MaintenanceRecordElement), nameof(RouteElement), nameof(VehicleElement) }, elements);
    }

    [Fact]
    public void TheConcreteVisitors_AreTheFourThatWereDesigned()
    {
        var found = Application.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.IsNested && type.BaseType is { IsGenericType: true } baseType && baseType.GetGenericTypeDefinition() == typeof(FleetVisitor<>))
            .Select(type => type.Name)
            .Order()
            .ToArray();

        Assert.Equal(ConcreteVisitors.Select(visitor => visitor.Name).Order().ToArray(), found);
    }

    [Fact]
    public void EveryVisitor_DeliversItsResultAsAResultDto()
    {
        foreach (var visitor in ConcreteVisitors)
        {
            var result = visitor.GetProperty("Result")!.PropertyType;
            var shown = result.IsGenericType ? result.GetGenericArguments()[0] : result;
            Assert.Equal("FleetManagement.Application.DTOs", shown.Namespace);
        }
    }

    [Fact]
    public void TheConcreteClasses_AreSealed()
    {
        Assert.All(ConcreteVisitors, type => Assert.True(type.IsSealed, $"{type.Name} no es sealed"));
        Assert.True(typeof(FleetElementStructure).IsSealed);
    }

    // ---------------- Visitantes con estado ----------------

    [Fact]
    public void TheAnalysisService_CreatesItsVisitorsPerAnalysis_ItNeverReceivesOne()
    {
        var constructor = Assert.Single(typeof(FleetAnalysisService).GetConstructors());
        var dependencies = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToList();

        Assert.DoesNotContain(dependencies, type => typeof(IFleetVisitor).IsAssignableFrom(type));   // se crean en cada análisis, no se inyectan
    }

    [Fact]
    public void TheVisitors_HoldTheDataTheyCollect_ThatIsWhyTheyAreNotShared()
    {
        var withDictionaries = ConcreteVisitors
            .Where(type => type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Any(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
            .ToList();

        Assert.Equal(ConcreteVisitors.Length, withDictionaries.Count);   // todos acumulan lo que visitan
    }
}
