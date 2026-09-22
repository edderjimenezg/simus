using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// El borrador que se guarda solo mientras alguien llena un formulario de la consola.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE PROBLEMA RESUELVE.</b> Los formularios de Agenda, Noticias y Catálogo Editorial se llenan
/// dentro de un diálogo, de una sentada. Un corte de conexión, una sesión caducada o una pestaña
/// cerrada sin querer se llevaban por delante todo lo escrito y sin rastro. Con esto, volver a
/// abrir el formulario ofrece recuperar lo último que llegó al servidor.
/// </para>
/// <para>
/// <b>NO CREA CONTENIDO.</b> La alternativa —crear el evento o la noticia en estado «borrador» al
/// primer teclazo— llenaría la consola de fichas a medias que alguien tendría que ir archivando.
/// Un borrador vive en <c>dbo.BorradoresProceso</c>, no aparece en ninguna lista de contenido y
/// desaparece en cuanto el formulario se guarda de verdad.
/// </para>
/// <para>
/// <b>UNO POR PERSONA Y DOMINIO, Y LO GARANTIZA LA BASE</b> con un índice único filtrado a los
/// abiertos. Sin él, dos pestañas abiertas producirían dos borradores y recuperar sería una
/// lotería sobre cuál de los dos sale.
/// </para>
/// <para>
/// <b>ES EL MISMO MECANISMO DEL ASISTENTE EXTERNO DE FESTIVAL</b>, con la organización en blanco:
/// quien administra desde el Ministerio no actúa en nombre de ninguna entidad del ecosistema.
/// </para>
/// </remarks>
public static class BorradoresDeConsolaEndpoints
{
    /// <summary>Los formularios de la consola que guardan borrador. Uno por dominio.</summary>
    /// <summary>Los módulos que admiten un borrador de consola.</summary>
    /// <remarks>
    /// DECIA «editorial» Y LA BASE YA DECIA «catalogo-editorial». La unificación del vocabulario del
    /// 21 de septiembre de 2026 tradujo los valores guardados, y esta lista se quedó con el nombre
    /// viejo: los cinco borradores del Catálogo Editorial que había dejaron de encontrarse, sin que
    /// nada fallara. Por eso ahora se nombran desde <see cref="Modulos"/> y no como cadenas sueltas.
    /// </remarks>
    public static readonly string[] DominiosValidos =
        [Modulos.Agenda, Modulos.Noticias, Modulos.CatalogoEditorial];

    private static readonly string[] DominioDesconocido =
        ["Ese formulario no guarda borradores. Los que sí: agenda, noticias, editorial."];
    private static readonly string[] JsonInvalido = ["El borrador debe contener JSON válido."];
    private static readonly string[] BorradorCambiado =
        ["El borrador cambió en otra pestaña. Recárgalo antes de seguir escribiendo."];

    public static RouteGroupBuilder MapBorradoresDeConsolaEndpoints(this RouteGroupBuilder api)
    {
        var consola = api.MapGroup("/institucional/borradores")
            .WithTags("borradores")
            .RequireAuthorization(Permisos.PoliticaFuncionario);

        consola.MapGet("/{dominio}", Leer).WithName("LeerBorradorDeConsola");
        consola.MapPut("/{dominio}", Guardar).WithName("GuardarBorradorDeConsola");
        consola.MapDelete("/{dominio}", Descartar).WithName("DescartarBorradorDeConsola");
        return api;
    }

    /// <summary>
    /// El borrador abierto de quien pregunta, si lo hay.
    /// </summary>
    /// <remarks>
    /// DEVUELVE 204 Y NO 404 CUANDO NO HAY NINGUNO. No haber empezado nada no es un error, y un
    /// 404 obligaría a cada pantalla a distinguir «no hay borrador» de «la ruta está mal».
    /// </remarks>
    private static async Task<IResult> Leer(PnmcDbContext db, ClaimsPrincipal principal, string dominio, CancellationToken ct)
    {
        var pedido = Normalizar(dominio);
        if (pedido is null) return Results.BadRequest(new { dominio = DominioDesconocido });

        var persona = Actor(principal);
        var fila = await db.BorradoresDeProceso.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ModuloId == pedido && x.PersonaId == persona && x.Estado == "borrador", ct);

