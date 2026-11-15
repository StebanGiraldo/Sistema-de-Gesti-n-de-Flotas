namespace FleetManagement.Application.Monitoring;

/// <summary>
/// OBSERVER (patrón OBSERVER): quien quiere enterarse de la telemetría de la flota implementa este contrato y se registra en el
/// sujeto (<see cref="ITelemetrySubject"/>). Es deliberadamente de un solo método (ISP): un observador no sabe quién publica, ni
/// cuántos más hay, ni conoce la simulación.
///
/// Reglas para quien lo implemente:
/// <list type="bullet">
/// <item>El sujeto lo invoca de a un evento por vez y nunca en paralelo consigo mismo, así que no necesita sincronización interna
/// para su propio estado; sí la necesita para lo que comparta con otros hilos (por ejemplo, lo que lean las peticiones HTTP).</item>
/// <item>Debe ser rápido: se ejecuta dentro del paso de la simulación. Un trabajo lento o de red no se hace aquí.</item>
/// <item>No debe depender del sujeto (se evitan las dependencias circulares) y no necesita capturar excepciones: si lanza, el
/// sujeto lo aísla, deja constancia en la auditoría y sigue con los demás observadores.</item>
/// </list>
/// </summary>
public interface ITelemetryObserver
{
    /// <summary>Recibe un evento publicado. Los observadores deciden por tipo cuáles les interesan y ignoran el resto.</summary>
    void OnTelemetry(TelemetryEvent telemetry);
}
