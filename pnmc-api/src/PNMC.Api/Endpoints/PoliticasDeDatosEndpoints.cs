using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los textos que se aceptan, y lo que cada cuenta ha aceptado.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL TEXTO LO SIRVE EL SERVIDOR, SIEMPRE.</b> Ninguna pantalla escribe dentro de sí la frase
/// del consentimiento. Si lo hiciera, lo que la persona lee y lo que el servidor guarda como
/// evidencia serían dos cosas distintas que nadie compara nunca, y un cambio de redacción en el
/// HTML no dejaría rastro en ninguna fila. Esta es la razón por la que estas rutas existen antes
/// que la pantalla.
/// </para>
/// <para>
/// <b>LAS PÚBLICAS SON ANÓNIMAS A PROPÓSITO.</b> Quien se va a registrar todavía no tiene cuenta, y
/// quien solo quiere leer qué hace este sistema con los datos de la gente no tiene por qué crearse
/// una. El art. 12 de la Ley 1581 obliga a informar <i>antes</i> de la autorización; detrás de una
/// sesión, esa información llegaría después.
/// </para>
/// <para>
/// <b>«MIS AUTORIZACIONES» ES UN DERECHO, NO UNA PANTALLA DE CORTESÍA.</b> El art. 8 num. 2 y 3 de
/// la Ley 1581 da al titular el derecho a solicitar prueba de la autorización y a ser informado del
/// uso dado a sus datos. Esa prueba es el texto copiado en cada fila, y esta ruta es por donde se
/// entrega.
/// </para>
/// </remarks>
public static class PoliticasDeDatosEndpoints
{
    public static RouteGroupBuilder MapPoliticasDeDatosEndpoints(this RouteGroupBuilder group)
    {
        MapPublicas(group);
        MapDeLaCuenta(group);
        return group;
    }

    // -----------------------------------------------------------------------------------------
    // Lo que cualquiera puede leer
    // -----------------------------------------------------------------------------------------
    private static void MapPublicas(RouteGroupBuilder group)
    {
        var publico = group.MapGroup("/publico/politicas").WithTags("politicas-de-datos");

        publico.MapGet("/", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
            Results.Ok(await VigentesAsync(dbContext, cancellationToken)))
            .AllowAnonymous();

        publico.MapGet("/{clave}", async (
            string clave,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            // SE COMPRUEBA CONTRA EL CATALOGO ANTES DE IR A LA BASE. Una clave inventada devuelve
            // 404 igual, pero preguntando primero se evita que esta ruta sirva de sonda para
            // averiguar que filas hay.
            if (!FinalidadesDeDatos.EsConocida(clave)) return Results.NotFound();

            var politica = await RegistroDeAutorizaciones.PoliticaVigenteAsync(dbContext, clave, cancellationToken);
            return politica is null
                ? Results.NotFound()
                : Results.Ok(new PoliticaPublicaDto(
                    politica.Clave, politica.Version, politica.Titulo, politica.Texto,
                    politica.UrlOficial, politica.ReferenciaOficial));
        }).AllowAnonymous();
    }

    // -----------------------------------------------------------------------------------------
    // Lo que ve y hace la persona sobre lo suyo
    // -----------------------------------------------------------------------------------------
    private static void MapDeLaCuenta(RouteGroupBuilder group)
    {
        var mias = group.MapGroup("/externo/mis-autorizaciones").WithTags("politicas-de-datos");
        mias.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        mias.MapGet("/", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var titular = await ResolverTitularAsync(principal, dbContext, cancellationToken);
            if (titular is null) return Results.Unauthorized();

            return Results.Ok(await LeerAutorizacionesAsync(dbContext, titular.Value.IdUsuario, titular.Value.Correo, cancellationToken));
        });

