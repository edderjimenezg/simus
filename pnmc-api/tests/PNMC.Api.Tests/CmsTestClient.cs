using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PNMC.Contracts;

namespace PNMC.Api.Tests;

/// <summary>
/// Cliente de pruebas para el CMS: inicia sesión y adjunta el token antiforgery.
/// <para>
/// Las pruebas obtienen el token por la misma ruta que la consola, en vez de
/// desactivar la comprobación en el entorno de pruebas. Desactivarla dejaría la
/// protección sin probar y la suite pasaría en verde sobre una puerta abierta.
/// </para>
/// </summary>
public static class CmsTestClient
{
    public const string WebmasterEmail = "test@pnmc.local";
    public const string WebmasterPassword = "pnmc-master";

    /// <summary>Inicia sesión y deja el cliente listo para escribir.</summary>
    /// <remarks>
    /// Recibe la fábrica base y no <c>TestWebApplicationFactory</c>: hay fábricas con siembra
    /// propia —la del contador de consultas, por ejemplo— que necesitan exactamente este mismo
    /// inicio de sesión, y con el tipo cerrado cada una se copiaba el suyo. Ensanchar el
    /// parámetro no toca ninguna llamada existente.
    /// </remarks>
    public static async Task<HttpClient> LoginAsync(
        WebApplicationFactory<Program> factory,
        string email = WebmasterEmail,
        string password = WebmasterPassword,
        bool withCsrf = true)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = email,
            Password = password,
        });
        login.EnsureSuccessStatusCode();

        if (withCsrf)
        {
            await AttachCsrfAsync(client);
        }

        return client;
    }

    /// <summary>
    /// Pide el token y lo deja como cabecera por omisión. La cookie que lo
    /// acompaña la conserva el manejador de cookies del cliente de pruebas.
    /// </summary>
    public static async Task AttachCsrfAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/admin/contenido-web/csrf");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", payload!.Token);
    }

    private sealed class CsrfResponse
    {
        public string Token { get; set; } = string.Empty;
    }
}
