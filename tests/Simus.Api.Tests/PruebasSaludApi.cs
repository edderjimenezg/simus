using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Simus.Api.Tests;

public sealed class PruebasSaludApi(WebApplicationFactory<Program> fabricaBase) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> fabrica = fabricaBase.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuracion) =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Simus"] = null })));


    [Fact]
    public async Task Rechaza_el_estado_de_sesion_sin_cookie()
    {
        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.GetAsync("/api/sesion/estado");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_el_contexto_del_panel_sin_cookie()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/mi-panel/contexto");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_la_lista_de_festivales_sin_cookie()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/mi-panel/festivales");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Limita_los_intentos_repetidos_de_ingreso()
    {
        using var cliente = fabrica.CreateClient();
        for (var intento = 0; intento < 5; intento++)
        {
            var respuesta = await cliente.PostAsync("/api/acceso/ingresar", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        }

        var bloqueada = await cliente.PostAsync("/api/acceso/ingresar", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal((System.Net.HttpStatusCode)429, bloqueada.StatusCode);
    }

    [Fact]
    public async Task Informa_que_el_registro_no_esta_disponible_sin_base_configurada()
    {
        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.GetAsync("/api/registro/preparacion");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("\"registroDisponible\":false", cuerpo, StringComparison.Ordinal);
        Assert.Contains("base de datos", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Informa_que_la_base_no_esta_configurada_sin_simular_disponibilidad()
    {
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/salud");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("\"api\":\"no_disponible\"", cuerpo, StringComparison.Ordinal);
        Assert.Contains("\"baseDatos\":\"no_configurada\"", cuerpo, StringComparison.Ordinal);
    }
}
