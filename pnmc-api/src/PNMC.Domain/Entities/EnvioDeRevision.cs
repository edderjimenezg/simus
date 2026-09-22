namespace PNMC.Domain.Entities;

/// <summary>
/// Instantánea inmutable de lo que una organización entregó en un ciclo de revisión.
/// </summary>
/// <remarks>
/// <para>
/// No es una versión del perfil público ni una Edición. Su única función es dejar evidencia del
/// contenido recibido por la institución en cada envío, para que el siguiente pueda compararse
/// contra exactamente aquello que se revisó. Eso es lo que la distingue de la bitácora de
/// auditoría, que registra el hecho de haber enviado pero no lo que se envió.
/// </para>
/// <para>
/// <b>NO NOMBRA UN PROCESO.</b> Nació como <c>EnviosRevisionFestival</c>, con clave ajena a
/// <c>dbo.Festivales</c>, y pasó a identificar el registro por
/// <see cref="ModuloId"/> + <see cref="RegistroId"/>, el mismo par con el que lo identifican la
/// revisión, la propuesta de cambio, el historial y la procedencia. El motivo es el de siempre:
/// que Mercados y los procesos que vengan no tengan que crearse su propia tabla para lo mismo.
/// </para>
/// </remarks>
public sealed class EnvioDeRevisionRow
{
    public long Id { get; set; }

    /// <summary>El módulo del registro: <c>festivales</c>, <c>mercados</c>, <c>escuelas</c>…</summary>
    public string ModuloId { get; set; } = string.Empty;

    /// <summary>El identificador del registro dentro de su módulo, como texto.</summary>
    public string RegistroId { get; set; } = string.Empty;

    public int NumeroEnvio { get; set; }
    public int UsuarioRemitenteId { get; set; }
    public int OrganizacionId { get; set; }
    public long? RevisionOrigenId { get; set; }
    public string EstadoAnterior { get; set; } = string.Empty;
    public string DatosJson { get; set; } = string.Empty;
    public DateTime FechaEnvio { get; set; }
}
