using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El circuito de Noticias, de borrador a portal.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que una noticia llegue al portal sin las cuatro cosas que la
/// hacen legible —título, resumen, cuerpo y fecha— y, sobre todo, QUE UNA FECHA FUTURA APAREZCA
/// ANTES DE TIEMPO. Fechar una noticia el lunes y que se vea el viernes convierte la fecha en
/// decoración, y es el fallo que no se nota hasta que alguien anuncia algo antes de hora.
/// </para>
/// <para>
/// SE PRUEBA CONTRA EL API REAL. Que las reglas estén bien escritas lo comprueba
/// <see cref="ReglasDeNoticiasTests"/>; aquí se comprueba que además están ENCHUFADAS.
/// </para>
/// </summary>
public sealed class NoticiasCircuitoTests
{
    private const string Consola = "/api/v1/institucional/noticias";
    private const string Publico = "/api/v1/publico/noticias";

    private static object NoticiaNueva(string titulo, DateOnly? fecha = null, string? cuerpo = "Cuerpo de la noticia institucional.") => new
    {
        titulo,
        resumen = "Resumen breve de la noticia.",
        cuerpo,
        fechaPublicacion = fecha,
        autoriaNombre = "Comunicaciones PNMC",
        etiquetas = new[] { "convivencia", "territorio" },
    };

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task La_consola_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(Consola)).StatusCode);
    }

    [Fact]
    public async Task El_portal_es_anonimo_y_empieza_vacio()
    {
        await using var factory = new TestWebApplicationFactory();
        var anonimo = factory.CreateClient();

        var respuesta = await anonimo.GetAsync(Publico);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        // NO SE SIEMBRA NADA: un portal que nace con noticias de demostración acaba enseñándolas.
        Assert.Equal(0, pagina.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Una_noticia_nace_en_borrador_con_su_direccion_calculada()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await client.PostAsJsonAsync(Consola, NoticiaNueva("Música para la Convivencia llega al Cauca"));

        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var noticia = await creada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("borrador", noticia.GetProperty("estado").GetString());
        Assert.Equal(1, noticia.GetProperty("version").GetInt32());
        // LA DIRECCION SALE DEL TITULO, sin tildes ni signos: es la que se comparte.
        Assert.Equal("musica-para-la-convivencia-llega-al-cauca", noticia.GetProperty("slug").GetString());
        Assert.Equal(2, noticia.GetProperty("etiquetas").GetArrayLength());
    }

    [Fact]
    public async Task Dos_noticias_no_comparten_direccion()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, NoticiaNueva("Encuentro nacional de bandas"));
        var repetida = await client.PostAsJsonAsync(Consola, NoticiaNueva("Encuentro nacional de bandas"));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task Guardar_con_una_version_vieja_se_rechaza_y_no_escribe()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, NoticiaNueva("Primera versión"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var primera = await client.PutAsJsonAsync($"{Consola}/{id}", new { titulo = "Título corregido", resumen = "Resumen.", version = 1 });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);

        var segunda = await client.PutAsJsonAsync($"{Consola}/{id}", new { titulo = "Título que pisaría", resumen = "Resumen.", version = 1 });
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);

        var vigente = await (await client.GetAsync($"{Consola}/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Título corregido", vigente.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task La_direccion_no_cambia_aunque_cambie_el_titulo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, NoticiaNueva("Titulo con herror"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        var slug = creada.GetProperty("slug").GetString();

        var guardada = await (await client.PutAsJsonAsync($"{Consola}/{id}",
            new { titulo = "Título con error corregido", resumen = "Resumen.", version = 1 })).Content.ReadFromJsonAsync<JsonElement>();

        // UN ENLACE COMPARTIDO NO PUEDE DEJAR DE FUNCIONAR porque alguien corrigiera una tilde.
        Assert.Equal(slug, guardada.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Publicar_dice_TODO_lo_que_falta_y_no_solo_lo_primero()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola,
            NoticiaNueva("Noticia incompleta", fecha: null, cuerpo: null))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        Assert.Equal(HttpStatusCode.BadRequest, publicar.StatusCode);
        var mensaje = await publicar.Content.ReadAsStringAsync();
        // LAS DOS FALTAS EN UN SOLO MENSAJE: quien redacta prefiere una lista a dos intentos.
        Assert.Contains("no tiene cuerpo", mensaje, StringComparison.Ordinal);
        Assert.Contains("no tiene fecha de publicación", mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_noticia_completa_se_publica_y_aparece_en_el_portal()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola,
            NoticiaNueva("Bandas del Pacífico se encuentran en Buenaventura", Hoy))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        var slug = creada.GetProperty("slug").GetString();

        var publicar = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });
        Assert.Equal(HttpStatusCode.OK, publicar.StatusCode);

        var anonimo = factory.CreateClient();
        var pagina = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, pagina.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Una_noticia_fechada_en_el_futuro_no_viaja_todavia()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola,
            NoticiaNueva("Convocatoria que abre el lunes", Hoy.AddDays(7)))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        var slug = creada.GetProperty("slug").GetString();

        // PUBLICARLA SI SE PUEDE: la decisión ya está tomada. Lo que no ocurre es que se vea antes
        // de su fecha, que es justo para lo que sirve fecharla.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" })).StatusCode);

        var anonimo = factory.CreateClient();
        var pagina = await (await anonimo.GetAsync(Publico)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, pagina.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);

        // Y SIGUE ENTERA EN LA CONSOLA: no se ha perdido, solo no ha llegado su día.
        var enLaConsola = await client.GetAsync($"{Consola}/{id}");
        Assert.Equal(HttpStatusCode.OK, enLaConsola.StatusCode);

        // PERO LA CONSOLA NO LA LLAMA «PUBLICADA», PORQUE NO LO ESTA. El estado guardado sigue
        // siendo `publicado` —es un dato correcto: así se programa una publicación— y el que se
        // enseña es `programada`. Decirle «Publicado» hacía creer que ya estaba en el sitio, que es
        // justo lo contrario de lo que acaba de comprobar esta prueba tres líneas más arriba.
        var ficha = await enLaConsola.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("publicado", ficha.GetProperty("estado").GetString());
        Assert.Equal("programada", ficha.GetProperty("estadoEfectivo").GetString());
    }

    [Fact]
    public async Task Cuando_llega_su_fecha_la_noticia_deja_de_estar_programada()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola,
            NoticiaNueva("Convocatoria que abre hoy", Hoy))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" })).StatusCode);

        var ficha = await (await client.GetAsync($"{Consola}/{id}")).Content.ReadFromJsonAsync<JsonElement>();

        // LA OTRA MITAD DE LA REGLA, y hace falta: un `estadoEfectivo` que dijera «programada»
        // siempre pasaría la prueba anterior y rompería todas las noticias del sitio.
        Assert.Equal("publicado", ficha.GetProperty("estadoEfectivo").GetString());
    }

    [Fact]
    public async Task Archivar_la_saca_del_portal_sin_borrarla()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola,
            NoticiaNueva("Noticia que se archiva", Hoy))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();
        var slug = creada.GetProperty("slug").GetString();
        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "publicado" });

        await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "archivado" });

        var anonimo = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"{Publico}/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Consola}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Un_estado_que_no_existe_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var creada = await (await client.PostAsJsonAsync(Consola, NoticiaNueva("Estado inventado"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = creada.GetProperty("id").GetInt64();

        var respuesta = await client.PostAsJsonAsync($"{Consola}/{id}/estado", new { estado = "destacada" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_portal_filtra_por_etiqueta()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var primera = await (await client.PostAsJsonAsync(Consola, NoticiaNueva("Noticia con etiquetas", Hoy))).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync($"{Consola}/{primera.GetProperty("id").GetInt64()}/estado", new { estado = "publicado" });

        var anonimo = factory.CreateClient();
        var conEtiqueta = await (await anonimo.GetAsync($"{Publico}?etiqueta=convivencia")).Content.ReadFromJsonAsync<JsonElement>();
        var sinEtiqueta = await (await anonimo.GetAsync($"{Publico}?etiqueta=deporte")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, conEtiqueta.GetProperty("total").GetInt32());
        Assert.Equal(0, sinEtiqueta.GetProperty("total").GetInt32());
    }
}

