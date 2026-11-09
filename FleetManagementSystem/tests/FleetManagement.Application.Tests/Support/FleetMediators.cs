using FleetManagement.Application.Interfaces.Repositories;
using FleetManagement.Application.Mediators;

namespace FleetManagement.Application.Tests.Support;

/// <summary>
/// Arma el participante de rutas REAL (con su mediador y el participante de vehículos) para las pruebas que crean a mano
/// <c>DeliveryRouteService</c> o <c>TripAlertService</c>, igual que lo hace Program.cs con el contenedor de dependencias.
/// Así esas pruebas siguen probando el comportamiento real y no un doble.
/// </summary>
public static class FleetMediators
{
    public static IRouteColleague CreateRouteColleague(IDeliveryRouteRepository routes, IVehicleRepository vehicles)
        => new RouteColleague(routes, new FleetLifecycleMediator(new VehicleColleague(vehicles)));
}
