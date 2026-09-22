using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// El único sitio por el que se otorga o se retira una autorización de datos.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUÉ ESTO ES UNA PIEZA Y NO CÓDIGO REPETIDO EN CADA PUERTA.</b> Antes había tres formas de
/// guardar lo mismo —el alta de organización, el boletín y un booleano suelto en la ficha de la
/// persona responsable— y las tres guardaban cosas distintas. La que copiaba el texto no guardaba
/// la versión; la que guardaba la versión no copiaba el texto; la tercera no guardaba ninguna de
/// las dos. Con una sola pieza, una puerta nueva no puede inventarse una cuarta manera.
/// </para>
/// <para>
/// <b>LO QUE SIEMPRE HACE, VENGA DE DONDE VENGA.</b> Lee la política vigente de esa finalidad y
/// <b>copia su texto y su versión dentro de la autorización</b>. Sin política vigente no escribe
/// nada y lo dice: una autorización sin texto no prueba nada y es peor que no tenerla, porque
/// aparenta que sí.
/// </para>
/// </remarks>
public static class RegistroDeAutorizaciones
{
    /// <summary>
    /// Otorga una autorización, copiando dentro el texto vigente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO LLAMA A SaveChanges.</b> Quien la usa suele estar dentro de una transacción —el alta de
    /// organización escribe en cinco tablas—, y guardar aquí partiría esa transacción en dos. La
    /// fila queda añadida al contexto y se guarda con el resto.
    /// </para>
    /// <para>
    /// <b>NO COMPRUEBA SI YA HABÍA UNA.</b> Autorizar dos veces la misma finalidad es legítimo
    /// —ocurre cuando cambia el texto y se vuelve a pedir— y cada vez es una fila propia. Quien
    /// necesite saber si está vigente ahora mismo pregunta por la última sin revocar, que es lo que
    /// hace <see cref="EstaVigenteAsync"/>.
    /// </para>
    /// </remarks>
    /// <returns>La fila añadida, o <c>null</c> si no hay política vigente para esa finalidad.</returns>
    public static async Task<AutorizacionDeDatosRow?> OtorgarAsync(
        PnmcDbContext dbContext,
        string finalidad,
        string origen,
        DateTime cuando,
        int? idUsuario,
        string? correoTitular,
        string? referenciaId,
        CancellationToken cancellationToken)
    {
        if (idUsuario is null && string.IsNullOrWhiteSpace(correoTitular))
            throw new ArgumentException("Una autorización necesita una cuenta o un correo: sin titular no es de nadie.", nameof(idUsuario));

        var politica = await PoliticaVigenteAsync(dbContext, finalidad, cancellationToken);
        if (politica is null) return null;

        var autorizacion = new AutorizacionDeDatosRow
        {
            Finalidad = finalidad,
            IdPolitica = politica.Id,
            Version = politica.Version,
            TextoAceptado = politica.Texto,
            // Capturada de verdad: es el texto que el servidor acaba de servir a esa pantalla.
            TextoReconstruido = false,
            IdUsuario = idUsuario,
            CorreoTitular = string.IsNullOrWhiteSpace(correoTitular) ? null : correoTitular.Trim(),
            Origen = origen,
            ReferenciaId = referenciaId,
            FechaOtorgada = cuando,
        };

        dbContext.AutorizacionesDeDatos.Add(autorizacion);
        return autorizacion;
    }

    /// <summary>
    /// Retira todas las autorizaciones vigentes de una finalidad para un titular.
    /// </summary>
    /// <remarks>
    /// <b>PONE FECHA, NO BORRA.</b> Borrar la fila dejaría sin respuesta la pregunta «¿esta persona
    /// autorizó alguna vez, y hasta cuándo?», que es justo la que hay que poder contestar años
    /// después. Se marcan TODAS las vigentes y no solo la última: si por lo que sea quedaron dos
    /// abiertas, dejar una viva haría que la pantalla dijera «retirada» y el sistema siguiera
    /// teniendo permiso.
    /// </remarks>
    /// <returns>Cuántas se retiraron.</returns>
    public static async Task<int> RevocarAsync(
        PnmcDbContext dbContext,
        string finalidad,
        DateTime cuando,
        string motivo,
        int? idUsuario,
        string? correoTitular,
        CancellationToken cancellationToken)
    {
        var correo = string.IsNullOrWhiteSpace(correoTitular) ? null : correoTitular.Trim();

        var vigentes = await dbContext.AutorizacionesDeDatos
            .Where(item => item.Finalidad == finalidad
                && item.FechaRevocacion == null
                && ((idUsuario != null && item.IdUsuario == idUsuario)
                    || (correo != null && item.CorreoTitular == correo)))
            .ToListAsync(cancellationToken);

        foreach (var autorizacion in vigentes)
        {
            autorizacion.FechaRevocacion = cuando;
            autorizacion.MotivoRevocacion = motivo;
        }

        return vigentes.Count;
    }

    /// <summary>Si esa finalidad está autorizada ahora mismo por ese titular.</summary>
    public static async Task<bool> EstaVigenteAsync(
        PnmcDbContext dbContext,
        string finalidad,
        int? idUsuario,
        string? correoTitular,
        CancellationToken cancellationToken)
    {
        var correo = string.IsNullOrWhiteSpace(correoTitular) ? null : correoTitular.Trim();

        return await dbContext.AutorizacionesDeDatos.AsNoTracking()
            .AnyAsync(item => item.Finalidad == finalidad
                && item.FechaRevocacion == null
                && ((idUsuario != null && item.IdUsuario == idUsuario)
                    || (correo != null && item.CorreoTitular == correo)),
                cancellationToken);
    }

    /// <summary>La redacción en uso de una finalidad, o <c>null</c> si no hay ninguna publicada.</summary>
    public static Task<PoliticaDeDatosRow?> PoliticaVigenteAsync(
        PnmcDbContext dbContext, string finalidad, CancellationToken cancellationToken) =>
        dbContext.PoliticasDeDatos.AsNoTracking()
            .Where(item => item.Clave == finalidad && item.Vigente)
            .FirstOrDefaultAsync(cancellationToken);
}
