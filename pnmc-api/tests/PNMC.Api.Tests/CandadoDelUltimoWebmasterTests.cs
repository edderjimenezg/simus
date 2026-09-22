using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Ninguna edicion de usuarios puede dejar la consola sin nadie capaz de administrarla.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. <c>DELETE /admin/auth/users/{id}</c> no borra: hace <c>IsActive = false</c>, y
/// lo protege con dos guardas —no contra tu propia cuenta, y debe quedar al menos un webmaster
/// activo—. <c>POST /admin/auth/users</c> hacia lo mismo y mas sin ninguna. El detalle que lo
/// volvia un agujero real y no un rodeo teorico: <b>el panel desactiva por el POST</b>
/// (<c>saveAdminUser</c>), no por el DELETE. La ruta guardada era la que nadie usa.
/// </para>
/// <para>
/// HAY DOS FORMAS DE PERDER UN WEBMASTER, y la segunda es la que muerde: desactivarlo, o
/// <b>cambiarle el rol</b>. La guarda de la propia cuenta no cubre la segunda, asi que el ultimo
/// webmaster podia degradarse a si mismo a gestor y dejar el panel sin ningun administrador,
/// el incluido. Ese estado no tiene salida dentro del producto: se arregla entrando a la base.
/// </para>
/// <para>
/// LA SEGUNDA MITAD DEL DEFECTO estaba dentro de la propia guarda: contaba como «webmaster
/// activo» a <c>sistema@pnmc.local</c>, la cuenta tecnica que la semilla crea con el literal
/// <c>pendiente_configurar_hash_seguro</c> en lugar de un hash
/// (<c>seed/V20260519_03__administracion_control_seed.sql:99</c>). Ninguna contrasena abre esa
/// cuenta —el login responde 401 siempre—, de modo que la guarda creia que quedaban dos
/// webmasters cuando solo uno podia entrar.
/// </para>
/// <para>
/// Mutantes demostrados (24 ago 2026): con la guarda retirada, la degradacion del ultimo
/// webmaster pasa y la consola queda sin administrador; contando webmasters sin filtrar el hash
/// inutilizable, la cuenta tecnica cuadra el recuento y la guarda deja pasar la degradacion.
/// El control positivo del final impide que el arreglo degenere en «no se puede editar a nadie».
/// </para>
/// </remarks>
public sealed class CandadoDelUltimoWebmasterTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public CandadoDelUltimoWebmasterTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>
    /// El caso alcanzable de verdad: el unico webmaster que puede entrar se degrada a si mismo.
    /// </summary>
    [Fact]
    public async Task El_Ultimo_Webmaster_No_Puede_Degradarse_A_Si_Mismo()
    {
        var (cliente, sesion) = await ClienteWebmasterAsync();
        // El escenario se FIJA, no se hereda: las cuatro pruebas comparten la misma base y xunit
        // no garantiza el orden, asi que dar por supuesto «aqui solo hay un webmaster» hace que la
        // prueba pase o falle segun quien corriera antes. Se deja activa solo la sesion.
        DejarSoloLaSesionComoWebmaster(sesion);
        // Y se anade la cuenta tecnica de la semilla: activa, rol webmaster, hash que no es un
        // hash. Es la que hacia que el recuento diera dos cuando en realidad solo habia uno.
        SembrarWebmasterTecnico();

        var respuesta = await Editar(cliente, sesion, rol: "gestor_interno", activo: true);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("webmaster activo", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("webmaster", await RolEnBaseAsync(sesion));
    }

    /// <summary>Desactivarse a uno mismo tampoco, aunque queden otros webmasters.</summary>
    [Fact]
    public async Task Nadie_Puede_Desactivar_Su_Propia_Cuenta_Desde_El_Panel()
    {
        var (cliente, sesion) = await ClienteWebmasterAsync();
        SembrarWebmaster($"suplente.{Guid.NewGuid():N}"[..24] + "@pnmc.local");

        var respuesta = await Editar(cliente, sesion, rol: "webmaster", activo: false);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("propia cuenta", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.True(await SigueActivoAsync(sesion), "Un webmaster se desactivo a si mismo desde el panel.");
    }

    /// <summary>
    /// El hecho que sostiene todo lo anterior, aislado: esa cuenta no puede iniciar sesion, luego
    /// contarla como webmaster disponible es contar a alguien que no puede entrar.
    /// </summary>
    [Fact]
    public async Task La_Cuenta_Tecnica_De_La_Semilla_No_Puede_Iniciar_Sesion()
    {
        var correo = SembrarWebmasterTecnico();

        var respuesta = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = correo, Password = "pendiente_configurar_hash_seguro" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// CONTROL POSITIVO. Sin esto, «no se puede editar a nadie» pasaria las tres de arriba.
    /// </summary>
    [Fact]
    public async Task Con_Otro_Webmaster_Que_Puede_Entrar_La_Degradacion_Si_Procede()
    {
        var (cliente, _) = await ClienteWebmasterAsync();
        var prescindible = SembrarWebmaster($"prescindible.{Guid.NewGuid():N}"[..28] + "@pnmc.local");

        var respuesta = await Editar(cliente, prescindible, rol: "gestor_interno", activo: true);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("gestor_interno", await RolEnBaseAsync(prescindible));
    }

    private async Task<HttpResponseMessage> Editar(HttpClient cliente, int id, string rol, bool activo)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = db.Users.AsNoTracking().Single(item => item.Id == id);

        return await cliente.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            Id = id.ToString(),
            FullName = fila.FullName,
            Email = fila.Email,
            Role = rol,
            IsActive = activo,
        });
    }

    private async Task<(HttpClient Cliente, int Id)> ClienteWebmasterAsync()
    {
        var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var id = await db.Users.AsNoTracking()
            .Where(item => item.Email == "test@pnmc.local").Select(item => item.Id).SingleAsync();
        return (cliente, id);
    }

    /// <summary>Un webmaster de verdad: activo y con un hash que puede validar.</summary>
    private int SembrarWebmaster(string correo) => Sembrar(correo, tecnico: false);

    /// <summary>La cuenta tecnica de la semilla, con el marcador en vez de un hash.</summary>
    private string SembrarWebmasterTecnico()
    {
        var correo = $"sistema.{Guid.NewGuid():N}"[..24] + "@pnmc.local";
        Sembrar(correo, tecnico: true);
        return correo;
    }

    private int Sembrar(string correo, bool tecnico)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var idWebmaster = db.Roles.AsNoTracking().Single(item => item.Name == "webmaster").Id;

        var fila = new UserRow
        {
            FullName = correo,
            Email = correo,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        fila.PasswordHash = tecnico
            ? "pendiente_configurar_hash_seguro"
            : PNMC.Api.Endpoints.AdminAuthEndpoints.HashPassword(fila, "ClaveDePrueba123");
        db.Users.Add(fila);
        db.SaveChanges();

        // LA FILA DE dbo.UsuariosRoles: desde entonces es lo unico que da el rol, y sin esta
        // linea la cuenta existe pero no puede iniciar sesion. Ver RolesEnPruebas.
        db.UsuariosRoles.Add(new UsuarioRolRow { UserId = fila.Id, RoleId = idWebmaster, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
        return fila.Id;
    }

    /// <summary>
    /// Deja a la cuenta de la sesion como unico webmaster activo. NUNCA la toca a ella: si se
    /// desactivara la cuenta con la que las demas pruebas inician sesion, la clase entera se caeria
    /// por una razon que no tiene nada que ver con lo que mide.
    /// </summary>
    private void DejarSoloLaSesionComoWebmaster(int sesion)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var idWebmaster = db.Roles.AsNoTracking().Single(item => item.Name == "webmaster").Id;
        // POR dbo.UsuariosRoles, NO POR Usuarios.IdRol. Si esto siguiera preguntando por la
        // columna, un webmaster que solo tuviera el rol en la tabla de asignacion sobreviviria a
        // la limpieza, quedarian DOS webmasters activos y el candado —que salta con uno solo— no
        // se probaria: la prueba pasaria sin medir nada.
        var idsWebmaster = db.UsuariosRoles.AsNoTracking()
            .Where(asignacion => asignacion.RoleId == idWebmaster)
            .Select(asignacion => asignacion.UserId)
            .ToList();
        foreach (var fila in db.Users.Where(item => idsWebmaster.Contains(item.Id) && item.IsActive && item.Id != sesion))
        {
            fila.IsActive = false;
        }
        db.SaveChanges();
    }

    private async Task<string> RolEnBaseAsync(int id)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        // Desde la transicion, «el rol que tiene en la base» sale de dbo.UsuariosRoles. Se afirma
        // que hay exactamente uno porque estas cuentas se crean con uno; si algun dia tuvieran
        // varios, este ayudante tendria que decir cual, y es mejor que falle a que elija solo.
        var roles = await RolesEnPruebas.LeerAsync(db, id);
        return Assert.Single(roles);
    }

    private async Task<bool> SigueActivoAsync(int id)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        return await db.Users.AsNoTracking().Where(item => item.Id == id).Select(item => item.IsActive).SingleAsync();
    }
}
