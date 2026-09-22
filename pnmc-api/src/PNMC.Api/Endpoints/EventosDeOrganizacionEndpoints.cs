using System.Data;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los eventos que una organización anuncia sobre sus propios procesos.
/// </summary>
/// <remarks>
/// <para>
/// <b>LA REGLA ENTERA ES: NINGÚN EVENTO SIN PROCESO.</b> Una organización no publica eventos
/// sueltos en la agenda del Programa; publica eventos <b>de sus procesos</b> —su festival, su
/// mercado—. Si no tiene ninguno aprobado, no tiene qué anunciar aquí. Sin esa regla, la agenda
/// pública se convertiría en un tablón abierto donde cualquiera con cuenta escribe.
/// </para>
/// <para>
/// <b>HOY EL ÚNICO PROCESO ES EL FESTIVAL, Y SE DICE EN VOZ ALTA.</b> Los demás —mercados, redes,
/// escuelas— todavía no tienen su modelo en esta base. La forma del contrato ya prevé el tipo para
/// que el día que existan no haya que rehacer ni el API ni la pantalla.
/// </para>
/// <para>
/// <b>EL PROCESO TIENE QUE ESTAR PUBLICADO.</b> Un Festival en borrador o en revisión todavía no
/// existe para el público: colgarle un evento anunciaría en la agenda algo cuyo marco nadie ha
/// aprobado, y el evento aparecería antes que el Festival del que habla.
/// </para>
/// <para>
/// <b>EL EVENTO NACE EN REVISIÓN, SIEMPRE.</b> Quien lo escribe no decide si se publica. No se
/// acepta un estado del cliente: dejarlo viajar en el cuerpo pondría la decisión institucional en
/// manos del formulario.
/// </para>
/// <para>
/// <b>NO ES UN SEGUNDO MODELO DE AGENDA.</b> Escribe la misma fila de <c>dbo.EventosAgenda</c> que
/// la consola, con las mismas reglas de <c>ReglasDeAgenda</c> y el mismo circuito de estados. Lo
/// único propio es de dónde viene y que exige proceso.
/// </para>
/// </remarks>
public static class EventosDeOrganizacionEndpoints
{
    private const string TipoFestival = "festival";

    private static readonly string[] ProcesoObligatorio =
        ["Elige el proceso en el que se enmarca el evento. Un evento sin proceso no se puede anunciar."];
    private static readonly string[] ProcesoAjeno =
        ["Ese proceso no pertenece a tu organización o todavía no está publicado."];
    private static readonly string[] TituloObligatorio = ["El título del evento es obligatorio."];
    private static readonly string[] DescripcionObligatoria = ["La descripción del evento es obligatoria."];
    private static readonly string[] FinAntesDelInicio = ["El evento no puede terminar antes de empezar."];
    private static readonly string[] HoraFinSinInicio = ["No puede haber hora de fin sin hora de inicio."];
    private static readonly string[] TestigoInvalido = ["La solicitud no pudo validarse. Actualiza la página e inténtalo nuevamente."];
    private static readonly string[] MunicipioSinDepartamento =
        ["Elige también el departamento: un municipio sin su departamento no ubica el evento."];

    public static RouteGroupBuilder MapEventosDeOrganizacionEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo/organizaciones/{organizacionId:int}")
            .WithTags("eventos-de-organizacion");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/procesos-que-enmarcan", ProcesosAsync).WithName("ProcesosQueEnmarcanEventos");
        externo.MapGet("/eventos", ListarAsync).WithName("ListarEventosDeOrganizacion");
        externo.MapPost("/eventos", CrearAsync).WithName("CrearEventoDeOrganizacion");

