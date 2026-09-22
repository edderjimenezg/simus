namespace PNMC.Domain.Entities;

/// <summary>
/// Una revisión institucional de CUALQUIER proceso del Ecosistema, con sus cambios campo por campo.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES LA UNICA FORMA DEL CICLO DE REVISION, Y NO NOMBRA NINGUN PROCESO.</b> Identifica
/// el registro por <see cref="ModuloId"/> + <see cref="RegistroId"/>, el mismo par con el que lo
/// identifican la bitácora de auditoría y la procedencia. Nace para Mercados Musicales el 16 de
/// septiembre de 2026 y sirve tal cual para Escuelas, Escenarios, Redes y Lutería.
/// </para>
/// <para>
/// <b>LA FORMA ANTERIOR YA NO EXISTE.</b> Hubo una tabla por proceso —<c>RevisionesFestival</c>,
/// atada a <c>dbo.Festivales</c> con clave ajena obligatoria—, y durante un tiempo convivieron las
/// dos como deuda nombrada. El 18 de septiembre de 2026 se saldó: los expedientes se copiaron aquí,
/// las ediciones de festival dejaron de tener su propio par y las tablas viejas se retiraron en
/// <c>V20260918_01__el_ciclo_de_revision_en_una_sola_forma.sql</c>. Queda una sola forma.
/// </para>
/// <para>
/// El ciclo y la gobernanza son los mismos: <c>borrador</c> mientras el funcionario escribe,
/// <c>enviada</c> cuando la organización la recibe, <c>cerrada</c> cuando vuelve a enviar el
/// registro; y los dos nombres —quien envía y quien recibe— se copian al crear la fila.
/// </para>
/// </remarks>
public sealed class RevisionDeRegistroRow
{
    public long Id { get; set; }

    /// <summary>El módulo del registro: <c>mercados</c>, <c>festivales</c>, <c>escuelas</c>…</summary>
    public string ModuloId { get; set; } = string.Empty;

    /// <summary>El identificador del registro dentro de su módulo, como texto.</summary>
    public string RegistroId { get; set; } = string.Empty;

    /// <summary>borrador | enviada | cerrada.</summary>
    public string Estado { get; set; } = "borrador";

    public int IdUsuarioRevisor { get; set; }
    public string? RevisorNombre { get; set; }

    public int? IdUsuarioDestinatario { get; set; }
    public string? DestinatarioNombre { get; set; }

    public int? IdOrganizacion { get; set; }
    public string? OrganizacionNombre { get; set; }

    public string? ObservacionGeneral { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public DateTime? FechaCierre { get; set; }
}

/// <summary>
/// Un cambio pedido sobre UN campo concreto de la ficha de un registro.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL AMBITO NO ES DECORACION.</b> <c>principal</c> es la ficha del registro y
/// <c>subregistro</c> una de sus realizaciones —una edición—. «Lugar específico» existe en el
/// mercado y en cada una de sus ediciones: sin el ámbito, la nota no dice cuál corregir.
/// </para>
/// <para>
/// <b>EL ROTULO Y EL VALOR VIAJAN COPIADOS.</b> Cuando la organización corrija el campo, el valor de
/// hoy deja de existir —justo porque se pidió cambiarlo—, y un expediente que no dice sobre qué se
/// pidió el cambio no sirve de evidencia.
/// </para>
/// </remarks>
public sealed class RevisionDeRegistroObservacionRow
{
    public long Id { get; set; }
    public long IdRevision { get; set; }

    /// <summary>
    /// La revisión a la que pertenece.
    /// </summary>
    /// <remarks>
    /// EXISTE PARA PODER AÑADIR NOTAS A UNA REVISION QUE TODAVIA NO TIENE IDENTIFICADOR: en el
    /// primer guardado la fila y sus notas se escriben en la misma transacción.
    /// </remarks>
    public RevisionDeRegistroRow? Revision { get; set; }

    /// <summary>principal | subregistro.</summary>
    public string Ambito { get; set; } = "principal";

    /// <summary>La realización sobre la que se pide el cambio. Nulo cuando el ámbito es la ficha.</summary>
    public string? SubregistroId { get; set; }

    public string SeccionId { get; set; } = string.Empty;
    public string CampoId { get; set; } = string.Empty;
    public string CampoEtiqueta { get; set; } = string.Empty;
    public string? ValorObservado { get; set; }

    public string Nota { get; set; } = string.Empty;

    /// <summary>pendiente | atendida.</summary>
    public string Estado { get; set; } = "pendiente";

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaAtencion { get; set; }
    public int? IdUsuarioAtiende { get; set; }
}
