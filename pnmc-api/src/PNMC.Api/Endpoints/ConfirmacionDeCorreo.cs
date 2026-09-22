using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// El enlace con el que una cuenta confirma que ese correo es suyo.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE PROBLEMA RESUELVE, CON EL CASO CONCRETO.</b> El alta externa crea la cuenta y nadie
/// comprueba nada. De ahí salen dos fallos que no son hipotéticos: quien se equivoca en una letra de
/// su correo queda atado para siempre a una dirección que no controla —el correo es único y es el de
/// la organización, así que ni siquiera un restablecimiento de contraseña le llega—, y quien
/// registra una organización con el correo de otra persona le quema esa dirección, porque el buzón
/// legítimo ya no puede usarla.
/// </para>
/// <para>
/// <b>QUE BLOQUEA Y QUE NO.</b> Bloquea <b>actuar en nombre de la organización</b>: registrar un
/// proceso, anunciar un evento, reclamar la administración de un Festival, enviarlo a revisión o
/// publicar una edición. No bloquea el acceso. Entrar, mirar el ecosistema y consultar se puede
/// desde el primer minuto, así que nadie queda encerrado fuera de una cuenta que acaba de crear.
/// </para>
/// <para>
/// <b>ESTO ENDURECIO UNA REGLA ANTERIOR.</b> Hasta la puerta estaba
/// solo donde el dato se le entregaba al Programa, y crear el registro se podía. El plan de
/// consolidación la movió antes: «sin correo confirmado no se crean nuevos procesos». El motivo es
/// que un borrador ya arrastra trabajo —y expectativa— de quien lo escribió, y descubrir el bloqueo
/// al final es peor que encontrarlo al principio.
/// </para>
/// <para>
/// <b>LA PUERTA ES UNA SOLA FUNCION.</b>
/// <see cref="AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync"/>. Cada ruta que la use
/// escribe una línea; escribir el 409 en cada sitio es como se llega a que una lo explique y las
/// otras cinco digan «no tiene permisos».
/// </para>
/// <para>
/// <b>SE GUARDA EL HASH Y NO EL TESTIGO.</b> Quien pueda leer <c>dbo.ConfirmacionesDeCorreo</c> no
/// debe poder confirmar el correo de nadie: es el mismo criterio con el que se guardan las
/// contraseñas. El testigo en claro existe una sola vez, dentro del enlace que se envía.
/// </para>
/// <para>
/// <b>EL TRANSPORTE ES LO UNICO QUE FALTA.</b> El envío pasa por <see cref="IEnviadorDeCorreo"/>,
/// cuya implementación de hoy deja el mensaje en la cola de salida con estado <c>pendiente</c>.
/// Conectar un proveedor real es registrar otra implementación; aquí no cambia nada.
/// </para>
/// </remarks>
internal static class ConfirmacionDeCorreo
{
    /// <summary>Cuánto vive un enlace.</summary>
    /// <remarks>
    /// TRES DIAS, y no una hora: el destinatario puede no mirar su correo hasta el lunes. Tampoco
    /// eterno, porque un enlace que no caduca es una llave permanente a una cuenta en cualquier
    /// buzón comprometido.
    /// </remarks>
    internal static readonly TimeSpan Vigencia = TimeSpan.FromDays(3);

    /// <summary>El tipo de evento con el que viaja el mensaje, para poder buscarlo después.</summary>
    internal const string TipoDeEvento = "ConfirmacionDeCorreo";

    /// <summary>
    /// Emite un enlace nuevo, invalida los anteriores y deja el mensaje en la cola de salida.
    /// </summary>
    /// <remarks>
    /// INVALIDAR LOS ANTERIORES ES PARTE DEL ACTO. Si «reenviar» dejara vivos los enlaces viejos,
    /// cambiar de correo no cerraría el acceso del anterior, que es la mitad del problema que esto
    /// viene a resolver. No guarda: quien llama decide la transacción.
    /// </remarks>
    internal static async Task EmitirAsync(
        PnmcDbContext db,
        IEnviadorDeCorreo correo,
        UserRow cuenta,
        DateTime ahora,
        CancellationToken ct)
    {
        await VencerLosEnlacesVivosAsync(db, cuenta.Id, ahora, ct);

        var testigo = NuevoTestigo();
        db.ConfirmacionesDeCorreo.Add(new ConfirmacionDeCorreoRow
        {
            IdUsuario = cuenta.Id,
            CorreoDestino = cuenta.Email,
            HashTestigo = Resumir(testigo),
            FechaEmision = ahora,
            FechaExpiracion = ahora.Add(Vigencia),
        });

        await correo.EncolarAsync(
            new CorreoSaliente(
                cuenta.Email,
                "Confirma tu correo en SIMUS",
                $"Hola, {cuenta.FullName}.\n\n"
                + "Para terminar de activar tu organización en SIMUS necesitamos comprobar que este "
                + "correo es tuyo. Abre este enlace:\n\n"
                + $"{RutaDeConfirmacion(testigo)}\n\n"
                + $"El enlace vence en {Vigencia.TotalDays:0} días. Si no lo pediste tú, puedes ignorar este mensaje.",
                TipoDeEvento,
                cuenta.Id),
            ct);
    }

