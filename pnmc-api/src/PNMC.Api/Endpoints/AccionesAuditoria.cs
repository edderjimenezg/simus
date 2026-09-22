namespace PNMC.Api.Endpoints;

/// <summary>
/// Los trece verbos que <c>BitacoraAuditoria.Accion</c> admite, y la traduccion desde
/// los nombres de evento que usa el circuito Festival.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. <c>CK_BitacoraAuditoria_Accion</c> cierra la columna a trece verbos
/// tecnicos. El circuito Festival escribia ahi nombres de evento funcional
/// —<c>FestivalCreado</c>, <c>FestivalEnviadoARevision</c>,
/// <c>FestivalCambioPropuesto</c>...— y <b>ninguno de ellos esta en la lista</b>.
/// Medido contra <c>PNMC_LOCAL</c>:
/// </para>
/// <code>
/// Accion = 'FestivalEnviadoARevision'  ->  RECHAZADO
/// Accion = 'actualizar'                ->  ACEPTADO
/// </code>
/// <para>
/// Es decir: <b>toda escritura del circuito externo de Festival fallaba contra SQL
/// Server</b>, no solo el envio a revision. Crear el borrador, editarlo, proponer un
/// cambio: las tres insertaban una fila de auditoria que la base rechaza.
/// </para>
/// <para>
/// POR QUE SE ADAPTA EL CODIGO Y NO SE AFLOJA EL CHECK. Porque la separacion ya estaba
/// bien pensada y era el codigo el que la ignoraba. El diseño funcional distingue
/// registros distintos, y dos de ellos son justamente estos:
/// </para>
/// <code>
/// Auditoria             -> quien hizo tecnicamente que
/// Historial de revision -> que decision institucional se tomo
/// </code>
/// <para>
/// El verbo tecnico (<c>crear</c>, <c>actualizar</c>) es lo que pide la auditoria. El
/// evento funcional (<c>FestivalEnviadoARevision</c>) ya se guarda en
/// <c>RegistrosRevisionHistorial.Accion</c>, que <b>no</b> tiene <c>CHECK</c> y es su
/// sitio. Ampliar la lista de verbos habria sido relajar una restriccion para que
/// encajara un dato que pertenece a otra tabla.
/// </para>
/// <para>
/// NO SE PIERDE INFORMACION: el nombre del evento sigue viajando dentro de
/// <c>ValoresNuevos</c>, de modo que una consulta a la bitacora puede seguir
/// distinguiendo un alta de un envio a revision.
/// </para>
/// </remarks>
public static class AccionesAuditoria
{
    public const string Crear = "crear";
    public const string Actualizar = "actualizar";
    public const string Eliminar = "eliminar";
    public const string Publicar = "publicar";
    public const string Archivar = "archivar";
    public const string Aprobar = "aprobar";
    public const string Rechazar = "rechazar";
    public const string IniciarSesion = "iniciar_sesion";
    public const string CerrarSesion = "cerrar_sesion";
    public const string IniciarSesionExterna = "iniciar_sesion_externa";
    public const string CerrarSesionExterna = "cerrar_sesion_externa";
    public const string CrearOrganizacion = "crear_organizacion";
    public const string AsignarAdministradorInicial = "asignar_administrador_inicial";

    /// <summary>Los trece verbos de <c>CK_BitacoraAuditoria_Accion</c>, en el mismo orden
    /// en que los declara <c>V20260519_02__administracion_control.sql</c>.</summary>
    public static readonly string[] Admitidos =
    [
        Crear, Actualizar, Eliminar, Publicar, Archivar,
        Aprobar, Rechazar, IniciarSesion, CerrarSesion,
        IniciarSesionExterna, CerrarSesionExterna,
        CrearOrganizacion, AsignarAdministradorInicial,
    ];

    /// <summary>
    /// Verbo tecnico que corresponde a un evento funcional del circuito Festival.
    /// </summary>
    /// <remarks>
    /// El caso por defecto es <c>actualizar</c> y no una excepcion: un evento nuevo sin
    /// traducir debe quedar registrado como una modificacion generica —con su nombre
    /// completo en <c>ValoresNuevos</c>— antes que tumbar la operacion del usuario o,
    /// peor, dejar la accion sin rastro en la bitacora.
    /// </remarks>
    /// <summary>
    /// Verbo técnico que corresponde a un evento del circuito de un mercado musical.
    /// </summary>
    /// <remarks>
    /// <b>ES UNA FUNCION APARTE Y NO UN AÑADIDO A <see cref="DeEvento"/>.</b> Los dos circuitos
    /// tienen eventos con el mismo significado y distinto nombre, y mezclarlos en una sola tabla
    /// obligaría a leerla entera para saber a qué proceso pertenece cada línea. El caso por defecto
    /// es <c>actualizar</c>, por el mismo motivo: un evento nuevo sin traducir queda registrado como
    /// una modificación genérica —con su nombre completo en <c>ValoresNuevos</c>— antes que tumbar
    /// la operación de quien lo hizo o dejarla sin rastro.
    /// </remarks>
    public static string DeMercado(string? evento) => evento switch
    {
        "MercadoCreado" or "crear" => Crear,
        "MercadoPublicado" or "publicar" => Publicar,
        "MercadoArchivado" or "archivar" => Archivar,
        "MercadoAprobado" => Aprobar,
        "MercadoConAjustesSolicitados" => Rechazar,
        // EL CICLO DE VISIBILIDAD DE UNA EDICION. «despublicar» no está entre los trece verbos de
        // `CK_BitacoraAuditoria_Accion`, así que se registra como una actualización con el detalle
        // en el cuerpo; «eliminar» sí está, y es el único de este módulo que no se deshace.
        "eliminar" => Eliminar,
        // LA PROPUESTA DE CAMBIO SOBRE LO PUBLICADO. Proponer es crear un expediente; aplicarlo es
        // aprobarlo y rechazarlo es rechazarlo. «pedir ajustes» no tiene verbo propio entre los
        // trece y cae en `actualizar`, con su nombre completo en el cuerpo.
        "MercadoCambioPropuesto" => Crear,
        "MercadoPropuestaAplicada" => Aprobar,
        "MercadoPropuestaRechazada" => Rechazar,
        _ => Actualizar,
    };

    public static string DeEvento(string? evento) => evento switch
    {
        "FestivalCreado" => Crear,
        "FestivalCambioPropuesto" => Crear,
        "FestivalPropuestaPublicada" => Publicar,
        "FestivalPropuestaRechazada" => Rechazar,
        "FestivalRechazado" => Rechazar,
        "FestivalPublicado" => Publicar,
        "FestivalAprobado" => Aprobar,
        "FestivalArchivado" => Archivar,
        _ => Actualizar,
    };

    /// <summary>Indica si un verbo pasaria el <c>CHECK</c> de la base.</summary>
    public static bool EsAdmitido(string? accion) =>
        accion is not null && Array.IndexOf(Admitidos, accion) >= 0;
}
