using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Dos propiedades de la siembra local: <b>ningún guion nombra su base</b>, y <b>quien los
/// ejecuta enciende <c>QUOTED_IDENTIFIER</c></b>.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE LAS DOS JUNTAS. Son los dos defectos que destapó aplicar de punta a punta los guiones
/// de la migración de SIMUS, y los dos comparten causa: <b>el fichero y
/// quien lo ejecuta no se ponen de acuerdo sobre quién manda</b>. Uno se la quitaba al que ejecuta
/// (<c>USE</c> ganándole al <c>-d</c>); el otro se la dejaba entera (la opción de sesión que el
/// guion no puede declarar por su cuenta).
/// </para>
/// <para>
/// EL PRIMERO: <c>seed/V20260519_07</c> abría con <c>USE [PNMC_LOCAL]; GO</c>, único de los siete.
/// Como <c>USE</c> gana al <c>-d "$DB_NAME"</c>, sembrar cualquier otra base escribía igualmente en
/// PNMC_LOCAL — y lo primero que hace ese fichero es <c>DELETE ... WHERE IdEntidad &gt;= 100</c>
/// sobre cuatro tablas. Ocurrió dos veces con una base de ensayo.
/// </para>
/// <para>
/// EL SEGUNDO: <see cref="GuionesIndependientesDeLaSesionTests"/> ya obliga a declarar
/// <c>SET QUOTED_IDENTIFIER ON</c> a todo guion de <c>schema/</c> que <b>cree</b> un índice
/// filtrado. Resultó insuficiente, y por un motivo que esa prueba no puede ver: SQL Server exige
/// la misma opción para <b>escribir</b> en una tabla que ya tiene uno. El fichero que falla no es
/// el que crea el índice — es cada semilla que inserta detrás, y las semillas no crean índices,
/// así que aquella prueba ni las mira. La corrección tiene que estar en quien ejecuta.
/// </para>
/// <para>
/// LO QUE ESTA PRUEBA COMPRUEBA, Y LO QUE NO. Propiedades del <b>texto</b>, como su hermana. No
/// ejecuta <c>sqlcmd</c> ni reproduce ningún <c>Msg 1934</c>: reproducirlo exigiría invocar
/// <c>sqlcmd</c> dentro del contenedor, y <c>SqlClient</c> —el único cliente que estas pruebas
/// tienen a mano— restablece <c>QUOTED_IDENTIFIER</c> por su cuenta, de modo que una prueba de
/// comportamiento pasaría igual con el defecto dentro. Está medido con el mutante.
/// </para>
/// <para>
/// Mutantes demostrados (24 ago 2026): devolviendo el <c>USE [PNMC_LOCAL];</c> a
/// <c>V20260519_07</c>, falla el primer hecho nombrando el fichero; quitando un <c>-I</c> de
/// <c>seed-local-db.sh</c>, falla el segundo nombrando la línea.
/// </para>
/// </remarks>
public sealed class SiembraSinBaseCableadaTests
{
    /// <summary>
    /// Un <c>USE</c> de verdad: al principio de una línea. En T-SQL <c>USE</c> es una sentencia
    /// suelta —no cabe dentro de un <c>IF</c> ni de una expresión—, así que no hay forma de
    /// escribir uno real que no empiece su línea.
    /// </summary>
    private static readonly Regex SentenciaUse = new(
        @"^\s*USE\s+[\[\w]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Un <c>-I</c> suelto, no el final de otra opción ni parte de una ruta.</summary>
    private static readonly Regex OpcionI = new(
        @"(?<![\w-])-I(?![\w-])", RegexOptions.Compiled);

    [Fact]
    public void Ningun_Guion_De_Esquema_Ni_De_Semilla_Nombra_Su_Base()
    {
        var carpetas = new[] { RutasDelRepositorio.Esquema(), RutasDelRepositorio.Semillas() };
        var culpables = new List<string>();
        var revisados = 0;

        foreach (var carpeta in carpetas)
        {
            foreach (var guion in Directory.GetFiles(carpeta, "*.sql")
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                revisados++;
                if (SentenciaUse.IsMatch(SinComentarios(File.ReadAllText(guion))))
                {
                    culpables.Add(Path.GetFileName(Path.GetDirectoryName(guion)!)
                                  + "/" + Path.GetFileName(guion));
                }
            }
        }

        // Linea base honesta: sin esto, un cambio de ruta dejaria la prueba en verde por vacio.
        Assert.True(revisados >= 15, $"Solo se encontraron {revisados} guiones en schema/ y seed/.");

        Assert.True(
            culpables.Count == 0,
            "Estos guiones cablean su base con `USE`, que gana al `-d` de quien los ejecuta y los "
            + "hace escribir en PNMC_LOCAL venga de donde venga la invocacion:\n  "
            + string.Join("\n  ", culpables));
    }

    [Fact]
    public void La_Siembra_Enciende_Quoted_Identifier_En_Todas_Sus_Invocaciones()
    {
        var guion = Path.Combine(RutasDelRepositorio.Raiz(), "scripts", "seed-local-db.sh");
        Assert.True(File.Exists(guion), $"No se encontro {guion}.");

        var lineas = File.ReadAllLines(guion);
        var sinOpcion = new List<string>();
        var aplican = 0;

        for (var i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i];

            // Las lineas que APLICAN algo son las que eligen base con -d; las de deteccion
            // (un "SELECT 1" contra el servidor) no abren ninguna y no necesitan la opcion.
            if (linea.TrimStart().StartsWith("#", StringComparison.Ordinal)
                || !linea.Contains("-d \"$DB_NAME\"", StringComparison.Ordinal))
            {
                continue;
            }

            aplican++;
            if (!OpcionI.IsMatch(linea))
            {
                sinOpcion.Add($"{i + 1}: {linea.Trim()}");
            }
        }

        // Linea base honesta: si manana el guion dejara de invocar sqlcmd asi, esta prueba
        // pasaria por vacio y hay que enterarse, no seguir viendola verde.
        Assert.True(aplican >= 6,
            $"Solo se encontraron {aplican} invocaciones con `-d \"$DB_NAME\"` en seed-local-db.sh. "
            + "O el guion cambio de forma y esta prueba ya no vigila lo que cree, o falta alguna.");

        Assert.True(
            sinOpcion.Count == 0,
            "Estas invocaciones de sqlcmd aplican ficheros sin `-I`, asi que corren con "
            + "QUOTED_IDENTIFIER en OFF y fallan con Msg 1934 en cuanto un guion crea un indice "
            + "filtrado o una semilla escribe en una tabla que lo tenga:\n  "
            + string.Join("\n  ", sinOpcion));
    }

