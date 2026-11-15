namespace FleetManagement.Application.Monitoring;

/// <summary>
/// SUBJECT, lado de los observadores (patrón OBSERVER): permite registrarlos y retirarlos. Está separado de
/// <see cref="ITelemetryPublisher"/> (ISP): el simulador solo necesita publicar y no debe poder registrar ni retirar
/// observadores; quien registra observadores no debe poder publicar eventos.
/// </summary>
public interface ITelemetrySubject
{
    /// <summary>Cantidad de observadores registrados.</summary>
    int ObserverCount { get; }

    /// <summary>
    /// Registra al observador. Un observador está registrado como máximo UNA vez (se compara por referencia): registrar de nuevo
    /// al mismo no lo duplica ni le entrega los eventos dos veces. Se devuelve un token que, al liberarse, lo retira (así una
    /// suscripción nunca queda huérfana: basta un <c>using</c>); liberarlo más de una vez es inofensivo.
    /// </summary>
    IDisposable Subscribe(ITelemetryObserver observer);

    /// <summary>Retira al observador. Devuelve <c>false</c> si no estaba registrado. Un observador retirado deja de recibir eventos nuevos.</summary>
    bool Unsubscribe(ITelemetryObserver observer);
}
