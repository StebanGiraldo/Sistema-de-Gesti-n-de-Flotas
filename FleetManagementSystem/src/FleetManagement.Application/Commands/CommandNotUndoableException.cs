namespace FleetManagement.Application.Commands;

/// <summary>Deshacer ya no es posible porque el estado cambió después (por ejemplo, la ruta ya salió a la calle). La API la responde con 409.</summary>
public sealed class CommandNotUndoableException : InvalidOperationException
{
    public CommandNotUndoableException(string message) : base(message)
    {
    }
}