    /// <summary>
    /// Confirma el correo de una cuenta con el testigo recibido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DEVUELVE POR QUE FALLA, y no un booleano: «el enlace venció» y «ese enlace no existe» piden
    /// dos cosas distintas a quien lo abre —pedir otro, o revisar de dónde salió—, y un solo «no
    /// válido» obliga a adivinar.
    /// </para>
    /// <para>
    /// NO GUARDA: quien llama mueve también el estado de la organización, y las dos cosas son un
    /// solo acto.
    /// </para>
    /// </remarks>
    internal static async Task<(ResultadoDeConfirmacion Resultado, UserRow? Cuenta)> ConfirmarAsync(
        PnmcDbContext db, string? testigo, DateTime ahora, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(testigo)) return (ResultadoDeConfirmacion.NoExiste, null);

        var fila = await db.ConfirmacionesDeCorreo
            .FirstOrDefaultAsync(item => item.HashTestigo == Resumir(testigo.Trim()), ct);
        if (fila is null) return (ResultadoDeConfirmacion.NoExiste, null);
        if (fila.FechaUso is not null) return (ResultadoDeConfirmacion.YaUsado, null);
        if (fila.FechaExpiracion <= ahora) return (ResultadoDeConfirmacion.Vencido, null);

        var cuenta = await db.Users.FirstOrDefaultAsync(item => item.Id == fila.IdUsuario, ct);
        if (cuenta is null) return (ResultadoDeConfirmacion.NoExiste, null);

        // EL CORREO PUDO CAMBIAR entre la emisión y la apertura. Confirmar entonces daría por bueno
        // un buzón que ya no es el de la cuenta.
        if (!string.Equals(cuenta.Email, fila.CorreoDestino, StringComparison.OrdinalIgnoreCase))
            return (ResultadoDeConfirmacion.CorreoCambiado, null);

        fila.FechaUso = ahora;
        cuenta.CorreoConfirmado = true;
        cuenta.FechaConfirmacionCorreo = ahora;
        return (ResultadoDeConfirmacion.Confirmado, cuenta);
    }

    /// <summary>
    /// Pasa a <c>activa</c> las organizaciones que estaban esperando a que esta cuenta confirmara.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SOLO LAS QUE ESPERABAN.</b> Una organización inactiva o eliminada no vuelve a la vida
    /// porque alguien confirme un correo: esas dos son decisiones del Programa y se deshacen con
    /// otra decisión del Programa, no de rebote.
    /// </para>
    /// <para>
    /// <b>TODAS LAS SUYAS Y NO SOLO UNA.</b> Una misma persona puede responder por varias
    /// organizaciones. Como la puerta pregunta por la cuenta —«¿alguna responsable con correo
    /// comprobado?»—, confirmar una y dejar a las hermanas en «pendiente» dejaría el sello diciendo
    /// una cosa y la puerta haciendo otra.
    /// </para>
    /// <para>
    /// <b>ES DE LOS DOS CAMINOS.</b> La usan el enlace que abre la propia organización y la
    /// confirmación que hace el Programa desde la consola. Escribir la regla dos veces es como se
    /// llega a que uno de los dos caminos deje organizaciones a medias.
    /// </para>
    /// NO GUARDA: quien llama decide la transacción.
    /// </remarks>
    internal static async Task<List<EntityProfileRow>> ActivarLasQueEsperabanAsync(
        PnmcDbContext db, int cuentaId, DateTime ahora, CancellationToken ct)
    {
        var suyas = await db.UserEntities.AsNoTracking()
            .Where(vinculo => vinculo.UserId == cuentaId && vinculo.IsActive
                && RolesDeEntidad.QueResponden.Contains(vinculo.EntityRole))
            .Select(vinculo => vinculo.EntityId)
            .ToListAsync(ct);

        var activadas = await db.EntityProfiles
            .Where(organizacion => suyas.Contains(organizacion.Id)
                && organizacion.StatusCode == EstadosDeOrganizacion.PendienteDeConfirmacion)
            .ToListAsync(ct);

        foreach (var organizacion in activadas)
        {
            organizacion.StatusCode = EstadosDeOrganizacion.Activa;
            organizacion.IsActive = EstadosDeOrganizacion.VigenciaDe(organizacion.StatusCode);
            organizacion.UpdatedAt = ahora;
        }

        return activadas;
    }

    /// <summary>
    /// Invalida los enlaces de confirmación que sigan vivos para esa cuenta.
    /// </summary>
    /// <remarks>
    /// SE VENCEN, NO SE MARCAN COMO USADOS: nadie los usó. La distinción importa cuando alguien
    /// abra después el enlace que tenía en el buzón —la pantalla debe decirle «este enlace venció»
    /// y no «ya lo usaste», que le haría dudar de si entró alguien más—. No guarda.
    /// </remarks>
    internal static async Task VencerLosEnlacesVivosAsync(
        PnmcDbContext db, int cuentaId, DateTime ahora, CancellationToken ct)
    {
        var vivas = await db.ConfirmacionesDeCorreo
            .Where(fila => fila.IdUsuario == cuentaId && fila.FechaUso == null && fila.FechaExpiracion > ahora)
            .ToListAsync(ct);
        foreach (var anterior in vivas) anterior.FechaExpiracion = ahora;
    }

    /// <summary>La ruta pública del sitio que recibe el testigo.</summary>
    internal static string RutaDeConfirmacion(string testigo) => $"/ecosistema/confirmar-correo?testigo={testigo}";

    private static string NuevoTestigo() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Resumir(string testigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(testigo))).ToLowerInvariant();
}

/// <summary>Por qué salió bien o mal la confirmación.</summary>
internal enum ResultadoDeConfirmacion
{
    Confirmado,
    NoExiste,
    Vencido,
    YaUsado,
    CorreoCambiado,
}
