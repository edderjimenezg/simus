using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.ConsultaGuiada;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Consultas agregadas y verificables para el Espacio de Gestión Administrativa.
/// </summary>
/// <remarks>
/// El texto del usuario solo elige una consulta de este archivo. No genera SQL, no llega a un
/// proveedor externo y no se conserva. Las respuestas no contienen filas de personas o registros:
/// solo conteos y nombres territoriales o de catálogos públicos.
/// </remarks>
public static class AnalisisAdministrativoEndpoints
{
    private const int MaximoCaracteresPregunta = 1_000;
    private const int MaximoFilas = 30;

    public static RouteGroupBuilder MapAnalisisAdministrativoEndpoints(this RouteGroupBuilder group)
    {
        var analisis = group.MapGroup("/admin/analisis")
            .WithTags("analisis-administrativo")
            .RequireAuthorization(Permisos.PoliticaFuncionario);
        analisis.ExigeModulo("analisis");

        analisis.MapGet("/estado", (IInterpreteDePregunta interprete) => Results.Ok(
                new EstadoDeConsultaGuiadaDto(
                    true,
                    interprete.ModeloLocalDisponible,
                    interprete.Modo,
                    interprete.Mensaje,
                    CatalogoDeConsultas.De(AmbitoDeConsulta.Institucional)
                        .Select(consulta => new ConsultaOfrecidaDto(consulta.Id, consulta.Rotulo, consulta.Descripcion))
                        .ToList(),
                    CatalogoDeConsultas.SugerenciasDe(AmbitoDeConsulta.Institucional))))
            .Produces<EstadoDeConsultaGuiadaDto>(StatusCodes.Status200OK);

        analisis.MapGet("/tablero", ConstruirTableroAsync)
            .Produces<TableroAnalisisAdministrativoDto>(StatusCodes.Status200OK);

        // Es POST porque la pregunta viaja en el cuerpo y no queda en historiales de URL. Sigue
        // siendo lectura: no modifica la base y no necesita antifalsificación. El cupo evita que
        // una pestaña en bucle repita las agregaciones para toda la oficina.
        analisis.MapPost("/consultar", ConsultarAsync)
            .RequireRateLimiting("analisis-administrativo")
            .Produces<RespuestaDeConsultaGuiadaDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return group;
    }

