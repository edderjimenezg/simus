using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Quien es <c>propietario</c> de una organizacion puede administrarla desde el canal externo.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO, CON SUS NUMEROS. Hasta, siete filtros del canal externo
/// aceptaban <b>solo</b> <c>EntityRole == "administrador"</c>, y el rol <c>propietario</c> existe y
/// es el que mas se usa: en <c>PNMC_LOCAL</c>, <b>12 filas <c>propietario</c> frente a 6
/// <c>administrador</c></b>. La consecuencia se veia entera en una sola pantalla: al entrar con
/// <c>externo@pnmc.local</c>, <c>GET /external/auth/me</c> respondia 200 y listaba sus <b>cinco</b>
/// organizaciones, y acto seguido <c>GET /externo/organizaciones/{id}/perfil</c> respondia
/// <b>403</b> en las cinco. La barra de navegacion escribia el nombre de la organizacion mientras
/// el panel decia «No fue posible consultar los datos de la organizacion».
/// </para>
/// <para>
/// LAS DOS MITADES DEL SISTEMA YA DISCREPABAN ENTRE SI, y esa es la razon de que esto sea un
/// defecto y no una decision de politica que alguien tomo: <c>AdminOrganizacionesEndpoints</c>
/// declaraba desde antes que responden los dos roles, mientras el canal externo aceptaba uno.
/// Peor: crear una organizacion desde la consola escribe <c>propietario</c>
/// (contrato institucional) y crearla desde el alta externa escribe <c>administrador</c>
/// (<c>AltaDeOrganizacion.cs</c>), de modo que <b>toda organizacion creada desde la consola interna
/// dejaba a su dueno sin poder administrarla desde fuera</b>.
/// </para>
/// <para>
/// LO QUE ESTA CLASE FIJA, Y NO ES «QUE RESPONDA 200». Fija las dos mitades: que el
/// <c>propietario</c> entra <b>y</b> que un vinculo que no responde —otro rol, o el mismo rol dado
/// de baja— sigue fuera. Sin la segunda mitad, un filtro que aceptara cualquier vinculo pasaria en
/// verde.
/// </para>
/// </remarks>
public sealed class PropietarioRespondePorSuOrganizacionTests : IClassFixture<TestWebApplicationFactory>
{
    private const string Clave = "PropietarioPrueba2026";

    private readonly TestWebApplicationFactory _factory;

    public PropietarioRespondePorSuOrganizacionTests(TestWebApplicationFactory factory) => _factory = factory;

    private sealed record Cuenta(string Correo, int UsuarioId, int OrganizacionId);

