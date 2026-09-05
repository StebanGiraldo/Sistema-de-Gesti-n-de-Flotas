namespace FleetManagement.Domain.Common;

/// <summary>
/// PATRÓN PROTOTYPE (creacional): contrato genérico que permite a una entidad
/// clonarse a sí misma en lugar de que el código cliente deba reconstruirla
/// campo por campo. Lo implementan <see cref="FleetManagement.Domain.Entities.Vehicle"/>,
/// <see cref="FleetManagement.Domain.Entities.DeliveryRoute"/>,
/// <see cref="FleetManagement.Domain.Entities.Waypoint"/> y
/// <see cref="FleetManagement.Domain.Entities.CargoItem"/>.
///
/// Beneficio principal: cuando un objeto es costoso o complejo de construir
/// (por ejemplo, una ruta con varias paradas y artículos de carga), clonar una
/// instancia "plantilla" ya configurada es más simple, rápido y menos propenso
/// a errores que invocar un constructor con muchos parámetros o repetir la
/// lógica de creación. Cada implementación hace una copia PROFUNDA (deep copy)
/// de sus colecciones internas para que modificar el clon nunca afecte al
/// objeto original.
/// </summary>
/// <typeparam name="T">Tipo concreto que se clona a sí mismo.</typeparam>
public interface IPrototype<T>
{
    T Clone();
}