        return fila is null ? Results.NoContent() : Results.Ok(ADto(fila));
    }

    private static async Task<IResult> Guardar(
        PnmcDbContext db, ClaimsPrincipal principal, string dominio,
        GuardarBorradorDeConsolaSolicitud solicitud, CancellationToken ct)
    {
        var pedido = Normalizar(dominio);
        if (pedido is null) return Results.BadRequest(new { dominio = DominioDesconocido });

        var datos = string.IsNullOrWhiteSpace(solicitud.DatosJson) ? "{}" : solicitud.DatosJson;
        // SE COMPRUEBA QUE SEA JSON antes de escribirlo: guardar texto roto haría irrecuperable
        // justo lo que este mecanismo existe para recuperar.
        try { using var _ = JsonDocument.Parse(datos); }
        catch (JsonException) { return Results.BadRequest(new { datosJson = JsonInvalido }); }

        var persona = Actor(principal);
        var ahora = DateTime.UtcNow;
        var fila = await db.BorradoresDeProceso
            .FirstOrDefaultAsync(x => x.ModuloId == pedido && x.PersonaId == persona && x.Estado == "borrador", ct);

        if (fila is null)
        {
            fila = new BorradorDeProcesoRow
            {
                ModuloId = pedido,
                // EN BLANCO A PROPOSITO: quien administra desde el Ministerio no actúa en nombre
                // de ninguna entidad del ecosistema, y rellenarlo inventaría un dato que después
                // acabaría contándose en algún informe de participación.
                OrganizacionId = null,
                PersonaId = persona,
                Estado = "borrador",
                DatosJson = datos,
                Version = 1,
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            };
            db.BorradoresDeProceso.Add(fila);
        }
        else
        {
            if (solicitud.Version is int version && version != fila.Version)
            {
                return Results.Conflict(new { version = BorradorCambiado });
            }

            fila.DatosJson = datos;
            fila.Version += 1;
            fila.FechaActualizacion = ahora;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(fila));
    }

    /// <summary>
    /// Cierra el borrador abierto.
    /// </summary>
    /// <remarks>
    /// SE MARCA CERRADO, NO SE BORRA LA FILA. El índice único solo mira los abiertos, así que
    /// cerrar deja sitio para el siguiente sin perder cuándo existió; y descartar sin haber
    /// empezado nada responde que sí igualmente, porque el resultado es el que se pedía.
    /// </remarks>
    private static async Task<IResult> Descartar(PnmcDbContext db, ClaimsPrincipal principal, string dominio, CancellationToken ct)
    {
        var pedido = Normalizar(dominio);
        if (pedido is null) return Results.BadRequest(new { dominio = DominioDesconocido });

        var persona = Actor(principal);
        var fila = await db.BorradoresDeProceso
            .FirstOrDefaultAsync(x => x.ModuloId == pedido && x.PersonaId == persona && x.Estado == "borrador", ct);

        if (fila is not null)
        {
            fila.Estado = "cerrado";
            fila.FechaActualizacion = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    /// <summary>
    /// El dominio en minúsculas si es uno de los conocidos; <c>null</c> si no.
    /// </summary>
    /// <remarks>
    /// SE VALIDA CONTRA UNA LISTA y no se acepta cualquier texto: sin esto, cualquiera podría
    /// escribir en la misma tabla con un dominio inventado y convertirla en un almacén libre de
    /// JSON asociado a su cuenta.
    /// </remarks>
    private static string? Normalizar(string dominio)
    {
        var pedido = (dominio ?? string.Empty).Trim().ToLowerInvariant();
        return Array.Exists(DominiosValidos, d => d == pedido) ? pedido : null;
    }

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static BorradorDeConsolaDto ADto(BorradorDeProcesoRow fila) =>
        new(fila.Id, fila.ModuloId, fila.DatosJson, fila.Version, fila.FechaActualizacion);
}
