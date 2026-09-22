namespace PNMC.Api.Endpoints;

/// <summary>
/// Como se lee cada verbo de la bitácora de auditoría.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE ESTO ES UN FICHERO PROPIO Y NO UNA AYUDA DENTRO DE UN ENDPOINT.</b> El DTO de
/// auditoría se construye en DOS sitios —la bitácora general y la ficha de una organización—, y
/// los dos tienen que nombrar los verbos igual. Un diccionario por endpoint son dos traducciones
/// del mismo verbo, y la segunda envejece sola.
/// </para>
/// <para>
/// <b>LAS TRECE SON LAS QUE LA BASE ADMITE, no una selección.</b>
/// <c>CK_BitacoraAuditoria_Accion</c> cierra la columna a esta lista exacta, de modo que aquí no
/// puede faltar ninguna ni sobrar una que nunca llegará.
/// <c>Las_Etiquetas_De_Accion_Cubren_La_Lista_Blanca_Del_Esquema</c> lo contrasta con el guion de
/// esquema para que las dos listas no se separen con el tiempo.
/// </para>
/// <para>
/// <b>QUE DEFECTO CORRIGE.</b> Dos pantallas —el Resumen de la consola y el historial de un
/// Festival— ya pedían <c>accionEtiqueta</c> y ninguna lo recibía nunca, porque el servidor no lo
/// enviaba. Caían al verbo crudo y enseñaban «iniciar_sesion» a quien administra.
/// </para>
/// </remarks>
public static class VerbosDeBitacora
{
    private static readonly Dictionary<string, string> EtiquetaPorAccion =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["crear"] = "Creó",
            ["actualizar"] = "Actualizó",
            ["eliminar"] = "Eliminó",
            ["publicar"] = "Publicó",
            ["archivar"] = "Archivó",
            ["aprobar"] = "Aprobó",
            ["rechazar"] = "Rechazó",
            ["iniciar_sesion"] = "Inició sesión",
            ["cerrar_sesion"] = "Cerró sesión",
            ["iniciar_sesion_externa"] = "Inició sesión externa",
            ["cerrar_sesion_externa"] = "Cerró sesión externa",
            ["crear_organizacion"] = "Creó una organización",
            ["asignar_administrador_inicial"] = "Asignó al administrador inicial",
        };

    /// <summary>Los verbos que se saben nombrar. La prueba los contrasta con el esquema.</summary>
    public static IReadOnlyCollection<string> Conocidos => EtiquetaPorAccion.Keys;

    /// <summary>
    /// El verbo en palabras.
    /// </summary>
    /// <remarks>
    /// Un verbo desconocido SE DEVUELVE TAL CUAL. Es peor que traducirlo y mucho mejor que ocultar
    /// que algo ocurrió: la bitácora existe justamente para que nada se pierda.
    /// </remarks>
    public static string Etiquetar(string? accion) =>
        EtiquetaPorAccion.TryGetValue(accion ?? string.Empty, out var etiqueta)
            ? etiqueta
            : (string.IsNullOrWhiteSpace(accion) ? "Actividad administrativa" : accion);
}
