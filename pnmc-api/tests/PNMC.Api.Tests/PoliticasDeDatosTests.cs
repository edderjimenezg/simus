using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Que el sistema pueda demostrar qué autorizó cada persona, y con qué texto delante.
/// </summary>
/// <remarks>
/// <b>LO QUE ESTAS PRUEBAS VIGILAN NO ES UNA FUNCIONALIDAD, ES UNA OBLIGACIÓN.</b> La Ley 1581 de
/// 2012 exige autorización previa, para fines determinados, y poder demostrarla. Un defecto aquí no
/// rompe una pantalla: deja al Ministerio tratando datos personales sin constancia de permiso. Por
/// eso se comprueba el extremo que de verdad falla en silencio —que el texto servido y el guardado
/// sean el mismo— y no solo que las rutas contesten.
/// </remarks>
public sealed class PoliticasDeDatosTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly string[] ObligatoriasDelAlta = ["tratamiento", "terminos"];

    private readonly TestWebApplicationFactory _factory;

    public PoliticasDeDatosTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// El texto de C# y el del guion de migración son el mismo, carácter a carácter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POR QUÉ HACE FALTA ESTA PRUEBA.</b> Los tres textos existen por duplicado y no se puede
    /// evitar: las bases reales los reciben por DbUp desde un fichero <c>.sql</c>, y las de prueba
    /// desde <see cref="PoliticasDeDatosSembradas"/>, porque SQL no puede leer C# ni C# ejecutarse
    /// dentro de DbUp. La duplicación es inevitable; la divergencia no.
    /// </para>
    /// <para>
    /// Sin esta comprobación, alguien podría corregir una errata en el guion y dejar el de C#
    /// intacto. Las pruebas seguirían verdes —comparan el texto servido con el guardado, y en
    /// pruebas los dos salen de C#— mientras en producción se estaría mostrando otra redacción. El
    /// fallo aparecería el día que alguien pidiera prueba de su autorización.
    /// </para>
    /// </remarks>
    [Fact]
    public void TextosDePoliticaCoincidenConLaMigracion()
    {
        var guion = LeerGuionDeMigracion();

        foreach (var (nombre, texto) in new[]
        {
            ("tratamiento", PoliticasDeDatosSembradas.TextoTratamiento),
            ("terminos", PoliticasDeDatosSembradas.TextoTerminos),
            ("boletin", PoliticasDeDatosSembradas.TextoBoletin),
        })
        {
            // EL GUION LLEVA SALTOS DE LINEA REALES DENTRO DEL LITERAL N'...'; C# los normaliza
            // segun el fichero. Se comparan con los finales de linea unificados para que la prueba
            // hable del texto y no del formato del fichero.
            var enElGuion = Normalizar(guion);
            var enElCodigo = Normalizar(texto);

            Assert.True(
                enElGuion.Contains(enElCodigo, StringComparison.Ordinal),
                $"El texto de «{nombre}» en PoliticasDeDatosSembradas no aparece literalmente en "
                + "V20260912_07__politicas_y_autorizaciones_de_datos.sql. Los dos lados tienen que "
                + "decir lo mismo: uno siembra las bases reales y el otro las de prueba.");
        }
    }

    /// <summary>Las tres finalidades declaradas tienen texto vigente que el servidor sirve.</summary>
    [Fact]
    public async Task LasTresPoliticasVigentesSeSirven()
    {
        var cliente = _factory.CreateClient();

        foreach (var clave in new[] { "tratamiento", "terminos", "boletin" })
        {
            var politica = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/publico/politicas/{clave}");

            Assert.Equal(clave, politica.GetProperty("clave").GetString());
            Assert.False(string.IsNullOrWhiteSpace(politica.GetProperty("texto").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(politica.GetProperty("version").GetString()));
        }
    }

    /// <summary>
    /// Una finalidad que el sistema no ejerce no tiene política.
    /// </summary>
    /// <remarks>
    /// El desarrollo de septiembre declaraba además <c>participacion</c> y <c>directorio</c>.
    /// Ninguna existe aquí: no hay pantalla de participación ni directorio público de
    /// organizaciones. Pedir autorización para algo que no se hace deja al titular con una lista de
    /// permisos que no se corresponde con lo que ocurre con sus datos.
    /// </remarks>
    [Fact]
    public async Task UnaFinalidadQueNoSeEjerceNoTienePolitica()
    {
        var cliente = _factory.CreateClient();

        var participacion = await cliente.GetAsync("/api/v1/publico/politicas/participacion");
        var directorio = await cliente.GetAsync("/api/v1/publico/politicas/directorio");

        Assert.Equal(HttpStatusCode.NotFound, participacion.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, directorio.StatusCode);
    }

    /// <summary>
    /// El texto que el servidor sirve para el alta es el que queda guardado como evidencia.
    /// </summary>
    /// <remarks>
    /// <b>ESTE ES EL EXTREMO QUE FALLA EN SILENCIO.</b> Si la pantalla mostrara una redacción y el
    /// servidor guardara otra, todo seguiría funcionando y nadie lo notaría hasta que alguien
    /// pidiera prueba de su autorización —art. 8 num. 2 de la Ley 1581— y la prueba dijera algo
    /// distinto de lo que esa persona leyó. Es exactamente el motivo por el que el texto lo sirve el
    /// servidor y no lo escribe el formulario.
    /// </remarks>
    [Fact]
    public async Task ElTextoServidoEnElAltaEsElQueQuedaComoEvidencia()
    {
        var cliente = _factory.CreateClient();

        var preparacion = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/auth/register-preparation");
        var servidos = preparacion.GetProperty("politicas").EnumerateArray()
            .ToDictionary(item => item.GetProperty("clave").GetString()!, item => item.GetProperty("texto").GetString()!);

        var correo = $"autorizaciones-{Guid.NewGuid():N}@pnmc.test";
        var alta = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new
        {
            organizationName = $"Organizacion {Guid.NewGuid():N}"[..40],
            email = correo,
            headquartersDepartmentCode = "11",
            headquartersMunicipalityCode = "11001",
            firstName = "Ana",
            firstSurname = "Torres",
            documentType = "CC",
            documentNumber = "1020304050",
            phone = "3001234567",
            password = "Clave.Segura.2026",
            politicasAceptadas = ObligatoriasDelAlta,
        });

        Assert.True(alta.IsSuccessStatusCode, await alta.Content.ReadAsStringAsync());

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var guardadas = await db.AutorizacionesDeDatos.AsNoTracking()
            .Where(item => item.CorreoTitular == correo)
            .ToListAsync();

        Assert.Equal(2, guardadas.Count);
        foreach (var autorizacion in guardadas)
        {
            Assert.Equal(servidos[autorizacion.Finalidad], autorizacion.TextoAceptado);
            Assert.False(autorizacion.TextoReconstruido);
            Assert.Null(autorizacion.FechaRevocacion);
            Assert.Equal("registro", autorizacion.Origen);
        }
    }

    /// <summary>
    /// Sin aceptar lo obligatorio no hay alta; el boletín, en cambio, es opcional de verdad.
    /// </summary>
    /// <remarks>
    /// <b>QUE EL BOLETÍN SEA OPCIONAL ES LO QUE HACE QUE LA FINALIDAD SIGNIFIQUE ALGO.</b> Si
    /// hubiera que marcarlo para poder continuar, no sería una autorización libre y determinada
    /// —Ley 1581 art. 9— sino un peaje. Esta prueba fija que se puede completar el alta sin él.
    /// </remarks>
    [Fact]
    public async Task ElBoletinEsOpcionalYLoObligatorioNoLoEs()
    {
        var cliente = _factory.CreateClient();

        object Alta(string correo, string[] politicas) => new
        {
            organizationName = $"Organizacion {Guid.NewGuid():N}"[..40],
            email = correo,
            headquartersDepartmentCode = "11",
            headquartersMunicipalityCode = "11001",
            firstName = "Luis",
            firstSurname = "Gomez",
            documentType = "CC",
            documentNumber = "1020304051",
            phone = "3001234567",
            password = "Clave.Segura.2026",
            politicasAceptadas = politicas,
        };

        var sinTratamiento = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register",
            Alta($"sin-tratamiento-{Guid.NewGuid():N}@pnmc.test", ["terminos"]));
        Assert.Equal(HttpStatusCode.BadRequest, sinTratamiento.StatusCode);

        var sinBoletin = $"sin-boletin-{Guid.NewGuid():N}@pnmc.test";
        var valida = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register",
            Alta(sinBoletin, ["tratamiento", "terminos"]));
        Assert.True(valida.IsSuccessStatusCode, await valida.Content.ReadAsStringAsync());

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        Assert.False(await db.AutorizacionesDeDatos.AsNoTracking()
            .AnyAsync(item => item.CorreoTitular == sinBoletin && item.Finalidad == "boletin"));

        // Y TAMPOCO ENTRA EN LA LISTA DE ENVIO. Una suscripcion sin autorizacion seria un correo al
        // que se escribe sin permiso, que es el defecto entero que este bloque cierra.
        Assert.False(await db.BoletinSuscripciones.AsNoTracking()
            .AnyAsync(item => item.CorreoElectronico == sinBoletin));
    }

    // -----------------------------------------------------------------------------------------

    private static string Normalizar(string valor) =>
        valor.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    /// <summary>
    /// Encuentra el guion de migracion subiendo desde el ejecutable, igual que hace el migrador.
    /// </summary>
    private static string LeerGuionDeMigracion()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            var candidato = Path.Combine(actual.FullName, "pnmc-database", "schema",
                "V20260912_07__politicas_y_autorizaciones_de_datos.sql");
            if (File.Exists(candidato)) return File.ReadAllText(candidato);
            actual = actual.Parent;
        }

        throw new FileNotFoundException(
            "No se encontro el guion de politicas subiendo desde " + AppContext.BaseDirectory);
    }
}
