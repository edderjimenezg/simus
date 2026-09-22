using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Dar de alta organizaciones y Festivales desde el Panel de Gestión Administrativa.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUÉ FALTABA.</b> El ecosistema solo se podía poblar desde fuera: una organización se
/// registraba a sí misma y después registraba sus Festivales. Eso deja fuera todo lo que el
/// Programa conoce y nadie ha reclamado —un festival histórico, una organización que todavía no
/// tiene cuenta—, y obligaba a esperar a que alguien apareciera para poder escribirlo.
/// </para>
/// <para>
/// <b>NO ES UN MODELO PARALELO.</b> Las dos altas usan exactamente las mismas piezas que el canal
/// externo: <see cref="AltaDeOrganizacion"/> y <see cref="AltaDeFestival"/>. Lo único que cambia
/// es el contexto —quién lo incorpora— y los permisos de quien lo hace.
/// </para>
/// <para>
/// <b>TRES COSAS DISTINTAS, Y AQUÍ SE VEN LAS TRES.</b> La procedencia es el Programa; el usuario
/// es la cuenta que tiene la sesión; la organización responsable es la que gestiona el proceso, y
/// puede ser otra o no existir todavía. Confundirlas acabaría atribuyéndole al Programa festivales
/// que no organiza.
/// </para>
/// </remarks>
public static class AltaAdministrativaEndpoints
{
    private static readonly string[] ResponsableObligatorio = ["El nombre de quien responde por la organización es obligatorio."];
    private static readonly string[] OrganizacionDesconocida = ["La organización responsable indicada no existe."];
    private static readonly string[] MotivoDeLiberacion =
        ["Escribe por qué se devuelve el proceso al Programa. Es la decisión más fuerte de esta pantalla."];
    private static readonly string[] YaEstaEnCustodia =
        ["Este Festival ya está en custodia del Programa: no hay administración que liberar."];
    private static readonly string[] SinInstitucional =
        ["Falta la entidad institucional del Programa en la base. No hay a quién devolverle la custodia."];
    private static readonly string[] EstadoNoAdmitido =
        ["Desde la consola un Festival se publica, se archiva o vuelve a borrador. Los demás estados los decide el circuito de revisión."];
    private static readonly string[] EstadoDeMercadoNoAdmitido =
        ["Desde la consola un mercado se publica, se archiva o vuelve a borrador. Los demás estados los decide el circuito de revisión."];

    public static RouteGroupBuilder MapAltaAdministrativaEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin")
            .WithTags("alta-administrativa")
            .RequireAuthorization(Permisos.PoliticaFuncionario);

        admin.MapPost("/organizaciones", CrearOrganizacionAsync).WithName("CrearOrganizacionAdministrativa");
        admin.MapGet("/organizaciones/coincidencias", CoincidenciasDeOrganizacionAsync).WithName("CoincidenciasDeOrganizacion");

        admin.MapPost("/festivales", CrearFestivalAsync).WithName("CrearFestivalAdministrativo");
        admin.MapGet("/festivales/{id:int}", FichaDeFestivalAsync).WithName("FichaDeFestivalAdministrativa");
        admin.MapPost("/festivales/{id:int}/publicacion", PublicarFestivalAsync).WithName("PublicarFestivalAdministrativo");
        admin.MapPost("/festivales/{id:int}/liberar-administracion", LiberarAdministracionAsync).WithName("LiberarAdministracionDeFestival");
        admin.MapGet("/festivales/coincidencias", CoincidenciasDeFestivalAsync).WithName("CoincidenciasDeFestivalAdministrativo");

        // LAS MISMAS DOS PUERTAS QUE UN FESTIVAL. Un mercado se abría solo en un cajón de edición
        // dentro de su panel: no había forma de verlo entero —qué es, quién responde, de dónde vino
        // y qué realizaciones tiene— ni de publicarlo, despublicarlo o archivarlo desde su ficha.
        admin.MapGet("/mercados/{id:int}", FichaDeMercadoAsync).WithName("FichaDeMercadoAdministrativa");
        admin.MapPost("/mercados/{id:int}/publicacion", PublicarMercadoAsync).WithName("PublicarMercadoAdministrativo");
        admin.MapGet("/mercados/coincidencias", CoincidenciasDeMercadoAsync).WithName("CoincidenciasDeMercadoAdministrativo");