        return group;
    }

    /// <summary>
    /// Los procesos de la organización que pueden enmarcar un evento.
    /// </summary>
    /// <remarks>
    /// SI ESTA LISTA VIENE VACIA, LA PANTALLA NO OFRECE EL FORMULARIO. Es más honesto que dejar
    /// escribir un evento entero para rechazarlo al guardar por algo que se sabía desde el
    /// principio.
    /// </remarks>
    private static async Task<IResult> ProcesosAsync(
        int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = Persona(principal);
        if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();

        var festivales = await db.FestivalRecords.AsNoTracking()
            .Where(f => f.OrganizacionPrincipalId == organizacionId && f.StatusCode == EstadosFestival.Publicado)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.Name, f.DepartmentCode, f.MunicipalityCode })
            .ToListAsync(ct);

        if (festivales.Count == 0) return Results.Ok(Array.Empty<ProcesoQueEnmarcaDto>());

        // EL TERRITORIO DISTINGUE DOS PROCESOS DE NOMBRE PARECIDO. Una organización con el mismo
        // festival en dos municipios veía dos opciones idénticas y tenía que adivinar.
        var municipios = festivales.Select(f => f.MunicipalityCode).Where(c => c != null).ToList();
        var departamentos = festivales.Select(f => f.DepartmentCode).Where(c => c != null).ToList();
        var territorios = await db.DivipolaLocations.AsNoTracking()
            .Where(d => municipios.Contains(d.MunicipalityCode) || departamentos.Contains(d.DepartmentCode))
            .Select(d => new { d.DepartmentCode, d.DepartmentName, d.MunicipalityCode, d.MunicipalityName })
            .ToListAsync(ct);

        return Results.Ok(festivales.Select(f => new ProcesoQueEnmarcaDto(
            f.Id.ToString(CultureInfo.InvariantCulture),
            TipoFestival,
            "Festival",
            f.Name,
            territorios.FirstOrDefault(t => t.MunicipalityCode == f.MunicipalityCode)?.MunicipalityName
                ?? territorios.FirstOrDefault(t => t.DepartmentCode == f.DepartmentCode)?.DepartmentName)).ToList());
    }

    /// <summary>Los eventos que esta organización ha anunciado, con el proceso que los enmarca.</summary>
    private static async Task<IResult> ListarAsync(
        int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = Persona(principal);
        if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();

        // SUS EVENTOS SON LOS DE SUS PROCESOS. No hay columna de «organización» en la agenda, y no
        // hace falta: el proceso que los enmarca ya dice de quién son, y esa es justo la relación
        // que este circuito obliga a declarar.
        var procesos = await db.FestivalRecords.AsNoTracking()
            .Where(f => f.OrganizacionPrincipalId == organizacionId)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(ct);
        if (procesos.Count == 0) return Results.Ok(Array.Empty<EventoDeOrganizacionDto>());

        var ids = procesos.Select(p => p.Id).ToList();
        var eventos = await db.EventosAgenda.AsNoTracking()
            .Where(e => e.FestivalId != null && ids.Contains(e.FestivalId.Value))
            .OrderByDescending(e => e.FechaInicio)
            .ToListAsync(ct);

        return Results.Ok(eventos.Select(e => new EventoDeOrganizacionDto(
            e.Id.ToString(CultureInfo.InvariantCulture),
            e.Titulo,
            e.FechaInicio,
            e.FechaFin,
            e.Modalidad,
            e.Lugar,
            e.Estado,
            EtiquetaDeEstado(e.Estado),
            procesos.FirstOrDefault(p => p.Id == e.FestivalId)?.Name,
            e.FechaActualizacion)).ToList());
    }

    private static async Task<IResult> CrearAsync(
        int organizacionId,
        CrearEventoDeOrganizacionSolicitud solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        IAntiforgery antiforgery,
        HttpContext http,
        CancellationToken ct)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(new { message = TestigoInvalido[0] });

        var persona = Persona(principal);
        if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();

        // ANUNCIAR UN EVENTO ES REPRESENTAR A LA ORGANIZACION EN PUBLICO, así que pide la misma
        // dirección comprobada que registrar un proceso.
        if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                db, organizacionId, "anunciar eventos", ct) is { } sinConfirmar)
        {
            return sinConfirmar;
        }

        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var titulo = Limpiar(solicitud.Titulo);
        var descripcion = Limpiar(solicitud.Descripcion);
        if (titulo is null) errores["titulo"] = TituloObligatorio;
        if (descripcion is null) errores["descripcion"] = DescripcionObligatoria;
        if (solicitud.FechaFin is { } fin && fin < solicitud.FechaInicio) errores["fechaFin"] = FinAntesDelInicio;
        if (solicitud.HoraFin is not null && solicitud.HoraInicio is null) errores["horaFin"] = HoraFinSinInicio;
        if (solicitud.FestivalId <= 0) errores["festivalId"] = ProcesoObligatorio;

        // UN MUNICIPIO SIN SU DEPARTAMENTO NO ES UN TERRITORIO. El formulario no deja llegar a esa
        // combinación —el municipio solo se ofrece tras elegir departamento—, pero esta ruta está
        // abierta a cualquiera con sesión externa, y sin esta comprobación el CHECK de la base la
        // rechazaría como un 500 sin explicación en vez de como un 400 sobre el campo.
        if (Limpiar(solicitud.CodigoDepartamento) is null && Limpiar(solicitud.CodigoMunicipio) is not null)
        {
            errores["codigoDepartamento"] = MunicipioSinDepartamento;
        }

        if (errores.Count > 0) return Results.ValidationProblem(errores);

        // EL PROCESO TIENE QUE SER SUYO Y ESTAR PUBLICADO. Las dos condiciones se comprueban en una
        // sola consulta a propósito: separadas, un proceso ajeno y uno sin publicar darían mensajes
        // distintos, y el primero le diría a quien pregunta que ese Festival existe.
        var procesoValido = await db.FestivalRecords.AsNoTracking().AnyAsync(
            f => f.Id == solicitud.FestivalId
                && f.OrganizacionPrincipalId == organizacionId
                && f.StatusCode == EstadosFestival.Publicado,
            ct);
        if (!procesoValido) return Results.ValidationProblem(new Dictionary<string, string[]> { ["festivalId"] = ProcesoAjeno });

        var slug = ReglasDeAgenda.SlugDesde(titulo!);
        if (slug.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["titulo"] = TituloObligatorio });
        slug = await SlugLibreAsync(db, slug, ct);

        var ahora = DateTime.UtcNow;
        var evento = new EventoAgendaRow
        {
            Slug = slug,
            Titulo = titulo!,
            Descripcion = descripcion!,
            DescripcionLarga = Limpiar(solicitud.DescripcionLarga),
            FechaInicio = solicitud.FechaInicio,
            FechaFin = solicitud.FechaFin,
            HoraInicio = solicitud.HoraInicio,
            HoraFin = solicitud.HoraFin,
            Modalidad = solicitud.Modalidad,
            Lugar = Limpiar(solicitud.Lugar),
            CodigoDepartamento = Limpiar(solicitud.CodigoDepartamento),
            CodigoMunicipio = Limpiar(solicitud.CodigoMunicipio),
            // EL NIVEL DE COBERTURA SE DERIVA DEL TERRITORIO, NO SE PIDE.
            //
            // ESTE CANAL NO LO ESCRIBIA, y ese era el defecto: la entidad nace en «nacional» y
            // `CK_EventosAgenda_NivelCobertura` exige que en nacional los dos códigos sean nulos.
            // En cuanto alguien elegía un departamento —que es justo lo que el formulario ofrece—,
            // el INSERT chocaba con el CHECK y la pantalla decía «no fue posible anunciar el
            // evento. Revisa los campos», sobre unos campos que estaban bien.
            //
            // LA REGLA YA EXISTIA y la consola la aplicaba desde el principio: es la misma llamada
            // que hace `AgendaEndpoints`. Dos canales que escriben la misma tabla y solo uno deriva
            // la columna es la definición de «dos copias que divergen».
            NivelCobertura = ReglasDeAgenda.NivelQueCorresponde(
                Limpiar(solicitud.CodigoDepartamento), Limpiar(solicitud.CodigoMunicipio)),
            Url = Limpiar(solicitud.Url),
            FestivalId = solicitud.FestivalId,
            // NACE EN REVISION, SIEMPRE. Quien lo escribe no decide si se publica.
            Estado = "en_revision",
            Version = 1,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            db.EventosAgenda.Add(evento);
            await db.SaveChangesAsync(ct);

            // DE DONDE VIENE: lo anunció la organización desde su espacio, así que la procedencia
            // es ella y el ejecutor la persona que lo escribió.
            await ProcedenciaDeRegistro.AnotarAsync(
                db,
                Modulos.Agenda,
                evento.Id.ToString(CultureInfo.InvariantCulture),
                ProcedenciaDeRegistro.Externo,
                organizacionId,
                persona.Value,
                ct);

            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = persona.Value,
                TableName = "EventosAgenda",
                RecordId = evento.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Crear,
                NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    evento = "EventoAnunciadoPorOrganizacion",
                    organizacionId,
                    procesoId = solicitud.FestivalId,
                }),
                CreatedAt = ahora,
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Created(
            $"/api/v1/externo/organizaciones/{organizacionId}/eventos/{evento.Id}",
            new { id = evento.Id.ToString(CultureInfo.InvariantCulture), estado = evento.Estado });
    }

    // ---------- Apoyos --------------------------------------------------------------------

    /// <summary>
    /// Una dirección libre para el evento.
    /// </summary>
    /// <remarks>
    /// DOS EVENTOS PUEDEN LLAMARSE IGUAL —«Concierto de clausura» ocurre todos los años— y el
    /// índice único de la agenda rechazaría el segundo con un error que no explica nada. Se numera
    /// aquí en vez de pedirle a quien escribe que invente un título distinto.
    /// </remarks>
    private static async Task<string> SlugLibreAsync(PnmcDbContext db, string propuesto, CancellationToken ct)
    {
        var candidato = propuesto;
        var sufijo = 2;
        while (await db.EventosAgenda.AnyAsync(e => e.Slug == candidato, ct))
        {
            candidato = $"{propuesto}-{sufijo.ToString(CultureInfo.InvariantCulture)}";
            sufijo++;
        }
        return candidato;
    }

    private static string EtiquetaDeEstado(string estado) => estado switch
    {
        "borrador" => "Borrador",
        "en_revision" => "En revisión",
        "publicado" => "Publicado",
        "archivado" => "Archivado",
        _ => estado,
    };

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int? Persona(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // LA REGLA VIVE EN `AdministracionDeOrganizacion`. Esta copia NO comprobaba si la organización
    // sigue activa: una organización dada de baja seguía pudiendo anunciar eventos.
    private static Task<bool> Administra(PnmcDbContext db, int persona, int organizacion, CancellationToken ct) =>
        AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona, organizacion, ct);
}
