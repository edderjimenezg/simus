namespace PNMC.Contracts;

/// <summary>
/// Un campo que la propuesta quiere cambiar, con lo que decía y lo que pasaría a decir.
/// </summary>
/// <param name="ValorAnterior">
/// Lo que el registro decía cuando la propuesta se envió. Se copia en ese momento y no se resuelve
/// contra el presente: si se leyera del registro, tras aplicar la propuesta la comparación diría
/// que no cambió nada.
/// </param>
public sealed record CampoPropuestoDto(
    long Id,
    string SeccionId,
    string CampoId,
    string CampoEtiqueta,
    string? ValorAnterior,
    string? ValorPropuesto);

/// <summary>
/// La propuesta de cambio de una organización sobre un registro suyo ya publicado.
/// </summary>
public sealed record PropuestaDeCambioDto(
    long Id,
    string ModuloId,
    string RegistroId,
    string NombreDelRegistro,
    string Estado,
    string? Motivo,
    string? OrganizacionNombre,
    string? ProponenteNombre,
    string? DecideNombre,
    string? MotivoDeLaDecision,
    DateTime? FechaEnvio,
    DateTime? FechaDecision,
    IReadOnlyList<CampoPropuestoDto> Campos);

/// <summary>Un campo de la propuesta, tal y como lo manda quien la escribe.</summary>
/// <remarks>
/// <b>NO LLEVA EL VALOR ANTERIOR.</b> Lo pone el servidor al enviar, leyéndolo del registro: si
/// viniera del cliente, quien propone podría decidir contra qué se le compara.
/// </remarks>
public sealed class CampoPropuestoSolicitud
{
    public string? CampoId { get; set; }
    public string? ValorPropuesto { get; set; }
}

/// <summary>
/// Guarda el borrador de una propuesta.
/// </summary>
/// <remarks>
/// SEMANTICA DE REEMPLAZO, igual que la revisión por campos: lo que llega es la lista completa de
/// campos y lo que no venga se retira de la propuesta. Una ruta por campo dejaría al cliente
/// llevando la cuenta de qué mandó y qué no.
/// </remarks>
public sealed class GuardarPropuestaDeCambioSolicitud
{
    /// <summary>Por qué se propone. Lo lee quien decide antes de mirar campo por campo.</summary>
    public string? Motivo { get; set; }

    /// <remarks>
    /// ANULABLE A PROPOSITO: un cuerpo con <c>"campos": null</c> deserializa a nulo por mucho que la
    /// propiedad nazca con lista vacía, y entonces el recorrido revienta en vez de contestar 400.
    /// </remarks>
    public List<CampoPropuestoSolicitud>? Campos { get; set; }
}

/// <summary>Lo que el Programa decide sobre una propuesta que tiene delante.</summary>
public sealed class DecidirPropuestaDeCambioSolicitud
{
    /// <summary><c>aplicar</c>, <c>pedir_ajustes</c> o <c>rechazar</c>.</summary>
    public string? Decision { get; set; }

    /// <summary>
    /// Obligatorio al rechazar y al pedir ajustes; opcional al aplicar, porque el cambio aprobado
    /// habla por sí mismo.
    /// </summary>
    public string? Motivo { get; set; }
}

/// <summary>Una propuesta esperando decisión, como la enseña la bandeja del Programa.</summary>
public sealed record PropuestaEnRevisionDto(
    long Id,
    string ModuloId,
    string RegistroId,
    string NombreDelRegistro,
    string? OrganizacionNombre,
    string? ProponenteNombre,
    string? Motivo,
    int CamposQueCambian,
    DateTime? FechaEnvio);
