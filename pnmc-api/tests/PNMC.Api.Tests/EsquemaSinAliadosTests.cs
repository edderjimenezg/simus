using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-065 — Ningún guion de esquema o semilla vuelve a crear el modelo de entidades aliadas.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE ESTA PRUEBA NO LEE LA BASE. La que sí la lee —
/// <c>MuestraSqlServerTests.Una_base_nueva_no_tiene_nada_del_modelo_de_aliados</c>— es mejor
/// prueba y se queda; pero lleva <c>[HechoSqlServer]</c> y se auto-omite si la variable
/// <c>PNMC_PRUEBAS_SQLSERVER</c> no está puesta. El trabajo <c>api</c> de <c>ci.yml</c> no
/// levanta SQL Server ni aplica <c>pnmc-database/schema/</c>, así que en la consola aquella
/// prueba <b>no se ejecuta nunca</b> y la propiedad que fija quedó verificada una sola vez, a
/// mano, el día que se hizo la retirada.
/// </para>
/// <para>
/// Esta lee los guiones como TEXTO. No necesita Docker, corre en cualquier portátil y en CI, y
/// cubre las dos formas de reaparecer que este concepto tenía —las dos reales, las dos medidas
///—:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Por el nombre antiguo.</b> Las tablas no nacían en el fichero que lleva «aliados» en
///     el nombre: nacían en <c>V20260525_01</c> como <c>EntidadesColaboradoras</c> y
///     <c>UsuariosEntidadesColaboradoras</c>, y <c>V20260525_02</c> solo las renombraba con
///     <c>sp_rename</c>. Quien retirase el DDL del segundo las vería reaparecer con el nombre
///     viejo en cada base nueva, sin que ningún guion se quejara.
///   </description></item>
///   <item><description>
///     <b>Por la semilla.</b> El <c>MERGE</c> de roles de <c>seed/V20260519_03</c> corre
///     <i>después</i> de todo <c>schema/</c>, de modo que un <c>DELETE</c> colocado en una
///     migración quedaba deshecho en la misma ejecución: el MERGE no falla, simplemente
///     reinserta.
///   </description></item>
/// </list>
/// <para>
/// La migración de retirada está exenta a propósito: su trabajo es <i>nombrar</i> lo que suelta.
/// </para>
/// </remarks>
public sealed class EsquemaSinAliadosTests
{
    /// <summary>El único guion al que se le permite nombrar el modelo retirado.</summary>
    private const string GuionDeRetirada = "V20260823_01__retirada_aliados.sql";

    /// <summary>
    /// Lo que ningún guion puede volver a crear. Son sentencias de creación, no menciones: un
    /// comentario histórico fechado es correcto y deliberado, y el barrido no debe prohibirlo.
    /// </summary>
    /// <remarks>
    /// <b>SE COMPARA EL NOMBRE ENTERO, NO EL PREFIJO.</b> El 18 de septiembre de 2026 la
    /// consolidación del circuito de Festivales creó <c>EntidadesAliadasDeRegistro</c> —las
    /// organizaciones que acompañan a una edición— y este barrido la señaló como si fuera el modelo
    /// retirado, porque su nombre EMPIEZA igual. Son dos cosas distintas: aquello eran cuentas de
    /// usuario de organizaciones aliadas, con sus tres roles; esto es una relación entre un registro
    /// y una organización. Un guardián que castiga nombres que solo se parecen acaba desactivado,
    /// que es peor que uno preciso.
    /// </remarks>
    private static readonly string[] TablasProhibidas =
    [
        "EntidadesAliadas",
        "UsuariosEntidadesAliadas",
        "SolicitudesAliado",
        "EntidadesColaboradoras",
        "UsuariosEntidadesColaboradoras",
    ];

