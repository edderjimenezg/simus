using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class ExternalOrganizationEndpoints
{
    public static RouteGroupBuilder MapExternalOrganizationEndpoints(this RouteGroupBuilder group)
    {
        var organizations = group.MapGroup("/externo/organizaciones").WithTags("externo-organizaciones");
        organizations.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        organizations.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new ExternalCsrfTokenResponse(tokens.RequestToken ?? string.Empty));
        });

        organizations.MapPost("/", async (
            ExternalOrganizationCreateRequest request,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La solicitud de registro no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var user = await ResolveExternalUserAsync(principal, dbContext, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            // UN CORREO, UNA ORGANIZACION. El criterio es este: «una
            // organizacion puede tener muchos festivales, pero un correo de una organizacion no
            // puede tener muchas organizaciones». Hasta ese dia esta ruta existia justamente para lo
            // contrario: creaba la SEGUNDA organizacion de una cuenta, copiandole la persona
            // responsable de la primera.
            //
            // LA COMPROBACION ES POR VINCULO ACTIVO Y ROL QUE RESPONDE, no por «tiene alguna fila en
            // UsuariosEntidades»: un vinculo de solo lectura no responde por nadie, y uno inactivo es
            // justamente el que se retiro. Es la misma lista que usan las ocho guardas del canal
            // externo, `RolesDeEntidad.QueResponden`.
            //
            // NO HAY INDICE QUE LO IMPONGA EN LA BASE, y no es un olvido: la base local tiene cinco
            // cuentas sembradas con mas de una organizacion —externo@pnmc.local tiene cinco—, asi que
            // un indice unico no se podria crear sin normalizarlas antes.
            var yaRespondePorUna = await dbContext.UserEntities.AsNoTracking().AnyAsync(
                vinculo => vinculo.UserId == user.Id
                    && vinculo.IsActive
                    && RolesDeEntidad.QueResponden.Contains(vinculo.EntityRole),
                cancellationToken);
            if (yaRespondePorUna)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["organizacion"] =
                    [
                        "Este correo ya responde por una organización. Un correo solo puede estar "
                        + "atado a una organización; una organización sí puede tener varios Festivales."
                    ]
                });
            }

            var errors = await ValidateRequestAsync(request, dbContext, cancellationToken);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var now = DateTime.UtcNow;

            // DE DONDE SALE LA PERSONA RESPONSABLE DE ESTA SEGUNDA ORGANIZACION. No se le vuelve a
            // pedir la cedula: ya la dio al registrarse, porque desde que el alta es un solo acto
            // toda cuenta externa nace con su organizacion y con su fila en EntidadesResponsable.
            // Se copia de la que ya tiene. Pedirsela otra vez seria preguntarle algo que el sistema
            // sabe, y abrir la puerta a que las dos copias digan cosas distintas.
            var responsablePrevio = await dbContext.EntidadesResponsable.AsNoTracking()
                .Where(fila => dbContext.UserEntities.Any(vinculo =>
                    vinculo.EntityId == fila.IdEntidad
                    && vinculo.UserId == user.Id
                    && RolesDeEntidad.QueResponden.Contains(vinculo.EntityRole)
                    && vinculo.IsActive))
                .OrderByDescending(fila => fila.FechaCreacion)
                .FirstOrDefaultAsync(cancellationToken);

            if (responsablePrevio is null)
            {
                // Solo alcanzable por cuentas anteriores al cambio, que nacieron sin organizacion.
                // Se dice lo que pasa y por donde se sale, en vez de escribir una fila a medias.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["responsable"] =
                    [
                        "Tu cuenta no tiene todavía una persona responsable registrada. "
                        + "Registra tu primera organización desde el formulario de acceso."
                    ]
                });
            }

            // LA TRANSACCION VA DENTRO DE LA ESTRATEGIA DE EJECUCION, Y NO AL REVES.
            // SQL Server esta configurado con reintentos, y `SqlServerRetryingExecutionStrategy`
            // se niega a trabajar sobre una transaccion abierta a mano: si algo falla a mitad no
            // sabria desde donde reintentar. Abrirla directamente devuelve un 500 con
            // InvalidOperationException, no un error de datos.
            //
            // ESTO NO SE VE CON SQLite, que es el motor de casi toda la suite: alli no hay
            // estrategia de reintentos y `BeginTransactionAsync` a secas funciona. La via de
            // SQL Server fue la que lo dijo. Es la misma leccion de siempre: lo que impone el
            // motor no se deduce leyendo el ORM.
            var estrategia = dbContext.Database.CreateExecutionStrategy();
            return await estrategia.ExecuteAsync(async () =>
            {
            await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var entity = await AltaDeOrganizacion.CrearAsync(
                dbContext,
                user,
                request.Name,
                request.IdentificationNumber,
                request.ContactEmail,
                request.HeadquartersDepartmentCode,
                request.HeadquartersMunicipalityCode,
                responsablePrevio.ResponsableNombre,
                responsablePrevio.ResponsableTipoDocumento,
                responsablePrevio.ResponsableNumeroDocumento,
                responsablePrevio.ResponsablePrimerNombre,
                responsablePrevio.ResponsableSegundoNombre,
                responsablePrevio.ResponsablePrimerApellido,
                responsablePrevio.ResponsableSegundoApellido,
                responsablePrevio.ResponsableTelefono,
                responsablePrevio.ResponsableAutorizacionDatos,
                now,
                cancellationToken);

            WriteAuditAsync(dbContext, user.Id, entity.Id, "crear_organizacion", cancellationToken);
            WriteAuditAsync(dbContext, user.Id, entity.Id, "asignar_administrador_inicial", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);

            return Results.Created($"/api/v1/externo/organizaciones/{entity.Id}", ToDto(entity));
            });
        });

        organizations.MapGet("/{id:int}", async (
            int id,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var user = await ResolveExternalUserAsync(principal, dbContext, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var entity = await dbContext.EntityProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.IsActive, cancellationToken);
            if (entity is null)
            {
                return Results.NotFound();
            }

            var canAdminister = await dbContext.UserEntities.AsNoTracking().AnyAsync(
                item => item.UserId == user.Id && item.EntityId == id && RolesDeEntidad.QueResponden.Contains(item.EntityRole) && item.IsActive,
                cancellationToken);
            return canAdminister ? Results.Ok(ToDto(entity)) : Results.Forbid();
        });


        // ==================================================================================
        // EL PERFIL DE LA ORGANIZACION Y SU PERSONA RESPONSABLE
        // ==================================================================================
        // POR QUE CUELGAN DE /externo Y NO DE /external/organizations, QUE ES EL GRUPO DE ARRIBA.
        // Todo lo que el panel de /ecosistema/mi-panel ya llama vive bajo /api/v1/externo: «mis
        // organizaciones», los festivales de una organizacion, sus catalogos. Colgar estas cuatro
        // rutas del grupo en ingles habria dejado a una sola pantalla hablando dos idiomas de URL
        // contra el mismo API.
        //
        // POR QUE NO SE AMPLIO GET /external/organizations/{id}, QUE YA EXISTE. Devuelve nueve
        // campos y ninguno de los once descriptivos de dbo.Entidades: sin descripcion, sin
        // telefono, sin sitio web, sin redes, sin direccion, sin nombre legal. Un panel construido
        // sobre esa ruta no puede mostrar ni editar lo que la organizacion escribio de si misma.
        var perfiles = group.MapGroup("/externo/organizaciones").WithTags("perfil-organizacion-externo");
        perfiles.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        perfiles.MapGet("/{organizacionId:int}/perfil", async (
            int organizacionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var acceso = await ResolverAccesoAsync(principal, organizacionId, dbContext, paraEscribir: false, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            return Results.Ok(await ConstruirPerfilAsync(acceso.Entidad!, dbContext, cancellationToken));
        });

        perfiles.MapPut("/{organizacionId:int}/perfil", async (
            int organizacionId,
            PerfilOrganizacionSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "Los datos de la organización no pudieron validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var acceso = await ResolverAccesoAsync(principal, organizacionId, dbContext, paraEscribir: true, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;
            var entidad = acceso.Entidad!;

            var errores = await ValidarPerfilAsync(solicitud, entidad, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;

            entidad.Name = ValidationHelpers.SanitizeText(solicitud.Nombre, LargoNombre);
            entidad.LegalName = TextoOpcional(solicitud.NombreLegal, LargoNombreLegal);
            entidad.Description = TextoOpcional(solicitud.Descripcion, LargoDescripcion);
            entidad.ContactEmail = CorreoElectronico.Normalizar(solicitud.CorreoContacto);
            entidad.ContactPhone = TextoOpcional(solicitud.TelefonoContacto, LargoTelefono);
            entidad.WebsiteUrl = TextoOpcional(solicitud.SitioWeb, LargoEnlace);
            entidad.FacebookUrl = TextoOpcional(solicitud.Facebook, LargoEnlace);
            entidad.InstagramUrl = TextoOpcional(solicitud.Instagram, LargoEnlace);
            entidad.OtherUrl = TextoOpcional(solicitud.OtroEnlace, LargoEnlace);
            entidad.AddressText = TextoOpcional(solicitud.Direccion, LargoDireccion);
            entidad.HeadquartersDepartmentCode = AltaDeOrganizacion.NormalizarOpcional(solicitud.CodigoDepartamentoSede);
            entidad.HeadquartersMunicipalityCode = AltaDeOrganizacion.NormalizarOpcional(solicitud.CodigoMunicipioSede);

            // LA FOTO SE COMPRUEBA CONTRA EL BANCO, no se acepta el numero que llegue. Un
            // identificador inventado dejaria el perfil apuntando a un archivo que no existe, y la
            // foranea lo rechazaria con un 500 en vez de con un aviso que se entiende. `null` es
            // legitimo: es como se quita la foto y se vuelve a las iniciales.
            if (solicitud.ArchivoFotoId is int fotoPedida)
            {
                if (!await dbContext.Files.AsNoTracking().AnyAsync(x => x.Id == fotoPedida, cancellationToken))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["archivoFotoId"] = ["La imagen elegida no está en el banco de archivos."],
                    });
                }
                entidad.ArchivoFotoId = fotoPedida;
            }
            else
            {
                entidad.ArchivoFotoId = null;
            }

            // LA IDENTIFICACION SE PUEDE CORREGIR SIEMPRE, POR DECISION DEL 14 DE SEPTIEMBRE DE 2026.
            //
            // Hasta esa fecha se congelaba en cuanto el Programa recibia algo de la organizacion: a
            // partir de ahi el campo se ignoraba EN SILENCIO y el panel lo pintaba como texto con un
            // «escribeles si hay un error». quedó descartado: una errata en el NIT es
            // justo lo que la organizacion tiene que poder arreglar sin escribir un correo, y el
            // circuito de revision no se sostiene sobre un campo bloqueado sino sobre la bitacora,
            // que registra quien lo cambio y cuando.
            //
            // LO QUE NO SE RELAJA: la identificacion sigue siendo unica entre organizaciones y sigue
            // pasando la validacion de formato del alta. Eso vive en `ValidarPerfilAsync`, y solo se
            // comprueba cuando el numero CAMBIA.
            entidad.IdentificationNumber = AltaDeOrganizacion.NormalizarIdentificacion(solicitud.NumeroIdentificacion);

            entidad.UpdatedAt = ahora;

            // LO QUE NO SE ESCRIBE, Y ES DELIBERADO: EstadoRegistro, EsInstitucional, Activo,
            // TipoEntidad, IdUsuarioCreador, IdUsuarioResponsable, TipoIdentificacion y las cuatro
            // fechas de moderacion. La barrera son DOS y no una:
            // arriba, PerfilOrganizacionSolicitud no tiene esos campos, asi que una carga maliciosa
            // no tiene por donde entrar; aqui, no hay ninguna asignacion que los toque. El unico
            // La transición institucional de EstadoRegistro no comparte este contrato externo.
            // Se incorporará mediante la ruta institucional específica de organizaciones; este
            // perfil nunca la puede escribir, incluso si una carga maliciosa agrega propiedades.
            RegistrarAuditoriaExterna(dbContext, acceso.PersonaId, "Entidades", entidad.Id, "actualizar_perfil_organizacion", ahora);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ConstruirPerfilAsync(entidad, dbContext, cancellationToken));
        });

        perfiles.MapGet("/{organizacionId:int}/responsable", async (
            int organizacionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var acceso = await ResolverAccesoAsync(principal, organizacionId, dbContext, paraEscribir: false, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            var fila = await dbContext.EntidadesResponsable.AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdEntidad == organizacionId, cancellationToken);
            if (fila is null) return SinResponsable();

            return Results.Ok(ADto(fila));
        });

        perfiles.MapPut("/{organizacionId:int}/responsable", async (
            int organizacionId,
            ResponsableOrganizacionSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "Los datos de la persona responsable no pudieron validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var acceso = await ResolverAccesoAsync(principal, organizacionId, dbContext, paraEscribir: true, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            var fila = await dbContext.EntidadesResponsable
                .FirstOrDefaultAsync(item => item.IdEntidad == organizacionId, cancellationToken);

            // ESTA RUTA CORRIGE UNA FILA; NO LA CREA. Faltan dos datos que no se pueden deducir sin
            // preguntarselos a la persona: ResponsableCorreo, que AltaDeOrganizacion copia de la
            // credencial de acceso, y ResponsableAutorizacionDatos, que la Ley 1581 de 2012 obliga
            // a guardar y prohibe presumir. Escribir aqui una autorizacion que nadie dio seria
            // fabricar un consentimiento.
            if (fila is null) return SinResponsable();

            var errores = ValidarResponsable(solicitud);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            fila.ResponsableNombre = ValidationHelpers.SanitizeText(solicitud.ResponsableNombre, LargoResponsableNombre);
            fila.ResponsableTipoDocumento = NormalizarTipoDocumento(solicitud.ResponsableTipoDocumento);
            fila.ResponsableNumeroDocumento = AltaDeOrganizacion.NormalizarDocumento(solicitud.ResponsableNumeroDocumento);
            fila.ResponsableTelefono = TextoOpcional(solicitud.ResponsableTelefono, LargoTelefono);
            fila.FechaActualizacion = ahora;

            // LO QUE NO SE ESCRIBE: IdEntidad (clave primaria y foranea a la vez), ResponsableCorreo,
            // ResponsableDesde, ResponsableAutorizacionDatos y FechaCreacion. Ninguno esta en
            // ResponsableOrganizacionSolicitud, que tiene cuatro campos y ni uno mas.
            RegistrarAuditoriaExterna(dbContext, acceso.PersonaId, "EntidadesResponsable", fila.IdEntidad, "actualizar_responsable_organizacion", ahora);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ADto(fila));
        });

        // EL DESPLEGABLE DE TIPO DE DOCUMENTO NECESITA ESTA LISTA, y sin ella el panel lo pinta
        // vacio: no se puede guardar a la persona responsable porque el tipo de documento es
        // NOT NULL. El contrato la declaraba y la entrega se quedo sin ella.
        //
        // VA EN EL GRUPO `/externo`, con su misma politica: la lista no es secreta, pero abrirla
        // a anonimos anade superficie publica a cambio de nada.
        perfiles.MapGet("/catalogos/tipos-documento", () =>
            Results.Ok(TiposDeDocumento
                .Select(par => new TipoDocumentoDto(par.Key, par.Value))
                .ToList()));

        return group;
    }

    /// <summary>
    /// Las mismas reglas que el alta completa, en el mismo sitio.
    /// </summary>
    /// <remarks>
    /// Aqui vivia una segunda copia de todo —nombre obligatorio, formato y unicidad del NIT, niveles
    /// de cobertura, existencia en DIVIPOLA—, escrita por separado de la del registro. Dos copias de
    /// una regla no se mantienen iguales: divergen, y la que divergio se descubre el dia que alguien
    /// crea una organizacion por la ruta equivocada. El prefijo va vacio porque el formulario de esta
    /// ruta llama a sus campos `name`, `contactEmail` y demas, sin anteponer nada.
    /// </remarks>
    private static async Task<Dictionary<string, string[]>> ValidateRequestAsync(
        ExternalOrganizationCreateRequest request,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        await AltaDeOrganizacion.ValidarAsync(
            errors,
            string.Empty,
            // AQUI EL CORREO DE CONTACTO SI ES UN CAMPO APARTE: quien llama ya tiene sesion, y su
            // cuenta se abrio con otro correo. El error sigue yendo a `contactEmail`.
            null,
            request.Name,
            request.IdentificationNumber,
            request.ContactEmail,
            dbContext,
            cancellationToken);
        await AltaDeOrganizacion.ValidarSedeAsync(
            errors, request.HeadquartersDepartmentCode, request.HeadquartersMunicipalityCode,
            dbContext, cancellationToken);
        return errors;
    }

    private static async Task<UserRow?> ResolveExternalUserAsync(ClaimsPrincipal principal, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId)) return null;

        // POR dbo.UsuariosRoles. Misma forma que en ExternalAuthEndpoints y por la misma
        // razon: con varios roles por persona, el Join contra la columna escalar deja de ser
        // «una fila como mucho».
        return await dbContext.Users
            .Where(user => user.Id == userId
                && user.IsActive
                && dbContext.UsuariosRoles
                    .Join(dbContext.Roles, asignacion => asignacion.RoleId, role => role.Id, (asignacion, role) => new { asignacion.UserId, role.Name })
                    .Any(par => par.UserId == user.Id && par.Name == "externo"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ExternalOrganizationDto ToDto(EntityProfileRow entity) => new(
        entity.Id.ToString(CultureInfo.InvariantCulture), entity.Name, entity.IdentificationNumber, entity.ContactEmail ?? string.Empty,
        entity.HeadquartersDepartmentCode ?? string.Empty, entity.HeadquartersMunicipalityCode ?? string.Empty, "administrador", entity.StatusCode);

    private static void WriteAuditAsync(PnmcDbContext dbContext, int userId, int entityId, string action, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = userId,
            TableName = "Entidades",
            RecordId = entityId.ToString(CultureInfo.InvariantCulture),
            Action = action,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ==========================================================================================
    // PERFIL DE LA ORGANIZACION: LARGOS, CATALOGOS Y AYUDANTES
    // ==========================================================================================

    /// <remarks>
    /// LOS LARGOS SALEN DE LA BASE, NO DEL FORMULARIO. Medidos en PNMC_LOCAL el 27 de agosto de
    /// 2026 con <c>SELECT c.name, c.max_length FROM sys.columns c WHERE c.object_id =
    /// OBJECT_ID('dbo.Entidades')</c>; <c>max_length</c> viene en BYTES y estas columnas son
    /// <c>nvarchar</c>, asi que el numero de caracteres es la mitad: Nombre 480 -> 240.
    ///
    /// SE COMPRUEBAN ANTES DE GUARDAR Y NO SE RECORTAN. <see cref="ValidationHelpers.SanitizeText"/>
    /// corta por lo sano, que esta bien para un texto que nadie va a releer y mal para el nombre
    /// legal de una organizacion: quien escribe 241 caracteres tiene que enterarse de que sobra uno,
    /// no descubrir meses despues que su nombre esta cortado a la mitad.
    /// </remarks>
    private const int LargoNombre = 240;
    private const int LargoNombreLegal = 240;
    private const int LargoIdentificacion = 60;
    private const int LargoCorreo = 180;
    private const int LargoTelefono = 80;
    private const int LargoEnlace = 500;
    private const int LargoDireccion = 300;
    private const int LargoResponsableNombre = 240;

    /// <remarks>
    /// <c>Descripcion</c> es <c>nvarchar(MAX)</c>: el tope no lo pone la base sino esta linea. Dos
    /// mil caracteres son unos cuatro parrafos, que es lo que cabe en la ficha publica de una
    /// organizacion; mas alla, el texto deja de leerse y empieza a pesar en cada listado.
    /// </remarks>
    private const int LargoDescripcion = 2000;

    /// <summary>
    /// Los ocho tipos de documento activos de <c>dbo.TiposDocumento</c>, en su orden de la tabla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LAS ETIQUETAS LLEVAN TILDE Y LA TABLA NO. Medido en PNMC_LOCAL:
    /// <c>dbo.TiposDocumento</c> guarda «Cedula de ciudadania», «Cedula de extranjeria» y
    /// «Numero unico de identificacion personal», sin una sola tilde. Estas de aqui SI las
    /// llevan porque son lo que lee una persona en un desplegable, y «Cedula» escrito asi es
    /// una falta de ortografia en pantalla. NO SON, POR TANTO, UNA COPIA DE LA TABLA: son la
    /// forma correcta de los mismos ocho conceptos, y el codigo —que es lo que se guarda— si
    /// coincide exactamente. Que la tabla no tenga tildes es un defecto de sus datos, anotado
    /// aparte; el dia que se corrija, esta constante se borra y se lee de ahi.
    ///
    /// LA TABLA YA ESTA MAPEADA, Y ESTA CONSTANTE ES DEUDA CON FECHA. El 15 de septiembre de 2026
    /// se creo <c>TipoDocumentoRow</c> y se corrigieron las tildes del catalogo en la base
    /// (<c>V20260915_02</c>), asi que el motivo original —«la tabla no esta mapeada»— ya no existe.
    /// Lo que queda es el trabajo de enhebrar el contexto por los DOS sitios que aun la usan sin
    /// tenerlo a mano: la validacion del alta externa y el mapeo de la ficha del responsable. El
    /// perfil administrativo, que se construyo ese mismo dia, YA lee la tabla. Mientras esas dos
    /// llamadas no se cambien, esto es una copia declarada como copia: los ocho codigos coinciden
    /// exactamente con los de la tabla y una prueba lo comprueba.
    /// </para>
    /// <para>
    /// EN MINUSCULA PORQUE ASI LO GUARDA EL CATALOGO, y la comparacion baja a minuscula porque la
    /// otra mitad de la base no lo hace: <c>AltaDeOrganizacion.TipoDocumentoPorOmision</c> escribe
    /// <c>"CC"</c> y las dos filas que hoy tiene <c>EntidadesResponsable</c> dicen <c>CC</c>. No hay
    /// clave foranea entre las dos tablas que obligue a que coincidan, asi que comparar tal cual
    /// rechazaria los datos que ya estan guardados.
    /// </para>
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, string> TiposDeDocumento =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["cc"] = "Cédula de ciudadanía",
            ["ce"] = "Cédula de extranjería",
            ["ti"] = "Tarjeta de identidad",
            ["rc"] = "Registro civil de nacimiento",
            ["nuip"] = "Número único de identificación personal",
            ["pa"] = "Pasaporte",
            ["pep"] = "Permiso especial de permanencia",
            ["ppt"] = "Permiso por protección temporal",
            ["die"] = "Documento de identificación extranjero",
            ["cd"] = "Carné diplomático",
        };

    /// <summary>
    /// Quien pide, si puede, y sobre que fila.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL ORDEN DE LAS TRES COMPROBACIONES ES EL RESULTADO QUE SE DEVUELVE, asi que no es libre.
    /// Primero la sesion (401), despues la existencia (404), despues el permiso (403) y solo al
    /// final la baja logica (404). Comprobar el permiso antes que la existencia contestaria 403
    /// sobre organizaciones que no existen, y eso convierte la respuesta en un buscador de
    /// identificadores validos.
    /// </para>
    /// <para>
    /// LA REGLA DE PERMISO ES LA MISMA QUE EN EL RESTO DEL CIRCUITO EXTERNO: fila en
    /// <c>dbo.UsuariosEntidades</c> con <c>RolEntidad = 'administrador'</c> y <c>Activo = 1</c>. Se
    /// comprueba en CADA peticion y no al entrar: una marca en el tiquete se comprueba una vez.
    /// </para>
    /// </remarks>
    private static async Task<(IResult? Resultado, int PersonaId, EntityProfileRow? Entidad)> ResolverAccesoAsync(
        ClaimsPrincipal principal,
        int organizacionId,
        PnmcDbContext dbContext,
        bool paraEscribir,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId))
        {
            return (Results.Unauthorized(), 0, null);
        }

        // La lectura va sin seguimiento y la escritura con el: sin la entidad rastreada, el PUT
        // guardaria un objeto que el contexto no conoce y no escribiria nada.
        IQueryable<EntityProfileRow> consulta = paraEscribir
            ? dbContext.EntityProfiles
            : dbContext.EntityProfiles.AsNoTracking();
        var entidad = await consulta.FirstOrDefaultAsync(item => item.Id == organizacionId, cancellationToken);
        if (entidad is null) return (Results.NotFound(), personaId, null);

        var administra = await dbContext.UserEntities.AsNoTracking().AnyAsync(
            item => item.UserId == personaId
                && item.EntityId == organizacionId
                && RolesDeEntidad.QueResponden.Contains(item.EntityRole)
                && item.IsActive,
            cancellationToken);
        if (!administra) return (Results.Forbid(), personaId, null);

        // Una organizacion dada de baja no se edita ni se lee desde fuera, y la respuesta es 404 y
        // no 403: para quien administra, una fila desactivada es una fila que ya no esta.
        if (!entidad.IsActive) return (Results.NotFound(), personaId, null);

        return (null, personaId, entidad);
    }

    private static async Task<PerfilOrganizacionDto> ConstruirPerfilAsync(
        EntityProfileRow entidad,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // EL NOMBRE DEL TERRITORIO LO RESUELVE EL API. La alternativa era que el panel se bajara
        // /divipola entero —1.122 filas— para imprimir dos nombres en una tarjeta de contexto.
        string? nombreDepartamentoSede = null;
        string? nombreMunicipioSede = null;
        var departamentoSede = AltaDeOrganizacion.NormalizarOpcional(entidad.HeadquartersDepartmentCode);
        var municipioSede = AltaDeOrganizacion.NormalizarOpcional(entidad.HeadquartersMunicipalityCode);

        // La sede es el único territorio propio de una organización. El alcance corresponde a
        // cada proceso del ecosistema, por ejemplo a un Festival, y no se duplica aquí.
        if (departamentoSede is not null)
        {
            nombreDepartamentoSede = await dbContext.DivipolaLocations.AsNoTracking()
                .Where(item => item.DepartmentCode == departamentoSede)
                .Select(item => item.DepartmentName)
                .FirstOrDefaultAsync(cancellationToken);

            if (municipioSede is not null)
            {
                nombreMunicipioSede = await dbContext.DivipolaLocations.AsNoTracking()
                    .Where(item => item.DepartmentCode == departamentoSede && item.MunicipalityCode == municipioSede)
                    .Select(item => item.MunicipalityName)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        // LA ETIQUETA DEL ESTADO SALE DE dbo.EstadosContenido Y NO DE UN DICCIONARIO EN C#. Es la
        // tabla a la que apunta FK_Entidades_EstadosContenido, asi que es la unica lista que no
        // puede desincronizarse del dato. Si el codigo no estuviera en la tabla se devuelve el
        // codigo crudo: se vera feo, que es mejor que verse vacio.
        var estado = (entidad.StatusCode ?? string.Empty).Trim();
        var estadoEnMinuscula = estado.ToLowerInvariant();
        var etiqueta = await dbContext.ContentStatuses.AsNoTracking()
            .Where(item => item.Code == estadoEnMinuscula)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return new PerfilOrganizacionDto(
            entidad.Id.ToString(CultureInfo.InvariantCulture),
            entidad.Name,
            entidad.LegalName,
            entidad.IdentificationNumber,
            entidad.IdentificationType,
            entidad.Description,
            entidad.ContactEmail,
            entidad.ContactPhone,
            entidad.WebsiteUrl,
            entidad.FacebookUrl,
            entidad.InstagramUrl,
            entidad.OtherUrl,
            entidad.AddressText,
            departamentoSede,
            nombreDepartamentoSede,
            municipioSede,
            nombreMunicipioSede,
            estado,
            string.IsNullOrWhiteSpace(etiqueta) ? estado : etiqueta,
            entidad.UpdatedAt,
            // LA DIRECCION SE ARMA AQUI Y NO SE GUARDA. El banco sirve por identificador; componer
            // la ruta en la lectura evita una segunda fuente de verdad que se quedaria vieja el dia
            // que esa ruta cambie.
            entidad.ArchivoFotoId is int foto
                ? "/api/v1/publico/archivos/" + foto.ToString(CultureInfo.InvariantCulture)
                : null,
            entidad.ArchivoFotoId);
    }

    private static async Task<Dictionary<string, string[]>> ValidarPerfilAsync(
        PerfilOrganizacionSolicitud solicitud,
        EntityProfileRow entidad,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        ComprobarLargo(errores, "name", solicitud.Nombre, LargoNombre, "El nombre de la organización");
        ComprobarLargo(errores, "nombreLegal", solicitud.NombreLegal, LargoNombreLegal, "El nombre legal");
        ComprobarLargo(errores, "identificationNumber", solicitud.NumeroIdentificacion, LargoIdentificacion, "La identificación");
        ComprobarLargo(errores, "descripcion", solicitud.Descripcion, LargoDescripcion, "La descripción");
        ComprobarLargo(errores, "contactEmail", solicitud.CorreoContacto, LargoCorreo, "El correo de contacto");
        ComprobarLargo(errores, "telefonoContacto", solicitud.TelefonoContacto, LargoTelefono, "El teléfono de contacto");
        ComprobarLargo(errores, "sitioWeb", solicitud.SitioWeb, LargoEnlace, "El sitio web");
        ComprobarLargo(errores, "facebook", solicitud.Facebook, LargoEnlace, "El enlace de Facebook");
        ComprobarLargo(errores, "instagram", solicitud.Instagram, LargoEnlace, "El enlace de Instagram");
        ComprobarLargo(errores, "otroEnlace", solicitud.OtroEnlace, LargoEnlace, "El otro enlace");
        ComprobarLargo(errores, "direccion", solicitud.Direccion, LargoDireccion, "La dirección");

        ComprobarEnlace(errores, "sitioWeb", solicitud.SitioWeb, "El sitio web");
        ComprobarEnlace(errores, "facebook", solicitud.Facebook, "El enlace de Facebook");
        ComprobarEnlace(errores, "instagram", solicitud.Instagram, "El enlace de Instagram");
        ComprobarEnlace(errores, "otroEnlace", solicitud.OtroEnlace, "El otro enlace");

        // LA UNICIDAD DEL NIT NO SE VUELVE A ESCRIBIR AQUI, PERO TAMPOCO SE PUEDE PASAR TAL CUAL.
        // AltaDeOrganizacion.ValidarAsync pregunta «¿alguna entidad tiene ya este numero?» SIN
        // excluir a la que se esta editando, porque nacio para dar de alta, donde no hay ninguna que
        // excluir. Guardar el perfil sin tocar el NIT devolveria «ya existe una organizacion
        // registrada con esta identificacion»: la propia, y sin manera de salir de ahi.
        //
        // La salida no es copiar la regla —una copia de una regla es una regla que va a divergir—
        // sino no preguntar cuando no hay nada que preguntar: el numero solo viaja a la validacion
        // cuando CAMBIA. Si no cambia, ya se comprobo el dia del alta.
        var identificacionActual = AltaDeOrganizacion.NormalizarIdentificacion(entidad.IdentificationNumber);
        var identificacionPedida = AltaDeOrganizacion.NormalizarIdentificacion(solicitud.NumeroIdentificacion);
        var identificacionQueCambia = string.Equals(identificacionPedida, identificacionActual, StringComparison.Ordinal)
            ? null
            : identificacionPedida;

        // El resto —nombre obligatorio, formato del correo y formato del NIT— es exactamente la
        // regla del alta, en el sitio donde ya vive. El
        // prefijo va vacio y la clave del correo tambien porque este formulario llama a sus campos
        // como los llama aquella ruta: name, contactEmail, identificationNumber.
        await AltaDeOrganizacion.ValidarAsync(
            errores,
            string.Empty,
            null,
            solicitud.Nombre,
            identificacionQueCambia,
            solicitud.CorreoContacto,
            dbContext,
            cancellationToken,
            idQueSeExcluye: entidad.Id);

        await AltaDeOrganizacion.ValidarSedeAsync(
            errores, solicitud.CodigoDepartamentoSede, solicitud.CodigoMunicipioSede,
            dbContext, cancellationToken);

        return errores;
    }

    private static Dictionary<string, string[]> ValidarResponsable(ResponsableOrganizacionSolicitud solicitud)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (ValidationHelpers.IsMissing(solicitud.ResponsableNombre))
        {
            errores["responsableNombre"] = ["El nombre de la persona responsable es obligatorio."];
        }

        ComprobarLargo(errores, "responsableNombre", solicitud.ResponsableNombre, LargoResponsableNombre, "El nombre de la persona responsable");
        ComprobarLargo(errores, "responsableTelefono", solicitud.ResponsableTelefono, LargoTelefono, "El teléfono de la persona responsable");

        var tipo = (solicitud.ResponsableTipoDocumento ?? string.Empty).Trim();
        if (!TiposDeDocumento.ContainsKey(tipo))
        {
            errores["responsableTipoDocumento"] = ["El tipo de documento no pertenece al catálogo."];
        }

        // MISMA NORMA QUE EL ALTA: solo digitos, y entre 6 y 15. Quien escribe su cedula la escribe
        // como la lee —«1.020.304.050»— y quien la busca despues la teclea seguida; guardar las dos
        // formas es no poder cruzarlas nunca.
        var documento = AltaDeOrganizacion.NormalizarDocumento(solicitud.ResponsableNumeroDocumento);
        if (documento.Length is < 6 or > 15)
        {
            errores["responsableNumeroDocumento"] = ["El número de documento debe tener entre 6 y 15 dígitos."];
        }

        return errores;
    }

    private static void ComprobarLargo(
        Dictionary<string, string[]> errores,
        string clave,
        string? valor,
        int largo,
        string etiqueta)
    {
        if (errores.ContainsKey(clave)) return;
        if (string.IsNullOrWhiteSpace(valor)) return;
        if (valor.Trim().Length <= largo) return;

        errores[clave] = [$"{etiqueta} no puede pasar de {largo.ToString(CultureInfo.InvariantCulture)} caracteres."];
    }

    private static void ComprobarEnlace(Dictionary<string, string[]> errores, string clave, string? valor, string etiqueta)
    {
        if (errores.ContainsKey(clave)) return;
        if (ValidationHelpers.IsValidHttpUrl(valor)) return;

        errores[clave] = [$"{etiqueta} debe empezar por http:// o https://."];
    }

    /// <summary>Texto que puede faltar: en blanco se guarda como NULL, no como cadena vacia.</summary>
    private static string? TextoOpcional(string? valor, int largo) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, largo);

    private static string NormalizarTipoDocumento(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant();

    /// <remarks>
    /// UN 404 Y NO UNA FILA EN BLANCO. <c>dbo.EntidadesResponsable</c> nacio
    /// y solo la escribe el alta externa: hoy tiene 2 filas frente a 19 entidades. Devolver campos
    /// vacios haria pasar «nadie ha declarado responsable» por «el responsable no tiene nombre», y
    /// el panel pintaria un formulario que no corrige nada.
    /// </remarks>
    private static IResult SinResponsable() => Results.NotFound(new
    {
        message = "Esta organización todavía no tiene una persona responsable declarada. "
            + "El equipo del PNMC es quien puede registrarla."
    });

    private static ResponsableOrganizacionDto ADto(EntidadResponsableRow fila)
    {
        // EL CODIGO DEL TIPO DE DOCUMENTO SE DEVUELVE EN MINUSCULA AUNQUE LA FILA DIGA «CC».
        // dbo.TiposDocumento guarda 'cc' y AltaDeOrganizacion escribe 'CC'; no hay clave foranea
        // entre las dos tablas. El desplegable del panel se llena con los codigos del catalogo, asi
        // que devolver 'CC' dejaria el campo sin ninguna opcion seleccionada y la persona creeria
        // que su tipo de documento se perdio.
        var codigo = NormalizarTipoDocumento(fila.ResponsableTipoDocumento);
        return new ResponsableOrganizacionDto(
            fila.IdEntidad.ToString(CultureInfo.InvariantCulture),
            fila.ResponsableNombre,
            codigo,
            TiposDeDocumento.TryGetValue(codigo, out var etiqueta) ? etiqueta : codigo,
            fila.ResponsableNumeroDocumento,
            fila.ResponsableCorreo,
            fila.ResponsableTelefono,
            fila.ResponsableDesde,
            fila.ResponsableAutorizacionDatos);
    }

    /// <remarks>
    /// EL VERBO NO ES EL DEL EVENTO, Y ESO LO IMPONE LA BASE. <c>CK_BitacoraAuditoria_Accion</c>
    /// cierra la columna a trece verbos tecnicos —medido en PNMC_LOCAL—, y
    /// ni <c>actualizar_perfil_organizacion</c> ni <c>actualizar_responsable_organizacion</c> estan
    /// entre ellos: escribirlos tal cual haria fallar el guardado ENTERO contra SQL Server mientras
    /// pasa contra SQLite, que es donde corre casi toda la suite. Es el mismo defecto que tumbaba
    /// todo el circuito externo de Festival hasta el 22 de agosto; ver <see cref="AccionesAuditoria"/>.
    ///
    /// EL NOMBRE DEL EVENTO NO SE PIERDE: viaja en <c>ValoresNuevos</c>, que la base exige que sea
    /// JSON valido (<c>CHECK isjson(ValoresNuevos) = 1</c>), asi que una consulta a la bitacora
    /// puede seguir distinguiendo el cambio del perfil del cambio de la persona responsable.
    /// </remarks>
    private static void RegistrarAuditoriaExterna(
        PnmcDbContext dbContext,
        int personaId,
        string tabla,
        int registroId,
        string evento,
        DateTime fecha)
    {
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = personaId,
            TableName = tabla,
            RecordId = registroId.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.Actualizar,
            NewValuesJson = JsonSerializer.Serialize(new { Evento = evento, OrganizacionId = registroId }),
            CreatedAt = fecha,
        });
    }
}
