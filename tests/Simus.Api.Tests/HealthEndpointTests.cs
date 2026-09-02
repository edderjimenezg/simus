using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Simus.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Informa_que_la_base_no_esta_configurada_sin_simular_disponibilidad()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("\"api\":\"unavailable\"", body, StringComparison.Ordinal);
        Assert.Contains("\"database\":\"not-configured\"", body, StringComparison.Ordinal);
    }
}