        mias.MapPost("/{finalidad}/revocar", async (
            string finalidad,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La solicitud no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            if (!FinalidadesDeDatos.EsConocida(finalidad)) return Results.NotFound();

            // LA NEGATIVA VIENE CON SU MOTIVO Y NO COMO UN 403 SECO. Quien pide retirar una
            // autorización está ejerciendo un derecho; si no procede, la ley exige decirle por qué,
            // no dejarle delante una puerta cerrada sin explicación.
            if (!FinalidadesDeDatos.SePuedeRevocarSolo(finalidad))
                return Results.Conflict(new { message = FinalidadesDeDatos.MotivoDeNoRevocable(finalidad) });

            var titular = await ResolverTitularAsync(principal, dbContext, cancellationToken);
            if (titular is null) return Results.Unauthorized();

            var ahora = DateTime.UtcNow;
            var retiradas = await RegistroDeAutorizaciones.RevocarAsync(
                dbContext, finalidad, ahora, "Retirada por el titular desde su cuenta.",
                titular.Value.IdUsuario, titular.Value.Correo, cancellationToken);

            // EL BOLETIN ES LO UNICO REVOCABLE HOY, y retirar su autorización tiene que apagar
            // también la lista de envío. Dejar la suscripción activa haría que el sistema siguiera
            // escribiéndole a alguien que acaba de decir que no: la pantalla diría «retirada» y los
            // correos seguirían saliendo.
            if (string.Equals(finalidad, FinalidadesDeDatos.Boletin, StringComparison.Ordinal))
            {
                var suscripciones = await dbContext.BoletinSuscripciones
                    .Where(item => item.CorreoElectronico == titular.Value.Correo && item.FechaBaja == null)
                    .ToListAsync(cancellationToken);
                foreach (var suscripcion in suscripciones)
                {
                    suscripcion.Estado = "baja";
                    suscripcion.FechaBaja = ahora;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await LeerAutorizacionesAsync(dbContext, titular.Value.IdUsuario, titular.Value.Correo, cancellationToken));
        });
    }

    // -----------------------------------------------------------------------------------------

    private static async Task<IReadOnlyList<PoliticaPublicaDto>> VigentesAsync(
        PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var politicas = await dbContext.PoliticasDeDatos.AsNoTracking()
            .Where(item => item.Vigente)
            .ToListAsync(cancellationToken);

        // EL ORDEN LO PONE EL CATALOGO Y NO LA BASE. `Todas` las lista como se presentan —primero
        // lo que sostiene la cuenta, al final lo voluntario—, y ordenar por clave alfabética las
        // pondría en un orden que no significa nada.
        return [.. FinalidadesDeDatos.Todas
            .Select(clave => politicas.FirstOrDefault(item => item.Clave == clave))
            .Where(item => item is not null)
            .Select(item => new PoliticaPublicaDto(
                item!.Clave, item.Version, item.Titulo, item.Texto, item.UrlOficial, item.ReferenciaOficial))];
    }

    /// <summary>
    /// Lo que esta persona ha autorizado, incluido lo que retiró.
    /// </summary>
    /// <remarks>
    /// <b>LO RETIRADO TAMBIEN SE ENSEÑA.</b> Una lista que solo muestra lo vigente no permite
    /// comprobar que algo se retiró de verdad ni cuándo, que es justo lo que quiere ver quien acaba
    /// de retirarlo. La pantalla las distingue; el servidor las manda las dos.
    /// </remarks>
    private static async Task<IReadOnlyList<AutorizacionDeDatosDto>> LeerAutorizacionesAsync(
        PnmcDbContext dbContext, int idUsuario, string correo, CancellationToken cancellationToken)
    {
        var filas = await dbContext.AutorizacionesDeDatos.AsNoTracking()
            .Where(item => item.IdUsuario == idUsuario || item.CorreoTitular == correo)
            .OrderByDescending(item => item.FechaOtorgada)
            .ToListAsync(cancellationToken);

        var titulos = await dbContext.PoliticasDeDatos.AsNoTracking()
            .ToDictionaryAsync(item => item.Id, item => item.Titulo, cancellationToken);

        return [.. filas.Select(item => new AutorizacionDeDatosDto(
            item.Id,
            item.Finalidad,
            titulos.TryGetValue(item.IdPolitica, out var titulo) ? titulo : item.Finalidad,
            item.Version,
            item.TextoAceptado,
            item.TextoReconstruido,
            item.Origen,
            item.FechaOtorgada,
            item.FechaRevocacion,
            item.EstaVigente,
            item.EstaVigente && FinalidadesDeDatos.SePuedeRevocarSolo(item.Finalidad),
            FinalidadesDeDatos.SePuedeRevocarSolo(item.Finalidad) ? null : FinalidadesDeDatos.MotivoDeNoRevocable(item.Finalidad)))];
    }

    private static async Task<(int IdUsuario, string Correo)?> ResolverTitularAsync(
        ClaimsPrincipal principal, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var valor = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idUsuario)) return null;

        var correo = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == idUsuario && user.IsActive)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return correo is null ? null : (idUsuario, correo);
    }
}
