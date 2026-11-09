namespace FleetManagement.Application.DTOs;

/// <summary>
/// Un artículo de carga de la lista plana de una ruta (patrón Iterator). <paramref name="Position"/> es el lugar
/// que ocupa en el recorrido en profundidad del manifiesto, empezando en 1; <paramref name="Priority"/> es la
/// prioridad del artículo, que en el manifiesto jerárquico se deduce del grupo "Prioridad X" que lo contiene.
/// Los pesos y volúmenes se redondean a 3 decimales, igual que en el manifiesto jerárquico.
/// </summary>
public record CargoManifestItemDto(
    int Position,
    string Description,
    double WeightKg,
    double VolumeM3,
    string Priority
);

/// <summary>
/// Lista plana de los artículos de carga de una ruta, en el mismo orden en que aparecen en el manifiesto
/// jerárquico. Los totales coinciden con los de la raíz de ese manifiesto.
/// </summary>
public record CargoManifestItemsDto(
    Guid RouteId,
    int ItemCount,
    double TotalWeightKg,
    double TotalVolumeM3,
    List<CargoManifestItemDto> Items
);
