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
/// Eliminar una cuenta de verdad, y solo cuando no cuesta trazabilidad.
/// </summary>
/// <remarks>
/// <para>
/// <c>DELETE /users/{id}</c> DESACTIVA; la cuenta sigue existiendo porque la bitacora y los
/// registros la nombran. La direccion de producto pidio poder eliminar, y
/// lo unico que se puede eliminar sin perder trazabilidad es una cuenta que nunca hizo nada: la que
/// se creo por error o la de prueba. Esa es la ruta <c>/definitiva</c>, con sus tres candados.
/// </para>
/// </remarks>
public sealed class EliminacionDeCuentasTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public EliminacionDeCuentasTests(TestWebApplicationFactory factory) => _factory = factory;

    private async Task<int> SembrarCuentaAsync(string sufijo, bool activa)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var idGestor = await db.Roles.AsNoTracking().Where(item => item.Name == "gestor_interno").Select(item => item.Id).SingleAsync();
        var ahora = DateTime.UtcNow;
        var fila = new UserRow
        {
            FullName = "Cuenta Por Eliminar " + sufijo,
            Email = $"por.eliminar.{sufijo}@pnmc.local",
            PasswordHash = "x",
            AccessChannel = "interno",
            IsActive = activa,
            CreatedAt = ahora,
            UpdatedAt = ahora,
        };
        db.Users.Add(fila);
        await db.SaveChangesAsync();
        db.UsuariosRoles.Add(new UsuarioRolRow { UserId = fila.Id, RoleId = idGestor, CreatedAt = ahora });
        db.ModulosPorCuenta.Add(new ModuloPorCuentaRow { IdUsuario = fila.Id, CodigoModulo = "agenda", FechaOtorgado = ahora });
        await db.SaveChangesAsync();
        return fila.Id;
    }

    [Fact]
    public async Task Una_Cuenta_Desactivada_Que_Nunca_Actuo_Se_Elimina_Con_Lo_Suyo()
    {
        var id = await SembrarCuentaAsync("limpia", activa: false);
        var webmaster = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuesta = await webmaster.DeleteAsync($"/api/v1/admin/auth/users/{id}/definitiva");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.Id == id));
        // Sus roles y sus modulos se van con ella: no quedan filas huerfanas.
        Assert.False(await db.UsuariosRoles.AnyAsync(item => item.UserId == id));
        Assert.False(await db.ModulosPorCuenta.AnyAsync(item => item.IdUsuario == id));
        // Y la bitacora conserva quien la elimino y que fue definitivo.
        var traza = await db.AuditLogs.AsNoTracking()
            .Where(item => item.TableName == "Usuarios" && item.RecordId == id.ToString() && item.Action == "eliminar")
            .OrderByDescending(item => item.Id).FirstAsync();
        Assert.Contains("definitiva", traza.NewValuesJson);
    }

    [Fact]
    public async Task Una_Cuenta_Activa_No_Se_Elimina_De_Un_Solo_Paso()
    {
        var id = await SembrarCuentaAsync("activa", activa: true);
        var webmaster = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuesta = await webmaster.DeleteAsync($"/api/v1/admin/auth/users/{id}/definitiva");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.True(await db.Users.AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task Una_Cuenta_Que_Actuo_No_Se_Elimina_Y_Se_Dice_Cuantas_Veces()
    {
        var id = await SembrarCuentaAsync("con-huella", activa: false);
        using (var alcance = _factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.AuditLogs.Add(new AuditLogRow { UserId = id, TableName = "Festivales", RecordId = "1", Action = "publicar", CreatedAt = DateTime.UtcNow });
            db.AuditLogs.Add(new AuditLogRow { UserId = id, TableName = "Festivales", RecordId = "2", Action = "publicar", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var webmaster = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        var respuesta = await webmaster.DeleteAsync($"/api/v1/admin/auth/users/{id}/definitiva");

        // NO ES UN 500 DE CLAVE FORANEA: es una negativa explicada, y la cuenta sigue ahi.
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("2 actuaciones", cuerpo!["message"]);
        using var alcance2 = _factory.Services.CreateScope();
        var db2 = alcance2.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.True(await db2.Users.AnyAsync(item => item.Id == id));
    }
}
