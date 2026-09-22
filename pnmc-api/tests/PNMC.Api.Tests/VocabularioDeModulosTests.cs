using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PNMC.Domain.Entities;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El módulo de un registro se nombra en un solo sitio.
/// </summary>
/// <remarks>
/// <para>
/// LA REGLA QUE TRAJO ESTA PRUEBA. La auditoría encontró que el
/// mismo registro se nombraba de cuatro maneras: diecisiete tablas guardaban el módulo en una
/// columna <c>ModuloId</c> y cinco en una <c>Dominio</c>; unas decían <c>festivales</c> y otras
/// <c>festival</c>; y convivían dos separadores. La consecuencia estaba medida: el cruce entre la
/// procedencia de un registro y su historial de revisión devolvía CERO filas sobre 1 050 de
/// procedencia y 38 de historial.
/// </para>
/// <para>
/// LA CAUSA NO ERA UN DESCUIDO, ERA LA FORMA. No existía ningún sitio donde el vocabulario
/// estuviera declarado: eran 152 cadenas sueltas repartidas por el código, y diecisiete constantes
/// privadas donde cada fichero escribía la suya. Nada obligaba a que dos coincidieran, así que no
/// coincidían. Arreglar los valores sin arreglar eso habría durado hasta el siguiente punto de
/// entrada.
/// </para>
/// <para>
/// POR ESO ESTA PRUEBA MIRA EL CODIGO Y NO LOS DATOS. Comprobar que hoy los valores cuadran deja
/// intacto el problema real, que no son los valores de hoy sino el que escriba alguien mañana. Lo
/// que se fija aquí es que el módulo SOLO se pueda nombrar desde <see cref="Modulos"/>.
/// </para>
/// </remarks>
public sealed class VocabularioDeModulosTests
{
    /// <summary>Los valores que solo pueden salir de <see cref="Modulos"/>.</summary>
    private static readonly string[] Reservados = Modulos.Todos;

    /// <summary>
    /// Las formas viejas, que no deben reaparecer en ninguna parte.
    /// </summary>
    /// <remarks>
    /// Se listan aparte porque el daño de estas es distinto: un valor de la lista de arriba escrito
    /// a mano solo duplica; uno de esta rompe, porque ya no existe en la base.
    /// </remarks>
    private static readonly string[] Retirados =
    [
        "festival", "organizacion", "noticia", "mercado", "editorial",
        "edicion_festival", "ediciones_festival", "versiones_festival",
        "edicion_mercado", "ediciones_mercado",
    ];

    /// <summary>
    /// Lo que puede seguir nombrando esos textos, y por qué.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO TODO TEXTO IGUAL A UN MODULO ES UN MODULO</b>, y prohibirlos a ciegas convertiría esta
    /// prueba en un estorbo que alguien acabaría desactivando. Las excepciones son:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>Modulos.cs</c>, que es donde el vocabulario se declara.
    ///   </description></item>
    ///   <item><description>
    ///     <c>ModulosDeLaConsola.cs</c> y <c>ModuloDeUnAviso.cs</c>: los módulos de la barra
    ///     izquierda son la unidad de permiso, y sus identificadores son los de la pantalla. Cinco
    ///     valores coinciden con los de aquí a propósito.
    ///   </description></item>
    ///   <item><description>
    ///     <c>TiposDeEntidad.cs</c>, <c>AdministracionDeOrganizacion.cs</c>,
    ///     <c>AdminOrganizacionesEndpoints.cs</c>: ahí <c>organizacion</c> es el TIPO de una
    ///     entidad, no un módulo.
    ///   </description></item>
    ///   <item><description>
    ///     <c>EventosDeOrganizacionEndpoints.cs</c>: ahí <c>festival</c> es un valor del contrato
    ///     que el frontend lee para saber qué clase de proceso enmarca un evento.
    ///   </description></item>
    ///   <item><description>
    ///     <c>CategoriasDeContenidoEndpoints.cs</c>: las categorías tienen su propio ámbito, con un
    ///     valor <c>comun</c> que no es ningún módulo.
    ///   </description></item>
    /// </list>
    /// </remarks>
    private static readonly string[] Exentos =
    [
        "Modulos.cs",
        "ModulosDeLaConsola.cs",
        "ModuloDeUnAviso.cs",
        "TiposDeEntidad.cs",
        "AdministracionDeOrganizacion.cs",
        "AdminOrganizacionesEndpoints.cs",
        "EventosDeOrganizacionEndpoints.cs",
        "CategoriasDeContenidoEndpoints.cs",
        "ImportacionAsistida",
    ];