/// <summary>
/// Las reglas de Noticias, sin base de datos.
/// </summary>
public sealed class ReglasDeNoticiasTests
{
    [Theory]
    [InlineData("Música para la Convivencia", "musica-para-la-convivencia")]
    [InlineData("  Bandas del Pacífico: 2026  ", "bandas-del-pacifico-2026")]
    [InlineData("¿Qué es el PNMC?", "que-es-el-pnmc")]
    [InlineData("Ñandú — ópera", "nandu-opera")]
    public void La_direccion_sale_del_titulo_sin_tildes_ni_signos(string titulo, string esperada)
    {
        Assert.Equal(esperada, ReglasDeNoticias.SlugDesde(titulo));
    }

    [Fact]
    public void Publicar_enumera_todas_las_faltas()
    {
        var faltas = ReglasDeNoticias.LoQueFaltaParaPublicar(null, null, null, null);

        Assert.Equal(4, faltas.Count);
    }

    [Fact]
    public void Una_noticia_completa_no_tiene_faltas()
    {
        var faltas = ReglasDeNoticias.LoQueFaltaParaPublicar("Título", "Resumen", "Cuerpo", new DateOnly(2026, 9, 11));

        Assert.Empty(faltas);
    }

    [Theory]
    [InlineData("publicado", 0, true)]
    [InlineData("publicado", -3, true)]
    [InlineData("publicado", 1, false)]
    [InlineData("borrador", 0, false)]
    [InlineData("en_revision", 0, false)]
    [InlineData("archivado", -3, false)]
    public void La_visibilidad_exige_estado_publicado_y_fecha_llegada(string estado, int diasDesdeHoy, bool visible)
    {
        var hoy = new DateOnly(2026, 9, 11);

        Assert.Equal(visible, ReglasDeNoticias.EsVisiblePublicamente(estado, hoy.AddDays(diasDesdeHoy), hoy));
    }

    [Fact]
    public void Sin_fecha_no_es_visible_aunque_este_publicada()
    {
        Assert.False(ReglasDeNoticias.EsVisiblePublicamente("publicado", null, new DateOnly(2026, 9, 11)));
    }
}
