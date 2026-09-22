using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Dar de baja a alguien lo deja fuera de verdad, y en la siguiente petición.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. Las dos cookies duran 8 horas y son deslizantes; las políticas de autorización
/// solo leen los <i>claims</i> escritos el día del inicio de sesión. Hasta
/// eso significaba que <b>desactivar a un usuario no lo echaba</b>: mientras siguiera usando la
/// consola su sesión se renovaba sola, con el rol que tenía al entrar. Para un funcionario que
/// deja el cargo, era acceso indefinido a toda la administración de datos.
/// </para>
/// <para>
/// LAS TRES FORMAS DE PERDER EL DERECHO A ESTAR DENTRO, una por prueba: que te desactiven desde el
/// panel, que te desactiven por fuera del API (un <c>UPDATE</c> a mano), y que te cambien el rol.
/// Las tres tenían que quedar cubiertas porque el mecanismo es distinto en cada una: las dos
/// primeras se ven en <c>Activo</c>, la tercera solo comparando el rol del tiquete con el de la
/// base.
/// </para>
/// <para>
/// EL CONTROL POSITIVO DEL FINAL no es decorativo: sin él, una revalidación que rechazara a todo
/// el mundo —o un servicio mal registrado que fallara siempre— pasaría las tres pruebas de arriba
/// y habría roto el inicio de sesión entero.
/// </para>
/// <para>
/// Mutantes demostrados (24 ago 2026): quitar el evento <c>OnValidatePrincipal</c> de
/// <c>Program.cs</c> deja pasar las tres primeras; quitar la llamada a <c>Invalidar</c> del upsert
/// deja pasar la del panel (la baja tardaría hasta el techo de la caché en notarse).
/// </para>
/// </remarks>
public sealed class RevocacionEfectivaTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>
    /// Ruta que depende SOLO de la politica de autorizacion.
    /// </summary>
    /// <remarks>
    /// La primera version de estas pruebas usaba <c>GET /admin/auth/me</c> y era inservible: esa
    /// ruta es <c>AllowAnonymous</c> y resuelve al usuario por su cuenta con
    /// <c>ResolveCurrentUserAsync</c>, que ya filtraba por <c>Activo</c> — o sea, era la unica
    /// ruta del API que NO necesitaba el arreglo. Las pruebas pasaban con el defecto dentro. Lo
    /// delato el mutante, que mato una prueba en vez de las tres previstas.
    /// <c>/admin/data/schema</c> no toca la base y no comprueba nada por su cuenta: si responde,
    /// es porque la cookie sigue valiendo.
    /// </remarks>
    private const string RutaProtegida = "/api/v1/admin/data/schema";

    private const string Gestor = "gestor@pnmc.local";
    private const string ClaveGestor = "pnmc-gestor";

    private readonly TestWebApplicationFactory _factory;

    public RevocacionEfectivaTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Desactivar_Desde_El_Panel_Echa_A_La_Persona_En_La_Siguiente_Peticion()
    {
        var (victima, idVictima) = await SesionDelGestorAsync();
        Assert.Equal(HttpStatusCode.OK, (await victima.GetAsync(RutaProtegida)).StatusCode);

        var webmaster = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var baja = await webmaster.PostAsJsonAsync("/api/v1/admin/auth/users", await CuerpoDeBajaAsync(idVictima));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);

        // Sin esperar nada: la ruta que da la baja purga la caché, así que la siguiente petición
        // de esa persona ya no vale. Es el caso que de verdad ocurre.
        Assert.Equal(HttpStatusCode.Unauthorized, (await victima.GetAsync(RutaProtegida)).StatusCode);
    }

    [Fact]
    public async Task Desactivar_Por_Fuera_Del_Api_Tambien_Echa_Cuando_Caduca_La_Ventana()
    {
        var (victima, idVictima) = await SesionDelGestorAsync();
        Assert.Equal(HttpStatusCode.OK, (await victima.GetAsync(RutaProtegida)).StatusCode);

        // Nadie llama a Invalidar: se cambia la fila por debajo, como haría un UPDATE contra la
        // base. Se fuerza la caducidad de la caché en vez de esperar treinta segundos de reloj —lo
        // que se mide es que al volver a preguntar se rechaza, no cuánto tarda en preguntar.
        await CambiarEnBaseAsync(idVictima, activo: false);
        CaducarCache(idVictima);

        Assert.Equal(HttpStatusCode.Unauthorized, (await victima.GetAsync(RutaProtegida)).StatusCode);
    }

    [Fact]
    public async Task Cambiar_El_Rol_Invalida_El_Tiquete_Que_La_Persona_Lleva_Encima()
    {
        var (victima, idVictima) = await SesionDelGestorAsync();
        Assert.Equal(HttpStatusCode.OK, (await victima.GetAsync(RutaProtegida)).StatusCode);

        // Sigue activa: lo que cambia es el rol. El tiquete dice «gestor_interno» y la base ya no.
        await CambiarEnBaseAsync(idVictima, activo: true, rol: "webmaster");
        CaducarCache(idVictima);

        Assert.Equal(HttpStatusCode.Unauthorized, (await victima.GetAsync(RutaProtegida)).StatusCode);
        await CambiarEnBaseAsync(idVictima, activo: true, rol: "gestor_interno");
    }

    [Fact]
    public async Task Una_Cuenta_Intacta_Sigue_Entrando()
    {
        var (persona, id) = await SesionDelGestorAsync();

        // Se fuerza la revalidación varias veces: si el mecanismo rechazara por su cuenta, o el
        // servicio estuviera mal registrado, esto lo delataría.
        for (var intento = 0; intento < 3; intento++)
        {
            CaducarCache(id);
            Assert.Equal(HttpStatusCode.OK, (await persona.GetAsync(RutaProtegida)).StatusCode);
        }
    }

    private async Task<(HttpClient Cliente, int Id)> SesionDelGestorAsync()
    {
        // La cuenta se deja sana antes de cada prueba: las cuatro comparten la base del arnés y
        // una que la desactive dejaría a la siguiente sin poder iniciar sesión.
        var id = await RestablecerGestorAsync();
        var cliente = await CmsTestClient.LoginAsync(_factory, Gestor, ClaveGestor, withCsrf: false);
        return (cliente, id);
    }

    private async Task<int> RestablecerGestorAsync()
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Users.SingleAsync(usuario => usuario.Email == Gestor);
        fila.IsActive = true;
        await db.SaveChangesAsync();

        // CONVERGE, NO ANADE. Las cuatro pruebas de esta clase comparten la base del arnes y esto
        // corre antes de cada una: un `Add` a secas chocaba con UQ_UsuariosRoles a la segunda.
        // AsignarAsync deja a la cuenta EXACTAMENTE con este rol, que es lo que la prueba quiere
        // dar por sentado al empezar.
        await RolesEnPruebas.AsignarAsync(db, fila.Id, "gestor_interno");
        CaducarCache(fila.Id);
        return fila.Id;
    }

    private async Task<AdminUserUpsertRequest> CuerpoDeBajaAsync(int id)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Users.AsNoTracking().SingleAsync(usuario => usuario.Id == id);
        var rol = Assert.Single(await RolesEnPruebas.LeerAsync(db, fila.Id));
        return new AdminUserUpsertRequest
        {
            Id = id.ToString(),
            FullName = fila.FullName,
            Email = fila.Email,
            Role = rol,
            IsActive = false,
        };
    }

    private async Task CambiarEnBaseAsync(int id, bool activo, string? rol = null)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var fila = await db.Users.SingleAsync(usuario => usuario.Id == id);
        fila.IsActive = activo;
        await db.SaveChangesAsync();

        // POR dbo.UsuariosRoles, QUE ES LO QUE LA REVALIDACION LEE. Antes de la transicion bastaba
        // con mover `Usuarios.IdRol`; ahora esa columna no decide nada, y escribirla sola dejaba
        // esta prueba en verde falso: cambiaba algo que el sistema ya no mira, la sesion seguia
        // valida y el aserto de «te echa al cambiarte el rol» pasaba a medir el aire.
        if (rol is not null)
        {
            await RolesEnPruebas.AsignarAsync(db, id, rol);
        }
    }

    /// <summary>
    /// Simula que la ventana de la caché expiró, sin gastar el reloj de la prueba.
    /// </summary>
    private void CaducarCache(int id) =>
        _factory.Services.GetRequiredService<RevalidacionDeSesion>().Invalidar(id);
}