    /// <summary>
    /// Lo que ningún <c>sp_rename</c> puede producir.
    /// </summary>
    /// <remarks>
    /// <b>ANTES SE PROHIBIA `sp_rename` ENTERO, y era demasiado.</b> El riesgo medido el 22 de
    /// agosto de 2026 no era renombrar: era renombrar <b>hacia</b> el modelo retirado, que es como
    /// las tablas aparecían sin que ningún <c>CREATE TABLE</c> las nombrara. Prohibir el verbo
    /// bloqueaba también renombrados legítimos —el, que generalizó
    /// <c>FilasImportacion.IdFestivalCoincidente</c>— y un guardián que estorba en lo correcto
    /// acaba desactivado, que es peor que uno preciso.
    /// </remarks>
    private static readonly string[] DestinosDeRenombradoProhibidos =
    [
        "EntidadesAliadas",
        "UsuariosEntidadesAliadas",
        "SolicitudesAliado",
        "EntidadesColaboradoras",
        "UsuariosEntidadesColaboradoras",
    ];

    /// <summary>Los tres roles que ninguna semilla puede volver a insertar.</summary>
    private static readonly string[] RolesProhibidos =
    [
        "N'aliado_admin'",
        "N'aliado_editor'",
        "N'aliado_lector'",
    ];

    [Fact]
    public void Ningun_guion_de_esquema_vuelve_a_crear_las_tablas_de_aliados()
    {
        var guiones = LeerGuiones("schema");

        // CANARIO. Un barrido que no lee ningún fichero pasa siempre. Si esto salta, lo roto es
        // la localización del directorio, no el esquema.
        Assert.True(guiones.Count >= 8,
            $"Solo se leyeron {guiones.Count} guiones de schema/ y hay más de ocho. "
            + "La prueba no está mirando lo que dice mirar.");

        var candidatos = guiones
            .Where(guion => !guion.Nombre.Equals(GuionDeRetirada, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var infractores = candidatos
            .SelectMany(guion => TablasProhibidas
                .Where(tabla => CreaLaTabla(guion.Contenido, tabla))
                .Select(tabla => $"{guion.Nombre}: CREATE TABLE dbo.{tabla}"))
            .Concat(candidatos
                // UN RENOMBRADO SOLO ES INFRACCION SI APUNTA AL MODELO RETIRADO. Ver
                // `DestinosDeRenombradoProhibidos` para por qué se dejó de prohibir el verbo entero.
                .Where(guion => guion.Contenido.Contains("sp_rename", StringComparison.OrdinalIgnoreCase))
                .SelectMany(guion => DestinosDeRenombradoProhibidos
                    .Where(destino => NombraExactamente(guion.Contenido, destino))
                    .Select(destino => $"{guion.Nombre}: sp_rename hacia {destino}")))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Estos guiones vuelven a crear el modelo de entidades aliadas, retirado el 22 de agosto "
            + "de 2026:\n  " + string.Join("\n  ", infractores)
            + "\n\nRecuerde que las tablas nacían con el nombre 'Colaboradoras' y solo después se "
            + "renombraban: borrar el DDL del fichero que lleva 'aliados' en el nombre no basta.");
    }

    [Fact]
    public void Ninguna_semilla_vuelve_a_insertar_los_roles_de_aliado()
    {
        var guiones = LeerGuiones("schema").Concat(LeerGuiones("seed")).ToList();
        Assert.True(guiones.Count >= 12, $"Solo se leyeron {guiones.Count} guiones entre schema/ y seed/.");

        var infractores = guiones
            .Where(guion => !guion.Nombre.Equals(GuionDeRetirada, StringComparison.OrdinalIgnoreCase))
            .SelectMany(guion => RolesProhibidos
                .Where(rol => guion.Contenido.Contains(rol, StringComparison.OrdinalIgnoreCase))
                .Select(rol => $"{guion.Nombre}: {rol}"))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Estos guiones vuelven a sembrar roles de aliado:\n  " + string.Join("\n  ", infractores)
            + "\n\nLa semilla de roles corre DESPUÉS de todo schema/, así que reinserta en silencio "
            + "lo que una migración hubiera borrado.");
    }

