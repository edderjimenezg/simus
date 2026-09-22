using PNMC.Api.Security;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Observability;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class FestivalesPublicosEndpoints
{
    /// <summary>
    /// PNMC-039. Categoria de registro de la lectura publica. Tercero de los tres endpoints
    /// instrumentados como patron, y el que da mas miedo: es anonimo, sirve datos de contacto y
    /// es el que mas trafico va a ver. Justamente por eso es el que fija la regla mas dura de la
    /// guia — de una lectura publica se registra CUANTO se sirvio, jamas QUE se sirvio.
    /// </summary>
    private const string CategoriaRegistro = "PNMC.LecturaPublica";

    public static RouteGroupBuilder MapFestivalesPublicosEndpoints(this RouteGroupBuilder group)
    {
        var publico = group.MapGroup("/publico/festivales").WithTags("festivales-publicos");

        // ANONIMAS A PROPOSITO (25 y 26 de 39). El listado y la ficha publica de festival:
        // el destino del ciclo de revision institucional. Todo el trabajo del canal
        // externo —proponer, revisar, aprobar, versionar— existe para que el resultado se
        // vea AQUI, sin cuenta. La ruta lo lleva en el nombre y lo confirma la lectura:
        // ambas pasan por LecturaFestivalesPublicados.ConsultarAsync, que filtra a
        // StatusCode publicado y retira las versiones cuya propuesta no quedo Publicada
        // (PNMC-054). Un borrador o un festival en revision responde 404 aqui, y esa es
        // la frontera que separa lo publicado de lo interno; la sesion no pinta nada.
        //
        // DATOS DE CONTACTO: las dos devuelven CorreoContacto del festival — son la sexta
        // y la septima de las siete lecturas anonimas con datos de contacto. Se dejan
        // como estan y se anotan: que el correo deba enmascararse es una decision
        // pendiente dla direccion de producto, no de este carril.
        publico.MapGet("", async (
            PnmcDbContext dbContext,
            ILoggerFactory registros,
            int? limit,
            int? offset,
            CancellationToken cancellationToken) =>
        {
            // PNMC-039. PATRON DE INSTRUMENTACION, LECTURA PUBLICA.
            //
            // QUE SE REGISTRA: la FORMA de la respuesta —cuantas fichas se devolvieron, sobre
            // cuantas publicadas, cuantas de ellas llevaban dato de contacto— y lo que tardo.
            // Eso responde a lo que se pregunta de verdad de una ruta publica: si esta sirviendo
            // algo, si se ha quedado vacia despues de un despliegue, y si se esta poniendo lenta.
            //
            // QUE NO SE REGISTRA, Y ES LA REGLA QUE ESTE ENDPOINT EXISTE PARA FIJAR: ni un solo
            // valor de los que sirve. Esta ruta devuelve CorreoContacto —es una de las siete
            // lecturas anonimas con datos de contacto— y registrar el cuerpo de la respuesta,
            // aunque fuera "solo para depurar", copiaria ese fichero de correos a un sitio sin
            // control de acceso, multiplicado por cada visita. Se cuenta cuantos hay, que es lo
            // util para operar, y no se escribe ninguno.
            //
            // POR QUE LA PROPIEDAD SE LLAMA {FichasConContacto} Y NO {CorreosServidos}: el
            // enmascarador clasifica por NOMBRE de campo, asi que una propiedad llamada
            // "CorreosServidos" con valor 7 seria tratada como si el 7 fuera un correo. Nombrar
            // un contador con una palabra sensible no es peligroso, es simplemente inutil: el
            // contador se pierde. Esta escrito tambien en la guia.
            var registro = registros.CreateLogger(CategoriaRegistro);
            var reloj = Stopwatch.StartNew();

            var festivales = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken);
            var resultados = await CrearDtosAsync(festivales.OrderBy(item => item.Nombre).ToList(), dbContext, cancellationToken);
            var pagina = Paginar(resultados, limit, offset);

            // EL CONTADOR CAMBIO DE SUJETO EL 30 DE AGOSTO DE 2026, y no es cosmetica. Contaba
            // «fichas con dato de contacto» sobre `CorreoContacto`, que ya no viaja: el dueno del
            // proyecto decidio que la lectura publica no lleva datos personales. Se cuenta ahora
            // cuantas fichas publican ALGUN enlace propio, que responde la misma pregunta
            // operativa —¿esta sirviendo algo util o se quedo en nombres?— sin tocar PII.
            var fichasConEnlacePublico = pagina.Items.Count(item =>
                !string.IsNullOrWhiteSpace(item.SitioWeb) || !string.IsNullOrWhiteSpace(item.Instagram) ||
                !string.IsNullOrWhiteSpace(item.Facebook) || !string.IsNullOrWhiteSpace(item.OtroEnlace));

            if (registro.IsEnabled(LogLevel.Information))
            {
                registro.LogInformation(
                    "Lectura publica del catalogo de festivales. Devueltas {Devueltas} de {Total} fichas, {FichasConEnlacePublico} con enlace publico, {DuracionMs} ms",
                    pagina.Items.Count,
                    pagina.Total,
                    fichasConEnlacePublico,
                    reloj.ElapsedMilliseconds);
            }

            return Results.Ok(pagina);
        }).AllowAnonymous();

        publico.MapGet("/{festivalId:int}", async (
            int festivalId,
            PnmcDbContext dbContext,
            ILoggerFactory registros,
            CancellationToken cancellationToken) =>
        {
            var registro = registros.CreateLogger(CategoriaRegistro);
            var reloj = Stopwatch.StartNew();

            var festival = (await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken))
                .FirstOrDefault(item => item.Festival.Id == festivalId);
            if (festival is null)
            {
                // El 404 aqui no distingue "no existe" de "existe pero no esta publicado", y esa
                // frontera es la de PNMC-054. Se registra que se pidio un identificador que no
                // esta publicado, porque es la pregunta que llega —"mi festival no sale"— y sin
                // este renglon hay que ir a mirar la base de datos a mano.
                if (registro.IsEnabled(LogLevel.Information))
                {
                    registro.LogInformation(
                        "Ficha publica de festival no servida. Festival {FestivalId}, motivo {Motivo}, {DuracionMs} ms",
                        festivalId,
                        "inexistente_o_no_publicado",
                        reloj.ElapsedMilliseconds);
                }

                return Results.NotFound();
            }

            var resultado = await CrearDtosAsync([festival], dbContext, cancellationToken);
            if (registro.IsEnabled(LogLevel.Information))
            {
                registro.LogInformation(
                    "Ficha publica de festival servida. Festival {FestivalId}, {DuracionMs} ms",
                    festivalId,
                    reloj.ElapsedMilliseconds);
            }

            return Results.Ok(resultado[0]);
        }).AllowAnonymous();

        // ANÓNIMA A PROPÓSITO. Esta es la lectura pública de las ocurrencias anuales reales,
        // no del historial `VersionesFestival` del perfil. Un Festival no publicado responde 404;
        // una edición solo aparece después de su propia decisión institucional de publicación.
        publico.MapGet("/{festivalId:int}/ediciones", async (
            int festivalId,
            PnmcDbContext dbContext,
            ILoggerFactory registros,
            CancellationToken cancellationToken) =>
        {
            var registro = registros.CreateLogger(CategoriaRegistro);
            var reloj = Stopwatch.StartNew();

            var publicados = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken);
            if (!publicados.Any(item => item.Festival.Id == festivalId))
            {
                if (registro.IsEnabled(LogLevel.Information))
                {
                    registro.LogInformation(
                        "Ediciones publicas no servidas. Festival {FestivalId}, motivo {Motivo}, {DuracionMs} ms",
                        festivalId,
                        "inexistente_o_no_publicado",
                        reloj.ElapsedMilliseconds);
                }
                return Results.NotFound();
            }

            var ediciones = await dbContext.EdicionesFestival.AsNoTracking()
                .Where(item => item.FestivalId == festivalId
                    && item.EstadoVisibilidad == "publicada")
                .OrderByDescending(item => item.Anio)
                .ThenByDescending(item => item.Id)
                .ToListAsync(cancellationToken);
            var resultado = ediciones.Select(item => new EdicionFestivalPublicaDto(
                item.Id, item.Anio, item.NumeroEdicion, item.Nombre, Limpiar(item.Descripcion), item.FechaInicio,
                item.FechaFin, item.Estado,
                EdicionesFestivalExternosEndpoints.EstadosDeEdicion.TryGetValue(item.Estado, out var etiqueta)
                    ? etiqueta : item.Estado)).ToList();

            if (registro.IsEnabled(LogLevel.Information))
            {
                registro.LogInformation(
                    "Ediciones publicas servidas. Festival {FestivalId}, {Devueltas} ediciones, {DuracionMs} ms",
                    festivalId,
                    resultado.Count,
                    reloj.ElapsedMilliseconds);
            }

            return Results.Ok(resultado);
        }).AllowAnonymous();

        // COMO SE VERA EN EL LISTADO, antes de publicarlo. Ver `PrevisualizacionEnListado` para por
        // qué existe, por qué recorre la misma lectura pública y por qué exige sesión de consola.
        group.MapGroup(PrevisualizacionEnListado.Prefijo + "/festivales")
            .WithTags("previsualizacion")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .MapGet("/{id:int}", PrevisualizarEnListadoAsync)
            .WithName("PrevisualizarFestivalEnListado")
            .Produces<FestivalPublicoDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>El Festival con la forma de la lectura pública, esté publicado o no.</summary>
    /// <remarks>
    /// <b>LA MISMA LECTURA Y EL MISMO CONSTRUCTOR DE DTO.</b> La única diferencia con la ruta
    /// pública es que aquí se le pide a <see cref="LecturaFestivalesPublicados"/> que incluya este
    /// identificador sea cual sea su estado. Armar una ficha «parecida» dejaría de parecerse en
    /// cuanto la lectura pública cambiara un campo, y lo haría en silencio.
    /// </remarks>
    private static async Task<IResult> PrevisualizarEnListadoAsync(
        int id, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var festivales = await LecturaFestivalesPublicados.ConsultarAsync(dbContext, cancellationToken, id);
        var elegido = festivales.FirstOrDefault(item => item.Festival.Id == id);
        if (elegido is null) return Results.NotFound();

        var dtos = await CrearDtosAsync([elegido], dbContext, cancellationToken);
        return dtos.Count == 0 ? Results.NotFound() : Results.Ok(dtos[0]);
    }

    private static async Task<List<FestivalPublicoDto>> CrearDtosAsync(
        IReadOnlyList<FestivalPublicadoVigenteLectura> festivales,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var organizacionesIds = festivales.Where(item => item.Festival.OrganizacionPrincipalId.HasValue)
            .Select(item => item.Festival.OrganizacionPrincipalId!.Value).Distinct().ToArray();
        var organizaciones = organizacionesIds.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => organizacionesIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var departamentos = await dbContext.DivipolaLocations.AsNoTracking()
            .GroupBy(item => item.DepartmentCode)
            .Select(group => new { Codigo = group.Key, Nombre = group.First().DepartmentName })
            .ToDictionaryAsync(item => item.Codigo, item => item.Nombre, cancellationToken);
        var municipios = await dbContext.DivipolaLocations.AsNoTracking()
            .ToDictionaryAsync(item => item.MunicipalityCode, item => item.MunicipalityName, cancellationToken);
        return festivales.Select(festival =>
        {
            return new FestivalPublicoDto(
            festival.Festival.Id.ToString(CultureInfo.InvariantCulture), festival.Nombre, Limpiar(festival.Descripcion),
            festival.Festival.OrganizacionPrincipalId is int organizacionId && organizaciones.TryGetValue(organizacionId, out var organizacion)
                ? organizacion : Limpiar(festival.Festival.OrganizerDisplayName),
            new TerritorioPrincipalPublicoDto(
                ResolverNombre(festival.CodigoDepartamento, departamentos), ResolverNombre(festival.CodigoMunicipio, municipios), festival.NivelCobertura),
            Limpiar(festival.Periodicidad), festival.PracticasMusicales, festival.TerritoriosSonoros,
            // NI CORREO, NI TELEFONO, NI DIRECTOR. La lectura leyo los tres —siguen en
            // `FestivalPublicadoVigenteLectura` porque los caminos CON sesion los usan— y aqui se
            // quedan. Lo unico que cruza al DTO publico es la presencia publica del Festival.
            Limpiar(festival.Instagram), Limpiar(festival.Facebook),
            Limpiar(festival.SitioWeb), Limpiar(festival.OtroEnlace),
            Limpiar(festival.PeriodicidadDetalle),
            // SALEN DE LA CABECERA Y NO DE LA VERSION, que es lo contrario que los enlaces de
            // arriba: los enlaces son «presencia publicada» y estas dos son «cuándo ocurre este
            // año», que es un dato del presente del Festival y no de lo que se publicó de él.
            AFecha(festival.Festival.CurrentYearStartDate),
            AFecha(festival.Festival.CurrentYearEndDate));
        }).ToList();
    }

    private static string? ResolverNombre(string? codigo, Dictionary<string, string> valores) =>
        string.IsNullOrWhiteSpace(codigo) ? null : valores.TryGetValue(codigo, out var nombre) ? nombre : codigo;

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    private static DateOnly? AFecha(DateTime? valor) => valor is null ? null : DateOnly.FromDateTime(valor.Value);

    private static PagedResponse<FestivalPublicoDto> Paginar(List<FestivalPublicoDto> items, int? limit, int? offset)
    {
        var limite = Math.Clamp(limit ?? 100, 1, 500);
        var desde = Math.Max(offset ?? 0, 0);
        return new PagedResponse<FestivalPublicoDto>(items.Skip(desde).Take(limite).ToList(), limite, desde, items.Count);
    }
}
