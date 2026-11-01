namespace FleetManagement.Application.Commands;

/// <summary>
/// PATRÓN COMMAND — la orden: una operación convertida en un objeto que se puede crear en un lugar y ejecutar en otro.
/// Quien la pide (el controlador) no sabe cómo se hace; quien la hace (los servicios) no sabe quién la pidió.
/// </summary>
public interface ICommand<TResult>
{
    /// <summary>Ejecuta la operación y devuelve lo mismo que devolvería el servicio al que delega.</summary>
    Task<TResult> ExecuteAsync();
}
