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
/// Los eventos que una organización anuncia sobre sus propios procesos.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que la agenda pública se convierta en un tablón abierto donde
/// cualquiera con cuenta escribe; que un evento cuelgue de un proceso ajeno o de uno que todavía no
/// está publicado; y que quien escribe el evento decida también si se publica.
/// </para>
/// </summary>
public sealed class EventosDeOrganizacionTests
{
    private const string Clave = "ClaveExterna123";

    private static object EventoNuevo(int festivalId, string titulo = "Concierto de clausura") => new
    {
        titulo,
        descripcion = "Concierto abierto al público en el marco del festival.",
        fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
        modalidad = "presencial",
        lugar = "Plaza principal",
        festivalId,
    };

    [Fact]
    public async Task Sin_procesos_publicados_no_hay_nada_que_enmarcar()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "sin.procesos@example.com");

        var procesos = await (await client.GetAsync($"/api/v1/externo/organizaciones/{organizacionId}/procesos-que-enmarcan"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // SI ESTA LISTA VIENE VACIA, LA PANTALLA NO OFRECE EL FORMULARIO: es más honesto que dejar
        // escribir un evento entero para rechazarlo al guardar por algo que se sabía al principio.
        Assert.Equal(0, procesos.GetArrayLength());
    }

    [Fact]
    public async Task Un_festival_en_borrador_todavia_no_puede_enmarcar_eventos()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "festival.borrador@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: false);

        var procesos = await (await client.GetAsync($"/api/v1/externo/organizaciones/{organizacionId}/procesos-que-enmarcan"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // UN FESTIVAL SIN PUBLICAR NO EXISTE PARA EL PUBLICO: colgarle un evento lo anunciaría
        // antes que al propio Festival del que habla.
        Assert.Equal(0, procesos.GetArrayLength());

        var respuesta = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_un_festival_publicado_la_organizacion_puede_anunciar_su_evento()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "con.festival@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        var procesos = await (await client.GetAsync($"/api/v1/externo/organizaciones/{organizacionId}/procesos-que-enmarcan"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, procesos.GetArrayLength());
        Assert.Equal("festival", procesos[0].GetProperty("tipo").GetString());

        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        // NACE EN REVISION, SIEMPRE: quien lo escribe no decide si se publica.
        Assert.Equal("en_revision", cuerpo.GetProperty("estado").GetString());
    }

    /// <summary>
    /// Un evento con territorio se guarda, y su nivel de cobertura se deriva solo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL DEFECTO QUE TRAJO ESTA PRUEBA.</b> Este canal escribía el departamento y el municipio y
    /// <b>nunca escribía <c>NivelCobertura</c></b>, que nace en «nacional».
    /// <c>CK_EventosAgenda_NivelCobertura</c> exige que en nacional los dos códigos sean nulos, así
    /// que en cuanto alguien elegía un departamento —justo lo que el formulario ofrece— el INSERT
    /// chocaba con el CHECK y la pantalla decía «no fue posible anunciar el evento. Revisa los
    /// campos», sobre unos campos que estaban bien. Lo reportó la dirección de producto el 15 de
    /// septiembre de 2026.
    /// </para>
    /// <para>
    /// <b>LA REGLA YA EXISTIA Y LA CONSOLA LA APLICABA.</b> Dos canales que escriben la misma tabla
    /// y solo uno deriva la columna: la definición de «dos copias que divergen».
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("05", null, "departamental")]
    [InlineData("05", "05001", "municipal")]
    [InlineData(null, null, "nacional")]
    public async Task El_nivel_de_cobertura_se_deriva_del_territorio(
        string? departamento, string? municipio, string esperado)
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, $"territorio.{esperado}@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", new
        {
            titulo = $"Concierto {esperado}",
            descripcion = "Concierto abierto al público en el marco del festival.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Plaza principal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
            festivalId,
        });

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(cuerpo.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var evento = await db.EventosAgenda.AsNoTracking().FirstAsync(e => e.Id == id);
        Assert.Equal(esperado, evento.NivelCobertura);
    }

    [Fact]
    public async Task Un_municipio_sin_su_departamento_se_rechaza_con_su_motivo()
    {
        // NO ES UN TERRITORIO, y el formulario no deja llegar ahí; pero la ruta está abierta a
        // cualquiera con sesión externa, y sin esto el CHECK lo rechazaría como un 500 sin
        // explicación en vez de como un 400 sobre el campo.
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "municipio.huerfano@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        var respuesta = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", new
        {
            titulo = "Concierto sin departamento",
            descripcion = "Concierto abierto al público en el marco del festival.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Plaza principal",
            codigoMunicipio = "05001",
            festivalId,
        });
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("departamento", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_evento_sin_proceso_no_se_puede_anunciar()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "sin.proceso@example.com");
        await FestivalAsync(client, factory, organizacionId, publicado: true);

        var respuesta = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", new
        {
            titulo = "Evento suelto",
            descripcion = "Un evento que no pertenece a ningún proceso.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Plaza principal",
            festivalId = 0,
        });

        // ES LA REGLA ENTERA: sin proceso, la agenda pública sería un tablón abierto.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_colgar_un_evento_de_un_proceso_ajeno()
    {
        await using var factory = new TestWebApplicationFactory();
        var (dueña, organizacionPropia) = await OrganizacionAsync(factory, "dueña@example.com");
        var festivalAjeno = await FestivalAsync(dueña, factory, organizacionPropia, publicado: true);

        var (otra, organizacionOtra) = await OrganizacionAsync(factory, "otra@example.com");

        var respuesta = await EnviarAsync(otra, $"/api/v1/externo/organizaciones/{organizacionOtra}/eventos", EventoNuevo(festivalAjeno));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_evento_anunciado_llega_a_la_bandeja_institucional_con_su_procedencia()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "llega.bandeja@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos",
            EventoNuevo(festivalId, "Evento que espera revisión"));

        var consola = await CmsTestClient.LoginAsync(factory);
        var pendientes = await (await consola.GetAsync("/api/v1/institucional/agenda?estado=en_revision"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var suyo = pendientes.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("titulo").GetString() == "Evento que espera revisión");

        // NO ES UN SEGUNDO MODELO DE AGENDA: escribe la misma fila y aparece en la misma bandeja
        // donde la consola revisa todo lo demás.
        Assert.Equal("externo", suyo.GetProperty("procedencia").GetProperty("contextoOrigen").GetString());
        Assert.Equal(festivalId, suyo.GetProperty("festivalId").GetInt32());
    }

    [Fact]
    public async Task Dos_eventos_con_el_mismo_titulo_no_chocan()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "mismo.titulo@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        var primero = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));
        var segundo = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));

        // «Concierto de clausura» OCURRE TODOS LOS AÑOS. El índice único de la agenda rechazaría el
        // segundo con un error ilegible; se numera en vez de pedir un título distinto.
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
    }

    [Fact]
    public async Task La_organizacion_ve_sus_eventos_con_el_proceso_que_los_enmarca()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "ve.sus.eventos@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));

        var eventos = await (await client.GetAsync($"/api/v1/externo/organizaciones/{organizacionId}/eventos"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, eventos.GetArrayLength());
        Assert.Equal("En revisión", eventos[0].GetProperty("estadoEtiqueta").GetString());
        Assert.False(string.IsNullOrWhiteSpace(eventos[0].GetProperty("procesoNombre").GetString()));
    }

    // ---------- Andamio ----------------------------------------------------------

    /// <summary>
    /// Lo que una organización envía llega a la bandeja del Programa y se decide allí.
    /// </summary>
    /// <remarks>
    /// <b>EL DEFECTO QUE TRAJO ESTA PRUEBA.</b> El circuito ponía el evento «en revisión» y no había
    /// ninguna cola que lo listara: la única forma de encontrarlo era abrir Agenda y filtrar por
    /// estado. El criterio es este: «al enviar evento a
    /// revisión debería aparecer en la bandeja de Solicitudes y revisiones».
    /// </remarks>
    [Fact]
    public async Task El_evento_enviado_aparece_en_la_cola_del_Programa()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "cola.programa@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);
        (await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId, "Taller en la cola")))
            .EnsureSuccessStatusCode();

        var consola = await CmsTestClient.LoginAsync(factory);
        var cola = await consola.GetFromJsonAsync<JsonElement>("/api/v1/institucional/revision-de-eventos");

        var titulos = cola.EnumerateArray().Select(e => e.GetProperty("titulo").GetString()).ToList();
        Assert.Contains("Taller en la cola", titulos);
    }

    [Fact]
    public async Task Publicar_un_evento_al_que_le_falta_el_lugar_se_rechaza_con_su_motivo()
    {
        // `CK_EventosAgenda_DatosDeModalidad` SOLO APRIETA AL PUBLICAR: un presencial necesita lugar.
        // Sin comprobarlo antes, publicar saldría como un 500 sin explicación —el mismo defecto que
        // se corrigió ese día en el alta externa— en vez de decir qué falta.
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "sin.lugar@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);

        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", new
        {
            titulo = "Evento sin lugar",
            descripcion = "Un evento presencial al que le falta el lugar.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            festivalId,
        });
        creado.EnsureSuccessStatusCode();
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = cuerpo.GetProperty("id").GetString();

        var consola = await CmsTestClient.LoginAsync(factory);
        var decision = await consola.PostAsJsonAsync($"/api/v1/institucional/revision-de-eventos/{id}/decision",
            new DecisionSobreEvento { Decision = "publicar" });
        var respuesta = await decision.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, decision.StatusCode);
        Assert.Contains("lugar", respuesta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Devolver_sin_decir_que_corregir_no_es_una_decision()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "devolver.sin.motivo@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);
        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId, "Evento por devolver"));
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = cuerpo.GetProperty("id").GetString();

        var consola = await CmsTestClient.LoginAsync(factory);
        var sinMotivo = await consola.PostAsJsonAsync($"/api/v1/institucional/revision-de-eventos/{id}/decision",
            new DecisionSobreEvento { Decision = "devolver" });

        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
    }

    [Fact]
    public async Task El_circuito_del_evento_se_cierra_publicandolo()
    {
        await using var factory = new TestWebApplicationFactory();
        var (client, organizacionId) = await OrganizacionAsync(factory, "circuito.evento@example.com");
        var festivalId = await FestivalAsync(client, factory, organizacionId, publicado: true);
        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId, "Evento que se publica"));
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = cuerpo.GetProperty("id").GetString();

        var consola = await CmsTestClient.LoginAsync(factory);
        var decision = await consola.PostAsJsonAsync($"/api/v1/institucional/revision-de-eventos/{id}/decision",
            new DecisionSobreEvento { Decision = "publicar" });
        decision.EnsureSuccessStatusCode();

        // Y SALE DE LA COLA: una bandeja que conserva lo ya decidido deja de ser una bandeja.
        var cola = await consola.GetFromJsonAsync<JsonElement>("/api/v1/institucional/revision-de-eventos");
        Assert.DoesNotContain("Evento que se publica",
            cola.EnumerateArray().Select(e => e.GetProperty("titulo").GetString()).ToList());
    }

    private static async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(
        TestWebApplicationFactory factory, string correo)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de " + correo,
            FullName = "Responsable de la organización",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        var id = int.Parse(mias[0].GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return (client, id);
    }

    /// <summary>
    /// Un Festival de la organización, publicado o no.
    /// </summary>
    /// <remarks>
    /// PUBLICAR SE HACE CONTRA LA BASE Y NO POR EL CIRCUITO porque lo que esta prueba mide es la
    /// regla del evento, no el circuito de revisión del Festival, que tiene sus propias pruebas.
    /// </remarks>
    private static async Task<int> FestivalAsync(
        HttpClient client, TestWebApplicationFactory factory, int organizacionId, bool publicado)
    {
        var creado = await EnviarAsync(client, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre = "Festival de la organización " + organizacionId,
            nivelCobertura = "nacional",
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        creado.EnsureSuccessStatusCode();

        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        var id = int.Parse(cuerpo.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        if (publicado)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var festival = await db.FestivalRecords.SingleAsync(x => x.Id == id);
            festival.StatusCode = "publicado";
            await db.SaveChangesAsync();
        }

        return id;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string ruta, object cuerpo)
    {
        var testigo = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        return await client.SendAsync(mensaje);
    }
}
