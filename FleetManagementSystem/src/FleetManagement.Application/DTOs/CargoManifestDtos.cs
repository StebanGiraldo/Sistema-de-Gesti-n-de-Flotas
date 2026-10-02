namespace FleetManagement.Application.DTOs;

/// <summary>
/// Nodo del manifiesto de carga (patrón Composite). Una hoja ("Item") y un
/// grupo ("Group") comparten exactamente la misma forma: un grupo simplemente
/// trae hijos en <paramref name="Children"/>. Los pesos y volúmenes se
/// redondean a 3 decimales para evitar ruido de punto flotante en la respuesta.
/// </summary>
public record CargoManifestNodeDto(
    string Name,
    string Kind,
    double TotalWeightKg,
    double TotalVolumeM3,
    int ItemCount,
    List<CargoManifestNodeDto> Children
);

/// <summary>Manifiesto jerárquico de la carga de una ruta.</summary>
public record CargoManifestDto(Guid RouteId, CargoManifestNodeDto Manifest);
