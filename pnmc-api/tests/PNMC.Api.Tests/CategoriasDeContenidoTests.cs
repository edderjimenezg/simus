using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Las categorías temáticas, y lo que el módulo `comun` resuelve.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que vuelva el texto libre por la puerta de atrás —dos categorías
/// que se llaman igual en el mismo módulo—, que una categoría compartida haya que duplicarla en
/// los tres formularios, y que se pueda borrar una que alguien está usando.
/// </para>
/// </summary>
public sealed class CategoriasDeContenidoTests
{
    private const string Consola = "/api/v1/institucional/categorias-contenido";
    private const string Publico = "/api/v1/publico/categorias-contenido";

    private static object Categoria(string modulo, string nombre) => new
    {
        codigoModulo = modulo,
        nombreCategoria = nombre,
    };

    [Fact]
    public async Task Administrar_categorias_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();

        var respuesta = await factory.CreateClient().PostAsJsonAsync(Consola, Categoria("agenda", "Encuentros"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_portal_las_lee_sin_sesion()
    {
        await using var factory = new TestWebApplicationFactory();

        // ANONIMA A PROPOSITO: el portal construye sus barras de filtro antes de que haya sesión.
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync(Publico)).StatusCode);
    }

    [Fact]
    public async Task Una_categoria_nueva_va_al_final_de_su_modulo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var primera = await (await client.PostAsJsonAsync(Consola, Categoria("agenda", "Encuentros"))).Content.ReadFromJsonAsync<JsonElement>();
        var segunda = await (await client.PostAsJsonAsync(Consola, Categoria("agenda", "Festivales"))).Content.ReadFromJsonAsync<JsonElement>();

