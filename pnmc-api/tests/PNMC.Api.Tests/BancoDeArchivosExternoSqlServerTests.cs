using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El banco de archivos, por la puerta de las organizaciones.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE SE ABRIO Y QUE NO.</b> El 13 de septiembre de 2026 el banco dejó de ser solo institucional
/// para que una organización pueda adjuntar el afiche de su Edición y su foto de perfil, que es lo
/// que la historia de usuario pide y lo único que faltaba. <b>La cadena de comprobaciones es la
/// MISMA</b> —el tope antes de leer, el texto alternativo, el formato por su FIRMA y el rechazo si
/// el tipo declarado miente—: el manejador externo no repite ni una, delega en el mismo.
/// </para>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS VIGILAN SON LAS DOS GUARDAS QUE SOLO TIENE ESTE CANAL</b>, y que existen
/// porque cambia quién responde: una cuenta institucional la abre el Programa, una externa la abre
/// cualquiera.
/// </para>
/// <list type="number">
///   <item>Sin sesión no se sube nada, y sin organización tampoco.</item>
///   <item>El archivo queda con la PROCEDENCIA de su organización, que es distinta de quién lo subió.</item>
///   <item>Lo que no es una imagen sigue sin entrar, también por aquí.</item>
/// </list>
/// <para>
/// VIVE EN LA VIA DE SQL SERVER porque necesita una organización de verdad, con su alta completa y
/// su sesión.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class BancoDeArchivosExternoSqlServerTests
{
    private const string Clave = "ClaveExterna123";
    private const string Ruta = "/api/v1/externo/archivos";

    private readonly SqlServerFixture _base;

    public BancoDeArchivosExternoSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    /// <summary>Un PNG de un píxel, con su firma y su CRC correctos. Los bytes son el dato.</summary>
    private static byte[] PngDeUnPixel() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static MultipartFormDataContent Formulario(
        byte[] datos, string nombre = "afiche.png", string? alt = "Afiche de la edición", string? tipo = "image/png")
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(datos);
        if (tipo is not null) parte.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        contenido.Add(parte, "file", nombre);
        if (alt is not null) contenido.Add(new StringContent(alt), "alt");
        return contenido;
    }

    [HechoSqlServer]
    public async Task Sin_Sesion_Externa_No_Se_Sube_Nada()
    {
        var anonimo = _base.CrearCliente();

        var respuesta = await anonimo.PostAsync(Ruta, Formulario(PngDeUnPixel()));

        // ABRIR EL BANCO NO ES ABRIRLO A CUALQUIERA. Es la primera guarda y la más barata de
        // romper si la ruta se registrara sin política.
        Assert.True(respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"una subida anónima devolvió {respuesta.StatusCode}");
    }

    [HechoSqlServer]
    public async Task Una_Organizacion_Sube_Su_Afiche_Y_El_Archivo_Queda_Con_Su_Procedencia()
    {
        var (cliente, organizacionId) = await OrganizacionAsync("banco");

        var respuesta = await cliente.PostAsync(Ruta, Formulario(PngDeUnPixel()));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var archivoId = cuerpo.GetProperty("id").GetInt32();

        var url = cuerpo.GetProperty("url").GetString()!;
        var anonimo = _base.CrearCliente();

        // RECIEN SUBIDO NO LO VE NADIE DE FUERA, Y ESO CAMBIO EL 13 DE SEPTIEMBRE DE 2026. Esta
        // prueba decía «se sirve públicamente: el afiche de un Festival PUBLICADO tiene que poder
        // verlo cualquiera» y no publicaba nada: medía que cualquiera podía bajar cualquier
        // archivo del banco por su número. Ahora la regla es la vinculación, así que un archivo que
        // todavía no cuelga de nada no es público.
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync(url)).StatusCode);
        // Y SU DUEÑA SI LO VE, que es lo mínimo: lo acaba de subir.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(url)).StatusCode);

        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // EN CUANTO CUELGA DE ALGO QUE SE VE, SE VE. La foto de perfil de una organización es
        // pública por decisión de la dirección de producto, así que vincularlo ahí lo hace público sin
        // tocar ninguna bandera: es la vinculación la que manda.
        var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);
        organizacion.ArchivoFotoId = archivoId;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await _base.CrearCliente().GetAsync(url)).StatusCode);

        var fila = await db.Files.AsNoTracking().SingleAsync(x => x.Id == archivoId);

        // LA PROCEDENCIA ES LA MITAD QUE NO SE VE. «Quién lo subió» ya estaba; «de qué organización
        // viene» es lo que esta versión añade, y es lo que distingue un afiche de una organización de
        // uno cargado por el Programa.
        Assert.Equal(organizacionId, fila.OrganizacionId);
        Assert.True(fila.UploadedByUserId > 0);
        Assert.Equal("image/png", fila.MimeType);
    }

    [HechoSqlServer]
    public async Task Lo_Que_No_Es_Una_Imagen_Tampoco_Entra_Por_Esta_Puerta()
    {
        // LA CADENA DE COMPROBACIONES ES LA MISMA, y esta prueba existe para que siga siéndolo: el
        // día que alguien copie el manejador para «adaptarlo» al canal externo, la copia se quedará
        // sin el reconocedor por firma y esto se pondrá en rojo.
        var (cliente, _) = await OrganizacionAsync("nopng");

        var disfrazado = System.Text.Encoding.UTF8.GetBytes("MZ esto es un ejecutable, no una imagen");
        var respuesta = await cliente.PostAsync(Ruta, Formulario(disfrazado, "afiche.png"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [HechoSqlServer]
    public async Task Mentir_Sobre_El_Tipo_Se_Rechaza_Tambien_Aqui()
    {
        var (cliente, _) = await OrganizacionAsync("mentira");

        // Los bytes SON un PNG; lo que miente es la cabecera. Corregirlo en silencio sería tratar
        // una señal como un descuido.
        var respuesta = await cliente.PostAsync(Ruta, Formulario(PngDeUnPixel(), "afiche.jpg", tipo: "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Andamio -----------------------------------------------------------------------

    private async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(string marca)
    {
        var cliente = _base.CrearCliente();
        var correo = $"banco.{marca}.{Guid.NewGuid():N}"[..24] + "@organizacion.test";
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