    /// <summary>
    /// CONTROL POSITIVO de las dos pruebas anteriores.
    /// </summary>
    /// <remarks>
    /// Sin esto, los dos asertos de ausencia pasarían igual si el barrido leyera basura o si los
    /// guiones estuvieran vacíos. Comprueba que lo que se está leyendo son los guiones de verdad:
    /// el catálogo de roles vigente está ahí, y el guion de retirada —que sí nombra lo retirado,
    /// porque su trabajo es soltarlo— también.
    /// </remarks>
    [Fact]
    public void El_barrido_lee_los_guiones_de_verdad()
    {
        var todos = LeerGuiones("schema").Concat(LeerGuiones("seed")).ToList();

        Assert.Contains(todos, guion => guion.Contenido.Contains("N'gestor_interno'", StringComparison.Ordinal));
        Assert.Contains(todos, guion => guion.Contenido.Contains("CREATE TABLE dbo.UsuariosEntidades", StringComparison.Ordinal));

        var retirada = todos.SingleOrDefault(guion => guion.Nombre.Equals(GuionDeRetirada, StringComparison.OrdinalIgnoreCase));
        Assert.True(retirada is not null, $"Falta {GuionDeRetirada}: sin él, una base ya creada conserva las tablas.");
        Assert.Contains("DROP TABLE dbo.EntidadesAliadas", retirada!.Contenido, StringComparison.Ordinal);
    }

    /// <summary>Si el guion crea ESA tabla, y no una cuyo nombre empieza igual.</summary>
    private static bool CreaLaTabla(string sql, string tabla) =>
        Regex.IsMatch(sql, @"CREATE\s+TABLE\s+dbo\.\[?" + Regex.Escape(tabla) + @"\]?\b(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase);

    /// <summary>Si el guion nombra ESE identificador, y no uno que lo lleva dentro.</summary>
    private static bool NombraExactamente(string sql, string nombre) =>
        Regex.IsMatch(sql, @"\b" + Regex.Escape(nombre) + @"\b(?![A-Za-z0-9_])", RegexOptions.IgnoreCase);

    // ---------- Andamio ----------------------------------------------------------

    /// <summary>
    /// Un guion leído dos veces: <paramref name="Contenido"/> es el SQL ejecutable, sin
    /// comentarios, y <paramref name="Literal"/> es el fichero tal cual. Los asertos de
    /// prohibición miran el primero; el control positivo mira el segundo.
    /// </summary>
    private sealed record Guion(string Nombre, string Contenido, string Literal);

    /// <summary>
    /// Localiza <c>pnmc-database/&lt;carpeta&gt;</c> desde la ruta de ESTE fichero fuente.
    /// </summary>
    /// <remarks>
    /// No se parte de <c>AppContext.BaseDirectory</c>: la suite se ejecuta a menudo con la salida
    /// redirigida a un temporal —para no chocar con el API que esté corriendo— y desde allí no hay
    /// ningún repositorio encima. <c>CallerFilePath</c> lo resuelve el compilador.
    /// </remarks>
    private static List<Guion> LeerGuiones(string carpeta, [CallerFilePath] string origen = "")
    {
        var raiz = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(origen)!, "..", "..", "..", "pnmc-database", carpeta));

        Assert.True(Directory.Exists(raiz), $"No se encontró {raiz}.");

        return Directory.GetFiles(raiz, "*.sql")
            .OrderBy(ruta => ruta, StringComparer.Ordinal)
            .Select(ruta => new Guion(
                Path.GetFileName(ruta),
                SinComentarios(File.ReadAllText(ruta)),
                File.ReadAllText(ruta)))
            .ToList();
    }

    /// <summary>
    /// Quita los comentarios de bloque y de línea, dejando solo SQL ejecutable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES LA MITAD DEL VALOR DE ESTA PRUEBA, no un detalle de implementación. Un barrido que
    /// mira el fichero entero prohíbe también <b>hablar</b> de lo retirado, y los guiones de este
    /// repositorio llevan a propósito notas fechadas que explican qué se retiró y por qué —
    /// precisamente para que el siguiente lector no vuelva a introducirlo—. Una regla que
    /// castiga la memoria del proyecto acaba borrándola.
    /// </para>
    /// <para>
    /// La distinción es la misma que se aplicó al barrer el árbol tras la retirada: una mención
    /// histórica en un comentario fechado es correcta; una sentencia que lo crea, no.
    /// </para>
    /// </remarks>
    private static string SinComentarios(string sql)
    {
        var sinBloques = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(sinBloques, "--[^\r\n]*", " ");
    }
}
