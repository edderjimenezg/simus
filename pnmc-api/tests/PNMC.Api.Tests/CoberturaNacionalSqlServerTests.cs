using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-066 — Un Festival de cobertura nacional se puede guardar contra el motor real.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO. <c>CK_Festivales_NivelCobertura</c> no comprueba solo que el nivel sea uno de los
/// tres: comprueba la <b>coherencia</b> entre el nivel y el territorio —nacional exige
/// departamento y municipio NULL; departamental, municipio NULL—. El alta escribía
/// <c>DepartmentCode = ... ?? string.Empty</c>, y <b>cadena vacía no es NULL para la
/// restricción</b>. Un nivel de cobertura entero del formulario era imposible de guardar: la
/// validación previa salta toda comprobación territorial cuando el nivel es nacional, así que la
/// petición pasaba entera y moría en el guardado con un 500 sin explicación.
/// </para>
/// <para>
/// POR QUE ESTA PRUEBA VIVE EN EL CARRIL DE SQL SERVER, y no en la suite de SQLite: la suite de
/// SQLite construye su esquema desde el modelo de EF, que <b>no declara ninguna restricción
/// CHECK</b>. Allí este INSERT entra sin queja y la prueba pasaría en verde sobre el defecto. La
/// restricción solo existe en el motor real, así que solo el motor real puede medirla.
/// </para>
/// <para>
/// HISTORIA, porque el arreglo se dio por hecho dos veces antes de estarlo. La primera versión
/// introdujo <c>DepartamentoSegunNivel()</c>, que devuelve <c>null</c> en nivel nacional, y dejó
/// el <c>?? string.Empty</c> en la misma línea: la normalización quedaba deshecha en el sitio
/// donde se aplicaba. La causa de fondo era que <c>FestivalRow.DepartmentCode</c> era
/// <c>string</c> no anulable, y hasta que esa propiedad no pasó a <c>string?</c> no había forma
/// de escribir NULL. Esta prueba existe para que no vuelva a darse por hecho.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CoberturaNacionalSqlServerTests
{
    private const string Correo = "agrupacion.nacional@example.com";
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public CoberturaNacionalSqlServerTests(SqlServerFixture baseDeDatos) => _base = baseDeDatos;

    [HechoSqlServer]
    public async Task Una_agrupacion_puede_crear_un_Festival_de_cobertura_nacional()
    {
        // LINEA BASE. Si la restricción no existiera en esta base, la prueba pasaría sin haber
        // medido nada: es la restricción, no el endpoint, lo que hace difícil este caso.
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_Festivales_NivelCobertura';")));

        using var agrupacion = _base.CrearCliente();
        await RegistrarVerificarYEntrarAsync(agrupacion);

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación de alcance nacional", correoContacto: "contacto@agrupacion-nacional.test");

        // EL CASO. Nivel nacional y, a propósito, CON códigos de territorio en el cuerpo: es lo
        // que produce el formulario por su camino natural, porque oculta el selector al cambiar
        // a Nacional pero no limpia el modelo. El servidor tiene que normalizarlos a NULL; si se
        // limita a copiarlos, la restricción rechaza la escritura.
        var (departamento, municipio) = await TerritorioRealAsync();
        var festival = await CrearAsync<FestivalBorradorDto>(
            agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
            {
                nombre = "Festival Nacional de Músicas",
                descripcion = "Festival de alcance nacional para comprobar la coherencia territorial.",
                nivelCobertura = "nacional",
                codigoDepartamento = departamento,
                codigoMunicipio = municipio,
            });

        // Y lo que quedó escrito, leído del motor y no del contexto de EF: NULL, no cadena vacía.
        var territorio = Convert.ToString(await _base.EscalarAsync(
            "SELECT CONCAT('dep=', ISNULL('[' + CodigoDepartamento + ']', 'NULL'), " +
            "' mun=', ISNULL('[' + CodigoMunicipio + ']', 'NULL')) " +
            $"FROM dbo.Festivales WHERE IdFestival = {int.Parse(festival.Id, System.Globalization.CultureInfo.InvariantCulture)};"));

        Assert.Equal("dep=NULL mun=NULL", territorio);
    }

    /// <summary>
    /// CONTROL POSITIVO. El nivel municipal sí conserva su territorio.
    /// </summary>
    /// <remarks>
    /// Sin esto, «normalizar a NULL» podría haberse implementado borrando el territorio siempre,
    /// y la prueba de arriba seguiría en verde mientras el caso normal —el 100 % de los Festivales
    /// que existen hoy— perdería su departamento y su municipio.
    /// </remarks>
    [HechoSqlServer]
    public async Task Un_Festival_municipal_conserva_su_territorio()
    {
        using var agrupacion = _base.CrearCliente();
        await RegistrarVerificarYEntrarAsync(agrupacion, "agrupacion.municipal@example.com");

        var (departamento, municipio) = await TerritorioRealAsync();

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupación de alcance municipal", correoContacto: "contacto@agrupacion-municipal.test", departamento: departamento, municipio: municipio);

        var festival = await CrearAsync<FestivalBorradorDto>(
            agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
            {
                nombre = "Festival Municipal de Músicas",
                descripcion = "Festival municipal, el caso normal del sistema.",
                nivelCobertura = "municipal",
                codigoDepartamento = departamento,
                codigoMunicipio = municipio,
            });

        var territorio = Convert.ToString(await _base.EscalarAsync(
            "SELECT CONCAT(RTRIM(CodigoDepartamento), '/', RTRIM(CodigoMunicipio)) " +
            $"FROM dbo.Festivales WHERE IdFestival = {int.Parse(festival.Id, System.Globalization.CultureInfo.InvariantCulture)};"));

        Assert.Equal($"{departamento}/{municipio}", territorio);
    }

    // ---------- Andamio ----------------------------------------------------------

    private static async Task RegistrarVerificarYEntrarAsync(HttpClient cliente, string correo = Correo)
    {
        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Responsable de la agrupación",
            NumeroDocumento = "1020304051",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        var cuenta = await registro.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(cuenta);

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Territorio tomado de la base desechable en vez de fijado a mano, por el mismo motivo que
    /// en <c>Pnmc059ColaDeRevisionSqlServerTests</c>: fijar «05» convertiría un cambio de siembra
    /// de DIVIPOLA en un fallo de esta prueba, que no es lo que mide.
    /// </summary>
    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP 1 CONCAT(RTRIM(CodigoDepartamento), '/', RTRIM(CodigoMunicipio)) FROM dbo.Divipola ORDER BY CodigoMunicipio;"));
        Assert.False(string.IsNullOrWhiteSpace(par), "La base desechable no tiene DIVIPOLA sembrada.");
        var partes = par!.Split('/');
        return (partes[0], partes[1]);
    }

    private static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"POST {ruta} respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
        }
        var creado = await respuesta.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(creado);
        return creado!;
    }

    private static async Task<string> CsrfExternoAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/externo/organizaciones/csrf");
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ExternalCsrfTokenResponse>())!.RequestToken;
    }
}
