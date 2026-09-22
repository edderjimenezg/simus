using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El formulario de alta, el panel y el geovisor comparten un solo catálogo territorial público.
/// La prueba evita que una nueva pantalla vuelva a necesitar una copia autenticada o paginada de
/// DIVIPOLA para resolver departamento y municipio.
/// </summary>
public sealed class CatalogoDivipolaSeSirveRapidoTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CatalogoDivipolaSeSirveRapidoTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "El catálogo público entrega todas las ubicaciones oficiales sin sesión")]
    public async Task ElCatalogoPublicoEntregaTodasLasUbicaciones()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var totalEnBase = await db.DivipolaLocations.CountAsync();

        var respuesta = await _client.GetAsync("/api/v1/publico/divipola");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ubicaciones = await respuesta.Content.ReadFromJsonAsync<List<DivipolaLocationDto>>();
        Assert.NotNull(ubicaciones);
        Assert.Equal(totalEnBase, ubicaciones!.Count);
        Assert.NotEmpty(ubicaciones);
        Assert.All(ubicaciones, ubicacion =>
        {
            Assert.False(string.IsNullOrWhiteSpace(ubicacion.DepartmentCode));
            Assert.False(string.IsNullOrWhiteSpace(ubicacion.MunicipalityCode));
            Assert.False(string.IsNullOrWhiteSpace(ubicacion.DepartmentName));
            Assert.False(string.IsNullOrWhiteSpace(ubicacion.MunicipalityName));
        });
    }

    [Fact(DisplayName = "El catálogo público tiene orden territorial estable")]
    public async Task ElCatalogoPublicoTieneOrdenEstable()
    {
        var respuesta = await _client.GetAsync("/api/v1/publico/divipola");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var ubicaciones = await respuesta.Content.ReadFromJsonAsync<List<DivipolaLocationDto>>();
        Assert.NotNull(ubicaciones);
        Assert.Equal(
            ubicaciones!.OrderBy(ubicacion => ubicacion.DepartmentName).ThenBy(ubicacion => ubicacion.MunicipalityName)
                .Select(ubicacion => ubicacion.MunicipalityCode),
            ubicaciones.Select(ubicacion => ubicacion.MunicipalityCode));
    }
}
