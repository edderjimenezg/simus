namespace PNMC.Contracts;

/// <summary>Una edición anual publicada para consulta pública.</summary>
/// <remarks>
/// Es un contrato de <c>EdicionesFestival</c>, no una versión del perfil del Festival. Solo
/// transporta los datos propios de la ocurrencia anual que el modelo vigente puede sostener:
/// año, número, nombre, descripción, fechas y estado operativo. La dirección artística y los
/// datos de contacto no se publican; pertenecen al expediente de gestión, no a la consulta abierta.
/// </remarks>
public sealed record EdicionFestivalPublicaDto(
    int Id,
    int? Anio,
    int? NumeroEdicion,
    string? Nombre,
    string? Descripcion,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string EstadoOperativo,
    string EstadoOperativoEtiqueta);
