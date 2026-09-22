using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Crear una cuenta desde la consola termina bien <b>contra SQL Server</b>, con uno o con dos
/// roles, y deja la fila de <c>dbo.UsuariosRoles</c> que le corresponde.
/// </summary>
/// <remarks>
/// <para>
/// POR QUÉ NACIÓ, Y ERA UN DEFECTO PROPIO. Al escribir la transicion se añadió a la ruta de alta un
/// guardado adelantado —<c>UsuariosRoles.IdUsuario</c> necesita el identificador del usuario, y una
/// fila recién añadida no lo tiene hasta que EF la escribe—. Ese guardado quedó <b>antes</b> de que
/// nadie asignara <c>Usuarios.IdRol</c>, que entonces era <c>NOT NULL</c> con foránea a
/// <c>dbo.Roles</c>: la fila salía con el <c>0</c> por omisión del entero y SQL Server la rechazaba
/// con <c>Msg 547</c>. <b>Crear un usuario respondía 500 siempre.</b>
/// </para>
/// <para>
/// Y LA SUITE ENTERA SEGUÍA EN VERDE, 433 de 433. La razón importa más que el fallo: la vía rápida
/// del arnés es SQLite y <b>el modelo de EF no declaraba esa foránea</b>, así que
/// <c>EnsureCreated</c> fabricaba una tabla sin la restricción y el <c>0</c> entraba sin protestar.
/// Es otra vez la misma forma de defecto: <b>vivía en el camino que ninguna prueba recorría</b>, y
/// lo encontró la primera alta contra la base real.
/// </para>
/// <para>
/// QUÉ MIDE HOY, TRAS LA TRANSICION. La columna desapareció y con ella aquel defecto concreto, pero no
/// la propiedad: <b>crear una cuenta desde la consola tiene que dejarla usable contra la base
/// real</b>, con uno o con dos roles, y la mezcla prohibida tiene que rechazarse sin dejar rastro.
/// </para>
/// <para>
/// POR ESO SIGUE EN EL CARRIL DE SQL SERVER Y NO EN EL RÁPIDO. Es la única prueba que recorre la
/// ruta de alta contra la base real, y la lección que la puso aquí no caducó: <b>el carril rápido
/// no puede desmentirte sobre restricciones que su base no tiene.</b>
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class AltaDeUsuarioSqlServerTests
{
    private const string CorreoWebmaster = "alta.webmaster@pnmc.local";
    private const string ClaveWebmaster = "PnmcAltaWebmaster123";

    private readonly SqlServerFixture _base;

    public AltaDeUsuarioSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task Crear_Una_Cuenta_Con_Un_Rol_Responde_200_Y_Deja_Su_Fila_De_Asignacion()
    {
        var cliente = await SesionDeWebmasterAsync();
        var correo = $"alta.uno.{Guid.NewGuid():N}@pnmc.local";

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Cuenta de un rol",
            Email = correo,
            Roles = ["gestor_interno"],
            Password = "ClaveDePrueba123",
            IsActive = true,
        });

        Assert.True(
            respuesta.StatusCode == HttpStatusCode.OK,
            $"POST /admin/auth/users respondio {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");

        await ExigirRolesAsync(correo, "gestor_interno");

        // Y LA CUENTA ENTRA. durante la transicion aqui se comprobaba que `Usuarios.IdRol` no
        // hubiera quedado a cero; esa columna ya no existe, asi que se comprueba la propiedad que
        // aquel aserto perseguia de verdad: que el alta deja una cuenta USABLE. Un 200 sobre una
        // cuenta que no puede iniciar sesion es un 200 que miente.
        using var recien = _base.CrearCliente();
        var acceso = await recien.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = correo,
            Password = "ClaveDePrueba123",
        });
        Assert.Equal(HttpStatusCode.OK, acceso.StatusCode);
    }

    [HechoSqlServer]
    public async Task Crear_Una_Cuenta_Con_Dos_Roles_Deja_Las_Dos_Filas_Y_La_Cuenta_Entra()
    {
        var cliente = await SesionDeWebmasterAsync();
        var correo = $"alta.dos.{Guid.NewGuid():N}@pnmc.local";

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Cuenta de dos roles",
            Email = correo,
            // EN ESTE ORDEN A PROPOSITO: el que NO manda va primero, para que el orden de llegada
            // no coincida con el de precedencia. Si algo del camino se quedara con «el primero»,
            // la cuenta acabaria solo con gestor_interno y ExigirRolesAsync lo diria.
            Roles = ["gestor_interno", "webmaster"],
            Password = "ClaveDePrueba123",
            IsActive = true,
        });

        Assert.True(
            respuesta.StatusCode == HttpStatusCode.OK,
            $"POST /admin/auth/users respondio {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");

        await ExigirRolesAsync(correo, "gestor_interno", "webmaster");

        // Y ENTRA. Una cuenta creada que no pudiera iniciar sesion seria un 200 que miente.
        using var recien = _base.CrearCliente();
        var acceso = await recien.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = correo,
            Password = "ClaveDePrueba123",
        });
        Assert.Equal(HttpStatusCode.OK, acceso.StatusCode);
    }

    [HechoSqlServer]
    public async Task La_Mezcla_De_Externo_Con_Interno_Se_Rechaza_Y_No_Deja_Rastro()
    {
        var cliente = await SesionDeWebmasterAsync();
        var correo = $"alta.mezcla.{Guid.NewGuid():N}@pnmc.local";

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/auth/users", new AdminUserUpsertRequest
        {
            FullName = "Cuenta con los dos ambitos",
            Email = correo,
            Roles = ["externo", "gestor_interno"],
            Password = "ClaveDePrueba123",
            IsActive = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var cuantas = Convert.ToInt32(await _base.EscalarAsync(
            $"SELECT COUNT(*) FROM dbo.Usuarios WHERE CorreoElectronico = N'{correo}';"),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, cuantas);
    }

    // ------------------------------------------------------------------ utilidades

    private async Task ExigirRolesAsync(string correo, params string[] esperados)
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var id = await db.Users.AsNoTracking()
            .Where(usuario => usuario.Email == correo)
            .Select(usuario => usuario.Id)
            .SingleAsync();

        Assert.Equal(
            esperados.OrderBy(rol => rol, StringComparer.Ordinal).ToList(),
            await RolesEnPruebas.LeerAsync(db, id));
    }

    private async Task<HttpClient> SesionDeWebmasterAsync()
    {
        await SembrarWebmasterAsync();

        var cliente = _base.CrearCliente();
        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest
        {
            Email = CorreoWebmaster,
            Password = ClaveWebmaster,
        });
        acceso.EnsureSuccessStatusCode();
        return cliente;
    }

    private async Task SembrarWebmasterAsync()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        var existente = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == CorreoWebmaster);
        if (existente is not null)
        {
            return;
        }

        var idWebmaster = await db.Roles.AsNoTracking()
            .Where(rol => rol.Name == "webmaster").Select(rol => rol.Id).FirstAsync();

        var usuario = new UserRow
        {
            FullName = "Webmaster de altas",
            Email = CorreoWebmaster,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        usuario.PasswordHash = PNMC.Api.Endpoints.AdminAuthEndpoints.HashPassword(usuario, ClaveWebmaster);
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

        await RolesEnPruebas.AsignarPorIdAsync(db, usuario.Id, [idWebmaster]);
    }
}