    /// <summary>
    /// Quita comentarios <c>--</c> y <c>/* */</c> para que la prueba mire sentencias y no prosa.
    /// </summary>
    /// <remarks>
    /// NO ES CELO EXCESIVO: la corrección de <c>V20260519_07</c> dejó en su sitio un comentario que
    /// EXPLICA el defecto y por tanto escribe <c>USE [PNMC_LOCAL]</c> varias veces. Un barrido que
    /// no entienda de comentarios acusa al fichero justo por documentar su arreglo — y la variante
    /// contraria, un barrido que se salta el <c>--</c> pero no el <c>/* */</c>, ya produjo un
    /// diagnóstico falso en este mismo trabajo. Los bloques anidan, como en T-SQL.
    /// </remarks>
    private static string SinComentarios(string sql)
    {
        var salida = new StringBuilder(sql.Length);
        var profundidad = 0;
        var enLinea = false;

        for (var i = 0; i < sql.Length; i++)
        {
            if (enLinea)
            {
                if (sql[i] == '\n')
                {
                    enLinea = false;
                    salida.Append('\n');
                }

                continue;
            }

            if (profundidad > 0)
            {
                if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    profundidad++;
                    i++;
                }
                else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                {
                    profundidad--;
                    i++;
                }
                else if (sql[i] == '\n')
                {
                    salida.Append('\n');
                }

                continue;
            }

            if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                enLinea = true;
                continue;
            }

            if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                profundidad = 1;
                i++;
                continue;
            }

            salida.Append(sql[i]);
        }

        return salida.ToString();
    }
}
