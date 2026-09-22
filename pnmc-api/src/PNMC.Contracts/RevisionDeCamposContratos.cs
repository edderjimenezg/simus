namespace PNMC.Contracts;

/// <summary>
/// Un cambio pedido sobre un campo, tal como lo escribe el funcionario y tal como lo lee la
/// organización.
/// </summary>
/*
    LOS CONTRATOS DE REVISION SON UNOS SOLOS, Y VIVEN MAS ABAJO.

    Hasta había aquí una copia para Festival —`ObservacionDeCampoDto`,
    `RevisionDeCamposDto` y sus dos peticiones— idéntica a la genérica salvo en dos nombres:
    `perfilVersionadoId` en vez de `subregistroId`, y `festivalId` en vez de `moduloId` +
    `registroId`. Eran el mismo contrato escrito dos veces, con el riesgo de siempre: que una
    cambiara y la otra no. Festival usa ahora los genéricos, como Mercados.
*/

/// <summary>Un campo que cambió entre dos envíos institucionales consecutivos.</summary>
public sealed record CambioEntreEnviosFestivalDto(
    string CampoId,
    string SeccionId,
    string CampoEtiqueta,
    string? ValorAnterior,
    string? ValorRecibido,
    bool RespondeAjusteSugerido);

/// <summary>
/// Comparación del envío que está en revisión contra el inmediatamente anterior.
/// </summary>
public sealed record ComparacionEnviosFestivalDto(
    int NumeroEnvio,
    DateTime FechaEnvio,
    int? NumeroEnvioAnterior,
    DateTime? FechaEnvioAnterior,
    bool EsPrimerEnvio,
    bool ComparacionParcial,
    int TotalCampos,
    int CamposModificados,
    int AjustesSugeridosAtendidos,
    int OtrosCambios,
    IReadOnlyList<CambioEntreEnviosFestivalDto> Cambios);

/// <remarks>
/// SE PUEDE DESMARCAR. Marcar por error un punto como resuelto es facil y frecuente; sin la vuelta
/// atras, la unica salida seria pedirle al funcionario que reescriba la nota.
/// </remarks>
public sealed class AtenderCambioSolicitud
{
    public bool Atendida { get; set; } = true;
}

public sealed record RevisionEdicionDeCamposDto(long Id, string EdicionId, string Estado, string? ObservacionGeneral, DateTime? FechaEnvio, IReadOnlyList<ObservacionEdicionDeCampoDto> Observaciones);
public sealed record ObservacionEdicionDeCampoDto(long Id, string SeccionId, string CampoId, string CampoEtiqueta, string? ValorObservado, string Nota, string Estado, DateTime? FechaAtencion);

// ═══════════════════════════════════════════════════════════════════════════════════════════
// LA MISMA REVISION, PARA CUALQUIER PROCESO DEL ECOSISTEMA
//
// Los tipos de arriba nombran el Festival en su forma —`FestivalId`, `PerfilVersionadoId`—. Estos
// identifican el registro por módulo + identificador, que es lo que permite que Mercados, y después
// Escuelas o Escenarios, usen la misma maquinaria sin un juego de contratos por proceso.
// ═══════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Un cambio pedido sobre un campo de la ficha de un registro.
/// </summary>
/// <remarks>
/// ES EL MISMO TIPO EN LOS DOS SENTIDOS, como en Festival: quien pide el cambio y quien lo atiende
/// miran exactamente lo mismo —el campo, su rótulo, lo que decía y la nota—. Con dos formas
/// distintas, la segunda se olvida.
/// </remarks>
public sealed record ObservacionDeCampoDeRegistroDto(
    long Id,
    string Ambito,
    string? SubregistroId,
    string SeccionId,
    string CampoId,
    string CampoEtiqueta,
    string? ValorObservado,
    string Nota,
    string Estado,
    DateTime? FechaAtencion);

/// <summary>
/// El borrador de revisión de un registro: la observación general y las notas por campo.
/// </summary>
/// <remarks>
/// <c>Id</c> en cero y <c>Estado</c> en <c>borrador</c> es la respuesta cuando el registro todavía
/// no tiene ninguna revisión abierta. Se devuelve una carcasa vacía y no un 404 porque la pantalla
/// se pinta igual en los dos casos, y un 404 la obligaría a distinguir «no hay revisión» de «el
/// registro no existe».
/// </remarks>
public sealed record RevisionDeCamposDeRegistroDto(
    long Id,
    string ModuloId,
    string RegistroId,
    string RegistroNombre,
    string Estado,
    string? ObservacionGeneral,
    string? RevisorNombre,
    string? DestinatarioNombre,
    string? OrganizacionNombre,
    DateTime? FechaActualizacion,
    DateTime? FechaEnvio,
    IReadOnlyList<ObservacionDeCampoDeRegistroDto> Observaciones);

/// <summary>Una nota tal como llega del formulario. Sin <c>Id</c>: el servidor casa por campo.</summary>
public sealed class ObservacionDeCampoDeRegistroSolicitud
{
    public string? Ambito { get; set; }
    public string? SubregistroId { get; set; }
    public string? SeccionId { get; set; }
    public string? CampoId { get; set; }
    public string? CampoEtiqueta { get; set; }
    public string? ValorObservado { get; set; }
    public string? Nota { get; set; }
}

/// <summary>
/// El borrador entero, en una sola petición.
/// </summary>
/// <remarks>
/// SEMANTICA DE REEMPLAZO, igual que en Festival: lo que llega es la lista completa y lo que no
/// venga se borra. Una ruta por nota dejaría al cliente llevando la cuenta de qué mandó y qué no.
/// </remarks>
public sealed class GuardarRevisionDeCamposDeRegistroSolicitud
{
    public string? ObservacionGeneral { get; set; }

    /// <remarks>
    /// ANULABLE A PROPOSITO: un cuerpo con `"observaciones": null` deserializa a nulo por mucho que
    /// la propiedad nazca con lista vacía, y entonces el recorrido revienta en vez de contestar 400.
    /// </remarks>
    public List<ObservacionDeCampoDeRegistroSolicitud>? Observaciones { get; set; }
}
