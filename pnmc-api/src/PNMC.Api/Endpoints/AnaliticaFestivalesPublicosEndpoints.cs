using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class AnaliticaFestivalesPublicosEndpoints
{
    public static RouteGroupBuilder MapAnaliticaFestivalesPublicosEndpoints(this RouteGroupBuilder group)
    {
        // ANONIMA A PROPOSITO (27 de 39). El tablero publico de cifras de festivales: la
        // pide la pagina abierta de analitica (/api/v1/publico/analitica/festivales/resumen
        // en pnmc-web) sin sesion. Devuelve solo distribuciones —cuantos festivales por
        // departamento, municipio, practica musical, territorio sonoro y periodicidad—,
        // ni una fila ni un nombre propio, y las cuenta sobre la misma lectura publicada
        // que el resto (LecturaFestivalesPublicados), de modo que un borrador no engorda
        // ningun total.
        //
        // PNMC-050. Anonima y sin tope, era la lectura mas cara del sitio publico: cada
        // llamada rehacia la agregacion entera. Lo que se hizo, y lo que NO:
        //
        //   * SI: cupo por minuto y por origen (LimiteDeTasaAnaliticaPublica, mas abajo).
        //   * SI: los nombres del DANE salen de CacheDivipola —el mismo singleton que ya
        //     usa el geovisor— en vez de traerse la tabla DIVIPOLA entera (1.122 filas
        //     medidas en la base local) en CADA peticion. Es dato inmutable: la unica
        //     escritura esta en DatabaseBootstrapper, al arrancar.
        //   * NO: no se cachea la RESPUESTA. Un total con minutos de retraso es una
        //     decision de producto (cuanta demora tolera el tablero publico), no de
        //     rendimiento, y no se toma aqui.
        group.MapGet("/publico/analitica/festivales/resumen", async (
            PnmcDbContext dbContext,
            CacheDivipola divipola,
            CancellationToken cancellationToken) =>
        {
            var festivales = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken);
            var (departamentos, municipios) = await divipola.ObtenerAsync(dbContext, cancellationToken);
            return Results.Ok(new ResumenAnaliticoFestivalesDto(
                festivales.Count,
                Distribuir(festivales.Select(item => departamentos.TryGetValue(item.CodigoDepartamento ?? string.Empty, out var nombre) ? nombre : item.CodigoDepartamento)),
                Distribuir(festivales.Select(item => municipios.TryGetValue(item.CodigoMunicipio ?? string.Empty, out var nombre) ? nombre : item.CodigoMunicipio)),
                Distribuir(festivales.SelectMany(item => item.PracticasMusicales.Select(practica => practica.Nombre))),
                Distribuir(festivales.SelectMany(item => item.TerritoriosSonoros.Select(territorio => territorio.Nombre))),
                Distribuir(festivales.Select(item => item.Periodicidad))));
        })
        .WithTags("analitica-festivales")
        .AllowAnonymous()
        .RequireRateLimiting(new LimiteDeTasaAnaliticaPublica());

        return group;
    }

    private static List<DistribucionAnaliticaFestivalDto> Distribuir(IEnumerable<string?> valores) =>
        valores.Where(item => !string.IsNullOrWhiteSpace(item))
            .GroupBy(item => item!.Trim())
            .Select(item => new DistribucionAnaliticaFestivalDto(item.Key, item.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre)
            .ToList();
}

/// <summary>
/// PNMC-050. El cupo del tablero publico de analitica: 120 peticiones por minuto y por
/// direccion de origen.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE VIVE AQUI Y NO EN <c>Program.cs</c>. Las cinco politicas con nombre de
/// <c>Program.cs</c> son de ESCRITURA o de credencial —login, registro, guardado, importacion—
/// y comparten la exencion del entorno "Test" porque la suite institucional inicia sesion
/// decenas de veces por minuto contra el mismo host. Esta es una LECTURA publica: la suite
/// entera la pide dos veces (ApiIntegrationTests y AutorizacionPorDefectoTests), asi que no
/// necesita exencion ninguna. Declararla como instancia, junto al endpoint que protege, evita
/// heredar esa exencion por descuido: sin ella el limitador corre TAMBIEN en pruebas y la
/// prueba de 429 ejercita el mecanismo de verdad, en vez de pasar en verde contra un
/// <c>GetNoLimiter</c>. Ver <c>Pnmc050AnaliticaPublicaTests</c>.
/// </para>
/// <para>
/// EL NUMERO. La peticion cuesta unos 5 ms de CPU medidos (ver la nota de medicion en
/// <c>Pnmc050AnaliticaPublicaTests</c>), de modo que 120 por minuto acotan a un origen en
/// torno al 1 % de un nucleo. Por arriba frena el abuso; por abajo no estorba: la pagina de
/// analitica pide el resumen una vez por visita, y 120 por minuto deja sitio de sobra a una
/// alcaldia entera saliendo por una sola IP publica.
/// </para>
/// <para>
/// EL LIMITE DEL LIMITE, ESCRITO. Se reparte por <c>RemoteIpAddress</c>, igual que
/// <c>external-login</c> y <c>admin-login</c>. Detras de un proxy inverso que no reescriba la
/// direccion de origen, TODAS las visitas caerian en la particion del proxy y el cupo se
/// volveria una ventana global —es decir, el propio limite seria la caida del servicio—. El
/// dia que esto se despliegue hay que configurar <c>UseForwardedHeaders</c>; hoy no esta
/// configurado en <c>Program.cs</c> y el proyecto no ha salido de local.
/// </para>
/// </remarks>
internal sealed class LimiteDeTasaAnaliticaPublica : IRateLimiterPolicy<string>
{
    /// <summary>Peticiones admitidas por minuto y por direccion de origen.</summary>
    internal const int CupoPorMinutoYPorOrigen = 120;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => (contexto, _) =>
    {
        // Se fija el codigo aqui y no se confia en RejectionStatusCode de Program.cs: esta
        // politica es autonoma y debe responder 429 aunque alguien cambie aquella opcion.
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            contexto.HttpContext.Response.Headers.RetryAfter =
                ((int)espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var origen = httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocido";
        return RateLimitPartition.GetFixedWindowLimiter(origen, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = CupoPorMinutoYPorOrigen,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
