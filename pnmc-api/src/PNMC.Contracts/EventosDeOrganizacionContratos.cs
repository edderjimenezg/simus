namespace PNMC.Contracts;

/// <summary>
/// Un proceso del ecosistema que puede enmarcar un evento de la organización.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUÉ UN EVENTO NECESITA UN PROCESO.</b> Lo decidió la dirección el 11 de septiembre de
/// 2026: una organización no publica eventos sueltos en la agenda del Programa. Publica eventos
/// <b>de sus procesos</b> —su festival, su mercado—, y si no tiene ninguno, no tiene qué anunciar
/// aquí. Sin esa regla, la agenda pública se convertiría en un tablón abierto.
/// </para>
/// <para>
/// <b>HOY SOLO HAY FESTIVALES, Y SE DICE.</b> Los demás procesos del ecosistema —mercados, redes,
/// escuelas— todavía no tienen su modelo en esta base. <c>Tipo</c> existe desde ahora para que el
/// día que lo tengan no haya que cambiar el contrato ni la pantalla.
/// </para>
/// </remarks>
public sealed record ProcesoQueEnmarcaDto(
    string Id,
    /// <summary>`festival` hoy; `mercado`, `red`… cuando existan.</summary>
    string Tipo,
    string TipoEtiqueta,
    string Nombre,
    /// <summary>Su territorio, para distinguir dos procesos de nombre parecido.</summary>
    string? Territorio);

/// <summary>Lo que una organización envía para anunciar un evento de uno de sus procesos.</summary>
/// <remarks>
/// NO LLEVA ESTADO. El evento nace siempre en revisión: quien lo escribe no decide si se publica.
/// Dejar que lo enviara el cliente sería poner la decisión institucional en manos del formulario.
/// </remarks>
public sealed class CrearEventoDeOrganizacionSolicitud
{
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? DescripcionLarga { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public string Modalidad { get; set; } = "presencial";
    public string? Lugar { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Url { get; set; }

    /// <summary>
    /// El proceso de la organización en el que se enmarca el evento.
    /// </summary>
    /// <remarks>
    /// ES OBLIGATORIO Y ES LA REGLA ENTERA. Un evento sin proceso no se puede anunciar desde el
    /// espacio de la organización.
    /// </remarks>
    public int FestivalId { get; set; }
}

/// <summary>Un evento de la organización, con el proceso que lo enmarca y en qué estado va.</summary>
public sealed record EventoDeOrganizacionDto(
    string Id,
    string Titulo,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Modalidad,
    string? Lugar,
    string Estado,
    string EstadoEtiqueta,
    string? ProcesoNombre,
    DateTime FechaActualizacion);
