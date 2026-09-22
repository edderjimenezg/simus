using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La misma aplicación sirve el front y el API, en un solo origen.
/// </summary>
/// <remarks>
/// <para>
/// POR QUÉ UN SOLO ORIGEN Y NO DOS SERVICIOS. Las tres cookies de sesión son <c>SameSite=Lax</c> y
/// su dominio no se configura en ninguna parte —los valores están literales en <c>Program.cs</c>—,
/// y <c>environment.apiBaseUrl</c> del front está vacío en sus dos entornos: el navegador pide
/// <c>/api/v1/...</c> a su propio origen. Con el sitio en un dominio y el API en otro, iniciar
/// sesión respondería 200 y cada guardado 401. Ese fallo ya ocurrió en desarrollo y está escrito
/// en <c>environment.ts</c>.
/// </para>
/// <para>
/// LA PRUEBA QUE IMPORTA ES LA TERCERA. Un reparto de rutas mal puesto hace que
/// <c>/api/v1/lo-que-sea</c> devuelva la PÁGINA del front con código 200 en vez de un 404: el
/// cliente recibe HTML donde espera JSON, y cada ruta equivocada se ve como una pantalla en blanco
/// en vez de como el error que es. Es un defecto que no se nota mirando el sitio.
/// </para>
/// <para>
/// EL DIRECTORIO WEB SE FABRICA AQUÍ. En el árbol de fuentes no hay <c>wwwroot</c>: lo llena el
/// pipeline al publicar, copiando <c>dist/pnmc-web/browser</c>. La prueba monta uno de mentira con
/// un <c>index.html</c> reconocible, que es lo que permite distinguir «me devolvió la página» de
/// «me devolvió otra cosa».
/// </para>
/// </remarks>
public sealed class ServidorDelFrontTests : IClassFixture<TestWebApplicationFactory>, IDisposable
{
    private const string MarcaDeLaPagina = "PNMC-PAGINA-DEL-FRONT";

    private readonly string _raizWeb;
    private readonly WebApplicationFactory<Program> _fabrica;

    public ServidorDelFrontTests(TestWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _raizWeb = Path.Combine(Path.GetTempPath(), $"pnmc-wwwroot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_raizWeb);
        File.WriteAllText(
            Path.Combine(_raizWeb, "index.html"),
            $"<!doctype html><html lang=\"es\"><head><title>{MarcaDeLaPagina}</title></head><body></body></html>");

        Directory.CreateDirectory(Path.Combine(_raizWeb, "assets"));
        File.WriteAllText(Path.Combine(_raizWeb, "assets", "marca.txt"), "un fichero estatico cualquiera");

        // `WithWebHostBuilder` en vez de heredar: la fábrica base es sellada, y además así esta
        // clase se queda con su propia base de datos temporal sin tocar la del resto de la suite.
        _fabrica = factory.WithWebHostBuilder(builder => builder.UseWebRoot(_raizWeb));
    }

    [Fact]
    public async Task La_Raiz_Devuelve_La_Pagina_Del_Front()
    {
        var cliente = _fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(MarcaDeLaPagina, await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_Front_Se_Sirve_Sin_Sesion()
    {
        // `FallbackPolicy` está cerrada por omisión: sin `AllowAnonymous` en el reparto, el sitio
        // pediría sesión para pintar su propia pantalla de entrada, y nadie podría entrar nunca.
        var cliente = _fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/", UriKind.Relative));

        Assert.NotEqual(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_Ruta_Inexistente_Del_Api_Devuelve_404_Y_No_La_Pagina()
    {
        var cliente = _fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync(new Uri("/api/v1/ruta-que-no-existe", UriKind.Relative));
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        // Las dos comprobaciones hacen falta: un 404 que devolviera la página seguiría siendo un
        // 404, y el cliente seguiría recibiendo HTML donde espera JSON.
        Assert.DoesNotContain(MarcaDeLaPagina, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_Ruta_Del_Enrutador_Del_Front_Devuelve_La_Pagina()
    {
        var cliente = _fabrica.CreateClient();

        // `/ecosistema/mi-panel` no es un fichero. Sin el reparto, recargar esa pantalla —o abrirla
        // desde un enlace pegado— daría 404 y el sitio parecería roto justo al compartirlo.
        using var respuesta = await cliente.GetAsync(new Uri("/ecosistema/mi-panel", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(MarcaDeLaPagina, await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_Fichero_Estatico_Se_Sirve_Tal_Cual()
    {
        var cliente = _fabrica.CreateClient();

        // Si esto devolviera el `index.html`, sería el reparto trabajando y no los ficheros
        // estáticos: el sitio cargaría la página en lugar de cada script y cada imagen.
        using var respuesta = await cliente.GetAsync(new Uri("/assets/marca.txt", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("un fichero estatico cualquiera", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Las_Sondas_De_Salud_Siguen_Respondiendo()
    {
        // Van montadas sobre `app` y no bajo `/api/v1`, así que no las cubre ninguna de las dos
        // reglas de reparto. Se comprueba porque son las que mira el App Service.
        var cliente = _fabrica.CreateClient();

        using var viva = await cliente.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, viva.StatusCode);
    }

    public void Dispose()
    {
        _fabrica.Dispose();
        try
        {
            Directory.Delete(_raizWeb, recursive: true);
        }
        catch (IOException)
        {
            // El directorio es temporal y del sistema; si algo lo tiene abierto, se queda ahí.
        }
    }
}
