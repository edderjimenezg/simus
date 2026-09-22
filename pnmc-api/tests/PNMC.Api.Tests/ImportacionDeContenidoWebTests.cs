using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Incremento 2C del CMS: importar a la base el respaldo de localStorage.
/// </summary>
public sealed class ImportacionDeContenidoWebTests
{
    private static Task<HttpClient> LoginAsync(
        TestWebApplicationFactory factory,
        string email = CmsTestClient.WebmasterEmail,
        string password = CmsTestClient.WebmasterPassword)
        => CmsTestClient.LoginAsync(factory, email, password);

    /// <summary>Un respaldo con la forma exacta que produce el panel.</summary>
    private static object Respaldo(
        Dictionary<string, object>? textos = null,
        object? equipo = null,
        Dictionary<string, object>? medios = null,
        int schemaVersion = 1,
        string app = "pnmc-cms")
        => new
        {
            schemaVersion,
            app,
            exportedAt = "2026-08-20T10:00:00.000Z",
            exportedBy = "Editora de Contenidos",
            stores = new Dictionary<string, object?>
            {
                ["pnmc_web_texts"] = textos ?? new Dictionary<string, object>(),
                ["pnmc_web_media"] = medios ?? new Dictionary<string, object>(),
                ["pnmc_web_team_members"] = equipo,
            },
        };

    private static object Texto(string contenido, string? publicado = null, int revisiones = 0)
        => new
        {
            content = contenido,
            publishedContent = publicado,
            status = publicado is null ? "borrador" : "publicado",
            updatedAt = "2026-08-20T10:00:00.000Z",
            updatedBy = "Editora de Contenidos",
            history = Enumerable.Range(0, revisiones).Select(i => new
            {
                content = $"revisión {i}",
                status = "borrador",
                updatedAt = "2026-08-19T10:00:00.000Z",
                updatedBy = "Editora de Contenidos",
            }).ToArray(),
        };

    private static async Task<JsonElement> SimularAsync(HttpClient client, object respaldo, bool overwriteEdited = false)
    {
        var url = $"/api/v1/admin/contenido-web/import/preview?overwriteEdited={overwriteEdited.ToString().ToLowerInvariant()}";
        var response = await client.PostAsJsonAsync(url, respaldo);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> AplicarAsync(
        HttpClient client, object respaldo, string? planHash, bool overwriteEdited = false)
        => client.PostAsJsonAsync("/api/v1/admin/contenido-web/import", new
        {
            backup = respaldo,
            planHash,
            overwriteEdited,
        });

    // ---------- El principio de fondo ----------

    [Fact]
    public async Task Importar_no_publica_nunca()
    {
        // La regla que la revisión impuso: importar escribe borradores, no publica.
        // Un respaldo cuyo texto estaba PUBLICADO en el navegador entra igual como
        // borrador, y el sitio público sigue exactamente igual.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);
        var anonimo = factory.CreateClient();

        var antes = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/contenido-web");
        Assert.Equal(0, antes.GetProperty("count").GetInt32());

        var respaldo = Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Título traído del navegador", publicado: "Título traído del navegador"),
        });

