using FleetManagement.Application.Geo;

namespace FleetManagement.Application.Optimization;

/// <summary>
/// Arma un plan de reparto: primero decide qué vehículo lleva cada entrega (<see cref="ICargoAssignmentStrategy"/>) y luego
/// en qué orden las visita cada uno (<see cref="IStopSequencer"/>). Es lógica pura, sin acceso a datos: recibe los
/// vehículos ya elegidos como disponibles y devuelve el plan, lo que permite probarla con números a mano.
///
/// Con otras estrategias el mismo planificador arma el plan "ingenuo" contra el que se compara el optimizado.
/// </summary>
public sealed class DispatchPlanner
{
    private readonly ICargoAssignmentStrategy _assignment;
    private readonly IStopSequencer _sequencer;

    public DispatchPlanner(ICargoAssignmentStrategy assignment, IStopSequencer sequencer)
    {
        _assignment = assignment;
        _sequencer = sequencer;
    }

    public PlannedDispatch Plan(GeoCoordinate depot, IReadOnlyList<PendingDelivery> deliveries, IReadOnlyList<CandidateVehicle> vehicles)
    {
        var assignment = _assignment.Assign(deliveries, vehicles, depot);
        var trips = assignment.Loads.Select(load => PlanTrip(depot, load)).ToList();
        return new PlannedDispatch(trips, assignment.Unassigned);
    }

    private PlannedTrip PlanTrip(GeoCoordinate depot, VehicleLoad load)
    {
        var stops = _sequencer.Sequence(depot, load.Deliveries);
        var km = TourLength.Km(depot, stops.Select(stop => stop.Destination).ToList());
        return new PlannedTrip(load, stops, km, TripEstimate.Minutes(km, load.Vehicle.SpeedKmh));
    }
}
