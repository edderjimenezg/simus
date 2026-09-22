using System.Text.Json;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Qué pasó de verdad en una línea de la bitácora, más allá del verbo técnico.
/// </summary>
/// <remarks>
/// <para>
/// <b>«ACTUALIZO» CINCO VECES NO ES UN HISTORIAL.</b> El 17 de septiembre de 2026, al recorrer el
/// flujo de Mercados, el historial de un mercado en la consola enseñaba cinco líneas idénticas
/// —«Actualizó», «Actualizó», «Actualizó»…— que solo se distinguían por la hora. Quien lo abre
/// quiere saber QUE cambió, y el verbo técnico no lo dice.
/// </para>
/// <para>
/// <b>EL DATO YA ESTABA GUARDADO.</b> <c>CK_BitacoraAuditoria_Accion</c> solo admite trece verbos,
/// así que los circuitos escriben el verbo admitido en <c>Accion</c> y el evento funcional —
/// <c>MercadoPublicado</c>, <c>FestivalPropuestaRechazada</c>— dentro de <c>ValoresNuevos</c>. Esta
/// clase lo saca de ahí y lo nombra; la fila no cambia.
/// </para>
/// <para>
/// <b>SI NO SE RECONOCE, NO SE INVENTA.</b> Un evento sin traducir devuelve <c>null</c> y la línea
/// se queda con su verbo, que es lo que enseñaba hasta hoy. Nunca se enseña el nombre técnico en
/// crudo: «MercadoConAjustesSolicitados» en una pantalla es peor que «Actualizó».
/// </para>
/// </remarks>
public static class EventosDeLaBitacora
{
    private static readonly Dictionary<string, string> EtiquetaPorEvento =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Mercados
            ["MercadoCreado"] = "Registró el mercado",
            ["MercadoEnviadoARevision"] = "Envió el mercado a revisión",
            ["MercadoPublicado"] = "Publicó el mercado",
            ["MercadoAprobado"] = "Aprobó el mercado",
            ["MercadoArchivado"] = "Archivó el mercado",
            ["MercadoConAjustesSolicitados"] = "Pidió ajustes en el mercado",
            ["MercadoCambioPropuesto"] = "Propuso un cambio",
            ["MercadoPropuestaAplicada"] = "Aplicó la propuesta de cambio",
            ["MercadoPropuestaRechazada"] = "Rechazó la propuesta de cambio",
            ["MercadoPropuestaConAjustes"] = "Pidió ajustes en la propuesta",
            ["EdicionDeMercadoCreada"] = "Registró una edición",
            ["EdicionDeMercadoActualizada"] = "Actualizó una edición",
            ["MercadoDecidido"] = "Tomó una decisión sobre el mercado",

            // Festivales
            ["FestivalCreado"] = "Registró el Festival",
            ["FestivalEnviadoARevision"] = "Envió el Festival a revisión",
            ["FestivalPublicado"] = "Publicó el Festival",
            ["FestivalAprobado"] = "Aprobó el Festival",
            ["FestivalRechazado"] = "Rechazó el Festival",
            ["FestivalArchivado"] = "Archivó el Festival",
            ["FestivalCambiosPedidosPorCampo"] = "Pidió cambios campo por campo",
            ["FestivalCambioPropuesto"] = "Propuso un cambio",
            ["FestivalCambioPropuestoActualizado"] = "Editó la propuesta de cambio",
            ["FestivalPropuestaEnviadaARevision"] = "Envió la propuesta a revisión",
            ["FestivalPropuestaPublicada"] = "Publicó la propuesta como nueva versión",
            ["FestivalPropuestaRechazada"] = "Rechazó la propuesta de cambio",
            ["FestivalPropuestaAjustesSolicitados"] = "Pidió ajustes en la propuesta",
            ["BorradorInstitucionalFestival"] = "Abrió un borrador institucional",
            ["BorradorInstitucionalActualizado"] = "Editó el borrador institucional",
            ["BorradorInstitucionalEnviadoARevision"] = "Envió el borrador institucional a revisión",
        };

    /// <summary>
    /// Cómo se nombra lo que pasó, leyéndolo de <c>ValoresNuevos</c>.
    /// </summary>
    /// <returns>La etiqueta del evento, o <c>null</c> si la fila no declara uno conocido.</returns>
    public static string? Etiquetar(string? valoresNuevosJson)
    {
        if (string.IsNullOrWhiteSpace(valoresNuevosJson)) return null;

        try
        {
            using var documento = JsonDocument.Parse(valoresNuevosJson);
            if (documento.RootElement.ValueKind != JsonValueKind.Object) return null;

            // LAS DOS FORMAS QUE SE USAN. Mercados escribe `{evento, detalle}` y el circuito de
            // Festival escribe `{…, "Evento": "…"}`: son dos convenciones de mayúscula sobre la
            // misma idea, y aquí se aceptan las dos en vez de obligar a unificarlas en diez sitios.
            foreach (var nombre in (string[])["evento", "Evento"])
            {
                if (documento.RootElement.TryGetProperty(nombre, out var valor)
                    && valor.ValueKind == JsonValueKind.String
                    && valor.GetString() is { Length: > 0 } evento
                    && EtiquetaPorEvento.TryGetValue(evento, out var etiqueta))
                {
                    return etiqueta;
                }
            }
        }
        catch (JsonException)
        {
            // Una fila con un cuerpo que no es JSON no es motivo para no enseñar la línea.
            return null;
        }

        return null;
    }
}
