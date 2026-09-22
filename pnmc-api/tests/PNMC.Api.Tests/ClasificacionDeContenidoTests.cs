using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La clasificación opcional de Agenda, Noticias y Catálogo Editorial por prácticas musicales y
/// territorios sonoros.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que un identificador inventado se guarde a medias o reviente como
/// un 500 ilegible; que un cliente que no conozca el campo borre en silencio una clasificación que
/// alguien se tomó el trabajo de hacer; y que retirar la última práctica sea imposible porque
/// «lista vacía» se confunda con «no dije nada».
/// </para>
/// </summary>
public sealed class ClasificacionDeContenidoTests
{
    /// <summary>La práctica y el territorio que siembra el arnés. Ver <see cref="TestWebApplicationFactory"/>.</summary>
    private const int PracticaSembrada = 1;
    private const int TerritorioSembrado = 1;

    /// <summary>Un identificador que no existe en ningún catálogo.</summary>
    private static readonly int[] PracticaInventada = [9_999];

    private static object EventoNuevo(object? practicas = null, object? territorios = null) => new
    {
        titulo = "Encuentro de bandas del altiplano",
        descripcion = "Encuentro abierto al público del ecosistema musical.",
        fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
        modalidad = "presencial",
        lugar = "Teatro Municipal de Paipa",
        practicasMusicalesIds = practicas,
        territoriosSonorosIds = territorios,
    };

    [Fact]
    public async Task Un_evento_puede_clasificarse_por_practica_y_territorio()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda",
            EventoNuevo(new[] { PracticaSembrada }, new[] { TerritorioSembrado })))
            .Content.ReadFromJsonAsync<JsonElement>();

        // VIAJA CON NOMBRE Y NO SOLO CON IDENTIFICADOR: es lo que permite pintar la ficha sin
        // pedir el catálogo entero para traducir un número.
        var practicas = creado.GetProperty("practicasMusicales");
        Assert.Equal(1, practicas.GetArrayLength());
        Assert.Equal(PracticaSembrada, practicas[0].GetProperty("id").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(practicas[0].GetProperty("nombre").GetString()));
        Assert.Equal(1, creado.GetProperty("territoriosSonoros").GetArrayLength());
    }

    [Fact]
    public async Task La_clasificacion_es_opcional()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync("/api/v1/institucional/agenda", EventoNuevo()))
            .Content.ReadFromJsonAsync<JsonElement>();

        // NO ES UN NULO NI UNA CLAVE AUSENTE: una lista vacía deja a quien consume sin tener que
        // distinguir dos formas de «no hay ninguna».
        Assert.Equal(0, creado.GetProperty("practicasMusicales").GetArrayLength());
        Assert.Equal(0, creado.GetProperty("territoriosSonoros").GetArrayLength());
    }

    [Fact]
    public async Task Una_practica_que_no_existe_se_rechaza_con_su_numero()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync("/api/v1/institucional/agenda", EventoNuevo(PracticaInventada));

        // NO SE IGNORA EN SILENCIO —guardaría una clasificación distinta de la enviada— NI SE DEJA
        // REVENTAR A LA FORANEA, que devuelve un 500 sin decir qué número estaba mal.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("9999", cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guardar_sin_hablar_de_clasificacion_no_la_borra()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo(new[] { PracticaSembrada })))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        // EL CUERPO NO LLEVA `practicasMusicalesIds`: es el cliente que no conoce el campo.
        var guardado = await (await client.PutAsJsonAsync($"/api/v1/institucional/agenda/{id}", new
        {
            titulo = "Encuentro de bandas del altiplano",
            descripcion = "Encuentro abierto al público del ecosistema musical.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
            version = creado.GetProperty("version").GetInt32(),
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, guardado.GetProperty("practicasMusicales").GetArrayLength());
    }

    [Fact]
    public async Task Una_lista_vacia_si_retira_la_clasificacion()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda", EventoNuevo(new[] { PracticaSembrada })))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = creado.GetProperty("id").GetInt64();

        var guardado = await (await client.PutAsJsonAsync($"/api/v1/institucional/agenda/{id}", new
        {
            titulo = "Encuentro de bandas del altiplano",
            descripcion = "Encuentro abierto al público del ecosistema musical.",
            fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
            modalidad = "presencial",
            lugar = "Teatro Municipal de Paipa",
            practicasMusicalesIds = Array.Empty<int>(),
            version = creado.GetProperty("version").GetInt32(),
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, guardado.GetProperty("practicasMusicales").GetArrayLength());
    }

    [Fact]
    public async Task Repetir_la_misma_practica_no_la_duplica()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creado = await (await client.PostAsJsonAsync(
            "/api/v1/institucional/agenda",
            EventoNuevo(new[] { PracticaSembrada, PracticaSembrada })))
            .Content.ReadFromJsonAsync<JsonElement>();

        // EL INDICE UNICO DE LA TABLA PUENTE LO IMPEDIRIA CON UN ERROR ILEGIBLE. Aquí se colapsa
        // antes de escribir, que es lo que espera quien pulsó dos veces la misma casilla.
        Assert.Equal(1, creado.GetProperty("practicasMusicales").GetArrayLength());
    }

    [Fact]
    public async Task Una_noticia_tambien_se_clasifica()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Se abre la convocatoria de bandas",
            resumen = "Las inscripciones quedan abiertas hasta fin de mes.",
            practicasMusicalesIds = new[] { PracticaSembrada },
            territoriosSonorosIds = new[] { TerritorioSembrado },
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, creada.GetProperty("practicasMusicales").GetArrayLength());
        Assert.Equal(1, creada.GetProperty("territoriosSonoros").GetArrayLength());
    }

    [Fact]
    public async Task Una_publicacion_editorial_tambien_se_clasifica()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync("/api/v1/institucional/catalogo-editorial", new
        {
            codigo = "CE-CLASIF-001",
            titulo = "Cartilla de iniciación a la banda",
            practicasMusicalesIds = new[] { PracticaSembrada },
        })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, creada.GetProperty("practicasMusicales").GetArrayLength());
        // EL TEXTO SUELTO DEL ACERVO NO SE PISA: transcribe lo que dijo la fuente y es otra cosa.
        Assert.True(creada.GetProperty("practicaMusical").ValueKind is JsonValueKind.Null or JsonValueKind.String);
    }
}
