using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class ResumenDeGestionDelSitioTests
{
    [Fact]
    public async Task El_resumen_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var response = await factory.CreateClient().GetAsync("/api/v1/admin/contenido-web/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task La_siembra_no_aparece_como_cambio_pendiente()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/contenido-web/summary");

        Assert.Equal(0, summary.GetProperty("pendingTotal").GetInt32());
        Assert.Equal(SembradorDeContenidoWeb.LoadCatalog().Count, summary.GetProperty("texts").GetProperty("total").GetInt32());
        Assert.Equal(SembradorDeContenidoWeb.LoadImageCatalog().Count, summary.GetProperty("images").GetProperty("total").GetInt32());
        Assert.False(summary.GetProperty("team").GetProperty("pending").GetBoolean());
    }

    [Fact]
    public async Task Cuenta_borradores_reales_sin_devolver_su_contenido()
    {
        await using var factory = new TestWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

            var texto = await db.ContenidoWeb.FirstAsync(x => x.Key == "home_title");
            texto.Draft = "Cambio todavía sin publicar";

            var imagen = await db.ImagenesWeb.FirstAsync(x => x.Key == "home_hero_1");
            imagen.DraftHash = new string('a', 64);

            var equipo = await db.EquipoWeb.FirstAsync();
            equipo.Draft = "[]";
            await db.SaveChangesAsync();
        }

        var client = await CmsTestClient.LoginAsync(factory);
        var response = await client.GetAsync("/api/v1/admin/contenido-web/summary");
        var json = await response.Content.ReadAsStringAsync();
        var summary = JsonDocument.Parse(json).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, summary.GetProperty("pendingTotal").GetInt32());
        Assert.Equal(1, summary.GetProperty("texts").GetProperty("pending").GetInt32());
        Assert.Equal(1, summary.GetProperty("images").GetProperty("pending").GetInt32());
        Assert.True(summary.GetProperty("team").GetProperty("pending").GetBoolean());
        Assert.DoesNotContain("Cambio todavía sin publicar", json, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 64), json, StringComparison.Ordinal);
    }
}
