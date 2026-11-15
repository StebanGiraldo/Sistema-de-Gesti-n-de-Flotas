using FleetManagement.Application.DTOs;
using FleetManagement.Domain.Enums;
using FleetManagement.Domain.States;

namespace FleetManagement.Application.Analysis;

/// <summary>
/// CONCRETE VISITOR #1 (patrón VISITOR): resumen de utilización de los vehículos. Visita vehículos y rutas y responde cuántos
/// vehículos pueden operar, cuántos están realmente en uso (operables con una ruta en curso), cuánto han recorrido y cómo se
/// reparte el uso por tipo.
///
/// "Puede operar" no se decide aquí con un <c>switch</c> sobre el estado: se pregunta al estado del vehículo (patrón STATE,
/// <see cref="IVehicleStateRules.CanOperate"/>), que es quien conoce esa regla. Visitar dos veces el mismo elemento no lo cuenta
/// dos veces, y el resultado no depende del orden de la visita (se calcula al leer <see cref="Result"/>).
/// </summary>
public sealed class VehicleUtilizationVisitor : FleetVisitor<VehicleUtilizationDto>
{
    private const int MileageDecimals = 3;
    private const int PercentDecimals = 1;
    private const double PercentScale = 100.0;

    private readonly IVehicleStateResolver _states;
    private readonly Dictionary<Guid, VehicleElement> _vehicles = new();
    private readonly Dictionary<Guid, RouteElement> _routes = new();

    public VehicleUtilizationVisitor(IVehicleStateResolver? states = null)
    {
        _states = states ?? VehicleStateResolver.Default;
    }

    public override void VisitVehicle(VehicleElement vehicle) => _vehicles[vehicle.Id] = vehicle;

    public override void VisitRoute(RouteElement route) => _routes[route.Id] = route;

    public override VehicleUtilizationDto Result
    {
        get
        {
            var vehiclesWithActiveRoute = ActiveRouteVehicles.Of(_routes.Values);

            var usage = _vehicles.Values
                .OrderBy(vehicle => vehicle.LicensePlate, StringComparer.Ordinal)
                .Select(vehicle => ToUsage(vehicle, vehiclesWithActiveRoute))
                .ToList();

            var operable = _vehicles.Values.Count(vehicle => _states.For(vehicle.Status).CanOperate);
            var inUse = usage.Count(item => item.InUse);

            return new VehicleUtilizationDto(
                usage.Count,
                operable,
                inUse,
                operable == 0 ? 0 : Math.Round(PercentScale * inUse / operable, PercentDecimals),
                Math.Round(usage.Sum(item => item.MileageKm), MileageDecimals),
                usage.Count == 0 ? 0 : Math.Round(usage.Average(item => item.MileageKm), MileageDecimals),
                ByType(usage),
                usage);
        }
    }

    private VehicleUsageDto ToUsage(VehicleElement vehicle, IReadOnlySet<Guid> vehiclesWithActiveRoute)
    {
        var routesOfVehicle = _routes.Values.Where(route => route.AssignedVehicleId == vehicle.Id).ToList();

        return new VehicleUsageDto(
            vehicle.Id,
            vehicle.LicensePlate,
            vehicle.Type.ToString(),
            vehicle.Status.ToString(),
            Math.Round(vehicle.MileageKm, MileageDecimals),
            routesOfVehicle.Count,
            routesOfVehicle.Count(route => route.Status == DeliveryRouteStatus.Completed),
            _states.For(vehicle.Status).CanOperate && vehiclesWithActiveRoute.Contains(vehicle.Id));
    }

    private static IReadOnlyList<VehicleTypeUsageDto> ByType(IReadOnlyCollection<VehicleUsageDto> usage)
        => Enum.GetNames<VehicleType>()
            .Select(type =>
            {
                var ofType = usage.Where(item => item.Type == type).ToList();
                return new VehicleTypeUsageDto(
                    type,
                    ofType.Count,
                    ofType.Count(item => item.InUse),
                    ofType.Count == 0 ? 0 : Math.Round(ofType.Average(item => item.MileageKm), MileageDecimals));
            })
            .ToList();
}
