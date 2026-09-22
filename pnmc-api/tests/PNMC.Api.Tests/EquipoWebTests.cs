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
/// Incremento 2B del CMS: la nómina del equipo con fotografías.
/// </summary>
public sealed class EquipoWebTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public EquipoWebTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // Una imagen WebP mínima en base64. Sirve para comprobar el camino feliz sin
    // depender de un archivo de prueba en disco.
    private const string FotoValida =
        "data:image/webp;base64,UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==";

    private static Task<HttpClient> LoginAsWebmasterAsync(TestWebApplicationFactory factory)
        => CmsTestClient.LoginAsync(factory);

    private Task<HttpClient> LoginAsWebmasterAsync() => LoginAsWebmasterAsync(_factory);

    private static async Task<AdminTeamResponse> LeerNominaAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/equipo-web");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminTeamResponse>())!;
    }

    private static MemberDto Persona(string id, string nombre = "Persona de prueba", string foto = "")
        => new(id, "leadership", "Cargo de prueba", nombre, "persona@pnmc.gov.co", foto);

    // ---------- Siembra ----------

    [Fact]
    public async Task La_nomina_se_siembra_con_las_personas_del_catalogo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var fila = await db.EquipoWeb.AsNoTracking().SingleAsync();
        var personas = ContratoDeEquipoWeb.Deserialize(fila.Draft);

        Assert.Equal(SembradorDeContenidoWeb.LoadTeamDefaults().Count, personas.Count);
        Assert.All(personas, persona => Assert.NotEmpty(persona.Name));
        // Las dos primeras son coordinación; el resto, liderazgo.
        Assert.Equal(2, personas.Count(p => p.Group == "coordination"));
    }

    [Fact]
    public async Task La_siembra_omite_las_vacantes()
    {
        // TEAM_DEFAULTS trae un componente sin persona asignada. Sembrarlo dejaría
        // una tarjeta sin nombre en el sitio público.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var personas = ContratoDeEquipoWeb.Deserialize((await db.EquipoWeb.AsNoTracking().SingleAsync()).Draft);
        Assert.DoesNotContain(personas, persona => string.IsNullOrWhiteSpace(persona.Name));
    }

    [Fact]
    public async Task Sembrar_la_nomina_no_la_publica()
    {
        // Fábrica propia: esta prueba mira el estado recién sembrado, y las demás
        // publican sobre la fila compartida.
        using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        var publico = await anonimo.GetFromJsonAsync<PublicTeamResponse>("/api/v1/equipo-web");

        Assert.False(publico!.Published);
        Assert.Null(publico.Members);
    }

    [Fact]
    public async Task La_siembra_de_la_nomina_es_idempotente_y_no_pisa_lo_editado()
    {
        using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        var fila = await db.EquipoWeb.SingleAsync();
        fila.Draft = ContratoDeEquipoWeb.Serialize([new MiembroDelEquipoWeb("team-1", "coordination", "Cargo", "Editado a mano", "", "")]);
        fila.Version++;
        await db.SaveChangesAsync();
        var versionEditada = fila.Version;

        await SembradorDeEquipoWeb.EnsureSeededAsync(db, logger);

        var recargada = await db.EquipoWeb.AsNoTracking().SingleAsync();
        Assert.Equal(versionEditada, recargada.Version);
        Assert.Single(ContratoDeEquipoWeb.Deserialize(recargada.Draft));
        Assert.Equal(1, await db.EquipoWeb.CountAsync());
    }

    // ---------- Lectura pública ----------

    [Fact]
    public async Task El_publico_distingue_una_nomina_vacia_de_una_sin_publicar()
    {
        // El caso que el contrato explícito existe para resolver: `members: null`
        // es «use la nómina compilada»; `members: []` es «no muestre a nadie».
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);
        var anonimo = factory.CreateClient();

        var antes = await anonimo.GetFromJsonAsync<PublicTeamResponse>("/api/v1/equipo-web");
        Assert.False(antes!.Published);
        Assert.Null(antes.Members);

        var estado = await LeerNominaAsync(client);
        var guardado = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = Array.Empty<MemberDto>(),
            publish = true,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);

        var despues = await anonimo.GetFromJsonAsync<PublicTeamResponse>("/api/v1/equipo-web");
        Assert.True(despues!.Published);
        Assert.NotNull(despues.Members);
        Assert.Empty(despues.Members!);
    }

    [Fact]
    public async Task Guardar_sin_publicar_no_cambia_lo_que_ve_el_visitante()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);
        var anonimo = factory.CreateClient();

        var estado = await LeerNominaAsync(client);
        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", "Solo en borrador") },
            publish = false,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var publico = await anonimo.GetFromJsonAsync<PublicTeamResponse>("/api/v1/equipo-web");
        Assert.False(publico!.Published);

        // Pero el editor sí lo ve.
        var recargada = await LeerNominaAsync(client);
        Assert.Single(recargada.Members);
        Assert.Equal("Solo en borrador", recargada.Members[0].Name);
    }

    [Fact]
    public async Task Publicar_pone_la_nomina_en_el_sitio()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);
        var anonimo = factory.CreateClient();

        var estado = await LeerNominaAsync(client);
        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", "Jorge Enrique Sossa Santos", FotoValida) },
            publish = true,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var publico = await anonimo.GetFromJsonAsync<PublicTeamResponse>("/api/v1/equipo-web");
        Assert.True(publico!.Published);
        var persona = Assert.Single(publico.Members!);
        Assert.Equal("Jorge Enrique Sossa Santos", persona.Name);
        // La fotografía sobrevive intacta al viaje de ida y vuelta.
        Assert.Equal(FotoValida, persona.Photo);
    }

    // ---------- Concurrencia ----------

    [Fact]
    public async Task Guardar_sin_version_devuelve_400_y_no_409()
    {
        // Son dos fallos distintos: «no me dijo desde qué versión edita» no es
        // «alguien se le adelantó». Confundirlos haría que el panel ofreciera
        // recargar cuando el problema es suyo.
        var client = await LoginAsWebmasterAsync();
        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1") },
            publish = false,
            version = (int?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("version", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_version_vieja_devuelve_409()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);

        var estado = await LeerNominaAsync(client);
        var primero = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", "Primera edición") },
            publish = false,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);

        // Segunda persona guardando desde la versión que leyó antes del cambio.
        var segundo = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", "Segunda edición") },
            publish = false,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);

        var recargada = await LeerNominaAsync(client);
        Assert.Equal("Primera edición", recargada.Members[0].Name);
    }

    [Fact]
    public async Task Guardar_lo_mismo_no_sube_la_version()
    {
        // El panel envía la nómina entera en cada guardado. Sin este predicado,
        // cada clic subiría la versión y el siguiente editor chocaría sin motivo.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);

        var estado = await LeerNominaAsync(client);
        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = estado.Members,
            publish = false,
            version = estado.Version,
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var resultado = await respuesta.Content.ReadFromJsonAsync<SaveResponse>();
        Assert.False(resultado!.Changed);
        Assert.Equal(estado.Version, resultado.Version);
    }

    // ---------- Permisos ----------

    [Fact]
    public async Task La_nomina_de_administracion_exige_sesion()
    {
        var anonimo = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/admin/equipo-web")).StatusCode);

        var guardado = await anonimo.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1") },
            publish = true,
            version = 1,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, guardado.StatusCode);
    }

    /// <summary>
    /// El gestor interno guarda la nómina en borrador, pero no la publica.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes esta prueba usaba <c>aliado.admin@pnmc.local</c> y esperaba 403 en las dos
    /// llamadas. Al retirarse el concepto de entidad aliada (22 ago 2026) desapareció ese
    /// vehículo, y con él la única forma de llegar a un guarda de rol con sesión válida. El
    /// sustituto es <c>gestor_interno</c>, que es el rol «Funcionario» del modelo: entra con
    /// pleno derecho y aun así no alcanza <c>PublisherRoles</c>.
    /// </para>
    /// <para>
    /// Por eso el primer aserto cambió de 403 a 200: <b>no es una relajación</b>, es que la
    /// lectura y el borrador de la nómina sí le corresponden. Ese 200 es además el control
    /// positivo del 403 de abajo: sin él, el 403 podría venir de no tener sesión.
    /// </para>
    /// <para>
    /// El token antiforgery va puesto a propósito. Sin él la respuesta sería 400 y la prueba
    /// pasaría sin llegar nunca a mirar el rol.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Un_gestor_interno_guarda_el_borrador_pero_no_publica_la_nomina()
    {
        var client = await CmsTestClient.LoginAsync(_factory, "gestor@pnmc.local", "pnmc-gestor");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/equipo-web")).StatusCode);

        var borrador = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1") },
            publish = false,
            version = 1,
        });
        Assert.Equal(HttpStatusCode.OK, borrador.StatusCode);

        var publicado = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1") },
            publish = true,
            version = 2,
        });
        Assert.Equal(HttpStatusCode.Forbidden, publicado.StatusCode);
    }

    // ---------- Topes y validación ----------

    [Fact]
    public async Task Se_rechaza_una_fotografia_demasiado_grande()
    {
        var client = await LoginAsWebmasterAsync();
        var estado = await LeerNominaAsync(client);
        var enorme = "data:image/webp;base64," + new string('A', ContratoDeEquipoWeb.MaxPhotoChars);

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", foto: enorme) },
            publish = false,
            version = estado.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("fotograf", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("no-soy-un-data-uri")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("data:image/webp;base64,no-es-base64!!")]
    [InlineData("data:image/webp;base64,AAA=AAAA")]
    public async Task Se_rechaza_una_fotografia_que_no_es_lo_que_dice(string foto)
    {
        var client = await LoginAsWebmasterAsync();
        var estado = await LeerNominaAsync(client);

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[] { Persona("team-1", foto: foto) },
            publish = false,
            version = estado.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Se_rechazan_mas_personas_de_las_admitidas()
    {
        var client = await LoginAsWebmasterAsync();
        var estado = await LeerNominaAsync(client);
        var demasiadas = Enumerable.Range(1, ContratoDeEquipoWeb.MaxMembers + 1)
            .Select(i => Persona($"team-{i}"))
            .ToArray();

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = demasiadas,
            publish = false,
            version = estado.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_texto_de_la_nomina_tiene_su_propio_tope()
    {
        // Cada persona cabe holgadamente, pero la suma no. Sin el tope agregado,
        // 40 fichas al máximo pasarían una a una.
        var client = await LoginAsWebmasterAsync();
        var estado = await LeerNominaAsync(client);
        var largas = Enumerable.Range(1, ContratoDeEquipoWeb.MaxMembers)
            .Select(i => new MemberDto(
                $"team-{i}",
                "leadership",
                new string('c', ContratoDeEquipoWeb.MaxRoleLength),
                new string('n', ContratoDeEquipoWeb.MaxNameLength),
                new string('e', ContratoDeEquipoWeb.MaxEmailLength - 12) + "@pnmc.gov.co",
                ""))
            .ToArray();

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = largas,
            publish = false,
            version = estado.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(
            ContratoDeEquipoWeb.MaxTextChars.ToString(),
            await respuesta.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Theory]
    // Identificador repetido: dos tarjetas que el panel no podría distinguir.
    [InlineData("repetido")]
    // Grupo desconocido: la página los reparte en dos columnas y no habría tercera.
    [InlineData("grupo")]
    // Sin nombre: una tarjeta en blanco en el sitio público.
    [InlineData("sin-nombre")]
    // Correo sin arroba.
    [InlineData("correo")]
    // Marcado en el texto, misma regla que los 238 textos.
    [InlineData("menor-que")]
    public async Task Se_rechaza_una_nomina_mal_formada(string caso)
    {
        var client = await LoginAsWebmasterAsync();
        var estado = await LeerNominaAsync(client);

        MemberDto[] members = caso switch
        {
            "repetido" => [Persona("team-1", "Una"), Persona("team-1", "Otra")],
            "grupo" => [Persona("team-1") with { Group = "direccion" }],
            "sin-nombre" => [Persona("team-1") with { Name = "   " }],
            "correo" => [Persona("team-1") with { Email = "sin arroba" }],
            "menor-que" => [Persona("team-1") with { Name = "<script>alert(1)</script>" }],
            _ => throw new ArgumentOutOfRangeException(nameof(caso)),
        };

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members,
            publish = false,
            version = estado.Version,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_guardado_invalido_no_toca_la_fila()
    {
        // La nómina se guarda entera o no se guarda: nadie debe quedarse con media
        // nómina publicada porque la séptima persona traía una foto rota.
        using var factory = new TestWebApplicationFactory();
        var client = await LoginAsWebmasterAsync(factory);
        var antes = await LeerNominaAsync(client);

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[]
            {
                Persona("team-1", "Persona correcta"),
                Persona("team-2", foto: "data:image/webp;base64,roto!"),
            },
            publish = true,
            version = antes.Version,
        });
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var despues = await LeerNominaAsync(client);
        Assert.Equal(antes.Version, despues.Version);
        Assert.Equal(antes.Members.Count, despues.Members.Count);
    }

    [Fact]
    public void La_nomina_compilada_cumple_los_topes_de_la_api()
    {
        // Si el catálogo trajera algo que la API rechaza, la siembra dejaría una
        // nómina que nadie podría volver a guardar desde el panel.
        var errores = ContratoDeEquipoWeb.Validate(SembradorDeContenidoWeb.LoadTeamDefaults(), out var normalizada);

        Assert.Empty(errores);
        Assert.Equal(SembradorDeContenidoWeb.LoadTeamDefaults().Count, normalizada.Count);
    }

    // ---------- Contratos de respuesta ----------

    /// <summary>
    /// El panel lee los topes de aquí en vez de escribirlos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Los llevaba escritos a mano y se quedó en 120 para los tres campos
    /// mientras el servidor ya aceptaba 160 en el cargo y 180 en el correo: un
    /// cargo institucional largo <b>no se podía ni teclear</b>, porque el
    /// atributo del navegador cortaba antes de que nadie pudiera avisar.
    /// </para>
    /// <para>
    /// Nada lo delataba: las dos validaciones nunca se comparan entre sí, y cada
    /// una pasaba sus propias pruebas. Esta comprobación es la que las ata —si
    /// alguien cambia un tope en el contrato y no lo publica aquí, el panel
    /// vuelve a quedarse atrás y esta prueba lo dice.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task El_borrador_publica_los_topes_que_el_panel_necesita()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var borrador = await client.GetFromJsonAsync<AdminTeamResponse>("/api/v1/admin/equipo-web");

        Assert.NotNull(borrador!.Limits);
        Assert.Equal(ContratoDeEquipoWeb.MaxMembers, borrador.Limits!.MaxMembers);
        Assert.Equal(ContratoDeEquipoWeb.MaxPhotoChars, borrador.Limits.MaxPhotoChars);
        Assert.Equal(ContratoDeEquipoWeb.MaxSerializedChars, borrador.Limits.MaxSerializedChars);
        Assert.Equal(ContratoDeEquipoWeb.MaxNameLength, borrador.Limits.MaxNameLength);
        Assert.Equal(ContratoDeEquipoWeb.MaxRoleLength, borrador.Limits.MaxRoleLength);
        Assert.Equal(ContratoDeEquipoWeb.MaxEmailLength, borrador.Limits.MaxEmailLength);
    }

    /// <summary>
    /// Un cargo de más de 120 caracteres se guarda, que es el caso que el panel
    /// impedía escribir.
    /// </summary>
    [Fact]
    public async Task Acepta_un_cargo_mas_largo_que_el_viejo_tope_del_panel()
    {
        using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var borrador = await client.GetFromJsonAsync<AdminTeamResponse>("/api/v1/admin/equipo-web");

        var cargoLargo = new string('x', 140);
        Assert.True(cargoLargo.Length > 120, "el caso deja de tener sentido si no supera el tope viejo");
        Assert.True(cargoLargo.Length <= ContratoDeEquipoWeb.MaxRoleLength);

        var respuesta = await client.PostAsJsonAsync("/api/v1/admin/equipo-web", new
        {
            members = new[]
            {
                new { id = "prueba", group = "leadership", role = cargoLargo, name = "Persona", email = "", photo = "" },
            },
            publish = false,
            version = borrador!.Version,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private sealed record MemberDto(
        string Id, string Group, string Role, string Name, string Email, string Photo);

    private sealed class PublicTeamResponse
    {
        public bool Published { get; set; }
        public List<MemberDto>? Members { get; set; }
    }

    private sealed class AdminTeamResponse
    {
        public List<MemberDto> Members { get; set; } = new();
        public List<MemberDto>? PublishedMembers { get; set; }
        public string State { get; set; } = string.Empty;
        public int Version { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public TeamLimitsDto? Limits { get; set; }
    }

    private sealed class TeamLimitsDto
    {
        public int MaxMembers { get; set; }
        public int MaxPhotoChars { get; set; }
        public int MaxSerializedChars { get; set; }
        public int MaxNameLength { get; set; }
        public int MaxRoleLength { get; set; }
        public int MaxEmailLength { get; set; }
    }

    private sealed class SaveResponse
    {
        public bool Changed { get; set; }
        public bool Published { get; set; }
        public int Count { get; set; }
        public int Version { get; set; }
    }
}
