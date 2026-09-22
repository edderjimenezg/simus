using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El contrato del API, escrito en un fichero que se revisa como código.
/// </summary>
/// <remarks>
/// <para>
/// Swagger existía ya, pero solo como página: se servía en Desarrollo y Local, y
/// desaparecía en cualquier otro entorno. Eso deja el contrato en un sitio donde
/// nadie puede compararlo con el de ayer. Quien consume el API —el front-end
/// hoy, cualquier integración mañana— no tiene forma de ver que una ruta cambió
/// de forma hasta que algo se rompe en tiempo de ejecución.
/// </para>
/// <para>
/// Esto lo convierte en un artefacto versionado. Y no hace falta una herramienta
/// nueva ni un paso de compilación: el documento se pide al mismo generador que
/// alimenta la página, desde el arnés de pruebas que ya monta la aplicación
/// entera. Por eso la comprobación vive en una prueba y no en un script —así el
/// contrato se revisa en cada ejecución de la suite, no cuando alguien se
/// acuerda de correr el script—.
/// </para>
/// <para>
/// Para regenerarlo tras un cambio deliberado del API:
/// <code>ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi</code>
/// </para>
/// </remarks>
public sealed class ContratoOpenApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ContratoOpenApiTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// Ubica <c>openapi.yaml</c> a partir de la ruta de ESTE fichero fuente.
    /// </summary>
    /// <remarks>
    /// No se parte de <c>AppContext.BaseDirectory</c> ni se sube buscando el
    /// repositorio: la suite se ejecuta a menudo con la salida redirigida a un
    /// temporal —para no chocar con el API que esté corriendo— y desde allí no
    /// hay ningún repositorio encima. <c>CallerFilePath</c> lo resuelve el
    /// compilador, así que apunta al árbol de fuentes pase lo que pase con la
    /// carpeta de salida.
    /// </remarks>
    private static string RutaDelContrato([CallerFilePath] string origen = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(origen)!, "..", "..", "openapi.yaml"));

    private string GenerarDocumento()
    {
        using var ambito = _factory.Services.CreateScope();
        var generador = ambito.ServiceProvider.GetRequiredService<ISwaggerProvider>();
        var documento = generador.GetSwagger("v1");

        // 3.0 y no 3.1: es la versión que entienden todas las herramientas de
        // cliente que alguien vaya a usar contra esto, y el documento no usa
        // nada que 3.1 añada.
        return Normalizar(documento.SerializeAsYaml(OpenApiSpecVersion.OpenApi3_0));
    }

    /// <summary>
    /// Fin de línea uniforme. Sin esto la prueba falla en Windows contra un
    /// fichero escrito en Linux —y al revés— por una diferencia que no es del
    /// contrato.
    /// </summary>
    private static string Normalizar(string texto) =>
        texto.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    [Fact]
    public void El_Contrato_Publicado_Coincide_Con_El_Que_Genera_El_Api()
    {
        var ruta = RutaDelContrato();
        var generado = GenerarDocumento();

        if (Environment.GetEnvironmentVariable("ACTUALIZAR_OPENAPI") == "1")
        {
            File.WriteAllText(ruta, generado);
            return;
        }

        Assert.True(
            File.Exists(ruta),
            $"Falta {ruta}. Genérelo con: ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi");

        var publicado = Normalizar(File.ReadAllText(ruta));

        Assert.True(
            publicado == generado,
            "El contrato publicado no coincide con el que genera el API.\n"
            + "Si el cambio del API es deliberado, regenérelo y revíselo en el diff:\n"
            + "  ACTUALIZAR_OPENAPI=1 dotnet test --filter ContratoOpenApi\n"
            + PrimeraDiferencia(publicado, generado));
    }

    [Fact]
    public void El_Contrato_Describe_Las_Rutas_Publicas_Que_Usa_El_Portal()
    {
        // Una guarda contra el documento vacío. Si el generador dejara de ver los
        // endpoints —un cambio de registro, un grupo que se deja de mapear— el
        // contrato se regeneraría vacío y la comparación seguiría en verde el día
        // que alguien lo actualizara sin mirar.
        var generado = GenerarDocumento();

        Assert.Contains("/api/v1/contenido-web", generado, StringComparison.Ordinal);
        Assert.Contains("/api/v1/publico/festivales", generado, StringComparison.Ordinal);
        Assert.Contains("/api/v1/admin/analisis/consultar", generado, StringComparison.Ordinal);
        Assert.Contains("/api/v1/admin/importaciones/{dominio}/previsualizar", generado, StringComparison.Ordinal);
        // EL DOMINIO ES UN PARAMETRO DE LA RUTA desde: la dirección que
        // usa la consola —`/admin/importaciones/festivales/…`— no cambió, pero la plantilla sí,
        // porque la misma ruta sirve ahora a cualquier dominio importable.
        Assert.Contains("/api/v1/admin/importaciones/{dominio}/{id}/confirmar", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/news", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/agenda/events", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/editorial", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/gallery", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/data/agenda", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/data/news", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/data/editorial", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/data/gallery", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/data/ai/analyze", generado, StringComparison.Ordinal);
        Assert.DoesNotContain("/bulk", generado, StringComparison.Ordinal);
    }

    private static string PrimeraDiferencia(string esperado, string obtenido)
    {
        var i = 0;
        while (i < esperado.Length && i < obtenido.Length && esperado[i] == obtenido[i])
        {
            i++;
        }

        var desde = Math.Max(0, i - 120);
        return $"\nPrimera diferencia en el carácter {i}:\n"
            + $"  publicado: …{Recorte(esperado, desde, i + 200)}…\n"
            + $"  generado:  …{Recorte(obtenido, desde, i + 200)}…";
    }

    private static string Recorte(string texto, int desde, int hasta) =>
        texto[desde..Math.Min(texto.Length, hasta)].Replace("\n", "⏎", StringComparison.Ordinal);
}
