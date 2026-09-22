using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Los campos del volcado de SIMUS que existian en la base y no tenian por donde entrar.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO. <c>ART_MUS_FESTIVALES</c> guarda un bloque de contacto en la CABECERA del Festival
/// —INSTAGRAM, FACEBOOK, PAGINA_WEB, OTRO_ENLACE, CELULAR y OBSERVACIONES_CONTACTO—. Las seis
/// columnas equivalentes existian en <c>dbo.Festivales</c> desde el primer dia, y
/// <c>CrearFestivalBorradorSolicitud</c> tenia nueve campos que no incluian ninguna: el canal
/// externo no podia escribirlas, asi que todo Festival creado desde fuera nacia con las seis en
/// NULL y no habia forma de rellenarlas. Lo mismo con <c>DESCRIPCION_ARCHIVO</c> de
/// <c>ART_MUS_MATERIALMULTIMEDIA</c>, que ni siquiera existia como columna: en su lugar habia un
/// <c>RolArchivo</c> nuestro, obligatorio, con tres valores inventados.
/// </para>
/// <para>
/// POR QUE CONTRA SQL SERVER Y NO CONTRA SQLITE. Lo que se afirma es que el dato QUEDA ESCRITO en
/// la columna, y eso se lee del motor con una consulta, no del contexto de EF: un
/// <c>SaveChanges</c> que no persiste seguiria devolviendo el objeto en memoria con el valor
/// puesto, y la prueba pasaria sobre el defecto. Es el mismo motivo por el que
/// <c>CoberturaNacionalSqlServerTests</c> vive en este carril.
/// </para>
/// <para>
/// LO QUE NO SE MIDE AQUI, y esta a proposito: que la pantalla pinte los campos. Eso lo fija
/// <c>ficha-festival.component.spec.ts</c>. Aqui solo se mide el cable y la columna.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CamposDelVolcadoDeFestivalTests
{
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public CamposDelVolcadoDeFestivalTests(SqlServerFixture baseDeDatos) => _base = baseDeDatos;

    [HechoSqlServer]
    public async Task El_bloque_de_contacto_de_la_cabecera_queda_escrito_en_la_columna()
    {
        using var agrupacion = _base.CrearCliente();
        await RegistrarYEntrarAsync(agrupacion, "contacto.cabecera@example.com");

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupacion con contacto completo", correoContacto: "contacto@agrupacion-contacto.test");

        var festival = await CrearAsync<FestivalBorradorDto>(
            agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
            {
                nombre = "Festival con contacto completo",
                nivelCobertura = "nacional",
                instagram = "@festivalcompleto",
                facebook = "fb.com/festivalcompleto",
                paginaWeb = "https://festivalcompleto.co",
                otroEnlace = "https://otro.festivalcompleto.co",
                telefonoCelular = "3001112233",
                observacionesContacto = "Llamar de lunes a viernes.",
            });

        // LEIDO DEL MOTOR. Las seis columnas, en una sola cadena, para que el fallo diga cual falto.
        var escrito = Convert.ToString(await _base.EscalarAsync(
            "SELECT CONCAT(ISNULL(InstagramFestival, 'NULL'), '|', ISNULL(FacebookFestival, 'NULL'), '|', " +
            "ISNULL(SitioWebFestival, 'NULL'), '|', ISNULL(OtroEnlaceFestival, 'NULL'), '|', " +
            "ISNULL(TelefonoFestival, 'NULL'), '|', ISNULL(ObservacionesContacto, 'NULL')) " +
            $"FROM dbo.Festivales WHERE IdFestival = {Numero(festival.Id)};"));

        Assert.Equal(
            "@festivalcompleto|fb.com/festivalcompleto|https://festivalcompleto.co|" +
            "https://otro.festivalcompleto.co|3001112233|Llamar de lunes a viernes.",
            escrito);

        // Y VUELVE POR EL CABLE. Sin esto el formulario no podria releer lo que acaba de guardar, y
        // abrir la ficha para editarla borraria los seis campos al volver a guardar.
        Assert.Equal("@festivalcompleto", festival.Instagram);
        Assert.Equal("https://festivalcompleto.co", festival.PaginaWeb);
        Assert.Equal("3001112233", festival.TelefonoCelular);
        Assert.Equal("Llamar de lunes a viernes.", festival.ObservacionesContacto);
    }

    [HechoSqlServer]
    public async Task La_descripcion_del_material_de_la_edicion_queda_escrita_y_vuelve_por_el_cable()
    {
        using var agrupacion = _base.CrearCliente();
        await RegistrarYEntrarAsync(agrupacion, "material.multimedia@example.com");

        var organizacion = await OrganizacionDeLaCuenta.PrepararAsync(
            agrupacion, nombre: "Agrupacion con material", correoContacto: "contacto@agrupacion-material.test");

        var festival = await CrearAsync<FestivalBorradorDto>(
            agrupacion, $"/api/v1/externo/organizaciones/{organizacion.Id}/festivales", new
            {
                nombre = "Festival con material multimedia",
                nivelCobertura = "nacional",
            });

        // EL CUERPO NO MANDA `rolArchivo`, porque los materiales de una edicion no clasifican el
        // archivo con un vocabulario inventado. El contrato conserva la descripcion del volcado.
        var edicion = await CrearAsync<EdicionFestivalDto>(
            agrupacion, $"/api/v1/externo/festivales/{Numero(festival.Id)}/ediciones", new
            {
                anio = 2026,
                nombre = "Edicion con material",
                materiales = new[]
                {
                    new { url = "https://festivalcompleto.co/afiche.jpg", descripcionArchivo = "Afiche oficial 2026" },
                },
            });

        var escrito = Convert.ToString(await _base.EscalarAsync(
            "SELECT CONCAT(ISNULL(Url, 'NULL'), '|', ISNULL(DescripcionArchivo, 'NULL')) " +
            "FROM dbo.ArchivosDeRegistro " +
            $"WHERE ModuloId = 'ediciones-festival' AND RegistroId = '{Numero(edicion.Id)}';"));

        Assert.Equal("https://festivalcompleto.co/afiche.jpg|Afiche oficial 2026", escrito);

        var respuesta = await agrupacion.GetFromJsonAsync<EdicionDetalladaRespuesta>(
            $"/api/v1/externo/ediciones/{Numero(edicion.Id)}");
        var material = Assert.Single(Assert.IsType<EdicionDetalladaRespuesta>(respuesta).Materiales);
        Assert.Equal("https://festivalcompleto.co/afiche.jpg", material.Url);
        Assert.Equal("Afiche oficial 2026", material.DescripcionArchivo);
    }

    // ---------- Andamio ----------------------------------------------------------

    private static int Numero(string identificador) =>
        int.Parse(identificador, System.Globalization.CultureInfo.InvariantCulture);

    private static async Task RegistrarYEntrarAsync(HttpClient cliente, string correo)
    {
        var registro = await cliente.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organizacion de prueba",
            FullName = "Responsable de la agrupacion",
            NumeroDocumento = "1020304052",
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

    private sealed record EdicionDetalladaRespuesta(IReadOnlyList<MaterialEdicionRespuesta> Materiales);

    private sealed record MaterialEdicionRespuesta(string? Url, string? DescripcionArchivo);
}