    /// <summary>
    /// Una cuenta externa vinculada a una organizacion con el rol que se indique.
    /// </summary>
    /// <remarks>
    /// EL ROL ES PARAMETRO, no una constante escondida: es lo unico que cambia entre la prueba que
    /// afirma «entra» y la que afirma «no entra», y tenerlo a la vista es lo que hace que las dos
    /// se lean como el mismo experimento con una variable movida.
    /// </remarks>
    private async Task<Cuenta> SembrarAsync(string rolEnLaEntidad, bool vinculoActivo = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var ahora = DateTime.UtcNow;
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var cuenta = new UserRow
        {
            FullName = "Duena De La Organizacion " + sufijo,
            Email = $"propietaria.{sufijo}@example.com",
            AccessChannel = "externo",
            ProfileType = "organizacion",
            IsActive = true,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        cuenta.PasswordHash = AdminAuthEndpoints.HashPassword(cuenta, Clave);
        db.Users.Add(cuenta);

        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Organizacion De La Duena " + sufijo,
            ContactEmail = $"org.{sufijo}@example.com",
            CoverageLevel = "sin_definir",
            StatusCode = "registrada",
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();

        // El rol de plataforma —«externo»— es lo que deja iniciar sesion; el rol en la entidad
        // —«propietario» o «administrador»— es lo que decide que puede hacer con SU organizacion.
        // Son dos cosas distintas y esta prueba mueve solo la segunda.
        db.UsuariosRoles.Add(new UsuarioRolRow { UserId = cuenta.Id, RoleId = 6, CreatedAt = ahora });

        db.UserEntities.Add(new UserEntityRow
        {
            UserId = cuenta.Id,
            EntityId = organizacion.Id,
            EntityRole = rolEnLaEntidad,
            IsActive = vinculoActivo,
            CreatedAt = ahora,
        });

        await db.SaveChangesAsync();
        return new Cuenta(cuenta.Email, cuenta.Id, organizacion.Id);
    }

    private async Task<HttpClient> EntrarAsync(Cuenta cuenta)
    {
        var cliente = _factory.CreateClient();
        var entrada = await cliente.PostAsJsonAsync("/api/v1/externo/auth/login", new ExternalLoginRequest
        {
            Email = cuenta.Correo,
            Password = Clave
        });
        entrada.EnsureSuccessStatusCode();
        return cliente;
    }

    /// <summary>Agrega una segunda organización administrable a la misma persona.</summary>
    private async Task<int> AgregarOrganizacionAdministradaAsync(Cuenta cuenta, string rol = "administrador")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var ahora = DateTime.UtcNow;
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var organizacion = new EntityProfileRow
        {
            EntityType = "organizacion",
            Name = "Segunda Organizacion " + sufijo,
            ContactEmail = $"segunda.{sufijo}@example.com",
            CoverageLevel = "sin_definir",
            StatusCode = "registrada",
            IsActive = true,
            CreatedByUserId = cuenta.UsuarioId,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.EntityProfiles.Add(organizacion);
        await db.SaveChangesAsync();
        db.UserEntities.Add(new UserEntityRow
        {
            UserId = cuenta.UsuarioId,
            EntityId = organizacion.Id,
            EntityRole = rol,
            IsActive = true,
            CreatedAt = ahora,
        });
        await db.SaveChangesAsync();
        return organizacion.Id;
    }

    // -----------------------------------------------------------------------------------------
    // La mitad afirmativa: el propietario entra
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task El_Propietario_Ve_El_Perfil_De_Su_Organizacion()
    {
        // ES LA PANTALLA QUE ESTABA ROTA. El panel externo abre esta ruta nada mas montarse.
        var cuenta = await SembrarAsync("propietario");
        var cliente = await EntrarAsync(cuenta);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_Propietario_Aparece_En_Su_Listado_De_Organizaciones()
    {
        // LA OTRA MITAD DE LA CONTRADICCION QUE SE VEIA EN PANTALLA: `/external/auth/me` listaba
        // las organizaciones y esta ruta devolvia una lista vacia. Las dos tienen que decir lo
        // mismo, porque el panel lee la segunda y la barra de navegacion la primera.
        var cuenta = await SembrarAsync("propietario");
        var cliente = await EntrarAsync(cuenta);

        var mias = await cliente.GetFromJsonAsync<List<JsonOrganizacion>>("/api/v1/externo/organizaciones/mis");

        Assert.NotNull(mias);
        Assert.Contains(mias!, item => item.Id == cuenta.OrganizacionId.ToString());
    }

    [Fact]
    public async Task El_Propietario_Ve_La_Persona_Responsable_De_Su_Organizacion()
    {
        var cuenta = await SembrarAsync("propietario");
        var cliente = await EntrarAsync(cuenta);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/responsable");

        // 404 vale: significa que la guarda dejo pasar y no hay responsable declarado todavia. Lo
        // que no puede es 403, que es la guarda diciendo que esta persona no responde por su
        // propia organizacion.
        Assert.NotEqual(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("administrador")]
    [InlineData("propietario")]
    public async Task Los_Dos_Roles_Que_Responden_Se_Tratan_Igual(string rol)
    {
        // EL CANARIO DE LA CLASE. Con `administrador` esto ya pasaba antes del cambio; si algun dia
        // dejara de pasar, el arreglo habria roto lo que ya funcionaba en vez de anadirle un caso.
        var cuenta = await SembrarAsync(rol);
        var cliente = await EntrarAsync(cuenta);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_Persona_Puede_Elegir_Cualquiera_De_Sus_Organizaciones_Administradas()
    {
        var cuenta = await SembrarAsync("propietario");
        var segundaOrganizacionId = await AgregarOrganizacionAdministradaAsync(cuenta);
        var cliente = await EntrarAsync(cuenta);

        var sesion = await cliente.GetFromJsonAsync<ExternalSessionResponse>("/api/v1/externo/auth/me");

        Assert.NotNull(sesion);
        Assert.Contains(sesion!.Organizations, organizacion => organizacion.Id == cuenta.OrganizacionId.ToString());
        Assert.Contains(sesion.Organizations, organizacion => organizacion.Id == segundaOrganizacionId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/v1/externo/organizaciones/{segundaOrganizacionId}/perfil")).StatusCode);
    }

    // -----------------------------------------------------------------------------------------
    // La mitad negativa: lo que sigue fuera
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_Rol_Que_No_Responde_Sigue_Fuera()
    {
        // SIN ESTO, UN FILTRO QUE ACEPTARA CUALQUIER VINCULO PASARIA EN VERDE. La lista de roles
        // que responden tiene dos nombres y no es «cualquiera que este vinculado»: un contacto o
        // un colaborador apuntado a la organizacion no la administra.
        var cuenta = await SembrarAsync("contacto");
        var cliente = await EntrarAsync(cuenta);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_Vinculo_De_Contacto_No_Aparece_Como_Organizacion_Administrable_En_La_Sesion()
    {
        var cuenta = await SembrarAsync("contacto");
        var cliente = await EntrarAsync(cuenta);

        var sesion = await cliente.GetFromJsonAsync<ExternalSessionResponse>("/api/v1/externo/auth/me");

        Assert.NotNull(sesion);
        Assert.Empty(sesion!.Organizations);
    }

    [Fact]
    public async Task Un_Vinculo_Dado_De_Baja_No_Responde_Aunque_El_Rol_Sea_Propietario()
    {
        // `IsActive = false` es como se retira a alguien de una organizacion sin borrar el rastro.
        // Si el filtro mirara solo el nombre del rol, quien fue dueno seguiria entrando.
        var cuenta = await SembrarAsync("propietario", vinculoActivo: false);
        var cliente = await EntrarAsync(cuenta);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{cuenta.OrganizacionId}/perfil");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_Propietario_De_Una_Organizacion_No_Entra_En_La_De_Otra()
    {
        // ESCALADA HORIZONTAL. Ser dueno de la propia no abre la ajena, y esto no lo cubre ninguna
        // de las pruebas de arriba: todas usan la organizacion de su propia cuenta.
        var mia = await SembrarAsync("propietario");
        var ajena = await SembrarAsync("propietario");
        var cliente = await EntrarAsync(mia);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{ajena.OrganizacionId}/perfil");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private sealed record JsonOrganizacion(string Id, string Nombre);
}
