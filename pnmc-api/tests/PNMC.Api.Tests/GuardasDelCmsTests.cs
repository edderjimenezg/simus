using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Incremento 2E: antiforgery en las escrituras e historial por clave.
/// </summary>
public sealed class GuardasDelCmsTests
{
    // ---------- Antiforgery ----------

    [Fact]
    public async Task Guardar_sin_token_se_rechaza_aunque_la_sesion_sea_valida()
    {
        // El escenario real: otro sitio hace que el navegador de un editor con
        // sesión abierta envíe la petición. La cookie viaja; el token no, porque
        // ese sitio no puede leerlo.
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Escrito desde otro sitio" } },
            publish = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Null(fila.Published);
        Assert.NotEqual("Escrito desde otro sitio", fila.Draft);
    }

    [Theory]
    [InlineData("/api/v1/admin/contenido-web/home_title/retire")]
    [InlineData("/api/v1/admin/contenido-web/home_title/republish")]
    [InlineData("/api/v1/admin/contenido-web/import")]
    [InlineData("/api/v1/admin/contenido-web/import/preview")]
    [InlineData("/api/v1/admin/equipo-web")]
    public async Task Ninguna_ruta_de_escritura_acepta_una_peticion_sin_token(string ruta)
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        var respuesta = await client.PostAsJsonAsync(ruta, new { });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_token_de_otra_sesion_no_sirve()
    {
        using var factory = new TestWebApplicationFactory();

        // Dos sesiones distintas: el token de una no vale para la cookie de la otra.
        var ajeno = await CmsTestClient.LoginAsync(factory);
        var tokenAjeno = ajeno.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").First();

        var propio = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        propio.DefaultRequestHeaders.Add("X-CSRF-TOKEN", tokenAjeno);

        var respuesta = await propio.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Con token prestado" } },
            publish = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_lectura_publica_no_exige_token()
    {
        // Si lo exigiera, el sitio no podría leer sus propios textos.
        using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/contenido-web")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/equipo-web")).StatusCode);
    }

    // ---------- Historial ----------

    [Fact]
    public async Task El_historial_registra_guardar_publicar_retirar_y_republicar()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await GuardarAsync(client, "Primera versión", publish: false);
        await GuardarAsync(client, "Segunda versión", publish: true);
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/retire", new { reason = (string?)null });
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/republish", new { reason = (string?)null });

        var historial = await LeerHistorialAsync(client, "home_title");
        var acciones = historial.Select(x => x.GetProperty("action").GetString()).ToList();

        // Del más reciente al más antiguo.
        Assert.Equal(["republicado", "retirado", "publicado", "guardado"], acciones);
    }

    [Fact]
    public async Task El_historial_guarda_el_valor_nuevo_y_quien_lo_escribio()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await GuardarAsync(client, "El texto que quedó", publish: false);

        var entrada = (await LeerHistorialAsync(client, "home_title")).First();
        Assert.Equal("El texto que quedó", entrada.GetProperty("value").GetString());
        Assert.Equal("Usuario Prueba", entrada.GetProperty("user").GetString());
    }

    [Fact]
    public async Task Retirar_no_guarda_valor_porque_no_reescribe_el_borrador()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await GuardarAsync(client, "Publicado y retirado", publish: true);
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/retire", new { reason = (string?)null });

        var entrada = (await LeerHistorialAsync(client, "home_title")).First();
        Assert.Equal("retirado", entrada.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, entrada.GetProperty("value").ValueKind);
    }

    [Fact]
    public async Task Un_guardado_que_no_cambia_nada_no_deja_entrada()
    {
        // Mismo predicado de elisión que evita subir la versión: si no se escribió,
        // el historial no debe decir que sí.
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await GuardarAsync(client, "Un texto", publish: false);
        var antes = (await LeerHistorialAsync(client, "home_title")).Count;

        await GuardarAsync(client, "Un texto", publish: false);
        var despues = (await LeerHistorialAsync(client, "home_title")).Count;

        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task La_importacion_deja_su_propia_accion_en_el_historial()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respaldo = new
        {
            schemaVersion = 1,
            app = "pnmc-cms",
            exportedAt = "2026-08-20T10:00:00.000Z",
            exportedBy = "Editora",
            stores = new Dictionary<string, object?>
            {
                ["pnmc_web_texts"] = new Dictionary<string, object>
                {
                    ["home_title"] = new { content = "Rescatado del navegador", status = "borrador", updatedAt = "", updatedBy = "" },
                },
                ["pnmc_web_media"] = new Dictionary<string, object>(),
                ["pnmc_web_team_members"] = (object?)null,
            },
        };

        var plan = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/import/preview", respaldo);
        var planHash = (await plan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("planHash").GetString();

        var aplicar = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/import", new
        {
            backup = respaldo,
            planHash,
            overwriteEdited = false,
        });
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        var entrada = (await LeerHistorialAsync(client, "home_title")).First();
        Assert.Equal("importado", entrada.GetProperty("action").GetString());
        Assert.Equal("Rescatado del navegador", entrada.GetProperty("value").GetString());
    }

    [Fact]
    public async Task El_historial_acota_cuantas_entradas_devuelve()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        for (var i = 0; i < 5; i++)
        {
            await GuardarAsync(client, $"Versión {i}", publish: false);
        }

        Assert.Equal(2, (await LeerHistorialAsync(client, "home_title", limit: 2)).Count);
        // Un límite absurdo se recorta en vez de aceptarse.
        Assert.True((await LeerHistorialAsync(client, "home_title", limit: 100000)).Count <= 100);
    }

    /// <summary>
    /// El historial exige sesión, y publicar o retirar exigen además ser webmaster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Esta prueba medía antes el 403 de un <c>aliado_admin</c> sobre el historial. Al retirarse
    /// el concepto de entidad aliada (22 ago 2026) ese rol dejó de existir, y con él el único
    /// usuario capaz de obtener cookie de consola sin ser interno. El historial es
    /// <c>EditorRoles</c>, así que el gestor interno <b>sí</b> lo alcanza: ya no hay ningún rol
    /// con sesión válida al que negarle ese endpoint.
    /// </para>
    /// <para>
    /// Lo honesto es decirlo y mover el aserto de 403 al guarda que sí puede denegarle algo:
    /// <c>PublisherRoles</c>. El 200 del historial queda como control positivo —prueba que el
    /// 403 de abajo viene del rol y no de la sesión— y el 401 anónimo se conserva intacto.
    /// Que ningún guarda del CMS se amplíe hacia fuera del Ministerio lo vigila
    /// <c>PuertaInstitucionalTests</c> por reflexión.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Un gestor interno guarda el borrador de un grupo, pero no lo publica al sitio público.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ESTA ES LA RUTA PRINCIPAL DE PUBLICACIÓN del CMS y era la única de su familia sin prueba.
    /// Comprobado: neutralizando el <c>if</c> de
    /// <c>ContenidoWebEndpoints.cs</c>, la suite completa quedaba en verde. Sus tres hermanos
    /// con la misma lista —<c>retire</c>, <c>republish</c>, la nómina y la importación— sí
    /// estaban cubiertos; la que de verdad publica, no. Con esa línea rota, un funcionario
    /// publica cualquier texto al portal público en una sola petición.
    /// </para>
    /// <para>
    /// El 200 del borrador no es relleno: es el control positivo. Sin él, el 403 podría venir de
    /// que el gestor no llegó a entrar, y la prueba estaría midiendo otra cosa. El token
    /// antiforgery va puesto por la misma razón: sin él la respuesta sería 400 y el aserto
    /// pasaría sin haber mirado el rol.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Publicar_un_grupo_al_sitio_publico_exige_webmaster()
    {
        using var factory = new TestWebApplicationFactory();
        var gestor = await CmsTestClient.LoginAsync(factory, "gestor@pnmc.local", "pnmc-gestor");

        var borrador = await gestor.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Título guardado en borrador" } },
            publish = false,
        });
        Assert.Equal(HttpStatusCode.OK, borrador.StatusCode);

        var publicar = await gestor.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Título publicado a la brava" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.Forbidden, publicar.StatusCode);

        // Y el webmaster sí: sin esto, una lista de roles vacía dejaría el 403 de arriba en
        // verde mientras rompe la publicación para todo el mundo.
        var webmaster = await CmsTestClient.LoginAsync(factory);
        var publicado = await webmaster.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Título publicado por quien puede" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.OK, publicado.StatusCode);
    }

    [Fact]
    public async Task El_historial_exige_sesion_y_publicar_exige_webmaster()
    {
        using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/v1/admin/contenido-web/home_title/history")).StatusCode);

        var gestor = await CmsTestClient.LoginAsync(factory, "gestor@pnmc.local", "pnmc-gestor");
        Assert.Equal(HttpStatusCode.OK,
            (await gestor.GetAsync("/api/v1/admin/contenido-web/home_title/history")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/retire", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/republish", new { })).StatusCode);
    }

    // ---------- Ayudas ----------

    private static async Task GuardarAsync(HttpClient client, string contenido, bool publish)
    {
        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = contenido } },
            publish,
        });
        respuesta.EnsureSuccessStatusCode();
    }

    private static async Task<List<JsonElement>> LeerHistorialAsync(HttpClient client, string key, int? limit = null)
    {
        var url = $"/api/v1/admin/contenido-web/{key}/history" + (limit is null ? "" : $"?limit={limit}");
        var respuesta = await client.GetAsync(url);
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("entries").EnumerateArray().ToList();
    }
}
