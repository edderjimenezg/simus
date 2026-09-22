using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Todo guion de <c>schema/</c> que cree un índice <b>filtrado</b> declara él mismo
/// <c>SET QUOTED_IDENTIFIER ON</c>.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. SQL Server rechaza un <c>CREATE INDEX ... WHERE</c> —<c>Msg 1934</c>— si la
/// sesión no tiene <c>QUOTED_IDENTIFIER</c> en ON. Y esa opción <b>la trae quien ejecuta, no el
/// guion</b>, de modo que el mismo fichero funciona o falla según el camino:
/// </para>
/// <list type="bullet">
///   <item><description>
///     Las conexiones de <c>Microsoft.Data.SqlClient</c> la traen en ON: por ahí pasan el arranque
///     del API, el migrador y <see cref="ParidadEsquemaSinArranqueTests"/>. Ninguno se quejó nunca.
///   </description></item>
///   <item><description>
///     <c>scripts/seed-local-db.sh</c> —la forma real de construir la base local— canaliza cada
///     fichero hacia <c>sqlcmd ... -b</c> <b>sin <c>-I</c></b>, donde está en OFF. El 24 de agosto
///     de 2026 se añadieron tres índices filtrados a <c>V20260823_02</c> y la siembra local pasó a
///     abortar en ese fichero, llevándose por delante todo lo que venía detrás.
///   </description></item>
/// </list>
/// <para>
/// LO QUE ESTA PRUEBA COMPRUEBA, Y LO QUE NO. Comprueba una <b>propiedad del texto</b> del guion:
/// que declare la opción si la necesita. NO ejecuta nada ni reproduce el fallo. Se intentó lo
/// segundo y no se puede desde aquí: <c>SqlClient</c> restablece <c>QUOTED_IDENTIFIER</c> por su
/// cuenta, así que una prueba que apague la opción y aplique los guiones <b>pasa igual con el
/// defecto dentro</b> —se comprobó con el mutante—. Reproducir el camino real exigiría invocar
/// <c>sqlcmd</c> dentro del contenedor, atando la prueba a rutas de una imagen concreta.
/// </para>
/// <para>
/// Es, otra vez, el patrón que este proyecto persigue: <b>el arnés no puede reproducir el camino
/// que falla</b>. Cuando eso pasa, la salida honesta no es una prueba de comportamiento que en
/// realidad no comprueba el comportamiento, sino una prueba de la propiedad que sí se puede
/// garantizar, <b>diciendo en su nombre y en su comentario exactamente hasta dónde llega</b>.
/// </para>
/// <para>
/// Mutante demostrado (24 ago 2026): quitando el <c>SET QUOTED_IDENTIFIER ON;</c> de
/// <c>V20260823_02</c>, esta prueba falla nombrando el fichero y los índices que lo obligan.
/// </para>
/// </remarks>
public sealed class GuionesIndependientesDeLaSesionTests
{
    /// <summary>
    /// Un <c>CREATE INDEX</c> cuyo <c>WHERE</c> es suyo.
    /// </summary>
    /// <remarks>
    /// El <c>[^;]*?</c> no es cosmético: ata el <c>WHERE</c> a la MISMA sentencia. La primera
    /// versión buscaba un <c>WHERE</c> cualquiera dentro de los 400 caracteres siguientes y
    /// señalaba media carpeta, porque estos guiones envuelven cada índice en un
    /// <c>IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ...)</c> — un <c>WHERE</c> que no
    /// tiene nada que ver. Un punto y coma por medio significa otra sentencia, y ahí se corta.
    /// </remarks>
    private static readonly Regex IndiceFiltrado = new(
        @"CREATE\s+(?:UNIQUE\s+)?(?:CLUSTERED\s+|NONCLUSTERED\s+)?INDEX[^;]*?\bWHERE\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DeclaraOpcion = new(
        @"^\s*SET\s+QUOTED_IDENTIFIER\s+ON\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Todo_Guion_Con_Indice_Filtrado_Declara_Quoted_Identifier()
    {
        var directorio = LocalizarEsquema();
        var guiones = Directory.GetFiles(directorio, "*.sql")
            .OrderBy(ruta => Path.GetFileName(ruta), StringComparer.Ordinal)
            .ToList();

        // Sin esto, un cambio de ruta dejaria la prueba en verde por vacio.
        Assert.True(guiones.Count >= 10, $"Solo se encontraron {guiones.Count} guiones en {directorio}.");

        var culpables = new List<string>();
        var conFiltrado = 0;

        foreach (var guion in guiones)
        {
            var texto = File.ReadAllText(guion);
            if (!IndiceFiltrado.IsMatch(texto))
            {
                continue;
            }

            conFiltrado++;
            if (!DeclaraOpcion.IsMatch(texto))
            {
                culpables.Add(Path.GetFileName(guion));
            }
        }

        // Linea base honesta: si manana nadie usara indices filtrados, esta prueba dejaria de
        // medir algo y hay que enterarse, no seguir viendola verde.
        Assert.True(conFiltrado > 0,
            "Ningun guion de schema/ crea indices filtrados: esta prueba ya no vigila nada y sobra.");

        Assert.True(
            culpables.Count == 0,
            "Estos guiones crean indices filtrados y NO declaran `SET QUOTED_IDENTIFIER ON;`, asi que "
            + "fallan con Msg 1934 cuando los aplica scripts/seed-local-db.sh (sqlcmd sin -I):\n  "
            + string.Join("\n  ", culpables));
    }

    /// <summary>
    /// Ver <see cref="RutasDelRepositorio"/>: no se sube desde el binario, porque con la salida
    /// redirigida a un temporal no hay repositorio encima y la prueba no podria pasar nunca.
    /// </summary>
    private static string LocalizarEsquema() => RutasDelRepositorio.Esquema();

}
