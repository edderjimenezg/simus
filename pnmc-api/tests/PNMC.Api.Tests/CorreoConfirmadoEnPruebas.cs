using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Tests;

/// <summary>
/// Da por confirmado el correo de una cuenta recién registrada.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE HACE FALTA.</b> Desde una organización cuyo correo nadie
/// ha comprobado puede entrar y preparar su registro, pero no <b>entregarle nada al Programa</b>:
/// ni enviar un Festival a revisión ni publicar una edición. Las pruebas que recorren esos caminos
/// no tratan sobre la confirmación, así que la dan por hecha aquí en una línea en vez de recorrer
/// el enlace en cada una.
/// </para>
/// <para>
/// <b>ESCRIBE EN LA BASE Y NO LLAMA A LA RUTA, y es deliberado.</b> Lo que estas pruebas miden es lo
/// que pasa DESPUES de confirmar; recorrer la emisión y el enlace en cada una las ataría a un flujo
/// que ya tiene sus propias pruebas. Las del flujo de confirmación sí lo recorren entero.
/// </para>
/// </remarks>
internal static class CorreoConfirmadoEnPruebas
{
    internal static async Task ConfirmarAsync(TestWebApplicationFactory factory, string correo)
    {
        ArgumentNullException.ThrowIfNull(correo);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var normalizado = correo.Trim().ToLowerInvariant();
        var cuenta = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizado);
        if (cuenta is null) return;
        cuenta.CorreoConfirmado = true;
        cuenta.FechaConfirmacionCorreo = DateTime.UtcNow;

        // Y LAS ORGANIZACIONES QUE ESPERABAN A ESA CUENTA PASAN A ACTIVAS, igual que hace la ruta
        // real: si no, la prueba quedaría con una organización pendiente y una cuenta confirmada,
        // que es una combinación que el sistema nunca produce.
        var suyas = await db.UserEntities.Where(v => v.UserId == cuenta.Id && v.IsActive)
            .Select(v => v.EntityId).ToListAsync();
        var pendientes = await db.EntityProfiles
            .Where(o => suyas.Contains(o.Id) && o.StatusCode == "pendiente_de_confirmacion")
            .ToListAsync();
        foreach (var organizacion in pendientes)
        {
            organizacion.StatusCode = "activa";
            organizacion.IsActive = true;
        }

        await db.SaveChangesAsync();
    }
}
