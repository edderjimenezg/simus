using System.Globalization;
using PNMC.Api.Endpoints;

namespace PNMC.Api.ConsultaGuiada;

/// <summary>
/// Sobre qué se calculó una respuesta, dicho en palabras.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO ES DECORACION.</b> Quien pregunta «¿qué está incompleto?» desde dentro de un Festival
/// espera una respuesta sobre ese Festival. Si la consulta elegida solo sabe mirar toda la
/// operación, devolver la cifra global sin decirlo la hace parecer una cifra de ese Festival. El
/// contrato obliga a declararlo, y la pantalla lo enseña junto a la tabla.
/// </para>
/// <para>
/// <b>TAMBIEN CUANDO SI SE ACOTO</b>, por el mismo motivo del revés: una cifra pequeña sin decir de
/// qué es parece un error de la base.
/// </para>
/// </remarks>
public static class AlcanceDeLaRespuesta
{
    /// <summary>Lo que se dice cuando no había contexto que aplicar.</summary>
    public const string TodaLaOperacion = "Toda la operación";

    /// <summary>Lo que se dice cuando la consulta elegida no sabe acotarse a lo que se miraba.</summary>
    public static string NoSeAcota(string contexto) =>
        $"{TodaLaOperacion} · esta consulta no se acota a {contexto}";

    /// <summary>
    /// Lo que la pregunta nombró y la consulta elegida no supo usar.
    /// </summary>
    /// <remarks>
    /// <b>CALLARLO SERIA LO MISMO QUE NO ENTENDERLO, PERO PARECIENDO QUE SI.</b> Quien escribe «en
    /// 2024» y recibe una cifra sin ninguna advertencia concluye que es la de 2024. Reconocer una
    /// entidad y no poder aplicarla es un estado legítimo del sistema; esconderlo no lo es.
    /// </remarks>
    public static string LoQueNoSePudoAplicar(ConsultaDelCatalogo consulta, EntidadesDeLaPregunta entidades)
    {
        var avisos = new List<string>();

        if (entidades.TerritorioAmbiguo is { Length: > 0 } ambiguo)
        {
            avisos.Add($"«{TextoEnEspanol.ATituloDeColombia(ambiguo)}» es el nombre de varios municipios en " +
                "departamentos distintos, así que no acoté la respuesta a ninguno: hace falta decir en cuál.");
        }

        if (entidades.Territorio is not null && !consulta.SeAcotaPorTerritorio)
        {
            avisos.Add($"Esta consulta no se acota a {entidades.Territorio.EnPalabras}.");
        }

        if (entidades.Estado is not null && !consulta.SeAcotaPorEstado)
        {
            // «PUBLICADO» NO SE AVISA CUANDO LA CONSULTA YA SOLO MIRA LO PUBLICADO: la pregunta
            // está contestada, y decir «no se acota al estado Publicado» debajo de una cifra de
            // Festivales publicados sería una advertencia falsa. Cualquier otro estado sí, porque
            // esas consultas no pueden verlo ni aunque quisieran.
            var yaEsta = consulta.SoloMiraLoPublicado
                && string.Equals(entidades.Estado.Codigo, EstadosFestival.Publicado, StringComparison.Ordinal);
            if (!yaEsta)
            {
                avisos.Add(consulta.SoloMiraLoPublicado
                    ? $"Esta consulta solo mira los Festivales publicados, así que no responde por el estado «{entidades.Estado.Nombre}»."
                    : $"Esta consulta no se acota al estado «{entidades.Estado.Nombre}».");
            }
        }

        if (entidades.Anio is int anio && !consulta.SeAcotaPorAnio)
        {
            avisos.Add($"Esta consulta no se acota al año {anio.ToString(CultureInfo.InvariantCulture)}: " +
                "describe la situación actual.");
        }

        return avisos.Count == 0 ? string.Empty : " " + string.Join(" ", avisos);
    }
}
