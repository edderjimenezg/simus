using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Ver un registro como se verá en su listado, antes de publicarlo.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE.</b> La consola solo ofrecía «Ver en el portal» cuando el registro ya
/// estaba publicado: la única forma de saber cómo iba a quedar algo era publicarlo. Un titular que
/// se corta, una imagen mal encuadrada o un resumen vacío se descubrían con el registro ya en la
/// calle.
/// </para>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que la previsualización se convierta en una puerta por la
/// que un anónimo lea borradores, y que devuelva una forma distinta a la de la lectura pública
/// —porque entonces deja de ser una previsualización y nadie se entera—.
/// </para>
/// </remarks>
public sealed class PrevisualizacionEnListadoTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PrevisualizacionEnListadoTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("noticias")]
    [InlineData("agenda")]
    [InlineData("catalogo-editorial")]
    [InlineData("festivales")]
    public async Task Sin_sesion_de_consola_no_se_previsualiza_nada(string modulo)
    {
        using var anonimo = _factory.CreateClient();

        var respuesta = await anonimo.GetAsync($"/api/v1/admin/previsualizacion/{modulo}/1");

        // ES LA UNICA GUARDA Y TIENE QUE BASTAR: la página pública pide el borrador con la cookie de
        // quien está en la consola. Sin ella, aquí no hay nada que leer.
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_borrador_se_previsualiza_con_la_forma_de_la_lectura_publica()
    {
        var id = await NoticiaEnBorradorAsync();
        using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        // LA LECTURA PUBLICA NO LA ENSEÑA —es un borrador— y esa es exactamente la situación en la
        // que hace falta previsualizar.
        using var anonimo = _factory.CreateClient();
        var publica = await anonimo.GetAsync("/api/v1/publico/noticias/borrador-para-previsualizar");
        Assert.Equal(HttpStatusCode.NotFound, publica.StatusCode);

        var respuesta = await consola.GetFromJsonAsync<JsonElement>($"/api/v1/admin/previsualizacion/noticias/{id}");

        // EL MISMO DTO QUE LA RUTA PUBLICA, por el mismo mapeador. Una forma propia dejaría de
        // coincidir con el portal en cuanto la lectura pública cambiara un campo, en silencio.
        Assert.Equal("Borrador para previsualizar", respuesta.GetProperty("titulo").GetString());
        Assert.Equal("borrador-para-previsualizar", respuesta.GetProperty("slug").GetString());
        Assert.True(respuesta.TryGetProperty("estadoEfectivo", out _));
        Assert.True(respuesta.TryGetProperty("resumen", out _));
    }

    [Fact]
    public async Task Un_registro_que_no_existe_responde_404_y_no_un_hueco()
    {
        using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuesta = await consola.GetAsync("/api/v1/admin/previsualizacion/noticias/999999");

        // 404 Y NO UN 200 VACIO: la pantalla tiene que poder distinguir «esto ya no está» de «esto
        // se ve así», y un cuerpo vacío se pinta como un listado sin la tarjeta, que no dice nada.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Previsualizar_no_publica_ni_cambia_el_estado()
    {
        var id = await NoticiaEnBorradorAsync();
        using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        (await consola.GetAsync($"/api/v1/admin/previsualizacion/noticias/{id}")).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Noticias.AsNoTracking().SingleAsync(n => n.Id == id);

        // PREVISUALIZAR NO ES UN PASO DEL CIRCUITO. No cambia el estado, no deja rastro de
        // publicación y no adelanta la fecha de aparición.
        Assert.Equal("borrador", fila.Estado);
        Assert.Null(fila.FechaPublicacion);
    }

    [Fact]
    public async Task Un_Festival_sin_publicar_se_previsualiza_con_la_lectura_publica()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.FirstAsync();
        var estadoOriginal = festival.StatusCode;
        festival.StatusCode = "borrador";
        await db.SaveChangesAsync();

        try
        {
            using var anonimo = _factory.CreateClient();
            var publica = await anonimo.GetAsync($"/api/v1/publico/festivales/{festival.Id}");
            Assert.Equal(HttpStatusCode.NotFound, publica.StatusCode);

            using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
            var vista = await consola.GetFromJsonAsync<JsonElement>(
                $"/api/v1/admin/previsualizacion/festivales/{festival.Id}");

            // LA MISMA LECTURA PUBLICA, con su versión vigente resuelta y sus catálogos: lo único
            // que cambia es que este identificador entra aunque no esté publicado. Una ficha
            // «parecida» dejaría de parecerse en cuanto la lectura pública cambiara un campo.
            Assert.Equal(festival.Name, vista.GetProperty("nombre").GetString());
            Assert.True(vista.TryGetProperty("practicasMusicales", out _));
            Assert.True(vista.TryGetProperty("territorioPrincipal", out _));
        }
        finally
        {
            festival.StatusCode = estadoOriginal;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Una noticia en borrador, creada una sola vez para toda la clase.</summary>
    private async Task<long> NoticiaEnBorradorAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var existente = await db.Noticias.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Slug == "borrador-para-previsualizar");
        if (existente is not null) return existente.Id;

        var fila = new NoticiaRow
        {
            Slug = "borrador-para-previsualizar",
            Titulo = "Borrador para previsualizar",
            Resumen = "Un resumen que todavía no ha visto nadie.",
            Cuerpo = "Cuerpo de la noticia.",
            Estado = "borrador",
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow,
        };
        db.Noticias.Add(fila);
        await db.SaveChangesAsync();
        return fila.Id;
    }
}
