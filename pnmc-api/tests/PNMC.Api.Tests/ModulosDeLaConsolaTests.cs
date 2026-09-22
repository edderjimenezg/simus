using System.Text.RegularExpressions;
using PNMC.Api.Security;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El catálogo de módulos del servidor y la barra izquierda describen lo mismo.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE SE LEE UN FICHERO DE TYPESCRIPT DESDE UNA PRUEBA DE C#.</b> Los permisos se conceden
/// por módulo, y el módulo es el que la persona ve en la barra izquierda. Si el servidor conoce
/// dieciséis y la barra dibuja diecisiete, el módulo de más queda sin permiso que lo gobierne: se
/// verá siempre o no se verá nunca, y nadie se enterará hasta que alguien lo eche en falta. Es el
/// mismo defecto que el proyecto ya tiene nombrado —«un contrato declarado que el servidor no
/// aplica»— y la única forma de cerrarlo es que una de las dos listas vigile a la otra.
/// </para>
/// <para>
/// <b>SE LEE LA LISTA Y NO SE DUPLICA.</b> Copiar aquí los dieciséis códigos sería una tercera
/// lista, y entonces habría tres cosas que mantener en vez de dos.
/// </para>
/// </remarks>
public sealed class ModulosDeLaConsolaTests
{
    /// <summary>Los `id` de sección de la navegación, tal como los declara el frontend.</summary>
    /// <remarks>
    /// SE DESCARTAN LOS IDENTIFICADORES DE GRUPO —bandeja, ecosistema, publicaciones, sitio,
    /// gobierno—, que nombran el encabezado de un bloque de la barra y no una pantalla. «ecosistema»
    /// es las dos cosas a la vez: encabeza su grupo y es además una sección, así que se conserva.
    /// Lo que los separa es que una sección declara `ruta`, y un grupo no.
    /// </remarks>
    private static List<string> SeccionesDeLaBarra()
    {
        var fichero = LocalizarNavegacion();
        var texto = File.ReadAllText(fichero);

        // Una sección es un `id: '...'` seguido, en la línea siguiente, de su `ruta`.
        var coincidencias = Regex.Matches(texto, @"id:\s*'(?<id>[a-z0-9-]+)',\s*\r?\n\s*ruta:\s*'");
        return coincidencias.Select(m => m.Groups["id"].Value).ToList();
    }

    private static string LocalizarNavegacion()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var candidato = Path.Combine(
                actual.FullName, "pnmc-web", "src", "app", "features", "admin", "domain",
                "navegacion-administrativa.ts");
            if (File.Exists(candidato)) return candidato;
            actual = actual.Parent;
        }

        // SI NO SE ENCUENTRA, SE FALLA. Una prueba que no puede mirar no puede decir que está bien:
        // es la misma lección que el trinquete aprendió al salir en verde sin barrer un solo fichero.
        throw new FileNotFoundException(
            "No se encontró navegacion-administrativa.ts. Esta prueba compara el catálogo de módulos "
            + "del servidor con la barra izquierda, y sin el fichero no puede comparar nada.");
    }

    [Fact]
    public void El_catalogo_del_servidor_tiene_exactamente_las_secciones_de_la_barra()
    {
        var barra = SeccionesDeLaBarra();

        Assert.NotEmpty(barra);
        Assert.Equal(barra.OrderBy(x => x, StringComparer.Ordinal), ModulosDeLaConsola.Todos.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void El_orden_del_catalogo_es_el_de_la_barra()
    {
        // Para que la pantalla que concede permisos pueda listarlos en el mismo orden en que la
        // persona los ve, y no obligue a buscar cada módulo dos veces.
        Assert.Equal(SeccionesDeLaBarra(), ModulosDeLaConsola.Todos);
    }

    [Fact]
    public void Los_dos_de_siempre_estan_en_el_catalogo_y_son_los_que_se_declararon()
    {
        Assert.Equal(["monitor", "solicitudes"], ModulosDeLaConsola.SiempreActivados);
        Assert.All(ModulosDeLaConsola.SiempreActivados, m => Assert.Contains(m, ModulosDeLaConsola.Todos));
        Assert.True(ModulosDeLaConsola.EsSiempreActivado("monitor"));
        Assert.False(ModulosDeLaConsola.EsSiempreActivado("catalogo-editorial"));
    }

    [Theory]
    [InlineData("catalogo-editorial", true)]
    [InlineData("  agenda  ", true)]
    [InlineData("bandeja", false)]      // es un grupo de la barra, no una sección
    [InlineData("publicaciones", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Existe_reconoce_solo_los_modulos_reales(string? codigo, bool esperado)
    {
        Assert.Equal(esperado, ModulosDeLaConsola.Existe(codigo));
    }
}
