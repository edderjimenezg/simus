namespace PNMC.Domain.Entities;

/// <summary>
/// Los cinco estados por los que pasa una propuesta de cambio sobre lo ya publicado.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO SON LOS DEL REGISTRO.</b> Un mercado publicado sigue publicado mientras su propuesta va y
/// viene: lo que cambia de estado es la propuesta, no la ficha que el público está leyendo. Esa
/// separación es justamente lo que hace que proponer un cambio no sea editar en caliente.
/// </para>
/// </remarks>
public static class EstadosDePropuesta
{
    /// <summary>La escribe la organización y solo ella la ve.</summary>
    public const string Borrador = "borrador";

    /// <summary>El Programa la tiene delante y debe decidir.</summary>
    public const string EnRevision = "en_revision";

    /// <summary>Devuelta con cambios pedidos; la organización la retoma.</summary>
    public const string AjustesSolicitados = "ajustes_solicitados";

    /// <summary>Aprobada, y sus campos ya están en el registro publicado.</summary>
    public const string Aplicada = "aplicada";

    /// <summary>No se aplica, y el motivo queda escrito.</summary>
    public const string Rechazada = "rechazada";

    /// <summary>Las dos que cierran el expediente: ya no admiten cambios ni decisiones.</summary>
    public static bool EstaCerrada(string estado) => estado is Aplicada or Rechazada;
}

/// <summary>
/// La propuesta de cambio de una organización sobre un registro suyo YA PUBLICADO.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE.</b> Lo que ve el público no puede cambiar porque alguien abrió un formulario.
/// Entre la intención de la organización y la ficha publicada tiene que haber una decisión del
/// Programa, y esta tabla es donde esa intención espera.
/// </para>
/// <para>
/// <b>GENERICA POR MODULO</b>, igual que <see cref="RevisionDeRegistroRow"/>: el registro se
/// identifica por <see cref="ModuloId"/> + <see cref="RegistroId"/> y no por una clave ajena a una
/// tabla concreta, así que los procesos que vengan heredan el circuito sin tablas propias.
/// </para>
/// <para>
/// <b>NO GUARDA UNA COPIA DEL REGISTRO</b>, solo lo que cambia: una fila por campo propuesto en
/// <see cref="PropuestaDeCambioCampoRow"/>. Era la diferencia con <c>PropuestasCambioFestival</c>,
/// una sombra completa del festival que obligaba a añadir cada campo nuevo en dos sitios; esa tabla
/// se retiró y esta es la única forma que queda.
/// </para>
/// </remarks>
public sealed class PropuestaDeCambioRow
{
    public long Id { get; set; }

    /// <summary>El módulo del registro: <c>mercados</c>, <c>festivales</c>, <c>escuelas</c>…</summary>
    public string ModuloId { get; set; } = string.Empty;

    /// <summary>El identificador del registro dentro de su módulo, como texto.</summary>
    public string RegistroId { get; set; } = string.Empty;

    /// <summary>
    /// Sobre qué realización del registro se propone, cuando el registro tiene realizaciones.
    /// </summary>
    /// <remarks>
    /// <b>ES EL MISMO CONCEPTO QUE EN LAS OBSERVACIONES DE REVISION</b>, con el mismo nombre y el
    /// mismo tipo. Un mercado no tiene realizaciones y aquí lleva nulo; un Festival sí —lo que el
    /// público lee es una VERSION de su perfil—, y la propuesta declara sobre cuál se hizo. Ese es
    /// su «antes», y no la versión vigente de hoy: una propuesta de hace un mes se comparó contra lo
    /// que el público leía entonces.
    /// </remarks>
    public string? SubregistroId { get; set; }

    /// <summary>
    /// La realización que nació de aplicar la propuesta, cuando aplicarla crea una.
    /// </summary>
    /// <remarks>
    /// La simétrica de <see cref="SubregistroId"/>, en el lado de la salida: aplicar una propuesta
    /// sobre un mercado reescribe su ficha y no nace nada; sobre un Festival nace la VERSION
    /// siguiente de su perfil. <b>NO ES UN ADORNO:</b> la lectura pública se apoya en ella para no
    /// servir una versión que nació de una propuesta que no acabó aprobada.
    /// </remarks>
    public string? SubregistroResultanteId { get; set; }

    /// <summary>Uno de <see cref="EstadosDePropuesta"/>.</summary>
    public string Estado { get; set; } = EstadosDePropuesta.Borrador;

    public int IdOrganizacion { get; set; }
    public string? OrganizacionNombre { get; set; }

    public int? IdUsuarioProponente { get; set; }
    public string? ProponenteNombre { get; set; }

    /// <summary>Por qué se propone. Lo lee quien decide antes de mirar campo por campo.</summary>
    public string? Motivo { get; set; }

    public int? IdUsuarioDecide { get; set; }
    public string? DecideNombre { get; set; }

    /// <summary>
    /// Lo que dijo quien decidió. Obligatorio al rechazar: rechazar sin decir por qué no es una
    /// decisión, es un portazo. Al aplicar no hace falta, porque el cambio aprobado habla solo.
    /// </summary>
    public string? MotivoDeLaDecision { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public DateTime? FechaDecision { get; set; }
}

/// <summary>
/// Un campo concreto que la propuesta quiere cambiar, con lo que decía y lo que pasaría a decir.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL VALOR ANTERIOR SE COPIA AL ENVIAR</b>, no se resuelve contra el registro. Si se leyera del
/// presente, en cuanto la propuesta se aplicara la comparación diría que no cambió nada: el «antes»
/// habría pasado a ser el «después».
/// </para>
/// <para>
/// <b><see cref="CampoId"/> ES EL MISMO VOCABULARIO</b> que usan las observaciones de revisión, así
/// que una propuesta sobre «Correo de contacto» y una nota pidiendo ajustes sobre «Correo de
/// contacto» hablan del mismo campo sin traducción en medio.
/// </para>
/// </remarks>
public sealed class PropuestaDeCambioCampoRow
{
    public long Id { get; set; }
    public long IdPropuesta { get; set; }

    /// <summary>Dónde está en la pantalla, para agrupar la comparación como se agrupa la ficha.</summary>
    public string SeccionId { get; set; } = string.Empty;

    public string CampoId { get; set; } = string.Empty;
    public string CampoEtiqueta { get; set; } = string.Empty;

    public string? ValorAnterior { get; set; }
    public string? ValorPropuesto { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    public PropuestaDeCambioRow? Propuesta { get; set; }
}
