using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El alta externa guarda la sede obligatoria sin inventar alcance territorial.
/// </summary>
/// <remarks>
/// <para>
/// LA LÓGICA YA EXISTÍA. <c>AltaDeOrganizacion.CrearAsync</c>/<c>ValidarAsync</c> siempre supieron
/// recibir departamento/municipio y aplicar <c>CK_Entidades_NivelCobertura</c> -los usa el alta
/// institucional-. El endpoint de registro externo simplemente les pasaba <c>null, null</c>. Esta
/// prueba mide justo esa costura: que <c>ExternalRegisterRequest.DepartmentCode</c>/
/// <c>MunicipalityCode</c> lleguen de verdad hasta <c>dbo.Entidades</c>, no que la regla de
/// coherencia territorial se comporte bien -esa ya la mide <c>CoberturaNacionalSqlServerTests</c>
/// y <c>PerfilDeLaOrganizacionTests</c> sobre otros dos caminos de escritura-.
/// </para>
/// <para>
/// EN EL CARRIL DE SQL SERVER, no en SQLite: la razón es la misma que en
/// <c>CoberturaNacionalSqlServerTests</c> -la restricción CHECK solo existe en el motor real-.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class AlcanceTerritorialEnElAltaExternaSqlServerTests
{
    private readonly SqlServerFixture _base;

    public AlcanceTerritorialEnElAltaExternaSqlServerTests(SqlServerFixture baseDeDatos) => _base = baseDeDatos;

    [HechoSqlServer]
    public async Task El_alta_externa_guarda_la_sede_y_conserva_el_alcance_sin_definir()
    {
        using var cliente = _base.CrearCliente();
        var (departamento, municipio) = await TerritorioRealAsync();

        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización con sede",
            FirstName = "Responsable",
            SecondName = "Territorial",
            FirstSurname = "Municipal",
            SecondSurname = "Prueba",
            DocumentType = "cc",
            DocumentNumber = "1020304052",
            Phone = "3000000001",
            Email = "sede.organizacion@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = departamento,
            HeadquartersMunicipalityCode = municipio,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();
        var cuenta = await registro.Content.ReadFromJsonAsync<ExternalRegisterResponse>();
        Assert.NotNull(cuenta);

        var territorio = Convert.ToString(await _base.EscalarAsync(
            "SELECT CONCAT(RTRIM(e.CodigoDepartamentoSede), '/', RTRIM(e.CodigoMunicipioSede), '/', r.ResponsablePrimerNombre, '/', r.ResponsablePrimerApellido, '/', r.ResponsableTipoDocumento) " +
            "FROM dbo.Entidades e INNER JOIN dbo.EntidadesResponsable r ON r.IdEntidad = e.IdEntidad " +
            $"WHERE e.IdEntidad = {int.Parse(cuenta!.OrganizationId, System.Globalization.CultureInfo.InvariantCulture)};"));

        Assert.Equal($"{departamento}/{municipio}/Responsable/Municipal/CC", territorio);
        // LAS AUTORIZACIONES, Y CON TEXTO. La comprobación anterior contaba filas en
        // `AceptacionesDocumentosLegales`, que solo guardaba a qué documento apuntaban: dos filas
        // podían estar bien contadas y no probar nada. Ahora se exige además que cada una lleve
        // dentro el texto que la persona tuvo delante, que es lo que la Ley 1581 pide poder
        // demostrar.
        var conTexto = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(1) FROM dbo.AutorizacionesDatos "
            + $"WHERE IdUsuario = {int.Parse(cuenta.UserId, System.Globalization.CultureInfo.InvariantCulture)} "
            + "AND LEN(TextoAceptado) > 0 AND FechaRevocacion IS NULL;"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, conTexto);
    }

    /// <summary>
    /// Una sede incompleta no es aceptable.
    /// </summary>
    [HechoSqlServer]
    public async Task El_alta_externa_rechaza_sede_incompleta()
    {
        using var cliente = _base.CrearCliente();
        var (departamento, municipio) = await TerritorioRealAsync();

        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización sin sede",
            FullName = "Responsable nacional",
            NumeroDocumento = "1020304053",
            Phone = "3000000002",
            Email = "sede.incompleta@example.com",
            Password = "ClaveExterna123",
            HeadquartersDepartmentCode = departamento,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, registro.StatusCode);
    }

    private async Task<(string Departamento, string Municipio)> TerritorioRealAsync()
    {
        var par = Convert.ToString(await _base.EscalarAsync(
            "SELECT TOP 1 CONCAT(RTRIM(CodigoDepartamento), '/', RTRIM(CodigoMunicipio)) FROM dbo.Divipola ORDER BY CodigoMunicipio;"));
        Assert.False(string.IsNullOrWhiteSpace(par), "La base desechable no tiene DIVIPOLA sembrada.");
        var partes = par!.Split('/');
        return (partes[0], partes[1]);
    }
}
