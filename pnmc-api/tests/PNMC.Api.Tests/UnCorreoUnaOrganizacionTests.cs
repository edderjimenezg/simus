using System.Net;
using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Un correo esta atado a una sola organizacion. Una organizacion si puede tener muchos Festivales.
/// </summary>
/// <remarks>
/// <para>
/// LA REGLA: «una organizacion puede tener
/// muchos festivales, pero un correo de una organizacion no puede tener muchas organizaciones, es
/// decir un correo debe estar atado a una sola organizacion».
/// </para>
/// <para>
/// QUE HABIA. Lo contrario, y por diseño: <c>POST /api/v1/externo/organizaciones/</c> existia para
/// crear la SEGUNDA organizacion de una cuenta, y su propio codigo lo decia —«DE DONDE SALE LA
/// PERSONA RESPONSABLE DE ESTA SEGUNDA ORGANIZACION»—. Nada lo impedia en la base:
/// <c>UQ_UsuariosEntidades</c> es <c>(IdUsuario, IdEntidad, RolEntidad)</c>, que permite N entidades
/// por usuario, y <c>dbo.Entidades.CorreoContacto</c> no tenia indice unico.
/// </para>
/// <para>
/// LAS DOS MITADES DE LA REGLA, y por que se prueban por separado. La primera —una cuenta no crea
/// una segunda organizacion— la impone el servidor y no la base: la base local tiene cinco cuentas
/// sembradas que ya responden por varias, asi que un indice unico no se podria crear sin borrarlas.
/// La segunda —dos organizaciones no comparten correo de contacto— la imponen las dos: la
/// validacion en <c>AltaDeOrganizacion</c> y el indice <c>UX_Entidades_CorreoContacto</c> de
/// <c>V20260828_04</c>.
/// </para>
/// <para>
/// LA TERCERA PRUEBA ES EL CONTROL POSITIVO. Sin ella, «una cuenta no crea otra organizacion» se
/// podria haber implementado bloqueando tambien los Festivales, y las dos primeras seguirian en
/// verde mientras el sistema deja de servir para lo que existe.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class UnCorreoUnaOrganizacionTests
{
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public UnCorreoUnaOrganizacionTests(SqlServerFixture baseDeDatos) => _base = baseDeDatos;

    [HechoSqlServer]
    public async Task Una_cuenta_que_ya_responde_por_una_organizacion_no_puede_crear_otra()
    {
        using var cliente = _base.CrearCliente();
        await RegistrarYEntrarAsync(cliente, "una.sola.organizacion@example.com");

        // El registro deja la cuenta con SU organizacion: desde que el alta es un solo acto, toda
        // cuenta externa nace con una.
        var respuesta = await IntentarCrearAsync(cliente, new
        {
            name = "Segunda organizacion de la misma cuenta",
            contactEmail = "segunda@organizacion.test",
            coverageLevel = "nacional",
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("ya responde por una organización", cuerpo);

        // Y NO QUEDO ESCRITA. Un 400 que igual inserta la fila es peor que no comprobar nada.
        var cuantas = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM dbo.Entidades WHERE Nombre = N'Segunda organizacion de la misma cuenta';"));
        Assert.Equal(0, cuantas);
    }

    [HechoSqlServer]
    public async Task Dos_organizaciones_no_pueden_compartir_el_correo_de_contacto()
    {
        // LA BASE TAMBIEN LO IMPIDE, no solo la validacion. Si el indice no existiera, esta prueba
        // mediria solo el `if` del servidor y no la garantia.
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = N'UX_Entidades_CorreoContacto' AND object_id = OBJECT_ID(N'dbo.Entidades');")));

        using var primera = _base.CrearCliente();
        await RegistrarYEntrarAsync(primera, "duena.del.correo@example.com");

        // El correo de contacto de su organizacion es el de la cuenta: asi lo escribe el alta.
        using var segunda = _base.CrearCliente();
        var registro = await segunda.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion que copia el correo",
            FullName = "Otra responsable",
            NumeroDocumento = "1020304099",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            // EL MISMO CORREO QUE LA PRIMERA. Aqui lo rechaza `UQ_Usuarios_CorreoElectronico`, que ya
            // existia: dos CUENTAS no pueden compartir correo. Lo que este guion añade es que dos
            // ORGANIZACIONES tampoco.
            Email = "duena.del.correo@example.com",
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });

        Assert.False(registro.IsSuccessStatusCode, "El registro con un correo ya usado deberia fallar.");

        var repetidos = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM (SELECT LOWER(LTRIM(RTRIM(CorreoContacto))) c FROM dbo.Entidades " +
            "WHERE CorreoContacto IS NOT NULL GROUP BY LOWER(LTRIM(RTRIM(CorreoContacto))) HAVING COUNT(*) > 1) t;"));
        Assert.Equal(0, repetidos);
    }

    [HechoSqlServer]
    public async Task La_organizacion_si_puede_tener_varios_Festivales()
    {
        // CONTROL POSITIVO. La regla limita las organizaciones por correo, no los Festivales por
        // organizacion: «una organizacion puede tener muchos festivales».
        using var cliente = _base.CrearCliente();
        await RegistrarYEntrarAsync(cliente, "muchos.festivales@example.com");

        var organizacionId = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT TOP 1 e.IdEntidad FROM dbo.Entidades e " +
            "JOIN dbo.UsuariosEntidades ue ON ue.IdEntidad = e.IdEntidad " +
            "JOIN dbo.Usuarios u ON u.IdUsuario = ue.IdUsuario " +
            "WHERE u.CorreoElectronico = N'muchos.festivales@example.com' ORDER BY e.IdEntidad DESC;"));

        foreach (var nombre in new[] { "Primero", "Segundo", "Tercero" })
        {
            var creado = await CrearAsync<FestivalBorradorDto>(
                cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
                {
                    nombre = $"Festival {nombre} de la misma organizacion",
                    nivelCobertura = "nacional",
                });
            Assert.False(string.IsNullOrWhiteSpace(creado.Id));
        }

        var cuantos = Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT COUNT(*) FROM dbo.Festivales WHERE OrganizacionPrincipalId = {organizacionId};"));
        Assert.Equal(3, cuantos);
    }

    // ---------- Andamio ----------------------------------------------------------

    private static async Task RegistrarYEntrarAsync(HttpClient cliente, string correo)
    {
        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de " + correo,
            FullName = "Responsable de la agrupacion",
            NumeroDocumento = "10203040" + Math.Abs(correo.GetHashCode() % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture),
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        });
        registro.EnsureSuccessStatusCode();

        (await cliente.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> IntentarCrearAsync(HttpClient cliente, object cuerpo)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, "/api/v1/externo/organizaciones/")
        {
            Content = JsonContent.Create(cuerpo)
        };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(cliente));
        return await cliente.SendAsync(mensaje);
    }

    private static async Task<T> CrearAsync<T>(HttpClient cliente, string ruta, object cuerpo)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", await CsrfExternoAsync(cliente));
        var respuesta = await cliente.SendAsync(mensaje);
        if (!respuesta.IsSuccessStatusCode)
        {
            Assert.Fail($"POST {ruta} respondio {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");
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
