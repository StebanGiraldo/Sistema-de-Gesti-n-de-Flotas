namespace FleetManagement.Application.Optimization;

/// <summary>
/// Un vehículo "a medio cargar" mientras se reparte: lleva la cuenta de peso y volumen usados para saber si cabe otra
/// entrega. Es el recipiente (bin) del problema de empaquetado que resuelven las estrategias de asignación.
/// </summary>
internal sealed class LoadBin
{
    // Las sumas de pesos decimales no son exactas; sin margen, una carga justa del tamaño del vehículo no cabría.
    private const double Tolerance = 1e-9;

    private readonly List<PendingDelivery> _deliveries = new();
    private double _usedKg;
    private double _usedM3;

    public LoadBin(CandidateVehicle vehicle)
    {
        Vehicle = vehicle;
    }

    public CandidateVehicle Vehicle { get; }

    public bool IsUsed => _deliveries.Count > 0;

    /// <summary>¿Cabe la entrega en peso y, si el vehículo declara volumen, en volumen?</summary>
    public bool Fits(PendingDelivery delivery)
        => _usedKg + delivery.WeightKg <= Vehicle.CapacityKg + Tolerance
           && (Vehicle.CapacityM3 is not { } capacityM3 || _usedM3 + delivery.VolumeM3 <= capacityM3 + Tolerance);

    /// <summary>Kilos que le sobrarían al vehículo después de cargar la entrega (menos = mejor ajuste).</summary>
    public double FreeKgAfter(PendingDelivery delivery) => Vehicle.CapacityKg - _usedKg - delivery.WeightKg;

    public void Add(PendingDelivery delivery)
    {
        _deliveries.Add(delivery);
        _usedKg += delivery.WeightKg;
        _usedM3 += delivery.VolumeM3;
    }

    public VehicleLoad ToLoad() => new(Vehicle, _deliveries.ToList());

    /// <summary>Arma el resultado con los vehículos que terminaron llevando algo, en el orden en que se recibieron.</summary>
    public static CargoAssignment Assignment(IEnumerable<LoadBin> bins, IReadOnlyList<UnassignedDelivery> unassigned)
        => new(bins.Where(bin => bin.IsUsed).Select(bin => bin.ToLoad()).ToList(), unassigned);
}
