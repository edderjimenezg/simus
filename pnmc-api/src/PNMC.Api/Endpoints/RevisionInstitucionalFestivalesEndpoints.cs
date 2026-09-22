using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

using PNMC.Domain.Entities;

namespace PNMC.Api.Endpoints;

public static class RevisionInstitucionalFestivalesEndpoints
{
    public static RouteGroupBuilder MapRevisionInstitucionalFestivalesEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/festivales").WithTags("revision-institucional-festivales");
        // Antes: InstitutionalPolicy, que solo dice «hay cookie de consola». Por esa puerta
        // entra tambien un aliado, y este fichero no comprobaba rol en ninguna de sus 267
        // lineas: publicar o rechazar cualquier Festival estaba al alcance de un
        // aliado_admin. Ahora exige ademas rol interno. Ver Permisos.
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);

        institucional.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new TokenAntiforgeryRespuesta(tokens.RequestToken ?? string.Empty));
        });

        institucional.MapGet("/en-revision", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            // ESTA es la consulta que devolvia siempre vacio. Preguntaba por "EnRevision" y
            // ningun Festival podia estar en ese estado: FK_Festivales_EstadosContenido solo
            // admite los siete codigos de EstadosContenido, y "EnRevision" no es uno.
            //
            // Se compara SOLO contra el codigo canonico, a diferencia de la lectura publica
            // —que si acepta las dos grafias porque los 30 Festivales historicos llegaron con
            // "publicado" en minuscula—. Aqui no hay historicos que respetar: ninguna fila
            // llego nunca a este estado, precisamente porque la base lo impedia.
            var festivales = await dbContext.FestivalRecords.AsNoTracking()
                .Where(item => item.StatusCode == EstadosFestival.EnRevision)
                .OrderBy(item => item.UpdatedAt ?? item.CreatedAt)
                .ToListAsync(cancellationToken);
            var organizaciones = await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => item.IsActive)
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
            var ids = festivales.Select(item => item.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
            var envios = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
                .Where(item => item.ModuloId == Modulos.Festivales && ids.Contains(item.RegistroId) && item.Accion == "FestivalEnviadoARevision")
                .GroupBy(item => item.RegistroId)
                .Select(group => new
                {
                    RegistroId = group.Key,
                    Fecha = group.Max(item => item.Fecha),
                    NumeroEnvio = group.Count(),
                })
                .ToDictionaryAsync(item => item.RegistroId, cancellationToken);

            var respuesta = festivales.Select(item => new FestivalRevisionInstitucionalDto(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                item.OrganizacionPrincipalId is int organizacionId && organizaciones.TryGetValue(organizacionId, out var nombre)
                    ? nombre : "Organización no disponible",
                item.CoverageLevel,
                string.IsNullOrWhiteSpace(item.DepartmentCode) ? null : item.DepartmentCode,
                item.MunicipalityCode,
                envios.TryGetValue(item.Id.ToString(CultureInfo.InvariantCulture), out var envio) ? envio.Fecha : item.UpdatedAt ?? item.CreatedAt,
                envio?.NumeroEnvio ?? 1));
            return Results.Ok(respuesta);
        });

        // EL DETALLE QUE LEE QUIEN DECIDE.
        //
        // QUE LE FALTABA. Devolvia siete campos de la ficha y el historial. No devolvia las
        // practicas musicales, ni los territorios sonoros, ni las ediciones, ni el nombre del
        // departamento y el municipio: solo sus codigos DIVIPOLA. Un funcionario que abriera esta
        // respuesta veia «05001» donde la organizacion escribio «Medellin», y no veia en absoluto
        // los dos catalogos que esa organizacion diligencio ni los anyos que declaro haber hecho.
        //
        // Y ADEMAS NO LA LLAMABA NADIE: ningun componente Angular pedia esta ruta. La bandeja
        // pintaba los cuatro campos de la LISTA —nombre, organizacion, cobertura y fecha— y sobre
        // eso se publicaba o se rechazaba. El criterio es este: «que llegue
        // con etiqueta de festival, todos los datos que se diligenciaron, las versiones anyos y
        // poder ver toda la info».
        institucional.MapGet("/{festivalId:int}", async (
            int festivalId,
            PnmcDbContext dbContext,
            CacheDivipola divipola,
            CancellationToken cancellationToken) =>
        {
            var festival = await dbContext.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            var organizacion = festival.OrganizacionPrincipalId is int organizacionId
                ? await dbContext.EntityProfiles.AsNoTracking().Where(item => item.Id == organizacionId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken)
                : null;
            var historial = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
                .Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == festivalId.ToString())
                .OrderByDescending(item => item.Fecha)
                // La instantanea viaja con cada entrada. `organizacionPrincipalNombre`, arriba,
                // sigue siendo el nombre de HOY —es la ficha del festival, y ahi corresponde—;
                // pero cada linea del historial dice el de ENTONCES.
                .Select(item => new
                {
                    item.EstadoAnterior, item.EstadoNuevo, item.Accion, item.Comentario,
                    item.MotivoRechazo, item.Fecha,
                    organizacionNombre = item.OrganizacionNombre,
                    responsableNombre = item.ResponsableNombre,
                    actorNombre = item.ActorNombre
                })
                .ToListAsync(cancellationToken);
            var practicas = await dbContext.De<PracticaMusicalDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
                .Join(dbContext.PracticasMusicales.AsNoTracking(), relacion => relacion.ValorId, practica => practica.Id,
                    (_, practica) => new { practica.Id, practica.Nombre })
                .OrderBy(item => item.Nombre)
                .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
                .ToListAsync(cancellationToken);
            var territorios = await dbContext.De<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
                .Join(dbContext.TerritoriosSonoros.AsNoTracking(), relacion => relacion.ValorId, territorio => territorio.Id,
                    (_, territorio) => new { territorio.Id, territorio.Nombre })
                .OrderBy(item => item.Nombre)
                .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
                .ToListAsync(cancellationToken);

            // La procedencia historica y una reclamacion de administracion son dos hechos
            // distintos. La primera respalda por que una organizacion pudo recibir una
            // coincidencia; la segunda prueba que una organizacion efectivamente inicio un
            // tramite. No inferimos que una coincidencia fue descartada si no existe una
            // decision persistida que lo demuestre.
            var referenciasHistoricas = await dbContext.ReferenciasHistoricasFestival.AsNoTracking()
                .Where(item => item.FestivalId == festival.Id)
                .Join(dbContext.EntityProfiles.AsNoTracking(), referencia => referencia.OrganizacionId, organizacion => organizacion.Id,
                    (referencia, organizacion) => new
                    {
                        ReferenciaId = referencia.Id,
                        referencia.CreatedAt,
                        OrganizacionId = organizacion.Id,
                        OrganizacionNombre = organizacion.Name
                    })
                .OrderBy(item => item.OrganizacionNombre)
                .ToListAsync(cancellationToken);
            var reclamaciones = await dbContext.AdministrationClaims.AsNoTracking()
                .Where(item => item.ModuloId == Modulos.Festivales && item.CanonicalRecordId == festivalId.ToString(CultureInfo.InvariantCulture))
                .Join(dbContext.EntityProfiles.AsNoTracking(), reclamacion => reclamacion.RequestingOrganizationId, organizacion => organizacion.Id,
                    (reclamacion, organizacion) => new
                    {
                        ReclamacionId = reclamacion.Id,
                        reclamacion.Status,
                        reclamacion.Justification,
                        reclamacion.DecisionReason,
                        reclamacion.CreatedAt,
                        reclamacion.SubmittedAt,
                        reclamacion.DecidedAt,
                        reclamacion.UpdatedAt,
                        OrganizacionSolicitanteId = organizacion.Id,
                        OrganizacionSolicitanteNombre = organizacion.Name
                    })
                .OrderByDescending(item => item.UpdatedAt)
                .ToListAsync(cancellationToken);

            // DE MAS RECIENTE A MAS ANTIGUA: al revisar interesa primero el anyo en curso, que es
            // el que sostiene `EstadoVersionAnoActual` en la ficha publica.
            var ediciones = await dbContext.EdicionesFestival.AsNoTracking()
                .Where(item => item.FestivalId == festival.Id)
                .OrderByDescending(item => item.Anio)
                .ToListAsync(cancellationToken);

            // Los nombres presentables son los mismos que ya usa el resto de la consola. Se leen
            // del cache y no de una consulta suelta: son 1.122 municipios que no cambian.
            var (departamentos, municipios) = await divipola.ObtenerPresentablesAsync(dbContext, cancellationToken);

            return Results.Ok(new
            {
                id = festival.Id.ToString(CultureInfo.InvariantCulture),
                nombre = festival.Name,
                descripcion = festival.Description,
                estado = festival.StatusCode,
                organizacionPrincipalId = festival.OrganizacionPrincipalId?.ToString(CultureInfo.InvariantCulture),
                organizacionPrincipalNombre = organizacion,
                nivelCobertura = festival.CoverageLevel,
                nivelCoberturaEtiqueta = EtiquetaDeCobertura(festival.CoverageLevel),
                codigoDepartamento = festival.DepartmentCode,
                codigoMunicipio = festival.MunicipalityCode,
                departamentoNombre = festival.DepartmentCode is { Length: > 0 } codigoDepartamento
                    && departamentos.TryGetValue(codigoDepartamento, out var nombreDepartamento) ? nombreDepartamento : null,
                municipioNombre = festival.MunicipalityCode is { Length: > 0 } codigoMunicipio
                    && municipios.TryGetValue(codigoMunicipio, out var nombreMunicipio) ? nombreMunicipio : null,
                periodicidad = festival.Periodicidad,
                periodicidadDetalle = festival.PeriodicidadDetalle,
                correoContacto = festival.ContactEmail,
                instagram = festival.InstagramUrl,
                facebook = festival.FacebookUrl,
                paginaWeb = festival.WebsiteUrl,
                otroEnlace = festival.OtherUrl,
                telefonoCelular = festival.ContactPhone,
                observacionesContacto = festival.ObservacionesContacto,
                practicasMusicales = practicas,
                territoriosSonoros = territorios,
                // SIN `sePuedeEliminar`: esta es la lectura del revisor, que no borra ediciones.
                // El grupo de metodo ya no vale porque `ADto` tiene parametros opcionales.
                ediciones = ediciones.Select(fila => EdicionesFestivalExternosEndpoints.ADto(fila)).ToList(),
                procedencia = new
                {
                    referenciasHistoricas = referenciasHistoricas.Select(item => new
                    {
                        id = item.ReferenciaId.ToString(CultureInfo.InvariantCulture),
                        organizacionId = item.OrganizacionId.ToString(CultureInfo.InvariantCulture),
                        organizacionNombre = item.OrganizacionNombre,
                        fechaRegistro = item.CreatedAt
                    }),
                    reclamacionesAdministracion = reclamaciones.Select(item => new
                    {
                        id = item.ReclamacionId.ToString(CultureInfo.InvariantCulture),
                        estado = item.Status,
                        justificacion = item.Justification,
                        motivoDecision = item.DecisionReason,
                        fechaCreacion = item.CreatedAt,
                        fechaEnvio = item.SubmittedAt,
                        fechaDecision = item.DecidedAt,
                        fechaActualizacion = item.UpdatedAt,
                        organizacionSolicitanteId = item.OrganizacionSolicitanteId.ToString(CultureInfo.InvariantCulture),
                        organizacionSolicitanteNombre = item.OrganizacionSolicitanteNombre
                    })
                },
                historial
            });
        });

        institucional.MapPost("/{festivalId:int}/decisiones", async (
            int festivalId,
            DecisionRevisionFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IEnviadorDeCorreo correo,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La decisión institucional no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaInstitucionalId = ObtenerPersonaId(principal);
            if (personaInstitucionalId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.EnRevision))
            {
                return Results.Conflict(new { message = "Solo un Festival en revisión puede recibir una decisión institucional.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
            }

            var decision = ResolverDecision(solicitud);
            if (decision.Errores.Count > 0) return Results.ValidationProblem(decision.Errores);

            // decision.EstadoNuevo conserva el nombre DE CONTRATO ("Publicado", "Rechazado",
            // "AjustesSolicitados") porque mas abajo se hace switch sobre el para redactar la
            // notificacion. Si se tradujera aqui, esos brazos dejarian de casar y la
            // organizacion recibiria «Tu Festival cambió de estado» en lugar del motivo del
            // rechazo: un fallo silencioso, sin error y sin 400. La traduccion se aplica solo
            // donde se persiste.
            var estadoAlmacenado = EstadosFestival.DesdeContrato(decision.EstadoNuevo);
            if (estadoAlmacenado is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["estadoNuevo"] = ["El estado resultante no pertenece al catálogo de EstadosContenido."],
                });
            }

            var ahora = DateTime.UtcNow;
            festival.StatusCode = estadoAlmacenado;
            festival.UpdatedAt = ahora;
            var filaHistorial = new HistorialRevisionRegistroRow
            {
                ModuloId = Modulos.Festivales,
                RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
                // CK_RegistrosRevisionHistorial_EstadoNuevo admite los mismos siete codigos.
                EstadoAnterior = EstadosFestival.EnRevision,
                EstadoNuevo = estadoAlmacenado,
                // Sin CHECK: es el sitio del evento funcional, segun §39.
                Accion = decision.Evento!,
                Comentario = decision.Observacion,
                MotivoRechazo = decision.MotivoRechazo,
                UsuarioId = personaInstitucionalId.Value,
                Fecha = ahora,
                MetadataJson = $"{{\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId}}}"
            };
            await InstantaneaDelHistorial.TomarAsync(
                dbContext, filaHistorial, festival.OrganizacionPrincipalId, personaInstitucionalId.Value, cancellationToken);
            dbContext.HistorialesRevisionRegistros.Add(filaHistorial);
            dbContext.AuditLogs.Add(new AuditLogRow
            {
                UserId = personaInstitucionalId.Value,
                TableName = "Festivales",
                RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
                // CK_BitacoraAuditoria_Accion solo admite trece verbos tecnicos. "FestivalPublicado"
                // y sus hermanos no estan entre ellos: publicar o rechazar un Festival fallaba
                // contra SQL Server igual que enviarlo a revision. El evento viaja abajo.
                Action = AccionesAuditoria.DeEvento(decision.Evento),
                PreviousValuesJson = $"{{\"Estado\":\"{EstadosFestival.EnRevision}\"}}",
                NewValuesJson = $"{{\"Estado\":\"{estadoAlmacenado}\",\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId},\"Evento\":\"{decision.Evento}\"}}",
                CreatedAt = ahora
            });
            await CrearNotificacionDecisionAsync(dbContext, correo, festival, decision, ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            // La respuesta devuelve el nombre de contrato, no el codigo almacenado: es lo que
            // el panel institucional lleva esperando desde el principio.
            return Results.Ok(new { id = festival.Id, estado = EstadosFestival.HaciaContrato(festival.StatusCode), observacion = decision.Observacion ?? decision.MotivoRechazo });
        });

        // «ELIMINAR» UN REGISTRO DEL ECOSISTEMA, DIRECTO Y NO POR EL RODEO DE LA ORGANIZACIÓN.
        //
        // HASTA AHORA SOLO EXISTÍA EL RETIRO SOLICITADO: un Festival publicado solo se archivaba
        // si la ORGANIZACIÓN pedía su retiro y un funcionario aprobaba esa solicitud
        // (`RecordGovernanceEndpoints.cs`, `festivales_retiro`). El criterio es este: la consola necesita poder eliminar un registro del ecosistema
        // directamente -piénsese en un Festival duplicado, fraudulento o que incumple las bases,
        // donde no tiene sentido esperar a que quien lo publicó pida borrarlo primero-.
        //
        // ES UN ARCHIVADO, NO UN DELETE DE VERDAD. Mismo patrón que ya usa la aprobación de
        // retiro: el registro se queda en la base -conserva su historial, su auditoría, sus
        // ediciones-, solo deja de estar público. Un DELETE real rompería las llaves foráneas de
        // PropuestasCambioFestival, RevisionesFestival, VersionesFestival y Agenda.
        institucional.MapPost("/{festivalId:int}/archivar", async (
            int festivalId,
            ArchivarFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IEnviadorDeCorreo correo,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La acción no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaInstitucionalId = ObtenerPersonaId(principal);
            if (personaInstitucionalId is null) return Results.Unauthorized();

            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.Publicado))
            {
                return Results.Conflict(new { message = "Solo un Festival publicado puede eliminarse del ecosistema desde aquí.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
            }

            var motivo = LimpiarTexto(solicitud.Motivo);
            if (motivo is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["motivo"] = ["Escribe el motivo para eliminar el registro del ecosistema."] });
            }

            var ahora = DateTime.UtcNow;
            var estadoAnterior = festival.StatusCode;
            festival.StatusCode = EstadosFestival.Archivado;
            festival.UpdatedAt = ahora;

            var filaHistorial = new HistorialRevisionRegistroRow
            {
                ModuloId = Modulos.Festivales,
                RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = EstadosFestival.Archivado,
                Accion = "FestivalArchivado",
                Comentario = null,
                MotivoRechazo = motivo,
                UsuarioId = personaInstitucionalId.Value,
                Fecha = ahora,
                MetadataJson = $"{{\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId}}}"
            };
            await InstantaneaDelHistorial.TomarAsync(
                dbContext, filaHistorial, festival.OrganizacionPrincipalId, personaInstitucionalId.Value, cancellationToken);
            dbContext.HistorialesRevisionRegistros.Add(filaHistorial);
            dbContext.AuditLogs.Add(new AuditLogRow
            {
                UserId = personaInstitucionalId.Value,
                TableName = "Festivales",
                RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Archivar,
                PreviousValuesJson = $"{{\"Estado\":\"{estadoAnterior}\"}}",
                NewValuesJson = $"{{\"Estado\":\"{EstadosFestival.Archivado}\",\"Evento\":\"FestivalArchivado\",\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId}}}",
                CreatedAt = ahora
            });

            await CrearNotificacionDecisionAsync(dbContext, correo, festival,
                ("Archivado", "FestivalArchivado", null, motivo, new Dictionary<string, string[]>()), ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { id = festival.Id, estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
        });

        return group;
    }

    /// <summary>Los tres niveles que admite <c>CK_Festivales_NivelCobertura</c>, con su rotulo.</summary>
    /// <remarks>
    /// NO SE CAPITALIZA LA CADENA DE LA BASE. Seria una linea mas corta y taparia el dia en que
    /// apareciera un cuarto valor: la restriccion admite exactamente tres, y un nivel que no este
    /// aqui debe salir tal cual y verse raro, no disfrazado de rotulo.
    /// </remarks>
    private static string EtiquetaDeCobertura(string? nivel) => nivel switch
    {
        "municipal" => "Municipal",
        "departamental" => "Departamental",
        "nacional" => "Nacional",
        _ => nivel ?? string.Empty
    };

    private static (string? EstadoNuevo, string? Evento, string? Observacion, string? MotivoRechazo, Dictionary<string, string[]> Errores) ResolverDecision(DecisionRevisionFestivalSolicitud solicitud)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var accion = solicitud.Accion?.Trim();
        var observacion = LimpiarTexto(solicitud.Observacion);
        var motivoRechazo = LimpiarTexto(solicitud.MotivoRechazo);
        return accion switch
        {
            "SolicitarAjustes" when string.IsNullOrWhiteSpace(observacion) => (null, null, null, null,
                new Dictionary<string, string[]> { ["observacion"] = ["La observación es obligatoria para solicitar ajustes."] }),
            "SolicitarAjustes" => ("AjustesSolicitados", "FestivalAjustesSolicitados", observacion, null, errores),
            "Rechazar" when string.IsNullOrWhiteSpace(motivoRechazo) => (null, null, null, null,
                new Dictionary<string, string[]> { ["motivoRechazo"] = ["El motivo es obligatorio para rechazar el Festival."] }),
            "Rechazar" => ("Rechazado", "FestivalRechazado", null, motivoRechazo, errores),
            "Publicar" => ("Publicado", "FestivalPublicado", null, null, errores),
            _ => (null, null, null, null, new Dictionary<string, string[]> { ["accion"] = ["La acción institucional no es válida."] })
        };
    }

    private static async Task CrearNotificacionDecisionAsync(
        PnmcDbContext dbContext,
        IEnviadorDeCorreo correo,
        FestivalRow festival,
        (string? EstadoNuevo, string? Evento, string? Observacion, string? MotivoRechazo, Dictionary<string, string[]> Errores) decision,
        DateTime fecha,
        CancellationToken cancellationToken)
    {
        var registroId = festival.Id.ToString(CultureInfo.InvariantCulture);

        // A QUIEN SE AVISA. Se busca en el HISTORIAL DE REVISION y no en la bitacora de
        // auditoria, y el cambio no es cosmetico.
        //
        // Antes se preguntaba a BitacoraAuditoria por una fila con
        // Action == "FestivalEnviadoARevision". Ese valor no esta entre los trece verbos que
        // admite CK_BitacoraAuditoria_Accion, de modo que contra SQL Server la fila NUNCA
        // llegaba a escribirse: la consulta devolvia vacio, el metodo salia por el
        // `return` de abajo y la organizacion no recibia aviso de que su Festival habia sido
        // publicado o rechazado. Sin error, sin traza, sin nada. El sistema registraba el
        // exito de algo que no ocurria.
        //
        // RegistrosRevisionHistorial.Accion no tiene CHECK y es donde vive el evento
        // funcional, que es la separación establecida por el diseño vigente. Ademas es
        // la fuente semanticamente correcta: «quien envio esto a revision» es una pregunta
        // sobre el circuito de revision, no sobre la traza tecnica.
        var personaRemitenteId = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == registroId
                && item.Accion == "FestivalEnviadoARevision")
            .OrderByDescending(item => item.Fecha)
            .Select(item => (int?)item.UsuarioId)
            .FirstOrDefaultAsync(cancellationToken);
        if (personaRemitenteId is null) return;

        var persona = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == personaRemitenteId.Value && item.IsActive, cancellationToken);
        if (persona is null) return;

        // Quien firma estas decisiones es el equipo del PNMC, no SIMUS: el
        // Sistema de Informacion de la Musica es una plataforma del Ministerio
        // que vive fuera de este portal y no revisa nada de lo que aqui se
        // registra. Decirle a una organizacion «SIMUS rechazo tu Festival» la
        // manda a reclamar a la puerta equivocada.
        var (titulo, cuerpo) = decision.EstadoNuevo switch
        {
            "AjustesSolicitados" => ("Festival con ajustes solicitados", $"El equipo del PNMC solicitó ajustes para tu Festival “{festival.Name}”. {decision.Observacion}"),
            "Rechazado" => ("Festival rechazado", $"El equipo del PNMC rechazó tu Festival “{festival.Name}”. Motivo: {decision.MotivoRechazo}"),
            "Publicado" => ("Festival publicado", $"Tu Festival “{festival.Name}” fue publicado en el ecosistema musical del PNMC."),
            "Archivado" => ("Festival eliminado del ecosistema", $"El equipo del PNMC eliminó tu Festival “{festival.Name}” del ecosistema musical. Motivo: {decision.MotivoRechazo}"),
            _ => ("Actualización de Festival", $"Tu Festival “{festival.Name}” cambió de estado.")
        };
        dbContext.Notifications.Add(new NotificationRow
        {
            RecipientUserId = persona.Id,
            RecipientEmail = persona.Email,
            EventType = decision.Evento!,
            Channel = "internal",
            Title = titulo,
            Body = cuerpo,
            Status = "enviada",
            ModuloId = Modulos.Festivales,
            RecordId = registroId,
            MetadataJson = $"{{\"Estado\":\"{decision.EstadoNuevo}\"}}",
            CreatedAt = fecha,
            SentAt = fecha,
            Attempts = 0
        });

        // Y TAMBIEN POR CORREO. Es lo que pide la H0 —«notificaciones automáticas a los usuarios
        // sobre el estado de sus solicitudes»— y hasta la decisión solo
        // se avisaba dentro de la aplicación: quien no volviera a entrar no se enteraba de que su
        // Festival se publicó, o de que le pidieron ajustes.
        //
        // EL MISMO TEXTO EN LOS DOS CANALES, a propósito: dos redacciones de la misma decisión se
        // separan en cuanto una se retoca, y la organización acabaría leyendo dos cosas distintas
        // sobre el mismo hecho. Lo que cambia es el asunto, que en un correo tiene que nombrar el
        // Festival para que se reconozca en la bandeja.
        await correo.EncolarAsync(new CorreoSaliente(
            persona.Email,
            $"SIMUS · {titulo} · {festival.Name}",
            $"Estimado(a) {persona.FullName}.\n\n{cuerpo}",
            decision.Evento!,
            persona.Id), cancellationToken);
    }

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId)
            ? personaId : null;

    private static string? LimpiarTexto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, 1200);
}
