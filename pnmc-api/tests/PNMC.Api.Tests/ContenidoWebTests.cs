using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Fase 2A del CMS: catálogo sembrado, lectura pública y guardado por grupo.
/// </summary>
public sealed class ContenidoWebTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContenidoWebTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Inicia sesion y adjunta el token antiforgery, igual que hace la consola.
    private Task<HttpClient> LoginAsWebmasterAsync() => CmsTestClient.LoginAsync(_factory);

    // ---------- Siembra ----------

    [Fact]
    public async Task La_siembra_corre_tambien_en_sqlite()
    {
        // Regresión: la siembra colgaba de la rama de SQL Server del bootstrapper,
        // que termina en `return`. Con SQLite —el proveedor de estas pruebas— nunca
        // se ejecutaba, y toda la suite habría pasado sobre una tabla vacía.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var total = await db.ContenidoWeb.CountAsync();
        Assert.Equal(SembradorDeContenidoWeb.LoadCatalog().Count, total);
        Assert.True(total > 200, $"Se esperaba el catálogo completo, hay {total} filas.");
    }

    [Fact]
    public async Task La_siembra_no_publica_nada()
    {
        // Sembrar no es publicar: el sitio debe seguir sirviendo su texto compilado
        // hasta que alguien publique de verdad desde la consola.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var publicadasDeFabrica = await db.ContenidoWeb
            .CountAsync(x => x.Published != null && x.UpdatedBy == "Sistema");
        Assert.Equal(0, publicadasDeFabrica);
    }

    [Fact]
    public async Task La_siembra_es_idempotente_y_no_pisa_lo_editado()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        var fila = await db.ContenidoWeb.FirstAsync(x => x.Key == "home_title");
        fila.Draft = "Texto escrito por un editor";
        fila.Version++;
        await db.SaveChangesAsync();

        var antes = await db.ContenidoWeb.CountAsync();
        await SembradorDeContenidoWeb.EnsureSeededAsync(db, logger);
        var despues = await db.ContenidoWeb.CountAsync();

        Assert.Equal(antes, despues);
        var recargada = await db.ContenidoWeb.AsNoTracking().FirstAsync(x => x.Key == "home_title");
        Assert.Equal("Texto escrito por un editor", recargada.Draft);
    }

    [Fact]
    public void El_catalogo_respeta_sus_propios_limites()
    {
        var excedidos = SembradorDeContenidoWeb.LoadCatalog()
            .Where(entry => entry.DefaultValue.Length > entry.Limit)
            .Select(entry => entry.Key)
            .ToList();
        Assert.Empty(excedidos);
    }

    // ---------- Lectura pública ----------

    [Fact]
    public async Task La_lectura_publica_no_exige_sesion()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/contenido-web");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task La_lectura_publica_solo_devuelve_lo_publicado()
    {
        var client = await LoginAsWebmasterAsync();
        var anonimo = _factory.CreateClient();

        var antes = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.NotNull(antes);
        Assert.False(antes!.Texts.ContainsKey("home_title_accent"));

        // Un borrador no debe aparecer en el sitio público.
        var borrador = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title_accent", content = "Solo borrador" } },
            publish = false,
        });
        Assert.Equal(HttpStatusCode.OK, borrador.StatusCode);

        var trasBorrador = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.False(trasBorrador!.Texts.ContainsKey("home_title_accent"));

        // Publicar sí lo hace visible.
        var publicado = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title_accent", content = "Ya publicado" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.OK, publicado.StatusCode);

        var trasPublicar = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.Equal("Ya publicado", trasPublicar!.Texts["home_title_accent"]);
    }

    [Fact]
    public async Task Guardar_borrador_no_despublica_lo_ya_publicado()
    {
        var client = await LoginAsWebmasterAsync();
        var anonimo = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_tag", content = "Version publicada" } },
            publish = true,
        });

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_tag", content = "Version en revision" } },
            publish = false,
        });

        var publico = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.Equal("Version publicada", publico!.Texts["home_tag"]);
    }

    // ---------- Autorización ----------

    [Fact]
    public async Task El_guardado_rechaza_a_quien_no_tiene_sesion()
    {
        var anonimo = _factory.CreateClient();
        var response = await anonimo.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_tag", content = "Intruso" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task La_lectura_de_un_grupo_rechaza_a_quien_no_tiene_sesion()
    {
        var anonimo = _factory.CreateClient();
        var response = await anonimo.GetAsync("/api/v1/admin/contenido-web/groups/home_hero");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Validación ----------

    [Fact]
    public async Task Rechaza_un_texto_que_excede_su_limite()
    {
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = new string('x', 5000) } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rechaza_una_clave_que_no_pertenece_al_grupo()
    {
        // Sin esta comprobación, cualquier grupo sería la puerta para escribir en
        // cualquier texto del sitio.
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "footer_credits_text", content = "Ajeno al grupo" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Un_grupo_invalido_no_escribe_ningun_campo()
    {
        // Todo o nada: si un campo del grupo falla, ninguno se guarda.
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[]
            {
                new { key = "home_title", content = "Valido" },
                new { key = "home_title_accent", content = new string('y', 5000) },
            },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().FirstAsync(x => x.Key == "home_title");
        Assert.NotEqual("Valido", fila.Draft);
    }

    [Fact]
    public async Task Un_grupo_inexistente_devuelve_404()
    {
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/grupo_que_no_existe", new
        {
            fields = new[] { new { key = "home_tag", content = "x" } },
            publish = false,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Concurrencia ----------

    [Fact]
    public async Task Reenviar_el_grupo_sin_cambios_no_sube_la_version()
    {
        // El panel envía SIEMPRE las claves del grupo completo. Si cada guardado
        // subiera la versión de todas, dos editores en campos distintos del mismo
        // grupo chocarían siempre.
        var client = await LoginAsWebmasterAsync();

        var grupo = await client.GetFromJsonAsync<GroupResponse>("/api/v1/admin/contenido-web/groups/about_approaches");
        Assert.NotNull(grupo);
        var versionesAntes = grupo!.Fields.ToDictionary(f => f.Key, f => f.Version);

        // Se reenvía el grupo entero cambiando un solo campo.
        var payload = grupo.Fields
            .Select(f => new { key = f.Key, content = f.Key == "about_approaches_tag" ? "TRANSVERSAL" : f.Draft })
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_approaches", new
        {
            fields = payload,
            publish = false,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var despues = await client.GetFromJsonAsync<GroupResponse>("/api/v1/admin/contenido-web/groups/about_approaches");
        foreach (var campo in despues!.Fields)
        {
            var esperada = campo.Key == "about_approaches_tag"
                ? versionesAntes[campo.Key] + 1
                : versionesAntes[campo.Key];
            Assert.Equal(esperada, campo.Version);
        }
    }


    // ---------- Retirar y republicar (2D) ----------

    [Fact]
    public async Task Retirar_quita_el_texto_del_sitio_pero_conserva_el_borrador()
    {
        var client = await LoginAsWebmasterAsync();
        var anonimo = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_hero_presentation", new
        {
            fields = new[] { new { key = "about_description", content = "Presentación publicada" } },
            publish = true,
        });
        var publicado = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.Equal("Presentación publicada", publicado!.Texts["about_description"]);

        var retiro = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_description/retire", new { reason = "prueba" });
        Assert.Equal(HttpStatusCode.OK, retiro.StatusCode);

        var trasRetiro = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.False(trasRetiro!.Texts.ContainsKey("about_description"));

        // El borrador sigue ahí: retirar quita del sitio, no borra del editor.
        var grupo = await client.GetFromJsonAsync<GroupResponse>("/api/v1/admin/contenido-web/groups/about_hero_presentation");
        var campo = grupo!.Fields.Single(f => f.Key == "about_description");
        Assert.Equal("Presentación publicada", campo.Draft);
        Assert.Equal("retirado", campo.State);
    }

    [Fact]
    public async Task Publicar_el_grupo_no_resucita_una_clave_retirada()
    {
        // Bloqueante señalado en la revisión: el panel publica el grupo entero, así
        // que sin esta regla el siguiente guardado deshacía el retiro en silencio.
        var client = await LoginAsWebmasterAsync();
        var anonimo = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_hero_presentation", new
        {
            fields = new[] { new { key = "about_hero_tag", content = "Etiqueta visible" } },
            publish = true,
        });
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_hero_tag/retire", new { reason = (string?)null });

        // Se vuelve a publicar el grupo completo, como hace el panel.
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_hero_presentation", new
        {
            fields = new[] { new { key = "about_hero_tag", content = "Etiqueta corregida" } },
            publish = true,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var publico = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.False(publico!.Texts.ContainsKey("about_hero_tag"));

        // El borrador sí recogió la corrección.
        var grupo = await client.GetFromJsonAsync<GroupResponse>("/api/v1/admin/contenido-web/groups/about_hero_presentation");
        var campo = grupo!.Fields.Single(f => f.Key == "about_hero_tag");
        Assert.Equal("Etiqueta corregida", campo.Draft);
        Assert.Equal("retirado", campo.State);
    }

    [Fact]
    public async Task Republicar_devuelve_el_borrador_vigente_al_sitio()
    {
        var client = await LoginAsWebmasterAsync();
        var anonimo = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_hero_presentation", new
        {
            fields = new[] { new { key = "about_hero_title", content = "Título v1" } },
            publish = true,
        });
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_hero_title/retire", new { reason = (string?)null });

        // Se corrige el borrador mientras está retirado.
        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/about_hero_presentation", new
        {
            fields = new[] { new { key = "about_hero_title", content = "Título v2" } },
            publish = false,
        });

        var republicar = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_hero_title/republish", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, republicar.StatusCode);

        var publico = await anonimo.GetFromJsonAsync<PublicContentResponse>("/api/v1/contenido-web");
        Assert.Equal("Título v2", publico!.Texts["about_hero_title"]);
    }

    [Fact]
    public async Task No_se_puede_retirar_lo_que_nunca_se_publico()
    {
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_hero_accent/retire", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_republicar_lo_que_no_esta_retirado()
    {
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/about_presentation_bg/republish", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Retirar_exige_sesion()
    {
        var anonimo = _factory.CreateClient();
        var response = await anonimo.PostAsJsonAsync("/api/v1/admin/contenido-web/home_tag/retire", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Una_clave_inexistente_devuelve_404_al_retirar()
    {
        var client = await LoginAsWebmasterAsync();
        var response = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/clave_inventada/retire", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Contratos de respuesta ----------

    private sealed class GroupResponse
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public List<GroupField> Fields { get; set; } = new();
    }

    private sealed class GroupField
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Limit { get; set; }
        public string Draft { get; set; } = string.Empty;
        public string? Published { get; set; }
        public string State { get; set; } = string.Empty;
        public int Version { get; set; }
    }
}

/// <summary>
/// Forma de la respuesta de <c>GET /api/v1/contenido-web</c>.
/// </summary>
/// <remarks>
/// Vive fuera de la clase de pruebas —y no anidada dentro— porque la comprueba
/// mas de un archivo (MuestraSqlServerTests entre ellos). Duplicar el contrato en
/// cada uno es como se llega a que dos copias discrepen sin que nadie lo note.
/// </remarks>
internal sealed class PublicContentResponse
{
    public Dictionary<string, string> Texts { get; set; } = new();
    public int Count { get; set; }
}
