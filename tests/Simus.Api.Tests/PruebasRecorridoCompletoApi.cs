using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Simus.Api.Tests;

[Collection(ColeccionBaseDatos.Nombre)]
public sealed class PruebasRecorridoCompletoApi(WebApplicationFactory<Program> fabricaBase, BaseDatosPruebasFixture baseDatos)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> fabrica = fabricaBase.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuracion) =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?> { ["LimiteIntentos:PermitLimit"] = "1000" })));


    private const string RazonOmision = "Requiere base de datos local: scripts/dev-test-integracion.sh";

    [SkippableFact]
    public async Task Registro_exitoso_crea_persona_y_organizacion()
    {
        Skip.IfNot(baseDatos.BaseDatosDisponible, RazonOmision);
        using var cliente = fabrica.CreateClient();

        var respuesta = await RegistrarPersonaDePruebaAsync(cliente);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("simus_sesion=", StringComparison.Ordinal)));
    }

    [SkippableFact]
    public async Task Ingreso_exitoso_devuelve_identificador_de_persona()
    {
        Skip.IfNot(baseDatos.BaseDatosDisponible, RazonOmision);
        using var cliente = fabrica.CreateClient();
        var correo = await RegistrarYObtenerCorreoAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync("/api/acceso/ingresar", new { correo, contrasena = ContrasenaPrueba });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [SkippableFact]
    public async Task Persona_administradora_actualiza_su_organizacion()
    {
        Skip.IfNot(baseDatos.BaseDatosDisponible, RazonOmision);
        using var cliente = fabrica.CreateClient();
        await RegistrarYObtenerCorreoAsync(cliente);
        var idOrganizacion = await ObtenerOrganizacionActivaAsync(cliente);

        var respuesta = await cliente.PatchAsJsonAsync($"/api/mi-panel/organizaciones/{idOrganizacion}", new
        {
            nombre = "Organización renombrada en prueba",
            numeroIdentificacion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [SkippableFact]
    public async Task Persona_administradora_crea_y_lista_su_festival()
    {
        Skip.IfNot(baseDatos.BaseDatosDisponible, RazonOmision);
        using var cliente = fabrica.CreateClient();
        await RegistrarYObtenerCorreoAsync(cliente);
        var idOrganizacion = await ObtenerOrganizacionActivaAsync(cliente);

        var creacion = await cliente.PostAsJsonAsync("/api/mi-panel/festivales", new
        {
            idOrganizacion,
            nombre = "Festival de prueba",
            descripcion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });
        Assert.Equal(HttpStatusCode.Created, creacion.StatusCode);

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/mi-panel/festivales");
        Assert.Contains(listado.EnumerateArray(), festival => festival.GetProperty("nombre").GetString() == "Festival de prueba");
    }

    [SkippableFact]
    public async Task Persona_no_puede_administrar_organizacion_ni_festival_ajenos()
    {
        Skip.IfNot(baseDatos.BaseDatosDisponible, RazonOmision);
        using var clienteA = fabrica.CreateClient();
        using var clienteB = fabrica.CreateClient();
        await RegistrarYObtenerCorreoAsync(clienteA);
        await RegistrarYObtenerCorreoAsync(clienteB);
        var idOrganizacionB = await ObtenerOrganizacionActivaAsync(clienteB);

        var creacionFestivalB = await clienteB.PostAsJsonAsync("/api/mi-panel/festivales", new
        {
            idOrganizacion = idOrganizacionB,
            nombre = "Festival de la organización B",
            descripcion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });
        creacionFestivalB.EnsureSuccessStatusCode();
        var idFestivalB = (await creacionFestivalB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idFestival").GetGuid();

        var intentoActualizarOrganizacion = await clienteA.PatchAsJsonAsync($"/api/mi-panel/organizaciones/{idOrganizacionB}", new
        {
            nombre = "Intento de secuestro",
            numeroIdentificacion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });
        Assert.Equal(HttpStatusCode.Forbidden, intentoActualizarOrganizacion.StatusCode);

        var intentoVerAdministradores = await clienteA.GetAsync($"/api/mi-panel/organizaciones/{idOrganizacionB}/administradores");
        Assert.Equal(HttpStatusCode.Forbidden, intentoVerAdministradores.StatusCode);

        var intentoCrearFestivalAjeno = await clienteA.PostAsJsonAsync("/api/mi-panel/festivales", new
        {
            idOrganizacion = idOrganizacionB,
            nombre = "Festival intruso",
            descripcion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });
        Assert.Equal(HttpStatusCode.Forbidden, intentoCrearFestivalAjeno.StatusCode);

        var intentoEditarFestivalAjeno = await clienteA.PatchAsJsonAsync($"/api/mi-panel/festivales/{idFestivalB}/perfil-borrador", new
        {
            nombre = "Perfil secuestrado",
            descripcion = (string?)null,
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba
        });
        Assert.Equal(HttpStatusCode.Forbidden, intentoEditarFestivalAjeno.StatusCode);

        var listadoFestivalesA = await clienteA.GetFromJsonAsync<JsonElement>("/api/mi-panel/festivales");
        Assert.DoesNotContain(listadoFestivalesA.EnumerateArray(), festival => festival.GetProperty("idFestival").GetGuid() == idFestivalB);
    }

    private const string ContrasenaPrueba = "ContrasenaSeguraDePrueba123";

    private static async Task<HttpResponseMessage> RegistrarPersonaDePruebaAsync(HttpClient cliente, string? correo = null)
    {
        var preparacion = await cliente.GetFromJsonAsync<JsonElement>("/api/registro/preparacion");
        var codigosDocumentos = preparacion.GetProperty("documentos").EnumerateArray()
            .Select(documento => documento.GetProperty("codigo").GetString())
            .ToArray();

        return await cliente.PostAsJsonAsync("/api/registro", new
        {
            primerNombre = "Ana",
            primerApellido = "Pérez",
            codigoTipoIdentificacion = "CC",
            numeroIdentificacion = Guid.NewGuid().ToString("N")[..10],
            correo = correo ?? $"prueba-{Guid.NewGuid():N}@simus.test",
            contrasena = ContrasenaPrueba,
            nombreOrganizacion = "Organización de prueba",
            codigoDepartamento = BaseDatosPruebasFixture.CodigoDepartamentoPrueba,
            codigoMunicipio = BaseDatosPruebasFixture.CodigoMunicipioPrueba,
            codigosDocumentosAceptados = codigosDocumentos
        });
    }

    private static async Task<string> RegistrarYObtenerCorreoAsync(HttpClient cliente)
    {
        var correo = $"prueba-{Guid.NewGuid():N}@simus.test";
        var respuesta = await RegistrarPersonaDePruebaAsync(cliente, correo);
        respuesta.EnsureSuccessStatusCode();
        await PrepararProteccionAsync(cliente);
        return correo;
    }

    private static async Task PrepararProteccionAsync(HttpClient cliente)
    {
        var proteccion = await cliente.GetFromJsonAsync<JsonElement>("/api/sesion/proteccion");
        cliente.DefaultRequestHeaders.Remove("X-SIMUS-CSRF");
        cliente.DefaultRequestHeaders.Add("X-SIMUS-CSRF", proteccion.GetProperty("token").GetString());
    }

    private static async Task<Guid> ObtenerOrganizacionActivaAsync(HttpClient cliente)
    {
        var contexto = await cliente.GetFromJsonAsync<JsonElement>("/api/mi-panel/contexto");
        return contexto.GetProperty("organizaciones")[0].GetProperty("id").GetGuid();
    }
}
