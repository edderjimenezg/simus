using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La organizacion que una cuenta externa ya tiene, ajustada a lo que la prueba necesita.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. Hasta, veinticinco pruebas armaban su escenario asi:
/// registraban una cuenta —que nace CON su organizacion— y despues llamaban a
/// <c>POST /api/v1/externo/organizaciones/</c> para crear una SEGUNDA, que era la que usaban. Ese
/// dia se define la regla contraria: «un correo de una organizacion no puede tener muchas
/// organizaciones, es decir un correo debe estar atado a una sola organizacion». Con la regla
/// puesta, esas veinticinco pruebas se caian en su tercera linea, antes de medir nada.
/// </para>
/// <para>
/// QUE HACE EN SU LUGAR. Lee la organizacion que la cuenta ya tiene y le ajusta el nombre, el correo
/// y el territorio por la ruta del perfil, que es la que usa la propia pantalla. El escenario
/// resultante es el mismo que antes —una organizacion con esos datos, administrada por esa cuenta—
/// con una diferencia: ahora hay UNA y no dos.
/// </para>
/// <para>
/// NO SE TOCA LA BASE POR DEBAJO, y es deliberado. Un <c>UPDATE</c> directo dejaria el escenario
/// montado mas rapido y saltandose las mismas reglas que la prueba dice comprobar; ademas el
/// la sede la vigilan sus restricciones de integridad, y armarla por SQL escondiendo la ruta que
/// la valida convertiria cualquier incoherencia en un fallo mudo mucho mas tarde.
/// </para>
/// </remarks>
internal static class OrganizacionDeLaCuenta
{
    /// <summary>Lo que devuelve <c>GET /api/v1/externo/organizaciones/mis</c>: id y nombre.</summary>
    private sealed record Resumen(string Id, string Nombre);

    /// <summary>
    /// Deja la organizacion de la cuenta con los datos que la prueba necesita y la devuelve.
    /// </summary>
    public static async Task<ExternalOrganizationDto> PrepararAsync(
        HttpClient cliente,
        string nombre,
        string? correoContacto = null,
        string? departamento = null,
        string? municipio = null,
        string? identificacion = null)
    {
        var mias = await cliente.GetFromJsonAsync<List<Resumen>>("/api/v1/externo/organizaciones/mis");
        Assert.NotNull(mias);
        Assert.True(mias!.Count > 0, "La cuenta tendria que haber nacido con su organizacion.");

        var id = int.Parse(mias[0].Id, System.Globalization.CultureInfo.InvariantCulture);

        var mensaje = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/externo/organizaciones/{id}/perfil")
        {
            Content = JsonContent.Create(new
            {
                nombre,
                nombreLegal = (string?)null,
                numeroIdentificacion = identificacion,
                descripcion = (string?)null,
                correoContacto = correoContacto ?? $"contacto.{id}@ejemplo.test",
                telefonoContacto = (string?)null,
                sitioWeb = (string?)null,
                facebook = (string?)null,
                instagram = (string?)null,
                otroEnlace = (string?)null,
                direccion = (string?)null,
                codigoDepartamentoSede = departamento ?? "05",
                codigoMunicipioSede = municipio ?? "05001",
            })
        };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"PUT del perfil respondio {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        }

        var organizacion = await cliente.GetFromJsonAsync<ExternalOrganizationDto>(
            $"/api/v1/externo/organizaciones/{id}");
        Assert.NotNull(organizacion);
        return organizacion!;
    }

    public static async Task<string> CsrfAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>())!.RequestToken;
    }
}