        var plan = await SimularAsync(client, respaldo);
        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        var despues = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/contenido-web");
        Assert.Equal(0, despues.GetProperty("count").GetInt32());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal("Título traído del navegador", fila.Draft);
        Assert.Null(fila.Published);
    }

    [Fact]
    public async Task Importar_no_resucita_una_clave_retirada()
    {
        // 2D: una clave retirada cuyo borrador cambie sigue retirada. Si importar
        // la devolviera al sitio, el retiro seria reversible por accidente.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Publicado y luego retirado" } },
            publish = true,
        });
        var retiro = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/home_title/retire", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, retiro.StatusCode);

        var respaldo = Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Otro texto del navegador", publicado: "Otro texto del navegador"),
        });
        var plan = await SimularAsync(client, respaldo, overwriteEdited: true);
        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString(), overwriteEdited: true);
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal("Otro texto del navegador", fila.Draft);
        Assert.NotNull(fila.Retired);
        Assert.Null(fila.Published);
    }

    // ---------- La simulación ----------

    [Fact]
    public async Task La_simulacion_no_escribe_nada()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var antes = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");

        var plan = await SimularAsync(client, Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Cambio simulado"),
        }));

        Assert.Equal(1, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
        Assert.False(plan.GetProperty("aplicado").GetBoolean());

        var despues = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal(antes.Draft, despues.Draft);
        Assert.Equal(antes.Version, despues.Version);
    }

    [Fact]
    public async Task Simular_sin_la_bandera_usa_el_valor_prudente()
    {
        // Regresión: la bandera era un parámetro obligatorio de query, así que
        // omitirla daba 400. Omitirla debe significar «no pises nada editado».
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(
            "/api/v1/admin/contenido-web/import/preview",
            Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Sin bandera") }));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var plan = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
    }

    [Fact]
    public async Task Aplicar_sin_la_huella_devuelve_400()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var respuesta = await AplicarAsync(client, Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Sin simular"),
        }), planHash: null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("planHash", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Si_el_archivo_no_es_el_simulado_no_se_escribe()
    {
        // El operador tenía dos archivos abiertos. La huella lo atrapa.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var simulado = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Archivo A") });
        var otro = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Archivo B") });

        var plan = await SimularAsync(client, simulado);
        var respuesta = await AplicarAsync(client, otro, plan.GetProperty("planHash").GetString());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.DoesNotContain("Archivo", fila.Draft);
    }

    [Fact]
    public async Task Si_alguien_edita_entre_simular_y_aplicar_no_se_escribe()
    {
        // El token de concurrencia por sí solo no basta: la fila que el import
        // pisa puede ser otra distinta de la que se mostró en la simulación.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var respaldo = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Del respaldo") });
        var plan = await SimularAsync(client, respaldo);

        // Otra persona guarda esa misma clave en el intervalo.
        var interferencia = await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Escrito por otra persona" } },
            publish = false,
        });
        Assert.Equal(HttpStatusCode.OK, interferencia.StatusCode);

        var respuesta = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal("Escrito por otra persona", fila.Draft);
    }

    // ---------- Conflictos e idempotencia ----------

    [Fact]
    public async Task Una_clave_ya_editada_no_se_pisa_por_omision()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Trabajo hecho en la consola" } },
            publish = false,
        });

        var respaldo = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Del navegador") });
        var plan = await SimularAsync(client, respaldo);

        Assert.Equal(0, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
        Assert.Equal(1, plan.GetProperty("textos").GetProperty("enConflictoTotal").GetInt32());

        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal("Trabajo hecho en la consola", fila.Draft);
    }

    [Fact]
    public async Task Con_overwriteEdited_el_respaldo_gana()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        await client.PostAsJsonAsync("/api/v1/admin/contenido-web/groups/home_hero", new
        {
            fields = new[] { new { key = "home_title", content = "Trabajo hecho en la consola" } },
            publish = false,
        });

        var respaldo = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Del navegador") });
        var plan = await SimularAsync(client, respaldo, overwriteEdited: true);
        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString(), overwriteEdited: true);
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal("Del navegador", fila.Draft);
    }

    [Fact]
    public async Task Importar_dos_veces_el_mismo_archivo_no_cambia_nada_la_segunda()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);
        var respaldo = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Una sola vez") });

        var plan1 = await SimularAsync(client, respaldo);
        await AplicarAsync(client, respaldo, plan1.GetProperty("planHash").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var version = (await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title")).Version;

        // La segunda vez el borrador ya coincide: cero filas por aplicar.
        var plan2 = await SimularAsync(client, respaldo);
        Assert.Equal(0, plan2.GetProperty("textos").GetProperty("porAplicar").GetInt32());
        Assert.Equal(1, plan2.GetProperty("textos").GetProperty("sinCambio").GetInt32());

        var aplicar2 = await AplicarAsync(client, respaldo, plan2.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.OK, aplicar2.StatusCode);

        var despues = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal(version, despues.Version);
    }

    // ---------- Lo que no entra, pero se nombra ----------

    [Fact]
    public async Task Las_claves_heredadas_se_nombran_no_se_traducen()
    {
        // Verificado en el repositorio: nada escribe nunca 'about_team_N_*'.
        // Traducirlas seria codigo para un caso que no ocurre; nombrarlas asegura
        // que si ocurriera, el operador lo ve en vez de perderlas en silencio.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var plan = await SimularAsync(client, Respaldo(new Dictionary<string, object>
        {
            ["about_team_3_name"] = Texto("Alguien del equipo"),
            ["clave_que_no_existe"] = Texto("Contenido huérfano"),
        }));

        var desconocidas = plan.GetProperty("noImportado").GetProperty("clavesDesconocidas")
            .EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.Equal(2, plan.GetProperty("noImportado").GetProperty("clavesDesconocidasTotal").GetInt32());
        Assert.Contains("about_team_3_name", desconocidas);
        Assert.Contains("clave_que_no_existe", desconocidas);
        Assert.Equal(0, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
    }

    [Fact]
    public async Task El_informe_separa_lo_publicado_identico_de_lo_distinto()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var plan = await SimularAsync(client, Respaldo(new Dictionary<string, object>
        {
            // Publicado igual al borrador: no se pierde nada.
            ["home_title"] = Texto("Mismo texto", publicado: "Mismo texto"),
            // Publicado distinto: había un texto visible que aquí no lo estará.
            ["home_tag"] = Texto("Borrador nuevo", publicado: "Lo que veía el visitante"),
        }));

        var noImportado = plan.GetProperty("noImportado");
        Assert.Equal(1, noImportado.GetProperty("publicadoIgualAlBorrador").GetInt32());
        Assert.Equal(1, noImportado.GetProperty("publicadoDistintoTotal").GetInt32());
        Assert.Contains("home_tag", noImportado.GetProperty("publicadoDistinto")
            .EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Las_revisiones_y_los_medios_se_cuentan_y_no_se_importan()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var plan = await SimularAsync(client, Respaldo(
            new Dictionary<string, object> { ["home_title"] = Texto("Con historial", revisiones: 5) },
            medios: new Dictionary<string, object>
            {
                ["about_team_1_photo"] = new { content = "data:image/webp;base64,AAAA", status = "publicado" },
            }));

        var noImportado = plan.GetProperty("noImportado");
        Assert.Equal(5, noImportado.GetProperty("revisionesDescartadas").GetInt32());
        Assert.Equal(1, noImportado.GetProperty("entradasDeMedios").GetInt32());
    }

    [Fact]
    public async Task Un_texto_que_excede_su_limite_se_omite_y_los_demas_entran()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var plan = await SimularAsync(client, Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Cabe de sobra"),
            ["home_tag"] = Texto(new string('x', 5000)),
        }));

        Assert.Equal(1, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
        Assert.Equal(1, plan.GetProperty("textos").GetProperty("rechazadosTotal").GetInt32());
        var motivo = plan.GetProperty("textos").GetProperty("rechazados")[0].GetProperty("reason").GetString();
        Assert.Contains("límite", motivo!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_texto_con_menor_que_se_omite_y_se_dice_donde()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var plan = await SimularAsync(client, Respaldo(new Dictionary<string, object>
        {
            ["home_title"] = Texto("Hola <script>"),
        }));

        Assert.Equal(0, plan.GetProperty("textos").GetProperty("porAplicar").GetInt32());
        var motivo = plan.GetProperty("textos").GetProperty("rechazados")[0].GetProperty("reason").GetString();
        Assert.Contains("posición 6", motivo!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- La nómina ----------

    [Fact]
    public async Task La_nomina_del_respaldo_entra_como_borrador()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);
        var anonimo = factory.CreateClient();

        var respaldo = Respaldo(equipo: new
        {
            content = new[]
            {
                new { id = "team-1", group = "coordination", role = "Coordinación", name = "Persona Uno", email = "uno@pnmc.gov.co", photo = "" },
            },
            publishedContent = new[]
            {
                new { id = "team-1", group = "coordination", role = "Coordinación", name = "Persona Uno", email = "uno@pnmc.gov.co", photo = "" },
            },
            status = "publicado",
        });

        var plan = await SimularAsync(client, respaldo);
        Assert.True(plan.GetProperty("equipo").GetProperty("porAplicar").GetBoolean());
        Assert.True(plan.GetProperty("noImportado").GetProperty("nominaPublicadaPresente").GetBoolean());

        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        // Entró al borrador, no al sitio.
        var publico = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/equipo-web");
        Assert.False(publico.GetProperty("published").GetBoolean());

        var admin = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/equipo-web");
        Assert.Equal(1, admin.GetProperty("members").GetArrayLength());
        Assert.Equal("Persona Uno", admin.GetProperty("members")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Una_nomina_invalida_no_entra_a_medias()
    {
        // La nómina es UN valor. Aplicar solo a las personas válidas dejaría una
        // nómina que nadie pidió y que el operador no podría distinguir de un fallo.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var respaldo = Respaldo(equipo: new
        {
            content = new[]
            {
                new { id = "team-1", group = "coordination", role = "R", name = "Persona Correcta", email = "uno@pnmc.gov.co", photo = "" },
                new { id = "team-2", group = "coordination", role = "R", name = "", email = "dos@pnmc.gov.co", photo = "" },
            },
            publishedContent = (object?)null,
            status = "borrador",
        });

        var plan = await SimularAsync(client, respaldo);
        var equipo = plan.GetProperty("equipo");

        Assert.False(equipo.GetProperty("porAplicar").GetBoolean());
        Assert.NotNull(equipo.GetProperty("motivoRechazo").GetString());
        var rechazadas = equipo.GetProperty("personasRechazadas");
        Assert.Equal(1, rechazadas.GetArrayLength());
        Assert.Equal("team-2", rechazadas[0].GetProperty("id").GetString());

        var aplicar = await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());
        Assert.Equal(HttpStatusCode.OK, aplicar.StatusCode);

        var admin = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/equipo-web");
        Assert.Equal(9, admin.GetProperty("members").GetArrayLength());
    }

    // ---------- La vuelta atrás ----------

    [Fact]
    public async Task La_instantanea_de_la_base_se_puede_reimportar_para_deshacer()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        // 1. Instantánea antes de tocar nada: esta es la vuelta atrás.
        var instantanea = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/contenido-web/backup");
        var original = instantanea.GetProperty("stores").GetProperty("pnmc_web_texts")
            .GetProperty("home_title").GetProperty("content").GetString();

        // 2. Una importación que cambia el texto.
        var respaldo = Respaldo(new Dictionary<string, object> { ["home_title"] = Texto("Texto importado") });
        var plan = await SimularAsync(client, respaldo);
        await AplicarAsync(client, respaldo, plan.GetProperty("planHash").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Equal("Texto importado", (await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title")).Draft);

        // 3. Reimportar la instantánea deshace el cambio.
        var vuelta = JsonSerializer.Deserialize<JsonElement>(instantanea.GetRawText());
        var planVuelta = await SimularAsync(client, vuelta, overwriteEdited: true);
        var deshacer = await AplicarAsync(client, vuelta, planVuelta.GetProperty("planHash").GetString(), overwriteEdited: true);
        Assert.Equal(HttpStatusCode.OK, deshacer.StatusCode);

        var restaurada = await db.ContenidoWeb.AsNoTracking().SingleAsync(x => x.Key == "home_title");
        Assert.Equal(original, restaurada.Draft);
    }

    // ---------- Sobre y permisos ----------

    [Theory]
    [InlineData("otra-app", 1)]
    [InlineData("pnmc-cms", 2)]
    [InlineData("pnmc-cms", 0)]
    public async Task Un_sobre_que_no_reconocemos_se_rechaza(string app, int schemaVersion)
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(
            "/api/v1/admin/contenido-web/import/preview",
            Respaldo(app: app, schemaVersion: schemaVersion));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Importar_exige_sesion()
    {
        using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/v1/admin/contenido-web/backup")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.PostAsJsonAsync("/api/v1/admin/contenido-web/import/preview", Respaldo())).StatusCode);
    }

    /// <summary>
    /// El gestor interno respalda el CMS, pero no lo importa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es el par que fija la divergencia deliberada de este fichero: el respaldo es
    /// <c>EditorRoles</c> y la aplicación es <c>ImporterRoles = [webmaster]</c>, por radio de
    /// daño y no por visibilidad. El 200 del respaldo es el control positivo del 403 de la
    /// importación; y si alguien «corrige» la importación a <c>EditorRoles</c>, este 403 se
    /// pone en rojo, que es exactamente lo que la nota de <c>ImporterRoles</c> pide.
    /// </para>
    /// <para>
    /// El vehículo era <c>aliado.admin@pnmc.local</c> hasta que el concepto de entidad aliada
    /// se retiró. <c>gestor_interno</c> lo sustituye y es más fiel: es
    /// un rol real del modelo, no uno que entraba por un descuido de la puerta.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_gestor_interno_respalda_pero_no_importa()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsync(factory, "gestor@pnmc.local", "pnmc-gestor");

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/admin/contenido-web/backup")).StatusCode);

        var aplicar = await AplicarAsync(client, Respaldo(), "loquesea");
        Assert.Equal(HttpStatusCode.Forbidden, aplicar.StatusCode);
    }
}
