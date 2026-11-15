namespace FleetManagement.Application.Monitoring;

/// <summary>
/// SUBJECT, lado del publicador (patrón OBSERVER): lo único que necesita quien emite telemetría (el simulador de movimiento).
/// No sabe cuántos observadores hay ni qué hacen con el evento: eso es lo que desacopla la simulación de la auditoría, del
/// tablero en vivo y de cualquier observador futuro (Open/Closed: se agrega un observador sin tocar al publicador).
/// </summary>
public interface ITelemetryPublisher
{
    /// <summary>
    /// Entrega el evento a los observadores registrados. No lanza por culpa de un observador. Si se llama desde un observador
    /// (un evento como reacción a otro) no hay recursión: el evento se encola y se entrega al terminar el actual.
    /// </summary>
    void Publish(TelemetryEvent telemetry);
}
