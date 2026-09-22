using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El borrador automático de los formularios de la consola.
///
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN. Que un corte de conexión se lleve por delante un formulario largo
/// sin dejar rastro; que la tabla de borradores se convierta en un almacén libre de JSON con
/// cualquier dominio inventado; y que dos pestañas de la misma persona se pisen en silencio.
/// </para>
/// </summary>
public sealed class BorradoresDeConsolaTests
{
    private const string Ruta = "/api/v1/institucional/borradores";

    [Fact]
    public async Task Guardar_un_borrador_exige_sesion_institucional()
    {
        await using var factory = new TestWebApplicationFactory();

        var respuesta = await factory.CreateClient().PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = "{}" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_haber_empezado_nada_no_hay_borrador_y_no_es_un_error()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // 204 Y NO 404: no haber empezado nada no es un error, y un 404 obligaría a cada pantalla
        // a distinguirlo de «la ruta está mal».
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"{Ruta}/agenda")).StatusCode);
    }

    [Fact]
    public async Task Lo_guardado_se_recupera_tal_cual()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        const string EscritoAMedias = """{"titulo":"Encuentro de bandas","paso":2}""";
        await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = EscritoAMedias });

        var recuperado = await (await client.GetAsync($"{Ruta}/agenda")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(EscritoAMedias, recuperado.GetProperty("datosJson").GetString());
        Assert.Equal("agenda", recuperado.GetProperty("dominio").GetString());
    }

    [Fact]
    public async Task Cada_formulario_tiene_el_suyo()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"Un evento"}""" });
        await client.PutAsJsonAsync($"{Ruta}/noticias", new { datosJson = """{"titulo":"Una noticia"}""" });

        var agenda = await (await client.GetAsync($"{Ruta}/agenda")).Content.ReadFromJsonAsync<JsonElement>();
        var noticias = await (await client.GetAsync($"{Ruta}/noticias")).Content.ReadFromJsonAsync<JsonElement>();

        // SI COMPARTIERAN BORRADOR, abrir una noticia pisaría el evento a medio escribir.
        Assert.Contains("Un evento", agenda.GetProperty("datosJson").GetString()!, StringComparison.Ordinal);
        Assert.Contains("Una noticia", noticias.GetProperty("datosJson").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guardar_dos_veces_no_crea_dos_borradores()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var primero = await (await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"A"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var segundo = await (await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"AB"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        // ES EL MISMO, con una versión más: el autoguardado escribe cada pocos segundos y una fila
        // por pulsación convertiría la tabla en un historial que nadie pidió.
        Assert.Equal(primero.GetProperty("id").GetInt64(), segundo.GetProperty("id").GetInt64());
        Assert.Equal(primero.GetProperty("version").GetInt32() + 1, segundo.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Escribir_sobre_una_version_vieja_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var primero = await (await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"A"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"AB"}""" });

        // LA OTRA PESTAÑA CITA LA VERSION QUE VIO. Dejar ganar al último pulsador borraría lo
        // escrito en la otra sin que nadie se enterara.
        var tarde = await client.PutAsJsonAsync($"{Ruta}/agenda", new
        {
            datosJson = """{"titulo":"otra pestaña"}""",
            version = primero.GetProperty("version").GetInt32(),
        });

        Assert.Equal(HttpStatusCode.Conflict, tarde.StatusCode);
    }

    [Fact]
    public async Task Un_dominio_inventado_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // SIN ESTO la tabla sería un almacén libre de JSON asociado a una cuenta.
        var respuesta = await client.PutAsJsonAsync($"{Ruta}/lo-que-sea", new { datosJson = "{}" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_borrador_que_no_es_JSON_se_rechaza()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        var respuesta = await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = "esto no es json" });

        // GUARDAR TEXTO ROTO haría irrecuperable justo lo que este mecanismo existe para recuperar.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Descartar_lo_deja_sin_borrador_y_permite_empezar_otro()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"A"}""" });
        await client.DeleteAsync($"{Ruta}/agenda");

        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"{Ruta}/agenda")).StatusCode);

        // Y EL INDICE UNICO NO LO IMPIDE: se cerró, no se borró, y solo cuentan los abiertos.
        var nuevo = await client.PutAsJsonAsync($"{Ruta}/agenda", new { datosJson = """{"titulo":"B"}""" });
        Assert.Equal(HttpStatusCode.OK, nuevo.StatusCode);
    }

    [Fact]
    public async Task Descartar_sin_haber_empezado_nada_responde_que_si()
    {
        await using var factory = new TestWebApplicationFactory();
        var client = await CmsTestClient.LoginAsync(factory);

        // EL RESULTADO ES EL QUE SE PEDIA —no hay borrador—, así que no es un error.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Ruta}/agenda")).StatusCode);
    }
}
