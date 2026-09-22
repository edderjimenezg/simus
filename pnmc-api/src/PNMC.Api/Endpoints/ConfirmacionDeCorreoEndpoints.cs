using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Confirmar el correo de la cuenta, y volver a pedir el enlace.
/// </summary>
/// <remarks>
/// <para>
/// <b>DOS RUTAS Y UNA ES ANONIMA.</b> Quien abre el enlace de confirmación puede estar en otro
/// navegador, en el móvil o sin sesión iniciada —es lo normal: se abre desde el correo—. Pedir
/// sesión para confirmar convertiría el enlace en un callejón. El testigo es la credencial: 32
/// bytes aleatorios, guardados con hash, de un solo uso y con tres días de vigencia.
/// </para>
/// <para>
/// <b>EL REENVIO SI PIDE SESION</b>, porque ahí sí hay alguien identificado y porque un reenvío
/// anónimo por correo sería una forma cómoda de llenarle el buzón a cualquiera.
/// </para>
/// </remarks>
public static class ConfirmacionDeCorreoEndpoints
{
    private static readonly string[] EnlaceNoValido =
        ["Este enlace no es válido. Pide uno nuevo desde tu panel."];
    private static readonly string[] EnlaceVencido =
        ["Este enlace ya venció. Pide uno nuevo desde tu panel."];
    private static readonly string[] EnlaceYaUsado =
        ["Este correo ya estaba confirmado. Puedes entrar con normalidad."];
    private static readonly string[] CorreoCambiado =
        ["El correo de la cuenta cambió después de enviarse este enlace. Pide uno nuevo desde tu panel."];
    private static readonly string[] YaConfirmado = ["Tu correo ya está confirmado."];
    private static readonly string[] CorreoNoValido = ["Escribe un correo electrónico válido."];
    private static readonly string[] CorreoEnUso =
        ["Ya hay una cuenta con ese correo. Si es tuya, entra con ella; si no, usa otra dirección."];
    private static readonly string[] CorreoIgual = ["Ese ya es el correo de tu cuenta."];
    private static readonly string[] NoSeCambiaConfirmado =
        ["Tu correo ya está confirmado y no se cambia desde aquí. Escríbele al Programa desde tu panel para cambiarlo."];

    public static RouteGroupBuilder MapConfirmacionDeCorreoEndpoints(this RouteGroupBuilder group)
    {
        // ANONIMA A PROPOSITO, y declarada como tal en AutorizacionPorDefectoTests: se abre desde el
        // buzón, donde por definición no hay sesión de SIMUS.
        group.MapPost("/externo/auth/confirmar-correo", ConfirmarAsync)
            .AllowAnonymous()
            .RequireRateLimiting("external-register")
            .WithName("ConfirmarCorreoDeLaCuenta")
            .WithTags("external-auth");

        group.MapPost("/externo/cuenta/reenviar-confirmacion", ReenviarAsync)
            .RequireAuthorization(SimusAuthentication.ExternalPolicy)
            .RequireRateLimiting("external-register")
            .WithName("ReenviarConfirmacionDeCorreo")
            .WithTags("external-auth");

        // CORREGIR LA DIRECCION ES PARTE DE LA CONFIRMACION, no una pantalla de perfil: quien
        // escribió mal su correo no puede abrir el enlace, y sin esta ruta su cuenta queda muerta.
        group.MapPut("/externo/cuenta/correo", CorregirAsync)
            .RequireAuthorization(SimusAuthentication.ExternalPolicy)
            .RequireRateLimiting("external-register")
            .WithName("CorregirCorreoDeLaCuenta")
            .WithTags("external-auth");

        return group;
    }

