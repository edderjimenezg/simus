using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.ConsultaGuiada;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// La Consulta Guiada del espacio de gestión: una organización preguntando por lo suyo.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE.</b> Hasta la Consulta Guiada era una pantalla de
/// la consola del Programa y nada más. El plan de consolidación la declaró <b>capacidad
/// transversal</b>, y una capacidad que solo existe en un sitio no lo es. Una organización tiene
/// exactamente las mismas preguntas —«¿qué me falta?», «¿qué espera algo de mí?»— y hoy las
/// respondía recorriendo cuatro pantallas y contando a ojo.
/// </para>
/// <para>
/// <b>COMPARTE EL NUCLEO Y NO EL ALCANCE.</b> El catálogo, el intérprete y la forma de la respuesta
/// son los mismos que los del Programa: una sola costura, un solo contrato, una sola forma de
/// declarar la fuente. Lo que no comparte son las consultas: las de <see cref="AmbitoDeConsulta"/>
/// <c>Organizacion</c> solo miran los registros de quien pregunta. No hay ninguna ruta por la que
/// una organización pueda pedir una consulta institucional, porque el ámbito se decide en el
/// servidor y el intérprete solo recibe las que puede elegir.
/// </para>
/// <para>
/// <b>EL IDENTIFICADOR DE LA ORGANIZACION VA EN LA RUTA Y SE COMPRUEBA.</b> Quien administra tres
/// organizaciones pregunta por una de ellas, y pedir la de otra responde 403 —no una lista vacía—,
/// porque una lista vacía diría «esa organización no tiene nada», que es información que tampoco
/// le corresponde.
/// </para>
/// </remarks>
public static class ConsultaGuiadaDeOrganizacionEndpoints
{
    private const int MaximoCaracteresPregunta = 1_000;
    private const int MaximoFilas = 30;

    public static RouteGroupBuilder MapConsultaGuiadaDeOrganizacionEndpoints(this RouteGroupBuilder group)
    {
        var consulta = group.MapGroup("/externo/organizaciones/{organizacionId:int}/consulta-guiada")
            .WithTags("consulta-guiada-externa")
            .RequireAuthorization(SimusAuthentication.ExternalPolicy);

        consulta.MapGet("/estado", EstadoAsync)
            .WithName("EstadoDeConsultaGuiadaDeOrganizacion")
            .Produces<EstadoDeConsultaGuiadaDto>(StatusCodes.Status200OK);

        // MISMO CUPO Y MISMO MOTIVO que en la consola: es lectura, pero repite agregaciones.
        consulta.MapPost("/consultar", ConsultarAsync)
            .RequireRateLimiting("analisis-administrativo")
            .WithName("ConsultarGuiadaDeOrganizacion")
            .Produces<RespuestaDeConsultaGuiadaDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<IResult> EstadoAsync(
        int organizacionId,
        ClaimsPrincipal principal,
        IInterpreteDePregunta interprete,
        PnmcDbContext db,
        CancellationToken ct)
    {
        if (await SinPermisoAsync(db, principal, organizacionId, ct)) return Results.Forbid();

        return Results.Ok(new EstadoDeConsultaGuiadaDto(
            true,
            interprete.ModeloLocalDisponible,
            interprete.Modo,
            interprete.Mensaje,
            CatalogoDeConsultas.De(AmbitoDeConsulta.Organizacion)
                .Select(item => new ConsultaOfrecidaDto(item.Id, item.Rotulo, item.Descripcion))
                .ToList(),
            CatalogoDeConsultas.SugerenciasDe(AmbitoDeConsulta.Organizacion)));
    }

    private static async Task<IResult> ConsultarAsync(
        int organizacionId,
        PreguntaDeConsultaGuiadaDto solicitud,
        ClaimsPrincipal principal,
        IInterpreteDePregunta interprete,
        CacheDivipola cacheDivipola,
        PnmcDbContext db,
        CancellationToken ct)
    {
        if (await SinPermisoAsync(db, principal, organizacionId, ct)) return Results.Forbid();

        var pregunta = (solicitud.Pregunta ?? string.Empty).Trim();
        if (pregunta.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = ["Escribe una pregunta sobre los registros de tu organización."],
            });
        }