    [Fact]
    public void Ninguna_constante_declara_un_modulo_por_su_cuenta()
    {
        var infractores = new List<string>();
        var declaracion = new Regex(
            @"const\s+string\s+\w+\s*=\s*""(" + string.Join('|', Reservados.Concat(Retirados).Select(Regex.Escape)) + @")""",
            RegexOptions.Compiled);

        foreach (var (nombre, contenido) in LeerFuentes())
        {
            foreach (Match m in declaracion.Matches(contenido))
            {
                infractores.Add($"{nombre}: const = \"{m.Groups[1].Value}\"");
            }
        }

        Assert.True(
            infractores.Count == 0,
            "Estas constantes declaran un módulo por su cuenta en vez de tomarlo de `Modulos`. Es "
            + "así como el vocabulario se partió en cuatro:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ninguna_consulta_escribe_el_modulo_como_texto_suelto()
    {
        var infractores = new List<string>();
        var enContexto = new Regex(
            @"\b(ModuloId|Dominio)\s*(?:==|=)\s*""([a-z_-]+)""",
            RegexOptions.Compiled);
        var admitidos = new HashSet<string>(Reservados.Concat(Retirados), StringComparer.Ordinal);

        foreach (var (nombre, contenido) in LeerFuentes())
        {
            foreach (Match m in enContexto.Matches(contenido))
            {
                if (admitidos.Contains(m.Groups[2].Value))
                {
                    infractores.Add($"{nombre}: {m.Groups[1].Value} = \"{m.Groups[2].Value}\"");
                }
            }
        }

        Assert.True(
            infractores.Count == 0,
            "Estas consultas nombran el módulo con una cadena en vez de con `Modulos`:\n  "
            + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Las_formas_retiradas_no_reaparecen_en_ninguna_parte()
    {
        var infractores = new List<string>();
        // Solo las que no son también palabras corrientes del dominio: `festival`, `mercado`,
        // `noticia` y `organizacion` aparecen en textos y comentarios sin ser el módulo.
        var soloTecnicas = new[] { "edicion_festival", "ediciones_festival", "versiones_festival", "edicion_mercado", "ediciones_mercado" };

        foreach (var (nombre, contenido) in LeerFuentes())
        {
            foreach (var viejo in soloTecnicas)
            {
                if (contenido.Contains('"' + viejo + '"', StringComparison.Ordinal))
                {
                    infractores.Add($"{nombre}: \"{viejo}\"");
                }
            }
        }

        Assert.True(
            infractores.Count == 0,
            "Estas formas del vocabulario viejo ya no existen en la base y reaparecen en el código. "
            + "Una consulta que las use no encuentra nada y no falla:\n  "
            + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// El control positivo: sin esto, los tres asertos de ausencia pasarían con el barrido roto.
    /// </summary>
    [Fact]
    public void El_barrido_lee_las_fuentes_de_verdad()
    {
        var fuentes = LeerFuentes().ToList();

        Assert.True(fuentes.Count >= 100, $"Solo se leyeron {fuentes.Count} ficheros y el API tiene más de cien.");
        Assert.Contains(fuentes, f => f.Nombre.EndsWith("FestivalesExternosEndpoints.cs", StringComparison.Ordinal));
        Assert.Contains(fuentes, f => f.Contenido.Contains("Modulos.Festivales", StringComparison.Ordinal));
    }

    // ---------- Andamio ----------------------------------------------------------

    /// <summary>
    /// Lee los fuentes del API y de la infraestructura, sin comentarios y sin los exentos.
    /// </summary>
    /// <remarks>
    /// SE QUITAN LOS COMENTARIOS a propósito: los guiones y las clases de este repositorio explican
    /// con nombre y apellidos qué vocabulario se retiró y por qué, y una regla que castigue esa
    /// memoria acaba borrándola. Lo que se prohíbe es escribirlo, no contarlo.
    /// </remarks>
    private static IEnumerable<(string Nombre, string Contenido)> LeerFuentes([CallerFilePath] string origen = "")
    {
        var raiz = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(origen)!, "..", "..", "src"));
        Assert.True(Directory.Exists(raiz), $"No se encontró {raiz}.");

        foreach (var ruta in Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories).OrderBy(r => r, StringComparer.Ordinal))
        {
            if (ruta.Contains("/obj/", StringComparison.Ordinal) || ruta.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (Exentos.Any(e => ruta.Contains(e, StringComparison.Ordinal))) continue;
            yield return (Path.GetFileName(ruta), SinComentarios(File.ReadAllText(ruta)));
        }
    }

    private static string SinComentarios(string fuente)
    {
        var sinBloques = Regex.Replace(fuente, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(sinBloques, "//[^\r\n]*", " ");
    }
}