    private static async Task<IResult> ConfirmarAsync(
        ConfirmacionDeCorreoSolicitud solicitud,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var (resultado, cuenta) = await ConfirmacionDeCorreo.ConfirmarAsync(db, solicitud?.Testigo, ahora, ct);

        // SE DICE POR QUE FALLA. «El enlace venció» y «ese enlace no existe» piden dos cosas
        // distintas a quien lo abre, y un «no válido» genérico obliga a adivinar cuál de las dos es.
        if (resultado != ResultadoDeConfirmacion.Confirmado)
        {
            var mensaje = resultado switch
            {
                ResultadoDeConfirmacion.Vencido => EnlaceVencido,
                ResultadoDeConfirmacion.YaUsado => EnlaceYaUsado,
                ResultadoDeConfirmacion.CorreoCambiado => CorreoCambiado,
                _ => EnlaceNoValido,
            };
            return Results.BadRequest(new { testigo = mensaje });
        }

        // LAS ORGANIZACIONES QUE ESPERABAN A ESTA CUENTA PASAN A ACTIVAS. La regla vive en
        // `ConfirmacionDeCorreo` porque la comparte con la confirmación que hace el Programa desde
        // la consola: escrita dos veces, uno de los dos caminos acabaría dejando organizaciones a
        // medias.
        var activadas = await ConfirmacionDeCorreo.ActivarLasQueEsperabanAsync(db, cuenta!.Id, ahora, ct);

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { confirmado = true, organizacionesActivadas = activadas.Count });
    }

    private static async Task<IResult> ReenviarAsync(
        ClaimsPrincipal principal,
        PnmcDbContext db,
        IEnviadorDeCorreo correo,
        CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var personaId))
            return Results.Unauthorized();

        var cuenta = await db.Users.FirstOrDefaultAsync(item => item.Id == personaId, ct);
        if (cuenta is null) return Results.Unauthorized();

        // PEDIR OTRO ENLACE CUANDO YA ESTA CONFIRMADO NO ES UN ERROR, es una pregunta que se
        // responde: no se emite nada y se dice por qué.
        if (cuenta.CorreoConfirmado) return Results.Conflict(new { correo = YaConfirmado });

        await ConfirmacionDeCorreo.EmitirAsync(db, correo, cuenta, DateTime.UtcNow, ct);
        await db.SaveChangesAsync(ct);

        // SE DEVUELVE EL CORREO DE DESTINO para que la pantalla pueda decir «lo enviamos a a@b.c» y
        // quien se equivocó al escribirlo lo vea ahí mismo, que es el fallo que esto viene a cerrar.
        return Results.Ok(new { enviadoA = cuenta.Email, venceEn = ConfirmacionDeCorreo.Vigencia.TotalDays });
    }

    /// <summary>
    /// Corrige la dirección de una cuenta que todavía no ha confirmado, y manda el enlace a la nueva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SOLO MIENTRAS SIGA SIN CONFIRMAR.</b> Después, cambiar de correo exige comprobar el nuevo
    /// <b>antes</b> de soltar el anterior: si no, quien encuentre una sesión abierta se queda con la
    /// cuenta apuntándola a su propio buzón. Esa versión es otra ruta y otra decisión; esta cierra
    /// el caso que hoy deja cuentas muertas, que es la errata del primer día.
    /// </para>
    /// <para>
    /// <b>EMITIR EL ENLACE NUEVO ES PARTE DEL ACTO</b>, no un segundo paso que el panel tenga que
    /// acordarse de hacer. Y <see cref="ConfirmacionDeCorreo.EmitirAsync"/> vence los anteriores, así
    /// que el enlace que fue a la dirección equivocada deja de servir en el mismo momento.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CorregirAsync(
        CorreccionDeCorreoSolicitud solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        IEnviadorDeCorreo correo,
        CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var personaId))
            return Results.Unauthorized();

        var cuenta = await db.Users.FirstOrDefaultAsync(item => item.Id == personaId, ct);
        if (cuenta is null) return Results.Unauthorized();
        if (cuenta.CorreoConfirmado) return Results.Conflict(new { correo = NoSeCambiaConfirmado });

        var nuevo = CorreoElectronico.Normalizar(solicitud?.Correo);
        if (!ValidationHelpers.IsValidEmail(nuevo)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["correo"] = CorreoNoValido });
        if (string.Equals(nuevo, cuenta.Email, StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(new { correo = CorreoIgual });
        if (await db.Users.AnyAsync(item => item.Email == nuevo && item.Id != cuenta.Id, ct))
            return Results.Conflict(new { correo = CorreoEnUso });

        var ahora = DateTime.UtcNow;
        var anterior = cuenta.Email;
        cuenta.Email = nuevo;
        cuenta.UpdatedAt = ahora;

        await ConfirmacionDeCorreo.EmitirAsync(db, correo, cuenta, ahora, ct);

        // QUEDA EL RASTRO DEL CAMBIO. El correo es la llave de la cuenta: que cambie sin dejar de
        // dónde a dónde haría imposible responder después quién la apuntó a otro buzón.
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = cuenta.Id,
            TableName = "Usuarios",
            RecordId = cuenta.Id.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.Actualizar,
            PreviousValuesJson = JsonSerializer.Serialize(new { correo = anterior, correoConfirmado = false }),
            NewValuesJson = JsonSerializer.Serialize(new { correo = nuevo, correoConfirmado = false, corregidoPor = "la propia cuenta" }),
            CreatedAt = ahora,
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { enviadoA = cuenta.Email, venceEn = ConfirmacionDeCorreo.Vigencia.TotalDays });
    }
}