        if (pregunta.Length > MaximoCaracteresPregunta)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = [$"La pregunta no puede superar {MaximoCaracteresPregunta} caracteres."],
            });
        }

        var organizacion = await db.EntityProfiles.AsNoTracking()
            .Where(item => item.Id == organizacionId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(ct);
        var alcance = $"la organización «{organizacion}»";

        // LAS MISMAS ENTIDADES Y EL MISMO VOCABULARIO QUE EN LA CONSOLA. Una organización pregunta
        // «¿dónde están mis Festivales en el Huila?» exactamente igual que un funcionario pregunta
        // por el país entero, y la respuesta tiene que acotarse igual. Construir aquí un
        // reconocimiento propio habría sido la forma de que las dos pantallas se separaran.
        var vocabulario = await cacheDivipola.ObtenerVocabularioAsync(db, ct);
        var entidades = ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)),
            vocabulario,
            DateTime.UtcNow.Year,
            await CatalogoDeEstadosDelCircuito.ObtenerAsync(db, ct));

        var elegida = await interprete.InterpretarAsync(
            pregunta, CatalogoDeConsultas.De(AmbitoDeConsulta.Organizacion), entidades, ct);

        if (elegida is null)
        {
            return Results.Ok(new RespuestaDeConsultaGuiadaDto(
                "No encontré una consulta que responda eso. Puedo decirte qué necesita tu atención, qué información les falta a tus Festivales, cómo van tus procesos y en qué territorios están registrados.",
                string.Empty,
                "sin_coincidencia",
                DateTime.UtcNow,
                null,
                alcance));
        }

        if (elegida.Consulta.Id == CatalogoDeConsultas.Ayuda)
        {
            return Results.Ok(new RespuestaDeConsultaGuiadaDto(
                "Este asistente responde con consultas ya escritas sobre los registros de tu organización y te enseña de dónde sale cada cifra. No ve los registros de otras organizaciones y no puede crear, enviar, aprobar ni publicar nada por ti.",
                elegida.Consulta.Id,
                elegida.ResueltoPor,
                DateTime.UtcNow,
                null,
                alcance));
        }

        // EL FESTIVAL DEL CONTEXTO SE COMPRUEBA CONTRA LA ORGANIZACION, no contra quien pregunta:
        // pasar el identificador de un Festival ajeno no acota nada, se ignora y la respuesta sigue
        // siendo de la organización entera, que es lo que esa persona sí puede ver.
        var festivalId = await FestivalPropioAsync(db, organizacionId, solicitud.Contexto?.FestivalId, ct);

        var resultado = elegida.Consulta.Id switch
        {
            CatalogoDeConsultas.MisPendientes => await MisPendientesAsync(db, organizacionId, entidades.Territorio, ct),
            CatalogoDeConsultas.MiCalidad => await MiCalidadAsync(db, organizacionId, festivalId, entidades, ct),
            CatalogoDeConsultas.MiTerritorio => await MiTerritorioAsync(db, organizacionId, entidades, ct),
            CatalogoDeConsultas.MisProcesos => await MisProcesosAsync(db, organizacionId, entidades, ct),
            CatalogoDeConsultas.MisMercados => await MisMercadosAsync(db, organizacionId, entidades, ct),
            _ => throw new InvalidOperationException("La consulta elegida no pertenece al ámbito de la organización."),
        };

        return Results.Ok(new RespuestaDeConsultaGuiadaDto(
            resultado.Resumen + AlcanceDeLaRespuesta.LoQueNoSePudoAplicar(elegida.Consulta, entidades),
            elegida.Consulta.Id,
            elegida.ResueltoPor,
            DateTime.UtcNow,
            resultado.Tabla,
            // LO QUE SE ACOTO SE SUMA AL CONTEXTO, no lo sustituye: quien pregunta sigue estando
            // dentro de su organización, y además la respuesta miró solo una parte. Enseñar una sola
            // de las dos cosas deja la cifra sin la mitad de su explicación.
            resultado.Acotado is { Length: > 0 } acotado ? $"{alcance} · {acotado}" : alcance));
    }

    // ---------- Las consultas de la organización ------------------------------------------

    /// <summary>
    /// Lo que espera una acción de la organización, que no es lo mismo que lo que está pendiente.
    /// </summary>
    /// <remarks>
    /// <b>«EN REVISION» NO ES UN PENDIENTE SUYO.</b> Es un pendiente del Programa, y ponerlo en esta
    /// lista le diría a la organización que tiene algo que hacer cuando lo único que puede hacer es
    /// esperar. La distinción es la misma que el panel hace desde entre
    /// «en revisión» y «requiere tu atención», y aquí se respeta en vez de reinventarse.
    /// </remarks>
    private static async Task<Resultado> MisPendientesAsync(
        PnmcDbContext db, int organizacionId, TerritorioNombrado? territorio, CancellationToken ct)
    {
        // SE TRAE Y SE FILTRA EN MEMORIA porque el recorte territorial mira dos columnas según lo
        // que se nombrara —departamento o municipio—, y una organización tiene decenas de
        // Festivales, no cientos de miles.
        var todos = await db.FestivalRecords.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId)
            .Select(item => new { item.Id, item.StatusCode, item.DepartmentCode, item.MunicipalityCode })
            .ToListAsync(ct);
        var mios = todos
            .Where(item => territorio is null || territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            .ToList();
        var donde = territorio is null ? string.Empty : $" en {territorio.EnPalabras}";

        var conAjustes = mios.Count(item => item.StatusCode == EstadosFestival.AjustesSolicitados);
        var borradores = mios.Count(item => item.StatusCode == EstadosFestival.Borrador);
        var enRevision = mios.Count(item => item.StatusCode == EstadosFestival.EnRevision);

        var identificadores = mios.Select(item => item.Id).ToList();
        var edicionesBorrador = await db.EdicionesFestival.AsNoTracking()
            .CountAsync(item => identificadores.Contains(item.FestivalId) && item.EstadoVisibilidad == "borrador", ct);

        var total = conAjustes + borradores + edicionesBorrador;
        var resumen = total == 0
            ? enRevision == 0
                ? $"No hay nada{donde} esperando una acción tuya."
                : $"No hay nada{donde} esperando una acción tuya. Hay {enRevision.ToString(CultureInfo.InvariantCulture)} en revisión, y eso lo resuelve el Programa."
            : $"Hay {TextoEnEspanol.Plural(total, "cosa", "cosas")}{donde} esperando una acción tuya. Lo demás está en manos del Programa.";

        return new Resultado(resumen, Tabla(
            $"Lo que espera una acción de tu organización{donde}",
            "dbo.Festivales y dbo.EdicionesFestival de tu organización",
            $"Solo lo que puedes resolver tú{donde}; lo que está en revisión se informa aparte",
            ["Asunto", "Cuántos"],
            Fila("Festivales con ajustes solicitados", conAjustes),
            Fila("Festivales en borrador sin enviar", borradores),
            Fila("Ediciones en borrador sin publicar", edicionesBorrador),
            Fila("En revisión (espera al Programa)", enRevision)),
            Acotacion(territorio));
    }

    /// <summary>
    /// Qué campos les faltan a los Festivales de la organización, nombrando cuál es cuál.
    /// </summary>
    /// <remarks>
    /// <b>AQUI SI SE NOMBRAN LOS REGISTROS</b>, y no rompe ninguna regla de privacidad: son los
    /// suyos, y un conteo anónimo —«a 3 Festivales les falta la descripción»— la obligaría a abrir
    /// los suyos uno por uno para encontrar cuáles. Esa era justamente la pregunta.
    /// </remarks>
    private static async Task<Resultado> MiCalidadAsync(
        PnmcDbContext db, int organizacionId, int? festivalId, EntidadesDeLaPregunta entidades, CancellationToken ct)
    {
        var consulta = db.FestivalRecords.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId);
        if (festivalId is int uno) consulta = consulta.Where(item => item.Id == uno);

        var todos = await consulta
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                item.Name,
                item.StatusCode,
                item.CoverageLevel,
                item.DepartmentCode,
                item.MunicipalityCode,
                item.Description,
                item.ContactEmail,
                item.ContactPhone,
            })
            .ToListAsync(ct);

        // EL CONTEXTO DE LA PANTALLA MANDA SOBRE LO QUE DIGA LA PREGUNTA. Si se está mirando un
        // Festival concreto, acotar además por territorio o por estado solo podría dejar la tabla
        // vacía: la pregunta ya está acotada a un registro.
        var territorio = festivalId is null ? entidades.Territorio : null;
        var estado = festivalId is null ? entidades.Estado : null;
        var festivales = todos
            .Where(item => territorio is null || territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            .Where(item => estado is null || estado.Incluye(item.StatusCode))
            .Take(MaximoFilas)
            .ToList();
        var donde = (territorio is null ? string.Empty : $" en {territorio.EnPalabras}")
            + (estado is null ? string.Empty : $" {estado.EnPalabras}");

        var filas = new List<IReadOnlyList<string>>();
        foreach (var festival in festivales)
        {
            var faltan = new List<string>();
            if (!EsNacional(festival.CoverageLevel) && string.IsNullOrWhiteSpace(festival.DepartmentCode)) faltan.Add("departamento");
            if (EsMunicipal(festival.CoverageLevel) && string.IsNullOrWhiteSpace(festival.MunicipalityCode)) faltan.Add("municipio");
            if (string.IsNullOrWhiteSpace(festival.Description)) faltan.Add("descripción");
            if (string.IsNullOrWhiteSpace(festival.ContactEmail) && string.IsNullOrWhiteSpace(festival.ContactPhone)) faltan.Add("contacto");
            filas.Add(Fila("Festival", festival.Name, faltan.Count == 0 ? "Completo" : string.Join(", ", faltan)));
        }

        // LOS MERCADOS TAMBIEN, y por el mismo criterio. «¿Qué información me falta?» no pregunta por
        // un módulo: pregunta por lo que hay que completar. Al declarar que las consultas de Festival
        // no contestan por mercados —para que «¿cómo van mis mercados?» llegara a la suya—, esta se
        // quedó contestando solo de Festivales a una pregunta que no los nombra, y «¿qué le falta a
        // mis mercados?» acababa en la tabla de estados, que es otra cosa.
        var mercados = festivalId is not null ? [] : await db.Mercados.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId && item.Activo)
            .OrderBy(item => item.Nombre)
            .Select(item => new
            {
                item.Nombre,
                item.EstadoRegistro,
                item.NivelCobertura,
                item.CodigoDepartamento,
                item.CodigoMunicipio,
                item.Descripcion,
                item.CorreoMercado,
                item.TelefonoMercado,
            })
            .ToListAsync(ct);
        var mercadosMios = mercados
            .Where(item => territorio is null || territorio.Cubre(item.CodigoDepartamento, item.CodigoMunicipio))
            .Where(item => estado is null || estado.Incluye(item.EstadoRegistro))
            .Take(MaximoFilas)
            .ToList();

        foreach (var mercado in mercadosMios)
        {
            var faltan = new List<string>();
            if (!EsNacional(mercado.NivelCobertura) && string.IsNullOrWhiteSpace(mercado.CodigoDepartamento)) faltan.Add("departamento");
            if (EsMunicipal(mercado.NivelCobertura) && string.IsNullOrWhiteSpace(mercado.CodigoMunicipio)) faltan.Add("municipio");
            if (string.IsNullOrWhiteSpace(mercado.Descripcion)) faltan.Add("descripción");
            if (string.IsNullOrWhiteSpace(mercado.CorreoMercado) && string.IsNullOrWhiteSpace(mercado.TelefonoMercado)) faltan.Add("contacto");
            filas.Add(Fila("Mercado", mercado.Nombre, faltan.Count == 0 ? "Completo" : string.Join(", ", faltan)));
        }

        var registros = festivales.Count + mercadosMios.Count;
        var incompletos = filas.Count(fila => fila[2] != "Completo");
        var resumen = registros == 0
            ? donde.Length == 0
                ? "Todavía no tienes registros, así que no hay información que revisar."
                : $"No tienes registros{donde}, así que no hay información que revisar."
            : incompletos == 0
                ? registros == 1
                    ? $"Tu único registro{donde} tiene completos los campos que su alcance exige."
                    : $"Tus {registros.ToString(CultureInfo.InvariantCulture)} registros{donde} tienen completos los campos que su alcance exige."
                // «A 1 DE TUS 1 REGISTRO» ES LA MISMA AVERIA que «1 de ellos publicado»: la frase da
                // por hecho que hay varios. Con uno solo se dice de otra manera.
                : registros == 1
                    ? $"A tu único registro{donde} le falta algún dato que su alcance exige."
                    : $"A {incompletos.ToString(CultureInfo.InvariantCulture)} de tus {registros.ToString(CultureInfo.InvariantCulture)} registros{donde} les falta algún dato que su alcance exige.";

        return new Resultado(
            resumen,
            Tabla(
                $"Información que les falta a tus registros{donde}",
                "dbo.Festivales y dbo.Mercados de tu organización",
                festivalId is null
                    ? $"Tus Festivales y mercados{donde}, hasta {MaximoFilas} de cada uno; se dice si falta un contacto, nunca cuál es"
                    : "Solo el Festival que estás mirando",
                ["Tipo", "Registro", "Qué le falta"], filas),
            // CUANDO SE ACOTO, SE DICE A QUE. Una tabla de una sola fila sin decir por qué parece
            // una organización con un solo Festival.
            festivalId is not null
                ? $"el Festival «{festivales.FirstOrDefault()?.Name}»"
                : Acotacion(territorio, estado));
    }

    private static async Task<Resultado> MiTerritorioAsync(
        PnmcDbContext db, int organizacionId, EntidadesDeLaPregunta entidades, CancellationToken ct)
    {
        var todos = await db.FestivalRecords.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId)
            .Select(item => new { item.StatusCode, item.DepartmentCode, item.MunicipalityCode, item.CoverageLevel })
            .ToListAsync(ct);
        var mios = todos
            .Where(item => entidades.Territorio is null || entidades.Territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            .Where(item => entidades.Estado is null || entidades.Estado.Incluye(item.StatusCode))
            .ToList();
        var donde = (entidades.Territorio is null ? string.Empty : $" en {entidades.Territorio.EnPalabras}")
            + (entidades.Estado is null ? string.Empty : $" {entidades.Estado.EnPalabras}");

        var codigos = mios.Select(item => item.MunicipalityCode).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        var nombres = await db.DivipolaLocations.AsNoTracking()
            .Where(lugar => codigos.Contains(lugar.MunicipalityCode))
            .Select(lugar => new { lugar.MunicipalityCode, lugar.MunicipalityName, lugar.DepartmentName })
            .ToListAsync(ct);
        var porCodigo = nombres
            .GroupBy(item => item.MunicipalityCode)
            .ToDictionary(grupo => grupo.Key, grupo => $"{grupo.First().MunicipalityName}, {grupo.First().DepartmentName}");

        var filas = mios
            .Where(item => !string.IsNullOrWhiteSpace(item.MunicipalityCode))
            .GroupBy(item => item.MunicipalityCode!)
            .Select(grupo => (Nombre: porCodigo.GetValueOrDefault(grupo.Key, grupo.Key), Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Select(item => Fila(item.Nombre, item.Total))
            .ToList();

        var nacionales = mios.Count(item => EsNacional(item.CoverageLevel));
        var resumen = filas.Count == 0
            ? nacionales == 0
                ? $"Tus Festivales{donde} todavía no tienen municipio registrado."
                : $"Tus {TextoEnEspanol.Plural(nacionales, "Festival es", "Festivales son")}{donde} de alcance nacional, así que no se cuentan en un municipio concreto."
            : $"Tienes Festivales registrados{donde} en {TextoEnEspanol.Plural(filas.Count, "municipio", "municipios")}." +
              (nacionales == 0 ? string.Empty : $" Otros {nacionales.ToString(CultureInfo.InvariantCulture)} son de alcance nacional y no se cuentan en un municipio.");

        return new Resultado(resumen, Tabla(
            $"Dónde están registrados tus Festivales{donde}",
            "dbo.Festivales de tu organización; nombres de dbo.Divipola",
            $"Tus Festivales{donde}; el alcance nacional no cuenta como municipio",
            ["Municipio", "Festivales"], filas),
            Acotacion(entidades.Territorio, entidades.Estado));
    }

    private static async Task<Resultado> MisProcesosAsync(
        PnmcDbContext db, int organizacionId, EntidadesDeLaPregunta entidades, CancellationToken ct)
    {
        var todos = await db.FestivalRecords.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId)
            .Select(item => new { item.Id, item.StatusCode, item.DepartmentCode, item.MunicipalityCode })
            .ToListAsync(ct);
        var mios = todos
            .Where(item => entidades.Territorio is null || entidades.Territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            .Where(item => entidades.Estado is null || entidades.Estado.Incluye(item.StatusCode))
            .ToList();
        var donde = (entidades.Territorio is null ? string.Empty : $" en {entidades.Territorio.EnPalabras}")
            + (entidades.Estado is null ? string.Empty : $" {entidades.Estado.EnPalabras}");

        var porEstado = mios
            .GroupBy(item => item.StatusCode)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToList();

        var etiquetas = await db.ContentStatuses.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase, ct);

        var identificadores = mios.Select(item => item.Id).ToList();
        var ediciones = await db.EdicionesFestival.AsNoTracking()
            .Where(item => identificadores.Contains(item.FestivalId))
            .GroupBy(item => item.EstadoVisibilidad)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(ct);

        var filas = porEstado
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Estado, StringComparer.Ordinal)
            .Select(item => Fila("Festival", etiquetas.GetValueOrDefault(item.Estado ?? string.Empty, item.Estado ?? "Sin estado"), item.Total))
            .Concat(ediciones
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.Estado, StringComparer.Ordinal)
                .Select(item => Fila("Edición", EtiquetaDeVisibilidad(item.Estado), item.Total)))
            .ToList();

        // LOS MERCADOS TAMBIEN SON PROCESOS SUYOS, y esta consulta se llama «cómo van mis procesos».
        // Dejarlos fuera hacía que la respuesta se leyera como completa sin serlo: una organización
        // con un mercado registrado veía una tabla que no lo nombraba.
        var mercados = await db.Mercados.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId && item.Activo)
            .Select(item => new { item.EstadoRegistro, item.CodigoDepartamento, item.CodigoMunicipio })
            .ToListAsync(ct);
        var mercadosMios = mercados
            .Where(item => entidades.Territorio is null || entidades.Territorio.Cubre(item.CodigoDepartamento, item.CodigoMunicipio))
            .Where(item => entidades.Estado is null || entidades.Estado.Incluye(item.EstadoRegistro))
            .ToList();
        filas.AddRange(mercadosMios
            .GroupBy(item => item.EstadoRegistro ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Key, StringComparer.Ordinal)
            .Select(grupo => Fila("Mercado", etiquetas.GetValueOrDefault(grupo.Key, grupo.Key.Replace('_', ' ')), grupo.Count())));

        var total = porEstado.Sum(item => item.Total);
        var publicados = porEstado.Where(item => item.Estado == EstadosFestival.Publicado).Sum(item => item.Total);
        var resumen = total == 0
            ? donde.Length == 0
                ? "Todavía no has registrado ningún Festival."
                : $"No tienes Festivales{donde}."
            // «1 DE ELLOS PUBLICADO» NO SE PUEDE ESCRIBIR: con un solo Festival, «de ellos» no tiene
            // a quién referirse. La frase se armó de nuevo para que funcione con uno y con treinta.
            : $"Tienes {TextoEnEspanol.Plural(total, "Festival registrado", "Festivales registrados")}{donde} y {TextoEnEspanol.Plural(publicados, "publicado", "publicados")}, con {TextoEnEspanol.Plural(ediciones.Sum(item => item.Total), "edición", "ediciones")}."
              + (mercadosMios.Count == 0 ? string.Empty : $" Y {TextoEnEspanol.Plural(mercadosMios.Count, "mercado musical", "mercados musicales")}.");

        return new Resultado(resumen, Tabla(
            $"Cómo van tus procesos{donde}",
            "dbo.Festivales, dbo.EdicionesFestival y dbo.Mercados de tu organización",
            $"Tus registros{donde}, en todos sus estados",
            ["Tipo", "Estado", "Cuántos"], filas),
            Acotacion(entidades.Territorio, entidades.Estado));
    }

    /// <summary>
    /// Cómo van los mercados musicales de la organización y sus ediciones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UN MODULO DEL ECOSISTEMA SE CONECTA DONDE ESTA FESTIVALES, y esta era la superficie que
    /// faltaba.</b> Mercados Musicales llegó al asistente de la consola
    /// con dos consultas, y en el espacio externo no llegó a ninguna: una organización que registra
    /// un mercado no podía preguntar por él, y al intentarlo recibía la cifra de sus Festivales.
    /// </para>
    /// <para>
    /// <b>LA RELACION CON EL FESTIVAL SE CUENTA APARTE</b>, como en la consola: es lo propio de este
    /// módulo y la pregunta que Festivales no puede responder por su lado.
    /// </para>
    /// </remarks>
    private static async Task<Resultado> MisMercadosAsync(
        PnmcDbContext db, int organizacionId, EntidadesDeLaPregunta entidades, CancellationToken ct)
    {
        var todos = await db.Mercados.AsNoTracking()
            .Where(item => item.OrganizacionPrincipalId == organizacionId && item.Activo)
            .Select(item => new
            {
                item.Id,
                item.EstadoRegistro,
                item.CodigoDepartamento,
                item.CodigoMunicipio,
                item.SeRealizaEnElMarcoDeUnFestival,
            })
            .ToListAsync(ct);

        var mios = todos
            .Where(item => entidades.Territorio is null || entidades.Territorio.Cubre(item.CodigoDepartamento, item.CodigoMunicipio))
            .Where(item => entidades.Estado is null || entidades.Estado.Incluye(item.EstadoRegistro))
            .ToList();
        var donde = (entidades.Territorio is null ? string.Empty : $" en {entidades.Territorio.EnPalabras}")
            + (entidades.Estado is null ? string.Empty : $" {entidades.Estado.EnPalabras}");

        var etiquetas = await db.ContentStatuses.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase, ct);

        var identificadores = mios.Select(item => item.Id).ToList();
        var ediciones = await db.EdicionesMercado.AsNoTracking()
            .Where(item => identificadores.Contains(item.MercadoId))
            .GroupBy(item => item.EstadoVisibilidad)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(ct);

        var filas = mios
            .GroupBy(item => item.EstadoRegistro ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(grupo => grupo.Count())
            .ThenBy(grupo => grupo.Key, StringComparer.Ordinal)
            .Select(grupo => Fila("Mercado", etiquetas.GetValueOrDefault(grupo.Key, grupo.Key.Replace('_', ' ')), grupo.Count()))
            .Concat(ediciones
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.Estado, StringComparer.Ordinal)
                .Select(item => Fila("Edición", EtiquetaDeVisibilidad(item.Estado), item.Total)))
            .ToList();

        var enFestival = mios.Count(item => item.SeRealizaEnElMarcoDeUnFestival);
        if (enFestival > 0) { filas.Add(Fila("Mercado", "En el marco de un Festival", enFestival)); }

        var publicados = mios.Count(item => string.Equals(item.EstadoRegistro, EstadosFestival.Publicado, StringComparison.OrdinalIgnoreCase));
        var resumen = mios.Count == 0
            ? donde.Length == 0
                ? "Todavía no has registrado ningún mercado musical."
                : $"No tienes mercados musicales{donde}."
            : $"Tienes {TextoEnEspanol.Plural(mios.Count, "mercado musical registrado", "mercados musicales registrados")}{donde} y {TextoEnEspanol.Plural(publicados, "publicado", "publicados")}, con {TextoEnEspanol.Plural(ediciones.Sum(item => item.Total), "edición", "ediciones")}.";

        return new Resultado(resumen, Tabla(
            $"Cómo van tus mercados musicales{donde}",
            "dbo.Mercados y dbo.EdicionesMercado de tu organización",
            $"Tus mercados activos{donde}, en todos sus estados",
            ["Tipo", "Estado", "Cuántos"], filas),
            Acotacion(entidades.Territorio, entidades.Estado));
    }

    // ---------- Andamio ---------------------------------------------------------------------

    /// <summary>El Festival del contexto, solo si de verdad es de esa organización.</summary>
    private static async Task<int?> FestivalPropioAsync(
        PnmcDbContext db, int organizacionId, int? festivalId, CancellationToken ct)
    {
        if (festivalId is not int id) return null;
        var suyo = await db.FestivalRecords.AsNoTracking()
            .AnyAsync(item => item.Id == id && item.OrganizacionPrincipalId == organizacionId, ct);
        return suyo ? id : null;
    }

    /// <summary>El recorte dicho en palabras, igual que en la consola.</summary>
    private static string? Acotacion(TerritorioNombrado? territorio, EstadoNombrado? estado = null)
    {
        var partes = new List<string>();
        if (territorio is not null) { partes.Add(territorio.EnPalabras); }
        if (estado is not null) { partes.Add(estado.EnPalabras); }
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static async Task<bool> SinPermisoAsync(
        PnmcDbContext db, ClaimsPrincipal principal, int organizacionId, CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var personaId)) return true;
        return !await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, personaId, organizacionId, ct);
    }

    private static string EtiquetaDeVisibilidad(string? estado) => estado switch
    {
        "publicada" => "Publicada",
        "archivada" => "Archivada",
        _ => "Borrador",
    };

    private static bool EsNacional(string? alcance) =>
        CatalogoDeConsultas.Normalizar(alcance ?? string.Empty).Contains("nacional", StringComparison.Ordinal);

    private static bool EsMunicipal(string? alcance) =>
        CatalogoDeConsultas.Normalizar(alcance ?? string.Empty).Contains("municipal", StringComparison.Ordinal);

    private static List<string> Fila(params object?[] valores) =>
        valores.Select(valor => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty).ToList();

    private static TablaDeConsultaDto Tabla(
        string titulo, string fuente, string alcance,
        IReadOnlyList<string> columnas, params IReadOnlyList<string>[] filas) =>
        new(titulo, fuente, alcance, columnas, filas);

    private static TablaDeConsultaDto Tabla(
        string titulo, string fuente, string alcance,
        IReadOnlyList<string> columnas, IReadOnlyList<IReadOnlyList<string>> filas) =>
        new(titulo, fuente, alcance, columnas, filas);

    /// <summary>
    /// Lo que una consulta devuelve: la frase, la tabla y sobre qué se calculó.
    /// </summary>
    /// <param name="Acotado">
    /// El recorte que la consulta aplicó de verdad. <b>LO DICE QUIEN FILTRA</b>: reconstruirlo fuera
    /// volvería a separarse en cuanto una consulta decidiera algo distinto, y el precio de que se
    /// separe es una respuesta que se contradice a sí misma.
    /// </param>
    private sealed record Resultado(string Resumen, TablaDeConsultaDto Tabla, string? Acotado = null);
}
