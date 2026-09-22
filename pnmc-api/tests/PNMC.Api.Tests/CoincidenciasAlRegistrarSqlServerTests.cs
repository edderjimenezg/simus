using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Los duplicados que se avisan al registrar un Festival.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTA BUSQUEDA NO PODIA DEVOLVER NADA, Y NADIE LO VEIA.</b> Filtraba por los Festivales de la
/// entidad INSTITUCIONAL, y medido contra PNMC_LOCAL esa entidad —el
/// Plan Nacional de Música para la Convivencia, id 1— es dueña de CERO: los 186 Festivales
/// pertenecen a corporaciones reales. El paso 3 del asistente decía siempre «no encontramos
/// registros históricos similares» sobre una base que ya tenía un duplicado: «Festival Huila», dos
/// veces en el municipio 11001.
/// </para>
/// <para>
/// <b>NO HABIA NI UNA PRUEBA DE ESTA RUTA</b>, ni en SQLite ni aquí, así que el filtro imposible no
/// tenía quien lo contradijera.
/// </para>
/// <para>
/// SON DOS PREGUNTAS Y ANTES SOLO SE HACIA UNA: «¿esto ya existe como registro histórico sin dueño?»
/// —que se puede reclamar— y «¿esto ya lo registró alguien?» —que no se reclama, solo se avisa—.
/// </para>
/// <para>
/// VIVE EN LA VIA DE SQL SERVER porque necesita dos organizaciones de verdad, cada una con su
/// sesión, y el alta externa completa. Omitida salvo que se encienda <c>PNMC_PRUEBAS_SQLSERVER=1</c>.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CoincidenciasAlRegistrarSqlServerTests
{
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public CoincidenciasAlRegistrarSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task Avisa_De_Un_Festival_Que_Ya_Administra_Otra_Organizacion()
    {
        var (departamento, municipio) = await TerritorioRealAsync();
        var nombre = "Festival de la Coincidencia " + Guid.NewGuid().ToString("N")[..6];

        // LA PRIMERA ORGANIZACION REGISTRA Y PUBLICA.
        var (primera, organizacionUno) = await OrganizacionAsync("duenia");
        var festivalId = await CrearFestivalAsync(primera, organizacionUno, nombre, departamento, municipio);
        await PublicarPorLaBaseAsync(festivalId);

        // LA SEGUNDA VA A REGISTRAR LO MISMO.
        var (segunda, organizacionDos) = await OrganizacionAsync("recienllegada");
        var coincidencias = await BuscarAsync(segunda, organizacionDos, nombre, departamento, municipio);

        var encontrada = Assert.Single(coincidencias, x => x.FestivalId == festivalId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("YaRegistradoPorOtraOrganizacion", encontrada.TipoCoincidencia);
        // Y DICE QUIEN LO ADMINISTRA: sin el nombre, el aviso obliga a buscarlo por fuera.
        Assert.False(string.IsNullOrWhiteSpace(encontrada.OrganizadorHistorico));
        Assert.Contains("no lo registres de nuevo", string.Join(" ", encontrada.Evidencias), StringComparison.OrdinalIgnoreCase);
    }

    [HechoSqlServer]
    public async Task Avisa_De_Un_Festival_Que_La_Propia_Organizacion_Ya_Tiene_En_Borrador()
    {
        var (departamento, municipio) = await TerritorioRealAsync();
        var nombre = "Festival Propio " + Guid.NewGuid().ToString("N")[..6];

        var (cliente, organizacionId) = await OrganizacionAsync("propia");
        var festivalId = await CrearFestivalAsync(cliente, organizacionId, nombre, departamento, municipio);

        // SIN PUBLICARLO: es un borrador, y de los PROPIOS se avisan también los borradores, porque
        // son datos de quien pregunta y «ya lo tienes» es justo lo que evita el duplicado.
        var coincidencias = await BuscarAsync(cliente, organizacionId, nombre, departamento, municipio);

        var encontrada = Assert.Single(coincidencias, x => x.FestivalId == festivalId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("YaRegistradoPorTuOrganizacion", encontrada.TipoCoincidencia);
    }

    [HechoSqlServer]
    public async Task El_Borrador_De_Otra_Organizacion_No_Se_Enseña()
    {
        var (departamento, municipio) = await TerritorioRealAsync();
        var nombre = "Festival Reservado " + Guid.NewGuid().ToString("N")[..6];

        // LO QUE ESTO PROTEGE: el borrador de otra organización es trabajo suyo y todavía no es
        // público. Avisar de que existe filtraría lo que está preparando, y a quien pregunta no le
        // sirve de nada: no puede ni reclamarlo ni compararse con él.
        var (primera, organizacionUno) = await OrganizacionAsync("reservada");
        var festivalId = await CrearFestivalAsync(primera, organizacionUno, nombre, departamento, municipio);

        var (segunda, organizacionDos) = await OrganizacionAsync("curiosa");
        var coincidencias = await BuscarAsync(segunda, organizacionDos, nombre, departamento, municipio);

        Assert.DoesNotContain(coincidencias, x => x.FestivalId == festivalId.ToString(CultureInfo.InvariantCulture));
    }

    [HechoSqlServer]
    public async Task Un_Nombre_Que_No_Se_Parece_A_Nada_No_Devuelve_Coincidencias()
    {
        // LA PRUEBA QUE IMPIDE QUE ESTO AVISE SIEMPRE. Sin ella, un filtro que devolviera la lista
        // entera pasaría las tres de arriba: todas buscan algo que SI está.
        var (departamento, municipio) = await TerritorioRealAsync();
        var (cliente, organizacionId) = await OrganizacionAsync("distinta");

        var coincidencias = await BuscarAsync(cliente, organizacionId,
            "Zurrapastro Quimbayalde " + Guid.NewGuid().ToString("N")[..8], departamento, municipio);

        Assert.Empty(coincidencias);
    }

    // ---------- Andamio -----------------------------------------------------------------------

    private static async Task<IReadOnlyList<CoincidenciaFestivalHistoricoDto>> BuscarAsync(
        HttpClient cliente, int organizacionId, string nombre, string departamento, string municipio)
    {
        var ruta = $"/api/v1/externo/organizaciones/{organizacionId}/festivales/coincidencias-historicas"
            + $"?nombre={Uri.EscapeDataString(nombre)}&codigoDepartamento={departamento}&codigoMunicipio={municipio}";
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<CoincidenciaFestivalHistoricoDto>>())!;
    }

    private async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(string marca)
    {
        var cliente = _base.CrearCliente();
        var correo = $"coinc.{marca}.{Guid.NewGuid():N}"[..24] + "@organizacion.test";
        var (departamento, municipio) = await TerritorioRealAsync();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización " + marca + Guid.NewGuid().ToString("N")[..5],
            FullName = "Responsable " + marca,
            NumeroDocumento = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000000, 999999999).ToString(CultureInfo.InvariantCulture),
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        return (cliente, int.Parse(mias[0].GetProperty("id").GetString()!, CultureInfo.InvariantCulture));
    }

    private static async Task<int> CrearFestivalAsync(HttpClient cliente, int organizacionId, string nombre, string departamento, string municipio)
    {
        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/externo/organizaciones/{organizacionId}/festivales")
        {
            Content = JsonContent.Create(new
            {
                nombre,
                nivelCobertura = "municipal",
                codigoDepartamento = departamento,
                codigoMunicipio = municipio,
                practicasMusicalesIds = Array.Empty<int>(),
                territoriosSonorosIds = Array.Empty<int>(),
            }),
        };
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        var creado = await cliente.SendAsync(mensaje);
        creado.EnsureSuccessStatusCode();
        return int.Parse(
            (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);
    }

    /// <summary>Publica por la base: lo que se mide aquí es la búsqueda, no el circuito del Festival.</summary>
    private async Task PublicarPorLaBaseAsync(int festivalId)
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
        festival.StatusCode = "publicado";
        await db.SaveChangesAsync();
    }

    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP (1) CodigoDepartamento + N'|' + CodigoMunicipio FROM dbo.Divipola "
            + "ORDER BY CodigoDepartamento, CodigoMunicipio;"), CultureInfo.InvariantCulture);
        Assert.False(string.IsNullOrWhiteSpace(par), "dbo.Divipola esta vacia.");
        var partes = par!.Split('|');
        return (partes[0], partes[1]);
    }
}