    /// <summary>
    /// Responde una pregunta del Programa, acotada a lo que quien pregunta está mirando.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL TEXTO SOLO ELIGE UNA CONSULTA DE ESTE ARCHIVO.</b> No genera SQL, no llega a un
    /// proveedor externo y no se conserva. Quien elige es <see cref="IInterpreteDePregunta"/>, que
    /// hoy son reglas y mañana puede ser un modelo sin que cambie nada más.
    /// </para>
    /// <para>
    /// <b>EL CONTEXTO ACOTA, NO ABRE.</b> Se comprueba contra la base —el Festival y la
    /// organización tienen que existir— y la respuesta declara siempre sobre qué se calculó,
    /// también cuando la consulta elegida no sabe acotarse.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ConsultarAsync(
        PreguntaDeConsultaGuiadaDto solicitud,
        IInterpreteDePregunta interprete,
        CacheDivipola cacheDivipola,
        PnmcDbContext db,
        CancellationToken cancellationToken)
    {
        var pregunta = (solicitud.Pregunta ?? string.Empty).Trim();
        if (pregunta.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = ["Escribe una pregunta para consultar los datos administrativos."],
            });
        }

        if (pregunta.Length > MaximoCaracteresPregunta)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pregunta"] = [$"La pregunta no puede superar {MaximoCaracteresPregunta} caracteres."],
            });
        }

        var mirando = await NombreDeLoQueSeMiraAsync(db, solicitud.Contexto, cancellationToken);

        // LO QUE LA PREGUNTA NOMBRA SE RESUELVE ANTES DE ELEGIR CONSULTA, y contra DIVIPOLA. El
        // intérprete recibe «Huila» ya convertido en el código 41, no la palabra: así elegir y
        // acotar miran lo mismo, y el día que detrás haya un modelo no tendrá que reconocer
        // territorios —que es justo donde un modelo se inventaría uno—.
        var vocabulario = await cacheDivipola.ObtenerVocabularioAsync(db, cancellationToken);
        var entidades = ExtractorDeEntidades.Extraer(
            CatalogoDeConsultas.PalabrasDe(CatalogoDeConsultas.Normalizar(pregunta)),
            vocabulario,
            DateTime.UtcNow.Year,
            await CatalogoDeEstadosDelCircuito.ObtenerAsync(db, cancellationToken));

        var elegida = await interprete.InterpretarAsync(
            pregunta, CatalogoDeConsultas.De(AmbitoDeConsulta.Institucional), entidades, cancellationToken);

        if (elegida is null)
        {
            return Results.Ok(new RespuestaDeConsultaGuiadaDto(
                "No encontré una consulta aprobada para esa pregunta. Puedo responder sobre el resumen administrativo, la distribución y cobertura de Festivales, los territorios sin Festival, la distribución temática, los pendientes de gestión y la calidad de datos.",
                string.Empty,
                "sin_coincidencia",
                DateTime.UtcNow,
                null,
                mirando ?? AlcanceDeLaRespuesta.TodaLaOperacion));
        }

        if (elegida.Consulta.Id == CatalogoDeConsultas.Ayuda)
        {
            return Results.Ok(new RespuestaDeConsultaGuiadaDto(
                "Este asistente elige una consulta agregada aprobada y muestra su fuente. No genera SQL, no consulta datos personales y no crea, modifica, aprueba ni publica registros.",
                elegida.Consulta.Id,
                elegida.ResueltoPor,
                DateTime.UtcNow,
                null,
                mirando ?? AlcanceDeLaRespuesta.TodaLaOperacion));
        }

        // LA UNICA CONSULTA QUE HOY SABE ACOTARSE es la de calidad, y solo a un Festival: es la que
        // se pregunta desde dentro de una ficha. Las demás describen el directorio entero, y decirlo
        // es más honesto que inventarles un recorte que no tienen.
        var acotadaAFestival = elegida.Consulta.Id == CatalogoDeConsultas.Calidad && solicitud.Contexto?.FestivalId is not null;
        var resultado = acotadaAFestival
            ? await ConsultarCalidadDeUnFestivalAsync(db, solicitud.Contexto!.FestivalId!.Value, cancellationToken)
            : elegida.Consulta.Id switch
            {
                CatalogoDeConsultas.Resumen => await ConsultarResumenAsync(db, cacheDivipola, entidades.Territorio, entidades.Estado, cancellationToken),
                CatalogoDeConsultas.Estados => await ConsultarEstadosAsync(db, entidades.Territorio, entidades.Estado, cancellationToken),
                CatalogoDeConsultas.Departamentos => await ConsultarDepartamentosAsync(db, cacheDivipola, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.Mercados => await ConsultarMercadosAsync(db, entidades.Estado, cancellationToken),
                CatalogoDeConsultas.MercadosPorDepartamento => await ConsultarMercadosPorDepartamentoAsync(db, cacheDivipola, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.Municipios => await ConsultarMunicipiosAsync(db, cacheDivipola, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.Cobertura => await ConsultarCoberturaAsync(db, cacheDivipola, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.SinFestival => await ConsultarTerritoriosSinFestivalAsync(db, cacheDivipola, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.Tematica => await ConsultarTematicaAsync(db, entidades.Territorio, cancellationToken),
                CatalogoDeConsultas.Ediciones => await ConsultarEdicionesAsync(db, entidades.Anio, cancellationToken),
                CatalogoDeConsultas.Pendientes => await ConsultarPendientesAsync(db, cancellationToken),
                CatalogoDeConsultas.Calidad => await ConsultarCalidadAsync(db, entidades.Territorio, entidades.Estado, cancellationToken),
                _ => throw new InvalidOperationException("La consulta clasificada no pertenece al catálogo."),
            };

        // LO QUE LA PREGUNTA ACOTO MANDA SOBRE LO QUE LA PANTALLA MIRABA, porque es lo que se
        // calculó de verdad. Quien pregunta por el Huila desde la lista de Festivales recibe una
        // cifra del Huila, y decir «toda la operación» encima de ella sería desmentir la propia
        // tabla que va debajo.
        var alcance = resultado.Acotado is { Length: > 0 } acotado
            ? acotado
            : mirando is null
                ? AlcanceDeLaRespuesta.TodaLaOperacion
                : acotadaAFestival ? mirando : AlcanceDeLaRespuesta.NoSeAcota(mirando);

        return Results.Ok(new RespuestaDeConsultaGuiadaDto(
            resultado.Resumen + AlcanceDeLaRespuesta.LoQueNoSePudoAplicar(elegida.Consulta, entidades),
            elegida.Consulta.Id,
            elegida.ResueltoPor,
            DateTime.UtcNow,
            resultado.Tabla,
            alcance));
    }

    /// <summary>
    /// Cómo se llama lo que la persona tiene delante, o <c>null</c> si no tiene nada.
    /// </summary>
    /// <remarks>
    /// SE LEE DE LA BASE Y NO DEL CUERPO DE LA PETICION. El nombre acaba escrito en la respuesta;
    /// aceptarlo de quien pregunta sería dejar que la pantalla se lo invente. Un identificador que
    /// no existe se trata como si no hubiera contexto: no es un error de quien pregunta, es una
    /// pantalla que se quedó con una referencia vieja.
    /// </remarks>
    private static async Task<string?> NombreDeLoQueSeMiraAsync(
        PnmcDbContext db, ContextoDeConsultaDto? contexto, CancellationToken ct)
    {
        if (contexto?.FestivalId is int festivalId)
        {
            var nombre = await db.FestivalRecords.AsNoTracking()
                .Where(item => item.Id == festivalId).Select(item => item.Name).FirstOrDefaultAsync(ct);
            if (nombre is not null) return $"el Festival «{nombre}»";
        }

        if (contexto?.OrganizacionId is int organizacionId)
        {
            var nombre = await db.EntityProfiles.AsNoTracking()
                .Where(item => item.Id == organizacionId).Select(item => item.Name).FirstOrDefaultAsync(ct);
            if (nombre is not null) return $"la organización «{nombre}»";
        }

        return null;
    }

    private static async Task<IResult> ConstruirTableroAsync(
        CacheDivipola cacheDivipola, PnmcDbContext db, CancellationToken cancellationToken)
    {
        var publicados = await LecturaFestivalesPublicados.ConsultarAsync(db, cancellationToken);
        var pendientes = await ContarPendientesAsync(db, cancellationToken);
        var estados = await db.FestivalRecords.AsNoTracking()
            .GroupBy(item => item.StatusCode)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(cancellationToken);
        // MERCADOS, COMO FESTIVALES. El tablero solo contaba Festivales y la direccion de producto fijo
        // que un modulo del Ecosistema se conecta en las visualizaciones
        // de datos de principio a fin, como Festivales, que es la base de funcionamiento.
        var estadosDeMercado = await db.Mercados.AsNoTracking()
            .GroupBy(item => item.EstadoRegistro)
            .Select(grupo => new { Estado = grupo.Key, Total = grupo.Count() })
            .ToListAsync(cancellationToken);
        var etiquetas = await db.ContentStatuses.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var departamentos = await NombresTerritorialesAsync(cacheDivipola, db, cancellationToken);

        var principales = publicados
            .Where(item => !string.IsNullOrWhiteSpace(item.CodigoDepartamento))
            .GroupBy(item => item.CodigoDepartamento!)
            .Select(grupo => new
            {
                Nombre = departamentos.Departamentos.GetValueOrDefault(grupo.Key, grupo.Key),
                Total = grupo.Count(),
            })
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Take(10)
            .Select(item => Fila(item.Nombre, item.Total))
            .ToList();

        var indicadores = new List<IndicadorAnalisisAdministrativoDto>
        {
            new("festivales", "Festivales registrados", await db.FestivalRecords.CountAsync(cancellationToken), "Todos los estados"),
            new("publicados", "Festivales publicados", publicados.Count, "Lectura pública vigente"),
            new("mercados", "Mercados registrados", estadosDeMercado.Sum(item => item.Total), "Todos los estados"),
            new("mercados_publicados", "Mercados publicados", estadosDeMercado.Where(item => item.Estado == "publicado").Sum(item => item.Total), "Visibles en el directorio público"),
            new("pendientes", "Asuntos pendientes", pendientes.Total, "Bandejas institucionales activas"),
            new("organizaciones", "Organizaciones activas", await db.EntityProfiles.CountAsync(item => item.IsActive, cancellationToken), "Organizaciones activas"),
        };

        var filasEstados = estados
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Estado)
            .Select(item => Fila(
                etiquetas.GetValueOrDefault(item.Estado ?? string.Empty, TextoEstado(item.Estado)),
                item.Total))
            .ToList();

        var filasEstadosDeMercado = estadosDeMercado
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Estado)
            .Select(item => Fila(
                etiquetas.GetValueOrDefault(item.Estado ?? string.Empty, TextoEstado(item.Estado)),
                item.Total))
            .ToList();

        return Results.Ok(new TableroAnalisisAdministrativoDto(
            DateTime.UtcNow,
            indicadores,
            filasEstados,
            filasEstadosDeMercado,
            principales));
    }

    /// <summary>
    /// La fotografía de la operación, o la del territorio que la pregunta nombra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ACOTARLO ES LO QUE ARREGLA LA PREGUNTA MAS FRECUENTE CON UN SITIO DENTRO.</b> «¿Cuántos
    /// Festivales hay publicados en el Huila?» es vocabulario del resumen —«cuántos festivales»— y
    /// hasta recibía la cifra del país entero. Se intentó primero
    /// resolverlo enrutándola a la consulta por departamento; la respuesta correcta era más simple:
    /// el resumen del Huila, que es literalmente lo que se preguntó.
    /// </para>
    /// <para>
    /// <b>LO QUE NO ES TERRITORIAL SE CAE DE LA TABLA Y SE DICE.</b> Los asuntos pendientes viven en
    /// bandejas institucionales y las suscripciones al Boletín no tienen territorio; enseñarlas
    /// dentro de un resumen del Huila sería presentar cifras nacionales como si fueran de allí.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Cuántos Festivales hay, con el territorio y el estado que la pregunta haya nombrado.
    /// </summary>
    /// <remarks>
    /// LA COMPARACION DEL ESTADO ES INSENSIBLE A MAYUSCULAS a propósito: la base guarda
    /// <c>publicado</c> en minúscula, pero pudo escribirse <c>Publicado</c> antes de que
    /// <see cref="EstadosFestival"/> unificara el vocabulario, y la <c>collation</c> lo aceptó. La
    /// lectura pública ya lo contempla; contarlos de otra forma aquí daría dos cifras distintas para
    /// la misma pregunta.
    /// </remarks>
    private static async Task<int> ContarFestivalesAsync(
        PnmcDbContext db, TerritorioNombrado? territorio, EstadoNombrado? estado, CancellationToken ct)
    {
        var festivales = await db.FestivalRecords.AsNoTracking()
            .Select(item => new { item.StatusCode, item.DepartmentCode, item.MunicipalityCode })
            .ToListAsync(ct);
        return festivales.Count(item =>
            (territorio is null || territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            && (estado is null || estado.Incluye(item.StatusCode)));
    }

    /// <summary>
    /// En qué estado están los Festivales: la única consulta que ve lo que no está publicado.
    /// </summary>
    /// <remarks>
    /// <b>POR QUE HACIA FALTA.</b> Todas las demás consultas de Festivales parten de la lectura
    /// pública, así que los quince borradores y los ocho Festivales en revisión que hay en la base
    /// —medido sobre <c>PNMC_LOCAL</c>— eran invisibles para el
    /// asistente: preguntarle «¿cuántos festivales hay en revisión?» devolvía el resumen general.
    /// </remarks>
    private static async Task<Resultado> ConsultarEstadosAsync(
        PnmcDbContext db, TerritorioNombrado? territorio, EstadoNombrado? estado, CancellationToken ct)
    {
        var festivales = await db.FestivalRecords.AsNoTracking()
            .Select(item => new { item.StatusCode, item.DepartmentCode, item.MunicipalityCode })
            .ToListAsync(ct);
        var etiquetas = await db.ContentStatuses.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase, ct);
        var dentro = territorio is null
            ? festivales
            : festivales.Where(item => territorio.Cubre(item.DepartmentCode, item.MunicipalityCode)).ToList();
        var donde = territorio is null ? string.Empty : $" en {territorio.EnPalabras}";

        if (estado is not null)
        {
            var cuantos = dentro.Count(item => estado.Incluye(item.StatusCode));
            return new Resultado(
                cuantos == 0
                    ? $"No hay Festivales {estado.EnPalabras}{donde}."
                    : $"Hay {TextoEnEspanol.Plural(cuantos, "Festival", "Festivales")} {estado.Frase(cuantos)}{donde}.",
                Tabla(
                    $"Festivales {estado.EnPalabras}{donde}",
                    "dbo.Festivales; nombres de dbo.EstadosContenido",
                    $"Solo lo que está {estado.EnPalabras}{donde}; incluye lo que no está publicado",
                    ["Estado", "Festivales"],
                    Fila(estado.Negado ? $"Fuera de «{estado.Nombre}»" : estado.Nombre, cuantos)),
                Acotacion(territorio, estado));
        }

        var filas = dentro
            .GroupBy(item => item.StatusCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(grupo => (Nombre: etiquetas.GetValueOrDefault(grupo.Key, TextoEstado(grupo.Key)), Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Select(item => Fila(item.Nombre, item.Total))
            .ToList();

        return new Resultado(
            filas.Count == 0
                ? $"No hay Festivales registrados{donde}."
                : $"Los {TextoEnEspanol.Plural(dentro.Count, "Festival registrado", "Festivales registrados")}{donde} se reparten en {TextoEnEspanol.Plural(filas.Count, "estado", "estados")}. Aquí sí entran los que no están publicados.",
            Tabla(
                $"Festivales por estado{donde}",
                "dbo.Festivales; nombres de dbo.EstadosContenido",
                $"Todos los estados del circuito{donde}; a diferencia del resto, no se limita a la lectura pública",
                ["Estado", "Festivales"], filas),
            Acotacion(territorio));
    }

    private static async Task<Resultado> ConsultarResumenAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, EstadoNombrado? estado, CancellationToken ct)
    {
        // CON UN ESTADO DENTRO, EL RESUMEN DEJA DE SER UN RESUMEN. «¿Cuántos Festivales hay en
        // revisión?» no pide los totales de la operación: pide una cifra. Las demás filas del
        // resumen —publicados, organizaciones— no se pueden acotar a un estado del circuito, y
        // enseñarlas al lado de la cifra pedida haría que la tabla mezclara dos alcances.
        if (estado is not null && !EsPublicado(estado))
        {
            var cuantos = await ContarFestivalesAsync(db, territorio, estado, ct);
            var lugar = territorio is null ? string.Empty : $" en {territorio.EnPalabras}";
            return new Resultado(
                cuantos == 0
                    ? $"No hay Festivales {estado.EnPalabras}{lugar}."
                    : $"Hay {TextoEnEspanol.Plural(cuantos, "Festival", "Festivales")} {estado.Frase(cuantos)}{lugar}.",
                Tabla(
                    $"Festivales {estado.EnPalabras}{lugar}",
                    "dbo.Festivales; nombres de dbo.EstadosContenido",
                    $"Solo lo que está {estado.EnPalabras}{lugar}; incluye lo que no está publicado",
                    ["Estado", "Festivales"],
                    Fila(estado.Negado ? $"Fuera de «{estado.Nombre}»" : estado.Nombre, cuantos)),
                Acotacion(territorio, estado));
        }

        if (territorio is not null)
        {
            var codigo = territorio.Codigo;
            var registradosAqui = territorio.EsDepartamento
                ? await db.FestivalRecords.CountAsync(item => item.DepartmentCode == codigo, ct)
                : await db.FestivalRecords.CountAsync(item => item.MunicipalityCode == codigo, ct);
            var publicadosAqui = Dentro(await LecturaFestivalesPublicados.ConsultarAsync(db, ct), territorio).Count;
            // LA SEDE DE UNA ORGANIZACION SON ESTAS DOS COLUMNAS Y NO LAS OTRAS DOS. `EntityProfileRow`
            // arrastra además un `DepartmentCode` y un `MunicipalityCode` que NO están mapeados a
            // ninguna columna, y usarlos no falla al compilar: falla en tiempo de ejecución con un
            // 500 y un «could not be translated». Lo cazó la prueba de esta consulta, que es la
            // única que los había pedido nunca.
            var organizacionesAqui = territorio.EsDepartamento
                ? await db.EntityProfiles.CountAsync(item => item.IsActive && item.HeadquartersDepartmentCode == codigo, ct)
                : await db.EntityProfiles.CountAsync(item => item.IsActive && item.HeadquartersMunicipalityCode == codigo, ct);

            return new Resultado(
                $"En {territorio.EnPalabras} hay {TextoEnEspanol.Plural(registradosAqui, "Festival registrado", "Festivales registrados")} en todos los estados y {publicadosAqui.ToString(CultureInfo.InvariantCulture)} {(publicadosAqui == 1 ? "visible" : "visibles")} en la lectura pública vigente, y {TextoEnEspanol.Plural(organizacionesAqui, "organización activa", "organizaciones activas")} con sede allí. Los asuntos pendientes y las suscripciones al Boletín no se acotan por territorio y no entran en esta tabla.",
                Tabla(
                    $"Resumen de {territorio.EnPalabras}",
                    "dbo.Festivales; dbo.VersionesFestival; dbo.Entidades",
                    $"Registros ubicados en {territorio.EnPalabras}; la sede de una organización no dice dónde trabaja",
                    ["Indicador", "Total"],
                    Fila("Festivales registrados", registradosAqui),
                    Fila("Festivales publicados", publicadosAqui),
                    Fila("Organizaciones activas con sede", organizacionesAqui)),
                Acotacion(territorio));
        }

        var registrados = await db.FestivalRecords.CountAsync(ct);
        var publicados = (await LecturaFestivalesPublicados.ConsultarAsync(db, ct)).Count;
        var organizaciones = await db.EntityProfiles.CountAsync(item => item.IsActive, ct);
        var pendientes = await ContarPendientesAsync(db, ct);
        var boletin = await db.BoletinSuscripciones.CountAsync(item => item.Estado == "activa", ct);

        // LA RESTA SE DA HECHA, y no es un adorno. «¿Cuántos festivales hay sin publicar?» es una
        // pregunta corriente y la tabla la contestaba solo si quien la leía restaba dos filas. Es
        // además la cifra que dice cuánto trabajo hay por delante: los borradores y lo que espera
        // revisión.
        var sinPublicar = registrados - publicados;

        return new Resultado(
            $"Hay {TextoEnEspanol.Plural(registrados, "Festival registrado", "Festivales registrados")} en todos los estados, {publicados.ToString(CultureInfo.InvariantCulture)} {(publicados == 1 ? "visible" : "visibles")} en la lectura pública vigente y {sinPublicar.ToString(CultureInfo.InvariantCulture)} todavía sin publicar. La operación tiene {TextoEnEspanol.Plural(organizaciones, "organización activa", "organizaciones activas")}, {pendientes.Total.ToString(CultureInfo.InvariantCulture)} asuntos pendientes y {boletin.ToString(CultureInfo.InvariantCulture)} suscripciones activas al Boletín.",
            Tabla(
                "Resumen del Espacio de Gestión Administrativa",
                "dbo.Festivales; dbo.VersionesFestival; dbo.Entidades; bandejas institucionales; dbo.BoletinSuscripciones",
                "Totales actuales; Festivales registrados y publicados se informan por separado",
                ["Indicador", "Total"],
                Fila("Festivales registrados", registrados),
                Fila("Festivales publicados", publicados),
                Fila("Festivales sin publicar", sinPublicar),
                Fila("Organizaciones activas", organizaciones),
                Fila("Asuntos pendientes", pendientes.Total),
                Fila("Suscripciones activas", boletin)));
    }

    /// <summary>
    /// Los Festivales publicados por departamento, o dentro de uno cuando la pregunta lo nombra.
    /// </summary>
    /// <remarks>
    /// <b>ACOTADA, BAJA UN NIVEL, y por eso la tabla cambia de columna.</b> Devolver una sola fila
    /// —«Huila | 12»— sería técnicamente correcto y prácticamente inútil: quien pregunta cuántos
    /// Festivales hay en el Huila quiere saber además dónde están. Al nombrar un municipio la tabla
    /// se queda en esa única fila, que ahí sí es toda la respuesta.
    /// </remarks>
    private static async Task<Resultado> ConsultarDepartamentosAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var publicados = await LecturaFestivalesPublicados.ConsultarAsync(db, ct);
        var nombres = await NombresTerritorialesAsync(cache, db, ct);

        if (territorio is not null)
        {
            var dentro = Dentro(publicados, territorio);
            var porMunicipio = PorMunicipio(dentro, nombres, declararSinMunicipio: true);
            var municipiosConPresencia = dentro.Select(item => item.CodigoMunicipio).Where(CodigoPresente).Distinct().Count();
            var texto = dentro.Count == 0
                ? $"No hay Festivales publicados con ubicación registrada en {territorio.EnPalabras}."
                : territorio.EsDepartamento
                    ? $"Hay {TextoEnEspanol.Plural(dentro.Count, "Festival publicado", "Festivales publicados")} en {territorio.Nombre}, repartidos en {municipiosConPresencia.ToString(CultureInfo.InvariantCulture)} de sus municipios."
                    : $"Hay {TextoEnEspanol.Plural(dentro.Count, "Festival publicado", "Festivales publicados")} en {territorio.EnPalabras}.";

            return new Resultado(texto, Tabla(
                $"Festivales publicados en {territorio.EnPalabras}",
                "dbo.Festivales y dbo.VersionesFestival vigentes; nombres de dbo.Divipola",
                $"Solo Festivales visibles en la lectura pública y ubicados en {territorio.EnPalabras}",
                ["Municipio", "Festivales"], porMunicipio),
                Acotacion(territorio));
        }

        var filas = publicados
            .Where(item => !string.IsNullOrWhiteSpace(item.CodigoDepartamento))
            .GroupBy(item => item.CodigoDepartamento!)
            .Select(grupo => (Nombre: nombres.Departamentos.GetValueOrDefault(grupo.Key, grupo.Key), Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Select(item => Fila(item.Nombre, item.Total))
            .ToList();

        var primero = filas.FirstOrDefault();
        var resumen = primero is null
            ? "No hay Festivales publicados con departamento registrado."
            : $"Hay presencia de Festivales publicados en {filas.Count} departamentos. El mayor conteo corresponde a {primero[0]} con {primero[1]}.";
        return new Resultado(resumen, Tabla(
            "Festivales publicados por departamento",
            "dbo.Festivales y dbo.VersionesFestival vigentes; nombres de dbo.Divipola",
            "Solo Festivales visibles en la lectura pública y con departamento registrado",
            ["Departamento", "Festivales"], filas));
    }

    /// <summary>
    /// Cuántos mercados hay y en qué estado, con los que ocurren dentro de un festival aparte.
    /// </summary>
    /// <remarks>
    /// <b>LA RELACION CON EL FESTIVAL ES LO PROPIO DE ESTE MODULO</b> y por eso se cuenta: es la
    /// pregunta que Festivales no puede responder por su lado.
    /// </remarks>
    private static async Task<Resultado> ConsultarMercadosAsync(
        PnmcDbContext db, EstadoNombrado? estado, CancellationToken ct)
    {
        var porEstado = await db.Mercados.AsNoTracking()
            .Where(m => m.Activo)
            .GroupBy(m => m.EstadoRegistro)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        // UN ESTADO DENTRO DE LA PREGUNTA PIDE UNA CIFRA, NO LA TABLA ENTERA. Los mercados comparten
        // el catálogo de estados con los Festivales, así que «¿cuántos mercados hay en borrador?» se
        // contesta igual que la misma pregunta sobre Festivales.
        if (estado is not null && !EsPublicado(estado))
        {
            var deEseEstado = porEstado.Where(x => estado.Incluye(x.Estado)).Sum(x => x.Total);
            return new Resultado(
                deEseEstado == 0
                    ? $"No hay mercados musicales {estado.EnPalabras}."
                    : $"Hay {TextoEnEspanol.Plural(deEseEstado, "mercado musical", "mercados musicales")} {estado.Frase(deEseEstado)}.",
                Tabla(
                    $"Mercados musicales {estado.EnPalabras}",
                    "dbo.Mercados",
                    $"Solo mercados activos {estado.EnPalabras}",
                    ["Estado", "Mercados"],
                    Fila(estado.Negado ? $"Fuera de «{estado.Nombre}»" : estado.Nombre, deEseEstado)),
                Acotacion(null, estado));
        }

        var registrados = porEstado.Sum(x => x.Total);
        var publicados = porEstado.Where(x => x.Estado == "publicado").Sum(x => x.Total);
        var enFestival = await db.Mercados.AsNoTracking()
            .CountAsync(m => m.Activo && m.SeRealizaEnElMarcoDeUnFestival, ct);
        var ediciones = await db.EdicionesMercado.AsNoTracking().CountAsync(ct);

        var filas = porEstado
            .OrderByDescending(x => x.Total).ThenBy(x => x.Estado, StringComparer.Ordinal)
            .Select(x => Fila(TextoEstado(x.Estado), x.Total))
            .ToList();
        filas.Add(Fila("En el marco de un festival", enFestival));
        filas.Add(Fila("Ediciones registradas", ediciones));

        // LA CIFRA MANDA SOBRE EL VERBO. «1 mercados se realizan» se lee como un error del sistema y
        // hace dudar del resto de la respuesta, que es lo último que le conviene a un asistente de
        // datos.
        var resumen = registrados == 0
            ? "Todavía no hay mercados musicales registrados."
            : $"Hay {TextoEnEspanol.Plural(registrados, "mercado musical registrado", "mercados musicales registrados")} y {publicados.ToString(CultureInfo.InvariantCulture)} {(publicados == 1 ? "publicado" : "publicados")}. "
              + $"De ellos, {enFestival.ToString(CultureInfo.InvariantCulture)} {(enFestival == 1 ? "se realiza" : "se realizan")} en el marco de un festival, y entre todos suman {TextoEnEspanol.Plural(ediciones, "edición registrada", "ediciones registradas")}.";
        return new Resultado(resumen, Tabla(
            "Mercados musicales por estado",
            "dbo.Mercados; dbo.EdicionesMercado",
            "Todos los mercados activos, en cualquier estado de registro",
            ["Estado", "Mercados"], filas));
    }

    /// <summary>Los mercados publicados por departamento, con el mismo criterio que los Festivales.</summary>
    private static async Task<Resultado> ConsultarMercadosPorDepartamentoAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var ubicados = await db.Mercados.AsNoTracking()
            .Where(m => m.Activo && m.EstadoRegistro == "publicado" && m.CodigoDepartamento != null)
            .Select(m => new { m.CodigoDepartamento, m.CodigoMunicipio })
            .ToListAsync(ct);
        var nombres = await NombresTerritorialesAsync(cache, db, ct);

        if (territorio is not null)
        {
            var dentro = ubicados.Where(m => territorio.Cubre(m.CodigoDepartamento, m.CodigoMunicipio)).ToList();
            var porMunicipio = dentro
                .Where(m => CodigoPresente(m.CodigoMunicipio))
                .GroupBy(m => m.CodigoMunicipio!)
                .Select(grupo => (Nombre: nombres.Municipios.GetValueOrDefault(grupo.Key, grupo.Key), Total: grupo.Count()))
                .OrderByDescending(item => item.Total)
                .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
                .Take(MaximoFilas)
                .Select(item => Fila(item.Nombre, item.Total))
                .ToList();
            var sinMunicipio = dentro.Count(m => !CodigoPresente(m.CodigoMunicipio));
            if (sinMunicipio > 0) { porMunicipio.Add(Fila("Sin municipio registrado", sinMunicipio)); }

            return new Resultado(
                dentro.Count == 0
                    ? $"No hay mercados musicales publicados con ubicación registrada en {territorio.EnPalabras}."
                    : $"Hay {TextoEnEspanol.Plural(dentro.Count, "mercado musical publicado", "mercados musicales publicados")} en {territorio.EnPalabras}.",
                Tabla(
                    $"Mercados musicales publicados en {territorio.EnPalabras}",
                    "dbo.Mercados; nombres de dbo.Divipola",
                    $"Solo mercados publicados y ubicados en {territorio.EnPalabras}",
                    ["Municipio", "Mercados"], porMunicipio),
                Acotacion(territorio));
        }

        var filas = ubicados
            .GroupBy(m => m.CodigoDepartamento!)
            .Select(grupo => (Nombre: nombres.Departamentos.GetValueOrDefault(grupo.Key, grupo.Key), Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Select(item => Fila(item.Nombre, item.Total))
            .ToList();

        var primero = filas.FirstOrDefault();
        var resumen = primero is null
            ? "No hay mercados musicales publicados con departamento registrado."
            : $"Hay mercados musicales publicados en {TextoEnEspanol.Plural(filas.Count, "departamento", "departamentos")}. El mayor conteo corresponde a {primero[0]} con {primero[1]}.";
        return new Resultado(resumen, Tabla(
            "Mercados musicales publicados por departamento",
            "dbo.Mercados; nombres de dbo.Divipola",
            "Solo mercados publicados y con departamento registrado",
            ["Departamento", "Mercados"], filas));
    }

    private static async Task<Resultado> ConsultarMunicipiosAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var publicados = await LecturaFestivalesPublicados.ConsultarAsync(db, ct);
        var nombres = await NombresTerritorialesAsync(cache, db, ct);
        var considerados = territorio is null ? publicados : Dentro(publicados, territorio);
        var filas = PorMunicipio(considerados, nombres, declararSinMunicipio: false);
        var donde = territorio is null ? string.Empty : $" en {territorio.EnPalabras}";

        var primero = filas.FirstOrDefault();
        var resumen = primero is null
            ? $"No hay Festivales publicados con municipio registrado{donde}."
            : $"El municipio con mayor presencia registrada{donde} es {primero[0]} con {primero[1]} Festivales publicados. Se muestran hasta {MaximoFilas} municipios.";
        return new Resultado(resumen, Tabla(
            $"Municipios con más Festivales publicados{donde}",
            "dbo.Festivales y dbo.VersionesFestival vigentes; nombres de dbo.Divipola",
            $"Solo Festivales visibles en la lectura pública y con municipio registrado{donde}",
            ["Municipio", "Festivales"], filas),
            Acotacion(territorio));
    }

    /// <summary>
    /// Cuánto territorio cubre el directorio publicado, del país o de un departamento.
    /// </summary>
    /// <remarks>
    /// ACOTADA TRABAJA SIEMPRE A NIVEL DE DEPARTAMENTO, también cuando lo que se nombró fue un
    /// municipio: la cobertura de un municipio suelto es siempre cero o cien por ciento, que no es
    /// una respuesta. Se dice cuál es el departamento en el que se calculó.
    /// </remarks>
    private static async Task<Resultado> ConsultarCoberturaAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var publicados = await LecturaFestivalesPublicados.ConsultarAsync(db, ct);
        var nombres = await NombresTerritorialesAsync(cache, db, ct);

        if (territorio is not null)
        {
            var vocabulario = await cache.ObtenerVocabularioAsync(db, ct);
            var codigo = territorio.CodigoDelDepartamento;
            var nombreDelDepartamento = territorio.NombreDelDepartamento;
            var dentro = publicados.Where(item => item.CodigoDepartamento == codigo).ToList();
            var conPresencia = dentro.Select(item => item.CodigoMunicipio).Where(CodigoPresente).Distinct().Count();
            var totalMunicipios = vocabulario.MunicipiosDe(codigo).Count;
            var porcentaje = Porcentaje(conPresencia, totalMunicipios);

            return new Resultado(
                $"En {nombreDelDepartamento} hay Festivales publicados en {conPresencia.ToString(CultureInfo.InvariantCulture)} de sus {totalMunicipios.ToString(CultureInfo.InvariantCulture)} municipios ({porcentaje}), con {TextoEnEspanol.Plural(dentro.Count, "Festival", "Festivales")} en total. Los Festivales de alcance nacional sin ubicación no se cuentan como presencia en cada territorio.",
                Tabla(
                    $"Cobertura territorial registrada en {nombreDelDepartamento}",
                    "dbo.Festivales y dbo.VersionesFestival vigentes frente a dbo.Divipola",
                    $"Presencia de al menos un Festival publicado en cada municipio de {nombreDelDepartamento}; no equivale a población atendida",
                    ["Indicador", "Con presencia", "Total DIVIPOLA", "Porcentaje"],
                    Fila("Municipios", conPresencia, totalMunicipios, porcentaje)),
                nombreDelDepartamento);
        }

        var departamentos = publicados.Select(item => item.CodigoDepartamento).Where(CodigoPresente).Distinct().Count();
        var municipios = publicados.Select(item => item.CodigoMunicipio).Where(CodigoPresente).Distinct().Count();
        var porcentajeDepartamentos = Porcentaje(departamentos, nombres.Departamentos.Count);
        var porcentajeMunicipios = Porcentaje(municipios, nombres.Municipios.Count);

        return new Resultado(
            $"La presencia territorial registrada de Festivales publicados comprende {departamentos} de {nombres.Departamentos.Count} departamentos ({porcentajeDepartamentos}) y {municipios} de {nombres.Municipios.Count} municipios ({porcentajeMunicipios}). Los Festivales de alcance nacional sin ubicación no se cuentan como presencia en cada territorio.",
            Tabla(
                "Cobertura territorial registrada",
                "dbo.Festivales y dbo.VersionesFestival vigentes frente a dbo.Divipola",
                "Presencia de al menos un Festival publicado con código territorial; no equivale a población atendida",
                ["Indicador", "Con presencia", "Total DIVIPOLA", "Porcentaje"],
                Fila("Departamentos", departamentos, nombres.Departamentos.Count, porcentajeDepartamentos),
                Fila("Municipios", municipios, nombres.Municipios.Count, porcentajeMunicipios)));
    }

    private static async Task<Resultado> ConsultarTerritoriosSinFestivalAsync(
        PnmcDbContext db, CacheDivipola cache, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var publicados = await LecturaFestivalesPublicados.ConsultarAsync(db, ct);
        var nombres = await NombresTerritorialesAsync(cache, db, ct);

        if (territorio is not null)
        {
            var vocabulario = await cache.ObtenerVocabularioAsync(db, ct);
            var codigo = territorio.CodigoDelDepartamento;
            var nombreDelDepartamento = territorio.NombreDelDepartamento;
            var conFestival = publicados
                .Where(item => item.CodigoDepartamento == codigo)
                .Select(item => item.CodigoMunicipio)
                .Where(CodigoPresente)
                .ToHashSet();
            var vacios = vocabulario.MunicipiosDe(codigo)
                .Where(municipio => !conFestival.Contains(municipio.Codigo))
                .OrderBy(municipio => municipio.Nombre, StringComparer.CurrentCulture)
                .Take(MaximoFilas)
                .Select(municipio => Fila(municipio.Nombre))
                .ToList();
            var cuantos = vocabulario.MunicipiosDe(codigo).Count(municipio => !conFestival.Contains(municipio.Codigo));

            return new Resultado(
                cuantos == 0
                    ? $"Todos los municipios de {nombreDelDepartamento} tienen al menos un Festival publicado registrado."
                    : $"En {nombreDelDepartamento} hay {TextoEnEspanol.Plural(cuantos, "municipio", "municipios")} sin presencia registrada de un Festival publicado. Esto describe el directorio, no prueba ausencia de actividad musical en el territorio.",
                Tabla(
                    $"Municipios de {nombreDelDepartamento} sin Festival publicado registrado",
                    "dbo.Festivales y dbo.VersionesFestival vigentes frente a dbo.Divipola",
                    $"Ausencia en el directorio publicado dentro de {nombreDelDepartamento}; se muestran hasta {MaximoFilas}",
                    ["Municipio"], vacios),
                nombreDelDepartamento);
        }

        var codigosDepartamentos = publicados.Select(item => item.CodigoDepartamento).Where(CodigoPresente).ToHashSet();
        var codigosMunicipios = publicados.Select(item => item.CodigoMunicipio).Where(CodigoPresente).ToHashSet();
        var departamentos = nombres.Departamentos
            .Where(item => !codigosDepartamentos.Contains(item.Key))
            .OrderBy(item => item.Value, StringComparer.CurrentCulture)
            .Select(item => Fila(item.Value))
            .ToList();
        var municipiosSinFestival = nombres.Municipios.Count(item => !codigosMunicipios.Contains(item.Key));

        return new Resultado(
            $"Hay {departamentos.Count} departamentos y {municipiosSinFestival} municipios sin presencia registrada de un Festival publicado. Esto describe el directorio, no prueba ausencia de actividad musical en el territorio.",
            Tabla(
                "Departamentos sin Festival publicado registrado",
                "dbo.Festivales y dbo.VersionesFestival vigentes frente a dbo.Divipola",
                "Ausencia en el directorio publicado; no equivale a ausencia de actividad cultural",
                ["Departamento"], departamentos));
    }

    private static async Task<Resultado> ConsultarTematicaAsync(
        PnmcDbContext db, TerritorioNombrado? territorio, CancellationToken ct)
    {
        var todos = await LecturaFestivalesPublicados.ConsultarAsync(db, ct);
        var publicados = territorio is null ? todos : Dentro(todos, territorio);
        var donde = territorio is null ? string.Empty : $" en {territorio.EnPalabras}";
        var filas = new List<IReadOnlyList<string>>();
        filas.AddRange(Distribuir("Práctica musical", publicados.SelectMany(item => item.PracticasMusicales.Select(x => x.Nombre))));
        filas.AddRange(Distribuir("Territorio sonoro", publicados.SelectMany(item => item.TerritoriosSonoros.Select(x => x.Nombre))));
        filas.AddRange(Distribuir("Periodicidad", publicados.Select(item => item.Periodicidad)));

        return new Resultado(
            filas.Count == 0
                ? $"No hay categorías temáticas registradas en los Festivales publicados{donde}."
                : $"La distribución temática reúne {TextoEnEspanol.Plural(filas.Count, "categoría", "categorías")} con presencia en los {TextoEnEspanol.Plural(publicados.Count, "Festival publicado", "Festivales publicados")}{donde}. Una misma ficha puede aportar más de una práctica o territorio sonoro.",
            Tabla(
                $"Distribución temática de Festivales publicados{donde}",
                "dbo.VersionesFestival y sus relaciones vigentes con prácticas y territorios; respaldo histórico de dbo.Festivales",
                $"Solo Festivales visibles en la lectura pública{donde}; las categorías no son mutuamente excluyentes",
                ["Dimensión", "Categoría", "Festivales"], filas),
            Acotacion(territorio));
    }

    /// <summary>
    /// Cuántas Ediciones hay por año, o cuántas hubo en el año que se preguntó.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>CUENTA TODAS LAS EDICIONES REGISTRADAS, no solo las publicadas</b>, y lo declara en el
    /// alcance. Una Edición que ocurrió es un hecho del territorio; que esté publicada o no es una
    /// decisión editorial posterior. Filtrar por visibilidad convertiría «¿cuántas ediciones hubo en
    /// 2024?» en «¿cuántas ediciones de 2024 hemos publicado?», que es otra pregunta y además
    /// contesta menos.
    /// </para>
    /// <para>
    /// <b>DOS COLUMNAS PORQUE SON DOS COSAS DISTINTAS:</b> un Festival puede tener varias Ediciones
    /// en un mismo año, y contar solo Ediciones haría creer que hubo más Festivales de los que hubo.
    /// </para>
    /// </remarks>
    private static async Task<Resultado> ConsultarEdicionesAsync(PnmcDbContext db, int? anio, CancellationToken ct)
    {
        var ediciones = await db.EdicionesFestival.AsNoTracking()
            .Where(item => item.Anio != null)
            .Select(item => new { Anio = item.Anio!.Value, item.FestivalId })
            .ToListAsync(ct);
        var sinAnio = await db.EdicionesFestival.CountAsync(item => item.Anio == null, ct);

        // LAS EDICIONES DE MERCADO TAMBIEN TIENEN AÑO, y hasta esta
        // consulta no las veía. La pregunta «¿cuántas ediciones hubo en 2024?» no dice de qué
        // proceso: contestarla contando solo las de Festival era dar media respuesta con la
        // autoridad de una tabla completa. Van en su propia columna porque son dos cosas distintas
        // —una Edición de mercado no es una Edición de Festival— y sumarlas escondería cuál es cuál.
        var deMercado = await db.EdicionesMercado.AsNoTracking()
            .Select(item => new { item.Anio, item.MercadoId })
            .ToListAsync(ct);

        var columnas = new[] { "Año", "De Festival", "De mercado" };
        const string fuente = "dbo.EdicionesFestival y dbo.EdicionesMercado";

        if (anio is int pedido)
        {
            var deFestivalEseAnio = ediciones.Count(item => item.Anio == pedido);
            var deMercadoEseAnio = deMercado.Count(item => item.Anio == pedido);
            var total = deFestivalEseAnio + deMercadoEseAnio;
            return new Resultado(
                total == 0
                    ? $"No hay Ediciones registradas con año {pedido.ToString(CultureInfo.InvariantCulture)}."
                    : $"En {pedido.ToString(CultureInfo.InvariantCulture)} hay {TextoEnEspanol.Plural(total, "Edición registrada", "Ediciones registradas")}: {deFestivalEseAnio.ToString(CultureInfo.InvariantCulture)} de Festival y {deMercadoEseAnio.ToString(CultureInfo.InvariantCulture)} de mercado musical.",
                Tabla(
                    $"Ediciones registradas en {pedido.ToString(CultureInfo.InvariantCulture)}",
                    fuente,
                    "Ediciones registradas en cualquier estado de visibilidad",
                    columnas,
                    Fila(pedido, deFestivalEseAnio, deMercadoEseAnio)),
                Acotacion(null, null, pedido));
        }

        var anios = ediciones.Select(item => item.Anio)
            .Concat(deMercado.Select(item => item.Anio))
            .Distinct()
            .OrderByDescending(cual => cual)
            .Take(MaximoFilas)
            .ToList();
        var filas = anios
            .Select(cual => Fila(cual, ediciones.Count(item => item.Anio == cual), deMercado.Count(item => item.Anio == cual)))
            .ToList();

        var totalRegistradas = ediciones.Count + deMercado.Count;
        var resumen = filas.Count == 0
            ? "No hay Ediciones con año registrado."
            : $"Hay {TextoEnEspanol.Plural(totalRegistradas, "Edición registrada", "Ediciones registradas")} con año —{ediciones.Count.ToString(CultureInfo.InvariantCulture)} de Festival y {deMercado.Count.ToString(CultureInfo.InvariantCulture)} de mercado musical—, repartidas en {TextoEnEspanol.Plural(anios.Count, "año", "años")}.";
        if (sinAnio > 0)
        {
            resumen += $" Otras {TextoEnEspanol.Plural(sinAnio, "Edición de Festival no tiene", "Ediciones de Festival no tienen")} año registrado y no entran en la tabla.";
        }

        return new Resultado(resumen, Tabla(
            "Ediciones por año",
            fuente,
            $"Ediciones registradas en cualquier estado de visibilidad; se muestran hasta {MaximoFilas} años",
            columnas, filas));
    }

    private static async Task<Resultado> ConsultarPendientesAsync(PnmcDbContext db, CancellationToken ct)
    {
        var pendientes = await ContarPendientesAsync(db, ct);
        return new Resultado(
            $"Las bandejas institucionales suman {pendientes.Total} asuntos pendientes. La cifra es informativa: las decisiones deben tomarse en Solicitudes y nunca desde el asistente.",
            Tabla(
                "Pendientes de gestión",
                "dbo.Festivales; dbo.PropuestasDeCambio; dbo.SolicitudesVinculacionRegistros; dbo.ReclamacionesAdministracion; dbo.RegistrosDuplicadosCandidatos; dbo.RegistrosCalidadDatos",
                "Estados activos de las bandejas institucionales",
                ["Tipo", "Pendientes"],
                Fila("Festivales en revisión", pendientes.Festivales),
                Fila("Propuestas de cambio", pendientes.Propuestas),
                Fila("Solicitudes y retiros", pendientes.Solicitudes),
                Fila("Reclamaciones de administración", pendientes.Reclamaciones),
                Fila("Posibles duplicados", pendientes.Duplicados),
                Fila("Alertas de calidad", pendientes.Calidad)));
    }

    /// <summary>
    /// Qué falta en las fichas de Festival, del directorio entero o de un territorio.
    /// </summary>
    /// <remarks>
    /// LAS ALERTAS ABIERTAS NO SE ACOTAN Y SE DICE. Cuelgan de registros de varios procesos y no de
    /// un territorio; acotar los campos vacíos y dejar la cifra de alertas nacional sin avisarlo
    /// haría que una tabla mezclara dos alcances sin que se notara.
    /// </remarks>
    private static async Task<Resultado> ConsultarCalidadAsync(
        PnmcDbContext db, TerritorioNombrado? territorio, EstadoNombrado? estado, CancellationToken ct)
    {
        var todos = await db.FestivalRecords.AsNoTracking()
            .Select(item => new
            {
                item.StatusCode,
                item.CoverageLevel,
                item.DepartmentCode,
                item.MunicipalityCode,
                item.Description,
                item.ContactEmail,
                item.ContactPhone,
            })
            .ToListAsync(ct);
        // ACOTAR LA CALIDAD POR ESTADO ES LA PREGUNTA DE QUIEN REVISA: «¿qué les falta a los que
        // están en revisión?» es lo que se hace antes de decidir sobre ellos, y mezclarlos con los
        // ciento sesenta y cinco publicados esconde justo lo que se quería mirar.
        var festivales = todos
            .Where(item => territorio is null || territorio.Cubre(item.DepartmentCode, item.MunicipalityCode))
            .Where(item => estado is null || estado.Incluye(item.StatusCode))
            .ToList();
        var donde = (territorio is null ? string.Empty : $" en {territorio.EnPalabras}")
            + (estado is null ? string.Empty : $" {estado.EnPalabras}");
        var alertas = await db.RecordQualityFlags.CountAsync(item => item.Status == "abierta" || item.Status == "en_revision", ct);
        var sinDepartamento = festivales.Count(item => !EsNacional(item.CoverageLevel) && string.IsNullOrWhiteSpace(item.DepartmentCode));
        var sinMunicipio = festivales.Count(item => EsMunicipal(item.CoverageLevel) && string.IsNullOrWhiteSpace(item.MunicipalityCode));
        var sinDescripcion = festivales.Count(item => string.IsNullOrWhiteSpace(item.Description));
        var sinContacto = festivales.Count(item => string.IsNullOrWhiteSpace(item.ContactEmail) && string.IsNullOrWhiteSpace(item.ContactPhone));

        var filas = new List<IReadOnlyList<string>>
        {
            Fila("Sin departamento requerido", sinDepartamento),
            Fila("Sin municipio requerido", sinMunicipio),
            Fila("Sin descripción", sinDescripcion),
            Fila("Sin correo ni teléfono de contacto", sinContacto),
        };
        if (donde.Length == 0) { filas.Add(Fila("Alertas de calidad abiertas", alertas)); }

        var cierre = donde.Length == 0
            ? $" y {alertas.ToString(CultureInfo.InvariantCulture)} alertas de calidad abiertas. Se informan conteos; no se identifica ningún registro."
            : ". Se informan conteos; no se identifica ningún registro. Las alertas de calidad abiertas no se acotan y no entran en esta tabla.";

        return new Resultado(
            $"Sobre {TextoEnEspanol.Plural(festivales.Count, "Festival registrado", "Festivales registrados")}{donde}, hay {sinDepartamento.ToString(CultureInfo.InvariantCulture)} sin departamento cuando su alcance lo requiere y {sinMunicipio.ToString(CultureInfo.InvariantCulture)} sin municipio para alcance municipal{cierre}",
            Tabla(
                $"Calidad agregada de datos de Festivales{donde}",
                "dbo.Festivales y dbo.RegistrosCalidadDatos",
                $"Todos los estados{donde}; conteos de ausencia sin valores de contacto ni identificación de registros",
                ["Hallazgo", "Registros"], filas),
            Acotacion(territorio, estado));
    }

    /// <summary>
    /// Qué le falta al Festival que la persona tiene delante.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ES LA MISMA PREGUNTA ACOTADA, NO OTRA CONSULTA.</b> «¿Qué está incompleto?» desde la lista
    /// pregunta por el directorio; desde dentro de una ficha pregunta por esa ficha. Obligar a
    /// formularlas distinto sería pedirle a la persona que sepa dónde está parada.
    /// </para>
    /// <para>
    /// <b>AQUI SI SE NOMBRAN CAMPOS DE UN REGISTRO</b>, y no contradice la regla de privacidad del
    /// asistente: quien pregunta está mirando esa ficha en la pantalla de al lado. Lo que sigue sin
    /// devolverse son los valores de contacto: se dice si falta el correo, nunca cuál es.
    /// </para>
    /// </remarks>
    private static async Task<Resultado> ConsultarCalidadDeUnFestivalAsync(
        PnmcDbContext db, int festivalId, CancellationToken ct)
    {
        var festival = await db.FestivalRecords.AsNoTracking()
            .Where(item => item.Id == festivalId)
            .Select(item => new
            {
                item.Name,
                item.CoverageLevel,
                item.DepartmentCode,
                item.MunicipalityCode,
                item.Description,
                item.ContactEmail,
                item.ContactPhone,
            })
            .FirstOrDefaultAsync(ct);

        if (festival is null)
        {
            return new Resultado(
                "Ese Festival ya no está en la base, así que no puedo revisar qué le falta.",
                Tabla("Calidad de datos del Festival", "dbo.Festivales", "El registro no existe",
                    ["Campo", "Estado"]));
        }

        var alertas = await db.RecordQualityFlags
            .CountAsync(item => item.RecordId == festivalId.ToString(CultureInfo.InvariantCulture)
                && (item.Status == "abierta" || item.Status == "en_revision"), ct);

        var faltas = new List<IReadOnlyList<string>>();
        void Revisar(string campo, bool falta, bool aplica = true)
        {
            if (!aplica) { faltas.Add(Fila(campo, "No aplica a su alcance")); return; }
            faltas.Add(Fila(campo, falta ? "Falta" : "Completo"));
        }

        Revisar("Departamento", string.IsNullOrWhiteSpace(festival.DepartmentCode), !EsNacional(festival.CoverageLevel));
        Revisar("Municipio", string.IsNullOrWhiteSpace(festival.MunicipalityCode), EsMunicipal(festival.CoverageLevel));
        Revisar("Descripción", string.IsNullOrWhiteSpace(festival.Description));
        Revisar("Correo o teléfono de contacto",
            string.IsNullOrWhiteSpace(festival.ContactEmail) && string.IsNullOrWhiteSpace(festival.ContactPhone));
        faltas.Add(Fila("Alertas de calidad abiertas", alertas.ToString(CultureInfo.InvariantCulture)));

        var cuantas = faltas.Count(fila => fila[1] == "Falta");
        var resumen = cuantas == 0
            ? $"«{festival.Name}» tiene completos los campos que su alcance exige. Hay {alertas} alertas de calidad abiertas sobre él."
            : $"A «{festival.Name}» le faltan {cuantas} datos de los que su alcance exige, y tiene {alertas} alertas de calidad abiertas.";

        return new Resultado(resumen, Tabla(
            "Calidad de datos del Festival",
            "dbo.Festivales y dbo.RegistrosCalidadDatos",
            "Solo este Festival; se informa si falta un contacto, nunca cuál es",
            ["Campo", "Estado"], faltas));
    }

    private static async Task<PendientesAdministrativos> ContarPendientesAsync(PnmcDbContext db, CancellationToken ct)
    {
        var festivales = await db.FestivalRecords.CountAsync(item => item.StatusCode == "en_revision", ct);
        // LAS PROPUESTAS DE TODOS LOS PROCESOS, no solo las de Festival: desde el 17 de septiembre
        // de 2026 viven en la misma tabla, y la cifra de «lo que espera una decisión» tiene que
        // contarlas todas o se queda corta en cuanto un mercado propone algo.
        var propuestas = await db.PropuestasDeCambio.CountAsync(item => item.Estado == EstadosDePropuesta.EnRevision, ct);
        var solicitudes = await db.RecordLinkRequests.CountAsync(item => item.Status == "pendiente" || item.Status == "en_revision", ct);
        var reclamaciones = await db.AdministrationClaims.CountAsync(item =>
            item.Status == "enviada" || item.Status == "en_revision" ||
            item.Status == "requiere_aclaracion" || item.Status == "aclaracion_enviada", ct);
        var duplicados = await db.RecordDuplicateCandidates.CountAsync(item => item.Status == "pendiente" || item.Status == "en_revision", ct);
        var calidad = await db.RecordQualityFlags.CountAsync(item => item.Status == "abierta" || item.Status == "en_revision", ct);
        return new PendientesAdministrativos(festivales, propuestas, solicitudes, reclamaciones, duplicados, calidad);
    }

    /// <summary>
    /// Los nombres territoriales, presentables y sin volver a leer DIVIPOLA.
    /// </summary>
    /// <remarks>
    /// <b>ERA UNA TERCERA LECTURA DE LA MISMA TABLA.</b> Este fichero se traía DIVIPOLA entera en
    /// cada consulta territorial —seis de las once la piden— mientras <c>CacheDivipola</c> ya la
    /// tenía cargada para el geovisor desde el arranque. Pasar por el caché no solo ahorra la
    /// consulta: hace que la consola y el mapa escriban el mismo nombre, porque ahora salen del
    /// mismo sitio. Y de paso los nombres llegan presentables —«Huila» y no «HUILA»—, que es como
    /// los escribe el resto de la consola.
    /// </remarks>
    private static async Task<NombresTerritoriales> NombresTerritorialesAsync(
        CacheDivipola cache, PnmcDbContext db, CancellationToken ct)
    {
        var (departamentos, municipios) = await cache.ObtenerPresentablesAsync(db, ct);
        return new NombresTerritoriales(departamentos, municipios);
    }

    /// <summary>Los Festivales publicados que caen dentro del territorio nombrado.</summary>
    private static List<FestivalPublicadoVigenteLectura> Dentro(
        IEnumerable<FestivalPublicadoVigenteLectura> publicados, TerritorioNombrado territorio) =>
        publicados.Where(item => territorio.Cubre(item.CodigoDepartamento, item.CodigoMunicipio)).ToList();

    /// <summary>
    /// El desglose por municipio de un conjunto de Festivales ya acotado.
    /// </summary>
    /// <remarks>
    /// LOS QUE NO TIENEN MUNICIPIO SE DECLARAN EN UNA FILA <b>CUANDO LA FRASE DA UN TOTAL</b>.
    /// Dejarlos fuera en ese caso haría que la tabla sumara menos que la frase que la encabeza, y
    /// quien lo notara no tendría forma de saber si falta un municipio o sobra un Festival. En el
    /// ranking nacional, en cambio, no hay total que cuadrar y la fila solo sería ruido al final de
    /// una lista recortada.
    /// </remarks>
    private static List<IReadOnlyList<string>> PorMunicipio(
        IReadOnlyList<FestivalPublicadoVigenteLectura> festivales,
        NombresTerritoriales nombres,
        bool declararSinMunicipio)
    {
        var filas = festivales
            .Where(item => CodigoPresente(item.CodigoMunicipio))
            .GroupBy(item => item.CodigoMunicipio!)
            .Select(grupo => (Nombre: nombres.Municipios.GetValueOrDefault(grupo.Key, grupo.Key), Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Take(MaximoFilas)
            .Select(item => (IReadOnlyList<string>)Fila(item.Nombre, item.Total))
            .ToList();

        if (!declararSinMunicipio) { return filas; }

        var sinMunicipio = festivales.Count(item => !CodigoPresente(item.CodigoMunicipio));
        if (sinMunicipio > 0) { filas.Add(Fila("Sin municipio registrado", sinMunicipio)); }
        return filas;
    }

    /// <summary>
    /// Si el estado nombrado es «publicado», que es el único que estas dos consultas ya informan.
    /// </summary>
    /// <remarks>
    /// <b>EL RESUMEN Y EL DE MERCADOS NO SE ACOTAN A «PUBLICADO», Y ES DELIBERADO.</b> Sus tablas ya
    /// separan lo registrado de lo publicado —esa decisión está fijada desde el 15 de septiembre de
    /// 2026 con su prueba—, así que la pregunta «¿cuántos hay publicados?» ya está contestada, y con
    /// su contexto: cuántos hay en total y cuántos faltan. Acotar ahí habría cambiado una respuesta
    /// de tres cifras por una de una, que responde lo mismo y dice menos. Para cualquier otro estado
    /// sí se acota, porque ninguna tabla lo enseña.
    /// </remarks>
    /// <remarks>
    /// <b>NEGADO NO CUENTA, y esa es la diferencia que hace útil la negación.</b> «¿Cuántos hay
    /// publicados?» ya está contestada por la tabla de tres cifras; «¿cuántos hay sin publicar?» no
    /// lo está por ninguna, y es la que dice cuánto trabajo queda por delante.
    /// </remarks>
    private static bool EsPublicado(EstadoNombrado estado) =>
        !estado.Negado && string.Equals(estado.Codigo, EstadosFestival.Publicado, StringComparison.OrdinalIgnoreCase);

    private static bool CodigoPresente(string? codigo) => !string.IsNullOrWhiteSpace(codigo);
    private static bool EsNacional(string? alcance) => CatalogoDeConsultas.Normalizar(alcance ?? string.Empty).Contains("nacional", StringComparison.Ordinal);
    private static bool EsMunicipal(string? alcance) => CatalogoDeConsultas.Normalizar(alcance ?? string.Empty).Contains("municipal", StringComparison.Ordinal);

    private static string Porcentaje(int parte, int total) =>
        total == 0 ? "0 %" : $"{Math.Round(parte * 100m / total, 1).ToString("0.0", CultureInfo.GetCultureInfo("es-CO"))} %";

    private static string TextoEstado(string? estado) => string.IsNullOrWhiteSpace(estado) ? "Sin estado" : estado.Replace('_', ' ');

    private static List<string> Fila(params object?[] valores) =>
        valores.Select(valor => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty).ToList();

    private static TablaDeConsultaDto Tabla(
        string titulo,
        string fuente,
        string alcance,
        IReadOnlyList<string> columnas,
        params IReadOnlyList<string>[] filas) => new(titulo, fuente, alcance, columnas, filas);

    private static TablaDeConsultaDto Tabla(
        string titulo,
        string fuente,
        string alcance,
        IReadOnlyList<string> columnas,
        IReadOnlyList<IReadOnlyList<string>> filas) => new(titulo, fuente, alcance, columnas, filas);

    private static IEnumerable<IReadOnlyList<string>> Distribuir(string dimension, IEnumerable<string?> valores) =>
        valores
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(valor => valor!.Trim())
            .GroupBy(valor => valor, StringComparer.CurrentCultureIgnoreCase)
            .Select(grupo => (Nombre: grupo.Key, Total: grupo.Count()))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Nombre, StringComparer.CurrentCulture)
            .Take(MaximoFilas)
            .Select(item => Fila(dimension, item.Nombre, item.Total));

    /// <summary>
    /// Lo que una consulta devuelve: la frase, la tabla y sobre qué se calculó.
    /// </summary>
    /// <param name="Acotado">
    /// El recorte que la consulta aplicó de verdad, o <c>null</c> si respondió de toda la operación.
    /// <b>LO DICE QUIEN FILTRA, y por eso es un campo del resultado y no un cálculo aparte.</b> Al
    /// verlo en el navegador, la respuesta a «¿cuántos festivales hay en
    /// el Huila?» llegaba titulada «Resumen de Huila» y justo encima decía «Calculado sobre: Toda la
    /// operación». Reconstruir el recorte fuera de la consulta habría vuelto a separarse en cuanto
    /// una de ellas decidiera algo distinto —el resumen, por ejemplo, no se acota a «publicado»
    /// aunque la pregunta lo nombre—.
    /// </param>
    private sealed record Resultado(string Resumen, TablaDeConsultaDto Tabla, string? Acotado = null);

    /// <summary>El recorte dicho en palabras: «Huila», «Huila · estado En revisión».</summary>
    private static string? Acotacion(TerritorioNombrado? territorio, EstadoNombrado? estado = null, int? anio = null)
    {
        var partes = new List<string>();
        if (territorio is not null) { partes.Add(territorio.EnPalabras); }
        if (estado is not null) { partes.Add(estado.EnPalabras); }
        if (anio is int cual) { partes.Add($"año {cual.ToString(CultureInfo.InvariantCulture)}"); }
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }
    private sealed record NombresTerritoriales(
        IReadOnlyDictionary<string, string> Departamentos,
        IReadOnlyDictionary<string, string> Municipios);
    private sealed record PendientesAdministrativos(int Festivales, int Propuestas, int Solicitudes, int Reclamaciones, int Duplicados, int Calidad)
    {
        public int Total => Festivales + Propuestas + Solicitudes + Reclamaciones + Duplicados + Calidad;
    }
}
