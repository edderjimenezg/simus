using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// El circuito de Festivales encola correo, y no solo avisos dentro de la aplicación.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO ENCOLABA NINGUNO.</b> Comprobado: <c>EncolarAsync</c> se llamaba en
/// un único sitio de todo el sistema —la confirmación de correo—, así que quien enviaba su Festival
/// a revisión y no volvía a entrar no se enteraba de nada. La HU 1 Req 05 trae las dos plantillas
/// escritas, con su asunto y su cuerpo, y la H0 pide «notificaciones automáticas a los usuarios
/// sobre el estado de sus solicitudes».
/// </para>
/// <para>
/// <b>SE COMPRUEBA QUE SE ENCOLA, NO QUE SE ENVIA</b>, que es lo único cierto mientras no haya
/// proveedor: el emisor provisional deja la fila con estado «pendiente» y <b>sin fecha de envío</b>,
/// que es la diferencia entre registrar y fingir. Esta prueba fija esa diferencia.
/// </para>
/// <para>
/// VIVE EN LA VIA DE SQL SERVER porque necesita el circuito entero: una organización real que envía
/// y un funcionario real que decide.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class CorreosDelCircuitoDeFestivalSqlServerTests
{
    private const string Clave = "ClaveExterna123";

    private readonly SqlServerFixture _base;

    public CorreosDelCircuitoDeFestivalSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task Enviar_A_Revision_Encola_Correo_Para_Quien_Envia_Y_Para_Quien_Valida()
    {
        // UN FUNCIONARIO DE VERDAD, CREADO POR LA PRUEBA. El arnés de SQL Server siembra solo datos
        // de REFERENCIA —catálogos y DIVIPOLA—, a propósito, para que ninguna prueba dependa de qué
        // usuarios trae la siembra. Sin alguien con rol interno no hay a quién avisar, y la prueba
        // mediría el vacío en vez de la regla.
        var funcionario = await CrearFuncionarioAsync();

        var (cliente, organizacionId, correoPersona) = await OrganizacionAsync("correos");
        var festivalId = await CrearFestivalAsync(cliente, organizacionId, "Festival con correo");

        Assert.Equal(HttpStatusCode.OK,
            (await EnviarAsync(cliente, $"/api/v1/externo/festivales/{festivalId}/enviar-a-revision")).StatusCode);

        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var registroId = festivalId.ToString(CultureInfo.InvariantCulture);

        var enCola = await db.Notifications.AsNoTracking()
            .Where(x => x.Channel == EnviadorDeCorreoPendienteDeProveedor.Canal
                && (x.EventType == "FestivalEnviadoARevision" || x.EventType == "FestivalRecibidoParaRevision"))
            .ToListAsync();

        // A QUIEN ENVIO: su correo, con el nombre del Festival en el asunto.
        var alRemitente = Assert.Single(enCola, x => x.EventType == "FestivalEnviadoARevision" && x.RecipientEmail == correoPersona);
        Assert.Contains("Festival con correo", alRemitente.Title, StringComparison.Ordinal);
        Assert.Contains("ha sido enviada correctamente", alRemitente.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SimusAuthentication.ExternalScope, alRemitente.AccessScope);

        // A QUIEN TIENE QUE VALIDARLO, y en AMBITO INSTITUCIONAL: marcarlo como externo lo mandaría
        // al buzón equivocado. Es la mitad que el emisor no sabía decir hasta esta versión.
        var alFuncionario = enCola.Where(x => x.EventType == "FestivalRecibidoParaRevision").ToList();
        Assert.NotEmpty(alFuncionario);
        Assert.All(alFuncionario, x => Assert.Equal(SimusAuthentication.InstitutionalScope, x.AccessScope));
        Assert.All(alFuncionario, x => Assert.Contains("validación", x.Body, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(alFuncionario, x => x.RecipientEmail == funcionario);

        // NI UNO SOLO DICE QUE SE ENVIO. Es la diferencia entre registrar y fingir, y es lo que
        // permite drenar esta cola el día que haya proveedor.
        Assert.All(enCola, x => Assert.Equal(EnviadorDeCorreoPendienteDeProveedor.EstadoPendiente, x.Status));
        Assert.All(enCola, x => Assert.Null(x.SentAt));

        // Y EL AVISO DE LA CAMPANA SIGUE AHI: son dos canales para dos momentos, no un reemplazo.
        Assert.True(await db.Notifications.AsNoTracking()
            .AnyAsync(x => x.Channel == "internal" && x.ModuloId == "festivales" && x.RecordId == registroId));
    }

    // ---------- Andamio -----------------------------------------------------------------------

    /// <summary>Crea una cuenta con rol interno y devuelve su correo.</summary>
    private async Task<string> CrearFuncionarioAsync()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var rol = await db.Roles.FirstOrDefaultAsync(x => x.Name == "gestor_interno");
        if (rol is null)
        {
            rol = new PNMC.Domain.Entities.RoleRow { Name = "gestor_interno", Description = "Gestor interno" };
            db.Roles.Add(rol);
            await db.SaveChangesAsync();
        }

        var correo = $"gestor.{Guid.NewGuid():N}"[..20] + "@pnmc.test";
        var usuario = new PNMC.Domain.Entities.UserRow
        {
            FullName = "Gestor de prueba",
            Email = correo,
            PasswordHash = "no-se-usa",
            AccessChannel = "interno",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        db.UsuariosRoles.Add(new PNMC.Domain.Entities.UsuarioRolRow
        {
            UserId = usuario.Id,
            RoleId = rol.Id,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return correo;
    }


    private async Task<(HttpClient Cliente, int OrganizacionId, string Correo)> OrganizacionAsync(string marca)
    {
        var cliente = _base.CrearCliente();
        var correo = $"correo.{marca}.{Guid.NewGuid():N}"[..24] + "@organizacion.test";
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
        return (cliente, int.Parse(mias[0].GetProperty("id").GetString()!, CultureInfo.InvariantCulture), correo);
    }

    private async Task<int> CrearFestivalAsync(HttpClient cliente, int organizacionId, string nombre)
    {
        var (departamento, municipio) = await TerritorioRealAsync();
        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", new
        {
            nombre,
            descripcion = "Un Festival para comprobar los correos del circuito.",
            correoContacto = "contacto@festival.test",
            periodicidad = "anual",
            nivelCobertura = "municipal",
            codigoDepartamento = departamento,
            codigoMunicipio = municipio,
            practicasMusicalesIds = Array.Empty<int>(),
            territoriosSonorosIds = Array.Empty<int>(),
        });
        creado.EnsureSuccessStatusCode();
        return int.Parse(
            (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!,
            CultureInfo.InvariantCulture);
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

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string ruta, object? cuerpo = null)
    {
        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta);
        if (cuerpo is not null) mensaje.Content = JsonContent.Create(cuerpo);
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        return await cliente.SendAsync(mensaje);
    }
}
