using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Los proyectos transversales del Programa, y el contenido que se enlaza con ellos.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que enlazar un evento con Celebra la Música sea un dato que nadie
/// puede consultar; que se enlace contenido nuevo a una iniciativa que ya terminó; y que el
/// contenido ya publicado deje de decir a qué pertenecía cuando el proyecto se cierra.
/// </para>
/// </summary>
public sealed class ProyectosTransversalesTests
{
    private const string Publico = "/api/v1/publico/proyectos-transversales";
    private const string Consola = "/api/v1/institucional/proyectos-transversales";

    private static object EventoNuevo(string titulo, object? proyectos = null) => new
    {
        titulo,
        descripcion = "Evento del Programa.",
        fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
        modalidad = "presencial",
        lugar = "Teatro Municipal de Paipa",
        proyectosTransversalesIds = proyectos,
    };

    private static async Task<int> CelebraLaMusicaAsync(HttpClient client)
    {
        var lista = await (await client.GetAsync(Consola)).Content.ReadFromJsonAsync<JsonElement>();
        foreach (var proyecto in lista.GetProperty("items").EnumerateArray())
        {
            if (proyecto.GetProperty("codigo").GetString() == "celebra-la-musica")
            {
                return proyecto.GetProperty("id").GetInt32();
            }
        }
        throw new InvalidOperationException("La siembra de Celebra la Música no llegó.");
    }

    [Fact]
    public async Task El_portal_los_lee_sin_sesion()
    {
        await using var factory = new TestWebApplicationFactory();

        // ANONIMA A PROPOSITO: el sitio de una iniciativa arma su calendario antes de que exista
        // sesión, y no expone más que el nombre de un proyecto que ya es público.
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync(Publico)).StatusCode);
    }

    [Fact]
    public async Task Un_evento_puede_pertenecer_a_una_iniciativa_sin_dejar_de_tener_su_categoria()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Encuentro de Celebra", new[] { celebra })))
            .Content.ReadFromJsonAsync<JsonElement>();

        // SON DOS PREGUNTAS DISTINTAS: la categoría dice de qué trata, el proyecto a qué iniciativa
        // pertenece. Mezclarlas obligaría a inventar categorías como «Encuentros de Celebra».
        var proyectos = creado.GetProperty("proyectosTransversales");
        Assert.Equal(1, proyectos.GetArrayLength());
        Assert.Equal("Celebra la Música", proyectos[0].GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, creado.GetProperty("categoriaId").ValueKind);
    }

    [Fact]
    public async Task El_portal_puede_pedir_solo_el_calendario_de_una_iniciativa()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var conProyecto = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Evento de Celebra", new[] { celebra })))
            .Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"/api/v1/institucional/agenda/{conProyecto.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var sinProyecto = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Evento suelto")))
            .Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"/api/v1/institucional/agenda/{sinProyecto.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var filtrado = await (await factory.CreateClient().GetAsync("/api/v1/publico/agenda?proyecto=celebra-la-musica"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // ES LO QUE HACE UTIL EL ENLACE: sin el filtro, marcar el evento sería un dato que nadie
        // puede consultar, y el sitio de la iniciativa acabaría duplicando eventos.
        var titulos = filtrado.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("titulo").GetString()).ToList();
        Assert.Contains("Evento de Celebra", titulos);
        Assert.DoesNotContain("Evento suelto", titulos);
    }

    [Fact]
    public async Task Una_noticia_tambien_se_enlaza_y_se_filtra()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var creada = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Arranca Celebra la Música",
            resumen = "La iniciativa abre su convocatoria.",
            // EL CUERPO Y LA FECHA HACEN FALTA PARA PUBLICAR: son dos de las cuatro condiciones
            // que `ReglasDeNoticias` exige, y sin ellas el cambio de estado se rechaza.
            cuerpo = "La convocatoria queda abierta hasta fin de mes.",
            fechaPublicacion = DateOnly.FromDateTime(DateTime.UtcNow),
            proyectosTransversalesIds = new[] { celebra },
        })).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"/api/v1/institucional/noticias/{creada.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var filtrado = await (await factory.CreateClient().GetAsync("/api/v1/publico/noticias?proyecto=celebra-la-musica"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, filtrado.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task No_se_puede_enlazar_contenido_nuevo_a_una_iniciativa_cerrada()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        await client.PutAsJsonAsync($"{Consola}/{celebra}", new { nombre = "Celebra la Música", activo = false });

        var respuesta = await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Evento tardío", new[] { celebra }));

        // ENLAZAR A UNA INICIATIVA QUE YA TERMINO es casi siempre un descuido, y decirlo aquí es
        // más barato que descubrirlo cuando su calendario empiece a crecer solo.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cerrar_una_iniciativa_no_borra_lo_que_ya_pertenecia_a_ella()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Evento histórico de Celebra", new[] { celebra })))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        await client.PutAsJsonAsync($"{Consola}/{celebra}", new { nombre = "Celebra la Música", activo = false });

        var despues = await (await client.GetAsync($"/api/v1/institucional/agenda/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        // UN PROYECTO QUE TERMINA SE DESACTIVA, NO SE BORRA: el contenido publicado bajo él tiene
        // que seguir diciendo a qué pertenecía, o la explicación desaparece.
        Assert.Equal(1, despues.GetProperty("proyectosTransversales").GetArrayLength());
    }

    [Fact]
    public async Task Guardar_sin_hablar_de_proyectos_no_borra_el_enlace()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo("Evento con iniciativa", new[] { celebra })))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var guardado = await (await client.PutAsJsonAsync($"/api/v1/institucional/agenda/{id}", new
        {
            titulo = "Evento con iniciativa",
            descripcion = "Evento del Programa.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
            version = creado.GetProperty("version").GetInt32(),
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, guardado.GetProperty("proyectosTransversales").GetArrayLength());
    }

    [Fact]
    public async Task Administrar_los_proyectos_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PostAsJsonAsync(Consola, new { nombre = "Otro proyecto" })).StatusCode);
    }

    [Fact]
    public async Task El_codigo_sale_del_nombre_y_no_se_teclea()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(Consola, new { nombre = "Música en los Territorios" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        // DEJAR QUE SE TECLEE produce dos proyectos que solo difieren en un guion.
        Assert.Equal("musica-en-los-territorios", creado.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task Renombrar_no_cambia_el_codigo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);
        var celebra = await CelebraLaMusicaAsync(client);

        var guardado = await (await client.PutAsJsonAsync($"{Consola}/{celebra}", new
        {
            nombre = "Celebra la Música 2027",
            activo = true,
        })).Content.ReadFromJsonAsync<JsonElement>();

        // EL CODIGO ES LA DIRECCION con la que el portal filtra: cambiarla rompería los enlaces del
        // sitio de la iniciativa sin avisar a nadie.
        Assert.Equal("celebra-la-musica", guardado.GetProperty("codigo").GetString());
    }
}