        // PEDIR EL ORDEN A MANO EN EL ALTA obligaría a saber cuántas hay antes de escribir la
        // primera letra. Va al final, que es donde se espera ver lo recién creado.
        Assert.Equal(1, primera.GetProperty("ordenVisualizacion").GetInt32());
        Assert.Equal(2, segunda.GetProperty("ordenVisualizacion").GetInt32());
        // Y su dirección sale del nombre, sin tildes.
        Assert.Equal("encuentros", primera.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task No_puede_haber_dos_con_el_mismo_nombre_en_un_modulo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, Categoria("noticias", "Convocatorias"));
        var repetida = await client.PostAsJsonAsync(Consola, Categoria("noticias", "Convocatorias"));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task El_mismo_nombre_si_puede_existir_en_dos_modulos_distintos()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, Categoria("noticias", "Encuentros"));
        var enAgenda = await client.PostAsJsonAsync(Consola, Categoria("agenda", "Encuentros"));

        // «Encuentros» significa lo mismo pero se administra por separado mientras no se declare
        // común: la unicidad es por módulo, no global.
        Assert.Equal(HttpStatusCode.Created, enAgenda.StatusCode);
    }

    [Fact]
    public async Task Una_categoria_comun_aparece_en_los_tres_modulos()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PostAsJsonAsync(Consola, Categoria("comun", "Bandas"));
        await client.PostAsJsonAsync(Consola, Categoria("agenda", "Festivales"));

        var anonimo = factory.CreateClient();
        foreach (var modulo in new[] { "agenda", "noticias", "editorial" })
        {
            var pagina = await (await anonimo.GetAsync($"{Publico}?modulo={modulo}")).Content.ReadFromJsonAsync<JsonElement>();
            var nombres = pagina.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("nombreCategoria").GetString()).ToList();

            // ES LO QUE HACE QUE DECLARARLA COMUN SIRVA DE ALGO: aparece en los tres formularios
            // sin que nadie tenga que duplicarla tres veces.
            Assert.Contains("Bandas", nombres);
        }

        // Y lo propio de un módulo no se cuela en los otros.
        var enNoticias = await (await anonimo.GetAsync($"{Publico}?modulo=noticias")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain("Festivales", enNoticias.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("nombreCategoria").GetString()));
    }

    [Fact]
    public async Task Un_modulo_inventado_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PostAsJsonAsync(Consola, Categoria("galeria", "Fotografía"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Renombrar_una_categoria_arrastra_a_todo_lo_que_la_usa()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var categoria = await (await client.PostAsJsonAsync(Consola, Categoria("noticias", "Convocatorias"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = categoria.GetProperty("id").GetInt32();

        var noticia = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Una convocatoria", resumen = "Resumen.", cuerpo = "Cuerpo.",
            categoriaId = id, fechaPublicacion = DateOnly.FromDateTime(DateTime.UtcNow),
        })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Convocatorias", noticia.GetProperty("categoria").GetString());

        await client.PutAsJsonAsync($"{Consola}/{id}", new { codigoModulo = "noticias", nombreCategoria = "Convocatorias abiertas" });

        // ES JUSTAMENTE PARA LO QUE EXISTE EL CATALOGO: corregir un nombre mal escrito sin abrir
        // una por una las fichas que lo llevaban.
        var releida = await (await client.GetAsync($"/api/v1/institucional/noticias/{noticia.GetProperty("id").GetInt64()}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Convocatorias abiertas", releida.GetProperty("categoria").GetString());
    }

    [Fact]
    public async Task No_se_borra_una_categoria_que_alguien_esta_usando()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var categoria = await (await client.PostAsJsonAsync(Consola, Categoria("noticias", "Memoria"))).Content.ReadFromJsonAsync<JsonElement>();
        var id = categoria.GetProperty("id").GetInt32();
        await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Una nota de memoria", resumen = "Resumen.", categoriaId = id,
        });

        var borrada = await client.DeleteAsync($"{Consola}/{id}");

        // LA FORANEA LO IMPEDIRIA IGUAL, pero con un error de base de datos que no explica nada.
        Assert.Equal(HttpStatusCode.BadRequest, borrada.StatusCode);
        Assert.Contains("en uso", await borrada.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_categoria_sin_usar_si_se_borra()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var categoria = await (await client.PostAsJsonAsync(Consola, Categoria("editorial", "Provisional"))).Content.ReadFromJsonAsync<JsonElement>();

        var borrada = await client.DeleteAsync($"{Consola}/{categoria.GetProperty("id").GetInt32()}");

        Assert.Equal(HttpStatusCode.NoContent, borrada.StatusCode);
    }

    [Fact]
    public async Task La_lista_dice_cuanto_contenido_usa_cada_categoria()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var categoria = await (await client.PostAsJsonAsync(Consola, Categoria("noticias", "Convocatorias de prueba")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = categoria.GetProperty("id").GetInt32();

        await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Una noticia clasificada",
            resumen = "Usa la categoría.",
            categoriaId = id,
        });

        var lista = await (await client.GetAsync(Consola)).Content.ReadFromJsonAsync<JsonElement>();
        var laSuya = lista.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("id").GetInt32() == id);

        // SIN ESTA CIFRA, elegir entre categorías propias de cada módulo y categorías comunes es
        // una discusión sin datos.
        Assert.Equal(1, laSuya.GetProperty("contenidosQueLaUsan").GetInt32());
    }

    [Fact]
    public async Task Fusionar_pasa_el_contenido_a_la_otra_y_retira_la_vacia()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var propia = await (await client.PostAsJsonAsync(Consola, Categoria("noticias", "Encuentros de noticias")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var comun = await (await client.PostAsJsonAsync(Consola, Categoria("comun", "Encuentros")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var idPropia = propia.GetProperty("id").GetInt32();
        var idComun = comun.GetProperty("id").GetInt32();

        var noticia = await (await client.PostAsJsonAsync("/api/v1/institucional/noticias", new
        {
            titulo = "Noticia que cambia de categoría",
            resumen = "Su categoría se va a fusionar.",
            categoriaId = idPropia,
        })).Content.ReadFromJsonAsync<JsonElement>();

        var fusionada = await client.PostAsJsonAsync($"{Consola}/{idPropia}/fusionar", new { destinoId = idComun });
        Assert.Equal(HttpStatusCode.OK, fusionada.StatusCode);

        var despues = await (await client.GetAsync($"/api/v1/institucional/noticias/{noticia.GetProperty("id").GetInt64()}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // FUSIONAR «Encuentros» DE NOTICIAS DENTRO DE «Encuentros» COMUN es exactamente cómo se
        // converge a un vocabulario compartido sin perder lo ya publicado.
        Assert.Equal(idComun, despues.GetProperty("categoriaId").GetInt32());
        Assert.Equal("Encuentros", despues.GetProperty("categoria").GetString());

        var lista = await (await client.GetAsync(Consola)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(lista.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt32() == idPropia);
    }

    [Fact]
    public async Task Una_categoria_no_se_fusiona_consigo_misma()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var categoria = await (await client.PostAsJsonAsync(Consola, Categoria("agenda", "Talleres")))
            .Content.ReadFromJsonAsync<JsonElement>();
        var id = categoria.GetProperty("id").GetInt32();

        var respuesta = await client.PostAsJsonAsync($"{Consola}/{id}/fusionar", new { destinoId = id });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