        return group;
    }

    // ---------- Organizaciones ------------------------------------------------------------

    /// <summary>
    /// Registra una organización que todavía no tiene cuenta.
    /// </summary>
    private static async Task<IResult> CrearOrganizacionAsync(
        CrearOrganizacionAdministrativaSolicitud solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var actor = await ActorAsync(db, principal, ct);
        if (actor is null) return Results.Unauthorized();

        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        await AltaDeOrganizacion.ValidarAsync(
            errores,
            prefijo: string.Empty,
            claveCorreo: "correoContacto",
            nombre: solicitud.Nombre,
            identificacion: solicitud.Identificacion,
            correoContacto: solicitud.CorreoContacto,
            dbContext: db,
            cancellationToken: ct);
        await AltaDeOrganizacion.ValidarSedeAsync(
            errores, solicitud.CodigoDepartamentoSede, solicitud.CodigoMunicipioSede, db, ct);

        if (ValidationHelpers.IsMissing(solicitud.ResponsableNombre))
        {
            errores["responsableNombre"] = ResponsableObligatorio;
        }

        if (errores.Count > 0) return Results.ValidationProblem(errores);

        // TODO EN UNA TRANSACCION, y con la estrategia de reintento porque la base la usa. La
        // entidad, su responsable y su procedencia son una sola cosa.
        var estrategia = db.Database.CreateExecutionStrategy();
        var entidad = await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var creada = await AltaDeOrganizacion.CrearAsync(
                db,
                responsable: actor,
                nombre: solicitud.Nombre,
                identificacion: solicitud.Identificacion,
                correoContacto: solicitud.CorreoContacto,
                codigoDepartamentoSede: solicitud.CodigoDepartamentoSede,
                codigoMunicipioSede: solicitud.CodigoMunicipioSede,
                nombreResponsable: solicitud.ResponsableNombre,
                tipoDocumentoResponsable: solicitud.ResponsableTipoDocumento ?? string.Empty,
                numeroDocumento: solicitud.ResponsableNumeroDocumento ?? string.Empty,
                primerNombreResponsable: null,
                segundoNombreResponsable: null,
                primerApellidoResponsable: null,
                segundoApellidoResponsable: null,
                telefono: solicitud.ResponsableTelefono,
                autorizacionDatos: solicitud.ResponsableAutorizacionDatos,
                ahora: DateTime.UtcNow,
                cancellationToken: ct,
                contexto: ProcedenciaDeRegistro.Administrativo,
                // NADIE LA ADMINISTRA TODAVIA: la organización no tiene cuenta, y poner al
                // funcionario como administrador diría que el Programa la gestiona.
                vincularResponsableComoAdministrador: false,
                correoResponsable: solicitud.ResponsableCorreo);

            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = actor.Id,
                TableName = "Entidades",
                RecordId = creada.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.CrearOrganizacion,
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    evento = "OrganizacionCreadaDesdeConsola",
                    nombre = creada.Name,
                }),
                CreatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return creada;
        });

        return Results.Created(
            $"/api/v1/admin/organizaciones/{entidad.Id}",
            new { id = entidad.Id.ToString(CultureInfo.InvariantCulture), nombre = entidad.Name });
    }

    /// <summary>
    /// Organizaciones que se parecen a la que se va a registrar.
    /// </summary>
    /// <remarks>
    /// SE OFRECE ANTES DE GUARDAR Y NO DECIDE NADA. Un duplicado no siempre es un error —dos
    /// fundaciones pueden llamarse parecido—, así que la coincidencia se enseña y quien registra
    /// decide. Bloquear por nombre produciría organizaciones imposibles de registrar.
    /// </remarks>
    private static async Task<IResult> CoincidenciasDeOrganizacionAsync(
        string? nombre, string? identificacion, PnmcDbContext db, CancellationToken ct)
    {
        var texto = (nombre ?? string.Empty).Trim();
        var documento = AltaDeOrganizacion.NormalizarIdentificacion(identificacion);
        if (texto.Length < 3 && documento is null) return Results.Ok(Array.Empty<CoincidenciaDeAltaDto>());

        var consulta = db.EntityProfiles.AsNoTracking().Where(x => x.EntityType == "organizacion");
        consulta = documento is null
            ? consulta.Where(x => x.Name.Contains(texto))
            : consulta.Where(x => x.Name.Contains(texto) || x.IdentificationNumber == documento);

        var filas = await consulta.OrderBy(x => x.Name).Take(10).ToListAsync(ct);

        return Results.Ok(filas.Select(x => new CoincidenciaDeAltaDto(
            x.Id.ToString(CultureInfo.InvariantCulture),
            x.Name,
            x.HeadquartersMunicipalityCode ?? x.HeadquartersDepartmentCode,
            x.StatusCode,
            null)).ToList());
    }

    // ---------- Festivales ----------------------------------------------------------------

    private static async Task<IResult> CrearFestivalAsync(
        CrearFestivalAdministrativoSolicitud solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var actor = await ActorAsync(db, principal, ct);
        if (actor is null) return Results.Unauthorized();

        var errores = await AltaDeFestival.ValidarAsync(solicitud.Festival, db, ct);

        if (solicitud.OrganizacionResponsableId is { } organizacionId)
        {
            var existe = await db.EntityProfiles.AsNoTracking()
                .AnyAsync(x => x.Id == organizacionId && x.EntityType == "organizacion", ct);
            if (!existe) errores["organizacionResponsableId"] = OrganizacionDesconocida;
        }

        if (errores.Count > 0) return Results.ValidationProblem(errores);

        // LA ENTIDAD INSTITUCIONAL PUEDE NO EXISTIR TODAVIA, y eso NO impide registrar.
        //
        // Lo dice el propio diseño de `dbo.ProcedenciasDeRegistro`: la columna es anulable porque
        // en una base a medio sembrar la entidad puede no estar, e inventar un identificador ahí
        // apuntaría a cualquier otra organización. El CONTEXTO —`administrativo`— ya responde la
        // pregunta importante: lo incorporó el Programa. Devolver un 503 dejaba la consola
        // inservible sobre una base recién levantada por un dato que la procedencia no necesita.
        var institucional = await ProcedenciaDeRegistro.IdInstitucionalAsync(db, ct);

        var estrategia = db.Database.CreateExecutionStrategy();
        var festival = await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var ahora = DateTime.UtcNow;

            // LA MISMA ALTA QUE EL CANAL EXTERNO. Lo único distinto son las tres dimensiones: la
            // procedencia es el Programa, el usuario es quien tiene la sesión y la organización
            // responsable es la que gestiona el Festival —que puede ser otra, o ninguna todavía—.
            var creado = await AltaDeFestival.CrearAsync(
                db,
                solicitud.Festival,
                organizacionResponsableId: solicitud.OrganizacionResponsableId,
                usuarioId: actor.Id,
                contexto: ProcedenciaDeRegistro.Administrativo,
                organizacionProcedenciaId: institucional,
                ahora: ahora,
                cancellationToken: ct);

            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = actor.Id,
                TableName = "Festivales",
                RecordId = creado.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Crear,
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    evento = "FestivalCreadoDesdeConsola",
                    nombre = creado.Name,
                    organizacionResponsableId = solicitud.OrganizacionResponsableId,
                }),
                CreatedAt = ahora,
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return creado;
        });

        return Results.Created(
            $"/api/v1/admin/festivales/{festival.Id}",
            new { id = festival.Id.ToString(CultureInfo.InvariantCulture), nombre = festival.Name });
    }

    /// <summary>
    /// La ficha de un Festival con sus ediciones.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL FESTIVAL ES LA ENTIDAD PRINCIPAL Y LAS EDICIONES CUELGAN DE ÉL.</b> No son un módulo
    /// equivalente al Festival: son su historial de realizaciones. Por eso viajan dentro de esta
    /// ficha y no por una ruta propia paralela que sugiriera que se administran aparte.
    /// </para>
    /// <para>
    /// <b>NO DUPLICA EL CIRCUITO DE REVISIÓN.</b> Publicar o supervisar una Edición sigue estando
    /// en <c>/institucional/ediciones-festival</c>, que ya existe. Esto es la vista que faltaba:
    /// entrar a un Festival desde la consola y ver de una lectura qué es, quién responde por él,
    /// de dónde vino y qué ediciones tiene.
    /// </para>
    /// </remarks>
    private static async Task<IResult> FichaDeFestivalAsync(int id, PnmcDbContext db, CancellationToken ct)
    {
        var festival = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (festival is null) return Results.NotFound();

        var territorios = await db.DivipolaLocations.AsNoTracking()
            .Where(x => x.DepartmentCode == festival.DepartmentCode)
            .Select(x => new { x.DepartmentCode, x.DepartmentName, x.MunicipalityCode, x.MunicipalityName })
            .ToListAsync(ct);

        var organizacion = festival.OrganizacionPrincipalId is { } organizacionId
            ? await db.EntityProfiles.AsNoTracking().Where(x => x.Id == organizacionId).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;

        var practicasIds = await db.ValoresAsync<PracticaMusicalDeRegistroRow>(Modulos.Festivales, id, ct);
        var territoriosIds = await db.ValoresAsync<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, id, ct);

        var ediciones = await db.EdicionesFestival.AsNoTracking()
            .Where(x => x.FestivalId == id)
            .OrderByDescending(x => x.Anio ?? 0).ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        // LA PROCEDENCIA DE LAS EDICIONES, TODAS EN UNA CONSULTA. Cada Edición pudo entrar por un
        // canal distinto del Festival que la contiene: una la registró la organización y la
        // siguiente la incorporó el Programa.
        var procedenciasDeEdiciones = await ProcedenciaDeRegistro.LeerVariasAsync(
            db,
            Modulos.EdicionesDeFestival,
            ediciones.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToList(),
            ct);

        return Results.Ok(new FichaFestivalAdministrativaDto(
            festival.Id.ToString(CultureInfo.InvariantCulture),
            festival.Name,
            festival.Description,
            festival.StatusCode,
            festival.CoverageLevel,
            territorios.FirstOrDefault()?.DepartmentName,
            territorios.FirstOrDefault(x => x.MunicipalityCode == festival.MunicipalityCode)?.MunicipalityName,
            festival.Periodicidad,
            festival.ContactEmail,
            festival.ContactPhone,
            festival.WebsiteUrl,
            festival.OrganizacionPrincipalId,
            organizacion,
            await ProcedenciaDeRegistro.LeerAsync(db, Modulos.Festivales, id.ToString(CultureInfo.InvariantCulture), ct),
            await ClasificacionDeContenido.PracticasAsync(db, practicasIds, ct),
            await ClasificacionDeContenido.TerritoriosAsync(db, territoriosIds, ct),
            ediciones.Select(x => new EdicionDelFestivalDto(
                x.Id.ToString(CultureInfo.InvariantCulture),
                x.Anio,
                x.NumeroEdicion,
                x.Nombre,
                x.FechaInicio,
                x.FechaFin,
                x.EstadoVisibilidad,
                procedenciasDeEdiciones.TryGetValue(x.Id.ToString(CultureInfo.InvariantCulture), out var suya) ? suya : null)).ToList(),
            festival.CreatedAt,
            festival.UpdatedAt));
    }

    /// <summary>
    /// Publica o retira un Festival desde la consola, sin pasar por la bandeja de revisión.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SOLO EL WEBMASTER.</b> Lo decidió la dirección. El circuito de
    /// revisión sigue existiendo y es el camino del canal externo: una organización envía su
    /// Festival y un funcionario lo aprueba. Esto es otra cosa: un Festival que el propio Programa
    /// incorporó no tiene a quién esperar, y obligarlo a recorrer una bandeja donde el revisor y el
    /// registrador son la misma persona convierte el circuito en un trámite vacío.
    /// </para>
    /// <para>
    /// <b>NO ES UNA PUERTA TRASERA AL CIRCUITO.</b> Un `gestor_interno` no alcanza aquí, y la
    /// actuación queda en la bitácora con su verbo propio, de modo que publicar por esta vía se
    /// distingue después de publicar por la bandeja.
    /// </para>
    /// </remarks>
    // ---------- Mercados musicales --------------------------------------------------------

    /// <summary>
    /// La ficha de un mercado en la consola.
    /// </summary>
    /// <remarks>
    /// <b>ES LA MISMA LECTURA QUE LA DE UN FESTIVAL</b>, con los datos que un mercado sí tiene:
    /// alcance, modalidad y si se realiza en el marco de un festival. Lo demás es el mismo armazón
    /// —estado, territorio, quién responde, de dónde vino, cómo está clasificado y sus
    /// realizaciones—, que es lo que hace que las dos fichas se recorran igual.
    /// </remarks>
    private static async Task<IResult> FichaDeMercadoAsync(int id, PnmcDbContext db, CancellationToken ct)
    {
        var mercado = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Activo, ct);
        if (mercado is null) return Results.NotFound();

        var territorios = await db.DivipolaLocations.AsNoTracking()
            .Where(x => x.DepartmentCode == mercado.CodigoDepartamento)
            .Select(x => new { x.DepartmentCode, x.DepartmentName, x.MunicipalityCode, x.MunicipalityName })
            .ToListAsync(ct);

        var organizacion = await db.EntityProfiles.AsNoTracking()
            .Where(x => x.Id == mercado.OrganizacionPrincipalId).Select(x => x.Name).FirstOrDefaultAsync(ct);

        var festival = mercado.FestivalId is { } festivalId
            ? await db.FestivalRecords.AsNoTracking().Where(x => x.Id == festivalId).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;

        var alcance = mercado.AlcanceMercadoId is { } alcanceId
            ? await db.AlcancesMercado.AsNoTracking().Where(x => x.Id == alcanceId).Select(x => x.Nombre).FirstOrDefaultAsync(ct)
            : null;
        var modalidad = mercado.ModalidadMercadoId is { } modalidadId
            ? await db.ModalidadesMercado.AsNoTracking().Where(x => x.Id == modalidadId).Select(x => x.Nombre).FirstOrDefaultAsync(ct)
            : null;

        var practicasIds = await db.MercadosPracticasMusicales.AsNoTracking()
            .Where(x => x.MercadoId == id).Select(x => x.PracticaMusicalId).ToListAsync(ct);
        var territoriosIds = await db.MercadosTerritoriosSonoros.AsNoTracking()
            .Where(x => x.MercadoId == id).Select(x => x.TerritorioSonoroId).ToListAsync(ct);

        var ediciones = await db.EdicionesMercado.AsNoTracking()
            .Where(x => x.MercadoId == id)
            .OrderByDescending(x => x.Anio).ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        // LA PROCEDENCIA DE LAS EDICIONES, TODAS EN UNA CONSULTA. Cada edición pudo entrar por un
        // canal distinto del mercado que la contiene.
        var procedenciasDeEdiciones = await ProcedenciaDeRegistro.LeerVariasAsync(
            db,
            Modulos.EdicionesDeMercado,
            ediciones.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToList(),
            ct);

        return Results.Ok(new FichaMercadoAdministrativaDto(
            mercado.Id.ToString(CultureInfo.InvariantCulture),
            mercado.Nombre,
            mercado.Descripcion,
            mercado.EstadoRegistro,
            mercado.NivelCobertura,
            territorios.Find(x => true)?.DepartmentName,
            territorios.Find(x => x.MunicipalityCode == mercado.CodigoMunicipio)?.MunicipalityName,
            mercado.LugarEspecifico,
            mercado.Periodicidad,
            alcance,
            modalidad,
            mercado.CorreoMercado,
            mercado.TelefonoMercado,
            mercado.SitioWebMercado,
            mercado.OrganizacionPrincipalId,
            organizacion,
            mercado.SeRealizaEnElMarcoDeUnFestival,
            mercado.FestivalId,
            festival,
            await ProcedenciaDeRegistro.LeerAsync(db, Modulos.Mercados, id.ToString(CultureInfo.InvariantCulture), ct),
            await ClasificacionDeContenido.PracticasAsync(db, practicasIds, ct),
            await ClasificacionDeContenido.TerritoriosAsync(db, territoriosIds, ct),
            ediciones.Select(x => new EdicionDelMercadoDto(
                x.Id.ToString(CultureInfo.InvariantCulture),
                x.Anio,
                x.NumeroEdicion,
                x.Nombre,
                x.FechaInicio is { } inicio ? DateOnly.FromDateTime(inicio) : null,
                x.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
                x.Estado,
                x.EstadoVisibilidad,
                procedenciasDeEdiciones.TryGetValue(x.Id.ToString(CultureInfo.InvariantCulture), out var suya) ? suya : null)).ToList(),
            mercado.FechaCreacion,
            mercado.FechaActualizacion));
    }

    /// <summary>
    /// Publica, archiva o devuelve a borrador un mercado desde la consola.
    /// </summary>
    /// <remarks>
    /// <b>LOS MISMOS TRES ESTADOS QUE UN FESTIVAL, Y POR EL MISMO MOTIVO:</b> los demás —en
    /// revisión, ajustes solicitados— pertenecen al circuito de revisión del canal externo, y
    /// escribirlos desde aquí dejaría la bandeja diciendo cosas que nadie decidió en ella.
    ///
    /// <b>SOLO EL WEBMASTER</b>, igual que en Festivales: publicar es la decisión que saca un
    /// registro al portal.
    /// </remarks>
    /// <summary>
    /// Mercados que ya existen con un nombre parecido al que se está escribiendo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SE PREGUNTA ANTES DE CREAR, NO DESPUES DE DUPLICAR.</b> Dos registros del mismo mercado
    /// —uno que incorporó el Programa y otro que registró su organización— no se detectan solos: se
    /// quedan los dos en el portal, cada uno con parte de las ediciones, y separarlos después
    /// obliga a decidir cuál es el bueno y a mover lo que cuelga del otro.
    /// </para>
    /// <para>
    /// <b>NO BLOQUEA: AVISA.</b> Hay mercados que de verdad se llaman parecido en departamentos
    /// distintos, y quien registra es quien sabe si es el mismo. Por eso devuelve con qué comparar
    /// —el territorio, el estado y quién responde por él— en vez de un sí o un no.
    /// </para>
    /// <para>
    /// <b>DESDE TRES LETRAS</b>, como en Festivales: con una o dos, la coincidencia sería con medio
    /// catálogo y el aviso dejaría de significar nada.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CoincidenciasDeMercadoAsync(
        string? nombre, PnmcDbContext db, CancellationToken ct)
    {
        var texto = (nombre ?? string.Empty).Trim();
        if (texto.Length < 3) return Results.Ok(Array.Empty<CoincidenciaDeAltaDto>());

        var filas = await db.Mercados.AsNoTracking()
            .Where(x => x.Activo && x.Nombre.Contains(texto))
            .OrderBy(x => x.Nombre)
            .Take(10)
            .ToListAsync(ct);

        var organizaciones = await db.EntityProfiles.AsNoTracking()
            .Where(e => filas.Select(m => m.OrganizacionPrincipalId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        return Results.Ok(filas.Select(x => new CoincidenciaDeAltaDto(
            x.Id.ToString(CultureInfo.InvariantCulture),
            x.Nombre,
            x.CodigoMunicipio ?? x.CodigoDepartamento,
            x.EstadoRegistro,
            organizaciones.TryGetValue(x.OrganizacionPrincipalId, out var nombreOrganizacion)
                ? nombreOrganizacion : null)).ToList());
    }

    private static async Task<IResult> PublicarMercadoAsync(
        int id, CambioDePublicacionDeFestival solicitud, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!Permisos.EsWebmaster(principal)) return Results.Forbid();

        var actor = await ActorAsync(db, principal, ct);
        if (actor is null) return Results.Unauthorized();

        var estado = (solicitud.Estado ?? string.Empty).Trim().ToLowerInvariant();
        if (estado is not ("publicado" or "archivado" or "borrador"))
        {
            return Results.BadRequest(new { estado = EstadoDeMercadoNoAdmitido });
        }

        var mercado = await db.Mercados.FirstOrDefaultAsync(x => x.Id == id && x.Activo, ct);
        if (mercado is null) return Results.NotFound();

        var anterior = mercado.EstadoRegistro;
        var ahora = DateTime.UtcNow;
        mercado.EstadoRegistro = estado;
        mercado.FechaActualizacion = ahora;
        // LA FECHA DE PUBLICACION SE PONE UNA VEZ Y NO SE BORRA AL DESPUBLICAR: dice cuándo salió
        // al portal por primera vez, que es un hecho, no el estado de hoy.
        if (estado == "publicado" && mercado.FechaPublicacion is null) mercado.FechaPublicacion = ahora;

        // PUBLICAR O ARCHIVAR CIERRA LA CONVERSACION, Y POR TANTO LA REVISION VIVA. Sin esto, un
        // mercado que el Programa publica estando en «ajustes solicitados» conserva su expediente
        // abierto, y su organización sigue viendo en la ficha «el Programa pidió cambios» sobre algo
        // que ya está publicado: la pantalla se contradice a sí misma. Encontrado el 17 de
        // septiembre de 2026 recorriendo el flujo de Mercados.
        if (estado is "publicado" or "archivado")
        {
            await RevisionDeCamposDeMercadoEndpoints.CerrarRevisionVivaAsync(db, id, actor.Id, ahora, ct);
        }

        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor.Id,
            TableName = "Mercados",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            // EL VERBO SALE DE LA LISTA BLANCA DE LA BITACORA. «borrador» no está entre los trece,
            // así que despublicar se registra como una actualización con el detalle en el cuerpo.
            Action = estado switch
            {
                "publicado" => AccionesAuditoria.Publicar,
                "archivado" => AccionesAuditoria.Archivar,
                _ => AccionesAuditoria.Actualizar,
            },
            PreviousValuesJson = System.Text.Json.JsonSerializer.Serialize(new { estado = anterior }),
            // LA MISMA FORMA QUE EL RESTO DEL CIRCUITO DEL MERCADO —`evento` y `detalle`—, que es de
            // donde lee el historial que ve la organización en su ficha.
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                evento = "decidir",
                detalle = new { estado },
            }),
            CreatedAt = ahora,
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { id = id.ToString(CultureInfo.InvariantCulture), estado });
    }

    private static async Task<IResult> PublicarFestivalAsync(
        int id, CambioDePublicacionDeFestival solicitud, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        if (!Permisos.EsWebmaster(principal)) return Results.Forbid();

        var actor = await ActorAsync(db, principal, ct);
        if (actor is null) return Results.Unauthorized();

        var estado = (solicitud.Estado ?? string.Empty).Trim().ToLowerInvariant();
        if (estado is not (EstadosFestival.Publicado or EstadosFestival.Archivado or EstadosFestival.Borrador))
        {
            return Results.BadRequest(new { estado = EstadoNoAdmitido });
        }

        var festival = await db.FestivalRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (festival is null) return Results.NotFound();

        var anterior = festival.StatusCode;
        festival.StatusCode = estado;
        festival.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor.Id,
            TableName = "Festivales",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            // EL VERBO SALE DE LA LISTA BLANCA DE LA BITACORA. «borrador» no está entre los trece,
            // así que despublicar se registra como una actualización con el detalle en el cuerpo.
            Action = estado switch
            {
                EstadosFestival.Publicado => AccionesAuditoria.Publicar,
                EstadosFestival.Archivado => AccionesAuditoria.Archivar,
                _ => AccionesAuditoria.Actualizar,
            },
            PreviousValuesJson = System.Text.Json.JsonSerializer.Serialize(new { estado = anterior }),
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                evento = "FestivalPublicadoDesdeConsola",
                estado,
            }),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { id = id.ToString(CultureInfo.InvariantCulture), estado });
    }

    /// <summary>
    /// Devuelve un Festival a custodia institucional para que otra organización pueda reclamarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>QUÉ DESBLOQUEA.</b> Cuando nadie de una organización puede entrar —se fue quien tenía la
    /// cuenta, se disolvió la entidad—, su Festival se queda congelado: nadie lo edita, nadie
    /// registra su edición, y ninguna otra organización puede tomarlo, porque el circuito de
    /// reclamación solo ofrece los que <b>ya están</b> en custodia institucional.
    /// </para>
    /// <para>
    /// <b>ES LA ALTERNATIVA A TOCAR CREDENCIALES.</b> No se recupera el acceso de nadie: se
    /// desbloquea el proceso. A partir de aquí, el circuito que ya existe hace el resto —otra
    /// organización lo pide, con sus aclaraciones y su conciliación de datos—.
    /// </para>
    /// <para>
    /// <b>NO CAMBIA LA PROCEDENCIA.</b> Quién incorporó el Festival al sistema es un hecho del
    /// pasado y no se reescribe; lo que cambia es quién responde por él ahora. Son las dos
    /// dimensiones que el corte anterior separó.
    /// </para>
    /// <para>
    /// <b>EL MOTIVO NO ES OPCIONAL.</b> Quitarle a una organización la administración de su propio
    /// proceso es la decisión más fuerte de esta pantalla, y dentro de un año nadie recordará por
    /// qué se tomó.
    /// </para>
    /// </remarks>
    private static async Task<IResult> LiberarAdministracionAsync(
        int id, LiberacionDeAdministracion solicitud, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        if (!Permisos.EsWebmaster(principal)) return Results.Forbid();

        var actor = await ActorAsync(db, principal, ct);
        if (actor is null) return Results.Unauthorized();

        var motivo = (solicitud.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) return Results.BadRequest(new { motivo = MotivoDeLiberacion });

        var festival = await db.FestivalRecords.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (festival is null) return Results.NotFound();

        var institucional = await ProcedenciaDeRegistro.IdInstitucionalAsync(db, ct);
        if (institucional is null) return Results.Problem(SinInstitucional[0], statusCode: StatusCodes.Status503ServiceUnavailable);

        var anterior = festival.OrganizacionPrincipalId;
        if (anterior is null || anterior == institucional)
        {
            // YA ESTA EN CUSTODIA: liberarlo otra vez no cambiaría nada y escribiría una
            // transferencia de la institución a sí misma.
            return Results.Conflict(new { organizacion = YaEstaEnCustodia });
        }

        var ahora = DateTime.UtcNow;
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // EL ACTO LO ESCRIBE `CustodiaDeProcesos`, que es el mismo que usa el cierre de una
            // organización cuando libera todos sus procesos de golpe. Dos copias de esta
            // transferencia serían dos formas de dejar un proceso libre, y solo una acabaría
            // apareciendo en el historial del registro.
            CustodiaDeProcesos.Liberar(db, festival, institucional.Value, actor.Id, motivo, ahora);

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(new
        {
            id = id.ToString(CultureInfo.InvariantCulture),
            organizacionResponsableId = institucional,
            // SOLO ES RECLAMABLE SI ESTA PUBLICADO: es la regla del circuito y conviene decirla
            // aquí, para que la pantalla no prometa que ya lo puede pedir alguien.
            reclamable = string.Equals(festival.StatusCode, EstadosFestival.Publicado, StringComparison.OrdinalIgnoreCase),
        });
    }

    /// <summary>Festivales ya registrados que se parecen al que se va a dar de alta.</summary>
    private static async Task<IResult> CoincidenciasDeFestivalAsync(
        string? nombre, PnmcDbContext db, CancellationToken ct)
    {
        var texto = (nombre ?? string.Empty).Trim();
        if (texto.Length < 3) return Results.Ok(Array.Empty<CoincidenciaDeAltaDto>());

        var filas = await db.FestivalRecords.AsNoTracking()
            .Where(x => x.Name.Contains(texto))
            .OrderBy(x => x.Name)
            .Take(10)
            .ToListAsync(ct);

        var organizaciones = await db.EntityProfiles.AsNoTracking()
            .Where(e => filas.Select(f => f.OrganizacionPrincipalId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        return Results.Ok(filas.Select(x => new CoincidenciaDeAltaDto(
            x.Id.ToString(CultureInfo.InvariantCulture),
            x.Name,
            x.MunicipalityCode ?? x.DepartmentCode,
            x.StatusCode,
            x.OrganizacionPrincipalId is { } id && organizaciones.TryGetValue(id, out var nombreOrganizacion)
                ? nombreOrganizacion : null)).ToList());
    }

    // ---------- Común ---------------------------------------------------------------------

    /// <summary>
    /// La cuenta que tiene la sesión, leída de la base.
    /// </summary>
    /// <remarks>
    /// SE LEE LA FILA Y NO SOLO EL IDENTIFICADOR DEL TESTIGO porque el alta de organización
    /// necesita el usuario entero —su correo hace de respaldo del contacto del responsable— y
    /// porque una cuenta desactivada entre medias no debe poder seguir escribiendo.
    /// </remarks>
    private static async Task<UserRow?> ActorAsync(PnmcDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
    }
}
