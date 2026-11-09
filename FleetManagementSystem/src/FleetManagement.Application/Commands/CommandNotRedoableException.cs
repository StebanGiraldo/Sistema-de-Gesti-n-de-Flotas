namespace FleetManagement.Application.Commands;

/// <summary>
/// Rehacer ya no es posible porque algo que la orden necesita dejó de existir y no va a volver (por ejemplo, la ruta sobre
/// la que actuaba se eliminó al deshacer su creación). La API la responde con 409 y la orden se quita del historial. Un fallo
/// pasajero (un vehículo ocupado, una regla de asignación) NO usa esta excepción: la orden queda disponible para reintentar.
/// </summary>
public sealed class CommandNotRedoableException : InvalidOperationException
{
    public CommandNotRedoableException(string message) : base(message)
    {
    }
}
