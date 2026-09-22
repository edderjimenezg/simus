using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// 2F — La lectura publica del CMS contesta 304 cuando nada ha cambiado.
/// </summary>
/// <remarks>
/// <para>
/// Es la ruta mas pedida del API: el front-end la llama al arrancar cualquier
/// pagina para pintar los textos del sitio. Sin peticion condicional, cada visita
/// se baja el diccionario entero otra vez.
/// </para>
/// <para>
/// Lo que estas pruebas vigilan no es el ahorro sino su contrapartida peligrosa:
/// que un ETag demasiado estable haga pasar por «no modificado» un texto que
/// SI cambio. Por eso hay tres, y la tercera —publicar mueve el ETag— es la que
/// de verdad importa.
/// </para>
/// </remarks>
public sealed class ContenidoWebEtagTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContenidoWebEtagTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task La_Lectura_Publica_Devuelve_ETag()
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/v1/contenido-web");

        respuesta.EnsureSuccessStatusCode();
        Assert.NotNull(respuesta.Headers.ETag);
        Assert.Equal("no-cache", respuesta.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Repetir_La_Peticion_Con_El_Mismo_ETag_Devuelve_304_Sin_Cuerpo()
    {
        var cliente = _factory.CreateClient();
        var primera = await cliente.GetAsync("/api/v1/contenido-web");
        var etag = primera.Headers.ETag!;

        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/contenido-web");
        peticion.Headers.IfNoneMatch.Add(etag);
        var segunda = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.NotModified, segunda.StatusCode);
        Assert.Empty(await segunda.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Publicar_Un_Texto_Cambia_El_ETag()
    {
        // La mitad que evita el desastre: si el ETag no se moviera, el portal
        // seguiria sirviendo el texto viejo a todo el que ya lo tuviera en cache,
        // y desde el panel se veria publicado. Un fallo invisible desde dentro.
        var cliente = _factory.CreateClient();
        var antes = (await cliente.GetAsync("/api/v1/contenido-web")).Headers.ETag!.Tag;

        PublicarUnTexto();

        var despues = (await cliente.GetAsync("/api/v1/contenido-web")).Headers.ETag!.Tag;

        Assert.NotEqual(antes, despues);
    }

    private void PublicarUnTexto()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var clave = $"clave_de_prueba_{Guid.NewGuid():N}"[..40];
        db.ContenidoWeb.Add(new ContenidoWebRow
        {
            Key = clave,
            GroupId = "pruebas",
            GroupLabel = "Pruebas",
            Section = "Pruebas",
            Label = "Clave de prueba",
            CharacterLimit = 100,
            Draft = "Texto",
            Published = "Texto publicado",
            Version = 1,
            UpdatedBy = "Pruebas",
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }
}
