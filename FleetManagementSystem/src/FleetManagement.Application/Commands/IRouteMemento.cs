namespace FleetManagement.Application.Commands;

/// <summary>
/// MEMENTO (patrón de comportamiento MEMENTO), interfaz ESTRECHA: lo único que ve quien solo GUARDA el recuerdo (el
/// Caretaker, <see cref="ApplyRouteOptimizationCommand"/>). Dice de qué ruta es y cómo se llamaba, para armar mensajes, y
/// nada más: ni las paradas ni las estimaciones. Así quien lo guarda no puede mirar dentro del recuerdo ni modificarlo; solo
/// puede devolvérselo a quien lo creó.
///
/// El contenido lo conoce <see cref="RouteSnapshot"/> (la interfaz ancha) y solo lo usa el Originator
/// (<see cref="RouteSnapshotService"/>) para restaurar. C# no tiene "friend", así que esa interfaz ancha es pública (las
/// pruebas la leen para verificar la captura) y la separación la sostienen los tipos: el Caretaker declara sus campos y
/// parámetros con esta interfaz y una prueba de arquitectura lo vigila.
/// </summary>
public interface IRouteMemento
{
    /// <summary>Ruta de la que se tomó el recuerdo.</summary>
    Guid RouteId { get; }

    /// <summary>Nombre que tenía la ruta al tomarlo; sirve para los mensajes cuando la ruta ya no existe.</summary>
    string RouteName { get; }
}
