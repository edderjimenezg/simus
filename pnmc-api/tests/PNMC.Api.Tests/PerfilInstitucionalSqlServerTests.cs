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
/// <c>PUT /admin/auth/profile</c> termina bien contra SQL Server y deja su rastro en la
/// bitacora con un verbo que el CHECK admite.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. La ruta escribia <c>Accion = 'actualizar_perfil'</c> en
/// <c>BitacoraAuditoria</c>, y <c>CK_BitacoraAuditoria_Accion</c> cierra esa columna a trece
/// verbos tecnicos entre los que no esta. El orden del manejador lo hacia peor: guardaba el
/// perfil, re-emitia la cookie y DESPUES moria con 500 al insertar la bitacora, de modo que el
/// usuario veia un error sobre un cambio que si se habia aplicado. La suite de SQLite no tiene
/// ese CHECK y la daba por buena. Lo encontro la documentacion del modulo de autenticacion el
/// 23 de agosto de 2026.
/// </para>
/// <para>
/// Mutante demostrado: con el literal <c>actualizar_perfil</c> la respuesta es 500.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class PerfilInstitucionalSqlServerTests
{
    private const string Correo = "funcionario.perfil@pnmc.local";
    private const string Clave = "PnmcFuncionario123";

    private readonly SqlServerFixture _base;

    public PerfilInstitucionalSqlServerTests(SqlServerFixture baseDesechable) => _base = baseDesechable;

    [HechoSqlServer]
    public async Task Actualizar_El_Perfil_Responde_200_Y_Deja_Bitacora_Con_Un_Verbo_Admitido()
    {
        var idUsuario = await SembrarFuncionarioAsync();

        using var cliente = _base.CrearCliente();
        (await cliente.PostAsJsonAsync("/api/v1/admin/auth/login", new AdminLoginRequest { Email = Correo, Password = Clave })).EnsureSuccessStatusCode();

        var respuesta = await cliente.PutAsJsonAsync("/api/v1/admin/auth/profile", new UpdateProfileRequest
        {
            FullName = "Funcionaria con perfil actualizado",
            Email = Correo,
            Telefono = "3000000000",
        });
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK,
            $"PUT /admin/auth/profile respondió {(int)respuesta.StatusCode}: {await respuesta.Content.ReadAsStringAsync()}");

        // El rastro: un verbo de los trece, y el nombre del evento en ValoresNuevos.
        var verbo = Convert.ToString(await _base.EscalarAsync(
            $"SELECT TOP (1) Accion FROM dbo.BitacoraAuditoria WHERE IdUsuario = {idUsuario} AND TablaAfectada = N'Usuarios' AND ValoresNuevos LIKE N'%actualizar_perfil%' ORDER BY IdAuditoria DESC;"));
        Assert.Equal(AccionesAuditoria.Actualizar, verbo);
        Assert.True(AccionesAuditoria.EsAdmitido(verbo!));

        Assert.Equal("Funcionaria con perfil actualizado", Convert.ToString(await _base.EscalarAsync(
            $"SELECT NombreCompleto FROM dbo.Usuarios WHERE IdUsuario = {idUsuario};")));
    }

    private async Task<int> SembrarFuncionarioAsync()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var existente = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Email == Correo);
        if (existente is not null)
        {
            return existente.Id;
        }

        var idWebmaster = await db.Roles.AsNoTracking().Where(item => item.Name == "webmaster").Select(item => item.Id).FirstAsync();
        var usuario = new PNMC.Domain.Entities.UserRow
        {
            FullName = "Funcionaria de perfil",
            Email = Correo,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        usuario.PasswordHash = AdminAuthEndpoints.HashPassword(usuario, Clave);
        db.Users.Add(usuario);
        await db.SaveChangesAsync();

            // LA FILA DE dbo.UsuariosRoles. `RoleId` de arriba ya no da ningun rol: desde la
            // El rol vive en la tabla de asignacion, y sin esta linea la cuenta existe
            // pero el login la rechaza con el motivo `sin_rol_valido`. Ver RolesEnPruebas.
        await RolesEnPruebas.AsignarPorIdAsync(db, usuario.Id, [idWebmaster]);
        return usuario.Id;
    }
}
