namespace FleetManagement.Application.Commands;

/// <summary>
/// Lo que el historial necesita saber de un comando ya ejecutado, sin importar qué devolvió (por eso no es genérica):
/// cómo se llama, si cambió algo y cómo deshacerlo y rehacerlo.
/// </summary>
public interface IUndoableAction
{
    /// <summary>Texto para el usuario ("Crear la ruta 'X'"); se muestra en el historial y en la auditoría.</summary>
    string Description { get; }

    /// <summary>Si la última ejecución cambió algo. Una orden que no hizo nada (por ejemplo, optimizar una ruta que ya estaba bien) no entra al historial.</summary>
    bool HasEffect { get; }

    /// <summary>Revierte lo que hizo. Lanza <see cref="CommandNotUndoableException"/> si el estado cambió y ya no es posible.</summary>
    Task UndoAsync();

    /// <summary>Vuelve a ejecutar la operación con los datos de ahora (con las mismas reglas que la primera vez).</summary>
    Task RedoAsync();
}
