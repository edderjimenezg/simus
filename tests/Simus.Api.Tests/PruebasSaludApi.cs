using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Simus.Api.Tests;

public sealed class PruebasSaludApi(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Informa_que_el_registro_no_esta_disponible_sin_base_configurada()
    {
        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.GetAsync("/api/registro/disponibilidad");
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
