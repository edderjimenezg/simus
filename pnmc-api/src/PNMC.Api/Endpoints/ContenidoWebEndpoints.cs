using PNMC.Api.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Textos editables del sitio público.
/// <para>
/// Dos rutas: una lectura anónima que sirve lo publicado, y un guardado por
/// grupo que solo alcanzan los roles de administración. Deliberadamente no hay
/// aquí historial, importación, medios ni nómina del equipo: cada uno de esos
/// entra como incremento propio, revisable por separado.
/// </para>
/// </summary>
public static class ContenidoWebEndpoints
{
    /// <summary>Roles que pueden guardar un borrador.</summary>
    private static readonly string[] EditorRoles = ["webmaster", "gestor_interno"];

    /// <summary>
    /// Roles que pueden publicar al sitio público. Es más estrecho que el de
    /// edición, para no contradecir la regla que el propio repositorio ya aplica
    /// al contenido editorial (AdminDataEndpoints.RolPuedeAplicar, donde
    /// gestor_interno nunca alcanza el estado "publicado").
    /// </summary>
    private static readonly string[] PublisherRoles = ["webmaster"];

    public sealed record CampoDeContenidoWebCuerpo(string Key, string Content);

    public sealed record GuardarGrupoDeContenidoWebSolicitud(
        IReadOnlyList<CampoDeContenidoWebCuerpo>? Fields,
        bool Publish);

    public sealed record CampoDeContenidoWebVista(
        string Key,
        string Label,
        int Limit,
        string Draft,
        string? Published,
        /// <summary>publicado | retirado | no_publicado. Derivado, nunca almacenado.</summary>
        string State,
        int Version,
        string UpdatedBy,
        DateTime UpdatedAt);

    public static RouteGroupBuilder MapContenidoWebEndpoints(this RouteGroupBuilder group)
    {
        // ---------- Lectura pública ----------
        var publico = group.MapGroup("/contenido-web").WithTags("contenido-web");

        // ANONIMA A PROPOSITO (38 de 39). Los textos del sitio. Es la ruta mas publica que
        // existe en el API: el front-end la pide al arrancar CUALQUIER pagina, incluida la
        // de inicio y la de login, para pintar sus 238 cadenas. Exigir sesion aqui deja el
        // sitio entero en su texto compilado de respaldo para todo visitante, que es la
        // averia mas silenciosa posible.
        //
        // Que no se filtre nada lo garantiza el propio Where, no la sesion: solo devuelve
        // filas con Published != null, es decir texto que un webmaster decidio publicar.
        // El borrador, el historial, el retirado y quien edito viven en el grupo
        // /admin/contenido-web de mas abajo, que si exige sesion (linea 73).
        publico.MapGet("/", async (PnmcDbContext dbContext, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            var publicadas = dbContext.ContenidoWeb.AsNoTracking().Where(x => x.Published != null);

            // Firma barata del estado publicado, para poder contestar 304.
            //
            // Esta ruta la pide el front-end al arrancar CUALQUIER pagina, y devuelve
            // el diccionario entero de textos del sitio. Con un ETag, el visitante que
            // ya lo tiene manda `If-None-Match` y recibe una respuesta SIN CUERPO: la
            // segunda pagina que abre no vuelve a descargar los textos del portal.
            //
            // Los tres agregados se calculan en una sola consulta y ninguno recorre el
            // texto: cuantas claves hay publicadas, cuando se toco la ultima y la suma
            // de versiones. Publicar, retirar o reeditar mueve al menos uno de los
            // tres, asi que un cambio nunca puede pasar por «no modificado».
            //
            // Se descarto hashear el contenido: obligaria a leer y recorrer todos los
            // textos justo para decidir si hace falta enviarlos, que es lo contrario
            // de lo que se busca.
            var firma = await publicadas
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    Cuantas = grupo.Count(),
                    UltimoCambio = grupo.Max(x => x.UpdatedAt),
                    SumaVersiones = grupo.Sum(x => x.Version),
                })
                .FirstOrDefaultAsync(cancellationToken);

            var etag = firma is null
                ? "\"vacio\""
                : $"\"{firma.Cuantas:x}-{firma.UltimoCambio.Ticks:x}-{firma.SumaVersiones:x}\"";

            // `no-cache` no significa «no guardes»: significa «guarda, pero preguntame
            // antes de usarlo». Es lo correcto aqui —publicar un texto tiene que verse
            // enseguida— y es justo lo que hace util al ETag.
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.ETag = etag;

            if (httpContext.Request.Headers.IfNoneMatch.Contains(etag))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            // Solo lo publicado. Una clave sin publicar no aparece, y el front-end
            // sirve su texto compilado: así el sitio nunca queda en blanco.
            var published = await publicadas
                .Select(x => new { x.Key, x.Published })
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                texts = published.ToDictionary(x => x.Key, x => x.Published!),
                count = published.Count,
            });
        })
        .WithName("GetPublicWebContent")
        .AllowAnonymous();

        // ---------- Consola de administración ----------
        var admin = group.MapGroup("/admin/contenido-web").WithTags("admin-contenido-web");
        admin.RequireAuthorization();

        // El token que la consola debe adjuntar en cada escritura, en la cabecera
        // X-CSRF-TOKEN. Va bajo el grupo autenticado a propósito: solo tiene
        // sentido para quien ya inició sesión.
        //
        // <b>Y FUERA DEL FILTRO DE MODULO, aunque su ruta viva en este grupo.</b> Este token no es
        // del CMS: es el ÚNICO de toda la consola —se decidió así para no tener dos cosas que
        // mantener— y lo pide cualquier pantalla antes de su primera escritura. Detrás de
        // «gestión del sitio» dejaría sin poder escribir a quien no tenga ese módulo, que es casi
        // todo el mundo. Se declara sobre el grupo padre para conservar su ruta y quedarse fuera.
        group.MapGet("/admin/contenido-web/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new { token = tokens.RequestToken ?? string.Empty });
        })
        .WithName("GetWebContentCsrfToken")
        .WithTags("admin-contenido-web")
        .RequireAuthorization();

        // Y EL RESTO DEL GRUPO SI: leer o escribir los textos del sitio es «gestión del sitio».
        admin.ExigeModulo("gestion-sitio");

        // Una sola lectura para el inicio y la bandeja de pendientes de Gestión del sitio.
        // El panel anterior solo podía saber el estado abriendo uno por uno sus 26 grupos de
        // texto y 13 de imágenes. Este resumen evita cuarenta peticiones y no trae textos,
        // fotografías ni bytes: únicamente conteos, nombres de grupo y fechas operativas.
        admin.MapGet("/summary", async (
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var valoresDeFabrica = SembradorDeContenidoWeb.LoadCatalog()
                .ToDictionary(x => x.Key, x => x.DefaultValue, StringComparer.Ordinal);

            var textos = await dbContext.ContenidoWeb.AsNoTracking()
                .Select(x => new
                {
                    x.Key,
                    x.GroupId,
                    x.GroupLabel,
                    x.Section,
                    x.Draft,
                    x.Published,
                    x.Retired,
                    x.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            var estadosDeTexto = textos.Select(x => new
            {
                x.GroupId,
                x.GroupLabel,
                x.Section,
                Publicado = x.Published is not null,
                Retirado = x.Published is null && x.Retired is not null,
                Pendiente = x.Draft != (x.Published
                    ?? valoresDeFabrica.GetValueOrDefault(x.Key, string.Empty)),
                x.UpdatedAt,
            }).ToList();

            var gruposDeTexto = estadosDeTexto
                .GroupBy(x => new { x.GroupId, x.GroupLabel, x.Section })
                .Select(x => new
                {
                    id = x.Key.GroupId,
                    label = x.Key.GroupLabel,
                    section = x.Key.Section,
                    total = x.Count(),
                    published = x.Count(y => y.Publicado),
                    pending = x.Count(y => y.Pendiente),
                    retired = x.Count(y => y.Retirado),
                    updatedAt = x.Max(y => y.UpdatedAt),
                })
                .OrderBy(x => x.section)
                .ThenBy(x => x.label)
                .ToList();

            // Solo metadatos de imagen. Nombrar las columnas de contenido aquí convertiría una
            // visita al resumen en una descarga de todos los originales guardados en SQL Server.
            var medios = await dbContext.ImagenesWeb.AsNoTracking()
                .Select(x => new
                {
                    x.GroupId,
                    x.GroupLabel,
                    x.Section,
                    x.DraftHash,
                    x.PublishedHash,
                    x.Retired,
                    x.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            var gruposDeMedios = medios
                .GroupBy(x => new { x.GroupId, x.GroupLabel, x.Section })
                .Select(x => new
                {
                    id = x.Key.GroupId,
                    label = x.Key.GroupLabel,
                    section = x.Key.Section,
                    total = x.Count(),
                    published = x.Count(y => y.PublishedHash != null),
                    pending = x.Count(y => y.DraftHash != null && y.DraftHash != y.PublishedHash),
                    retired = x.Count(y => y.PublishedHash == null && y.Retired != null),
                    updatedAt = x.Max(y => y.UpdatedAt),
                })
                .OrderBy(x => x.section)
                .ThenBy(x => x.label)
                .ToList();

            var equipo = await dbContext.EquipoWeb.AsNoTracking()
                .Select(x => new { x.Draft, x.Published, x.UpdatedAt })
                .FirstOrDefaultAsync(cancellationToken);
            var equipoDeFabrica = ContratoDeEquipoWeb.Serialize(SembradorDeContenidoWeb.LoadTeamDefaults());
            var miembros = equipo is null ? 0 : ContratoDeEquipoWeb.Deserialize(equipo.Draft).Count;
            var equipoPendiente = equipo is not null
                && equipo.Draft != (equipo.Published ?? equipoDeFabrica);

            var textosPendientes = estadosDeTexto.Count(x => x.Pendiente);
            var mediosPendientes = medios.Count(x => x.DraftHash != null && x.DraftHash != x.PublishedHash);

            return Results.Ok(new
            {
                generatedAt = DateTime.UtcNow,
                pendingTotal = textosPendientes + mediosPendientes + (equipoPendiente ? 1 : 0),
                texts = new
                {
                    total = estadosDeTexto.Count,
                    published = estadosDeTexto.Count(x => x.Publicado),
                    pending = textosPendientes,
                    retired = estadosDeTexto.Count(x => x.Retirado),
                    groups = gruposDeTexto,
                },
                images = new
                {
                    total = medios.Count,
                    published = medios.Count(x => x.PublishedHash != null),
                    pending = mediosPendientes,
                    retired = medios.Count(x => x.PublishedHash == null && x.Retired != null),
                    groups = gruposDeMedios,
                },
                team = new
                {
                    members = miembros,
                    published = equipo?.Published is not null,
                    pending = equipoPendiente,
                    updatedAt = equipo?.UpdatedAt,
                },
            });
        })
        .WithName("GetWebSiteManagementSummary");

        admin.MapGet("/{key}/history", async (
            string key,
            int? limit,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            // El tope se acota aquí: sin límite, una clave muy editada devolvería
            // su historia entera en cada apertura del panel.
            var take = Math.Clamp(limit ?? 20, 1, 100);

            var entries = await dbContext.HistorialDeContenidoWeb
                .AsNoTracking()
                .Where(x => x.Key == key)
                .OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new { x.Action, x.Value, x.User, x.At })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { key, entries });
        })
        .WithName("GetWebContentHistory");

        admin.MapGet("/groups/{groupId}/history", async (
            string groupId,
            int? perKey,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            // El panel abre un grupo entero, así que pedir el historial clave por
            // clave serían 27 peticiones para pintar una pantalla. Va en una.
            var take = Math.Clamp(perKey ?? 10, 1, 50);

            var keys = await dbContext.ContenidoWeb.AsNoTracking()
                .Where(x => x.GroupId == groupId)
                .Select(x => x.Key)
                .ToListAsync(cancellationToken);

            if (keys.Count == 0)
            {
                return Results.NotFound(new { message = $"El grupo '{groupId}' no existe en el catálogo." });
            }

            // Se traen las entradas del grupo y se recortan por clave en memoria:
            // el recorte por grupo en SQL exigiría una ventana por clave, y aquí
            // el conjunto ya está acotado por el propio tamaño del grupo.
            var entries = await dbContext.HistorialDeContenidoWeb.AsNoTracking()
                .Where(x => keys.Contains(x.Key))
                .OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
                .Take(keys.Count * take)
                .Select(x => new { x.Key, x.Action, x.Value, x.User, x.At })
                .ToListAsync(cancellationToken);

            var byKey = entries
                .GroupBy(x => x.Key)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.Take(take)
                        .Select(x => new { x.Action, x.Value, x.User, x.At })
                        .ToList());

            return Results.Ok(new { groupId, byKey });
        })
        .WithName("GetWebContentGroupHistory");

        admin.MapGet("/groups/{groupId}", async (
            string groupId,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var rows = await dbContext.ContenidoWeb
                .AsNoTracking()
                .Where(x => x.GroupId == groupId)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Results.NotFound(new { message = $"El grupo '{groupId}' no existe en el catálogo." });
            }

            return Results.Ok(new
            {
                groupId,
                groupLabel = rows[0].GroupLabel,
                section = rows[0].Section,
                fields = rows.Select(x => new CampoDeContenidoWebVista(
                    x.Key, x.Label, x.CharacterLimit, x.Draft, x.Published, ResolveState(x), x.Version, x.UpdatedBy, x.UpdatedAt)),
            });
        })
        .WithName("GetWebContentGroup");

        admin.MapPost("/groups/{groupId}", async (
            string groupId,
            GuardarGrupoDeContenidoWebSolicitud request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            if (request.Publish && !HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var fields = request.Fields;
            if (fields is null || fields.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["fields"] = ["Debe enviar al menos un campo."],
                });
            }

            var rows = await dbContext.ContenidoWeb
                .Where(x => x.GroupId == groupId)
                .ToDictionaryAsync(x => x.Key, cancellationToken);

            if (rows.Count == 0)
            {
                return Results.NotFound(new { message = $"El grupo '{groupId}' no existe en el catálogo." });
            }

            // Validar TODO antes de escribir NADA: un grupo se guarda entero o no
            // se guarda, para que el editor no quede con la mitad publicada.
            var errors = new Dictionary<string, string[]>();
            foreach (var field in fields)
            {
                if (!rows.TryGetValue(field.Key, out var row))
                {
                    // Una clave ajena al grupo no se ignora en silencio: sería la vía
                    // para escribir en cualquier parte del sitio desde cualquier grupo.
                    errors[field.Key] = [$"La clave no pertenece al grupo '{groupId}'."];
                    continue;
                }

                var content = field.Content ?? string.Empty;
                if (content.Length > row.CharacterLimit)
                {
                    errors[field.Key] = [$"Excede el límite de {row.CharacterLimit} caracteres (recibidos {content.Length})."];
                }
                else if (content.Contains('<'))
                {
                    // Todas las claves se consumen por interpolación, que Angular escapa.
                    // Rechazar '<' evita que alguien intente inyectar marcado por una
                    // vía que hoy no lo renderiza, pero que mañana podría hacerlo.
                    errors[field.Key] = ["El contenido no admite el carácter '<'."];
                }
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var author = ResolveAuthor(principal);
            var now = DateTime.UtcNow;
            var changed = 0;

            foreach (var field in fields)
            {
                var row = rows[field.Key];
                var content = field.Content ?? string.Empty;

                // Una fila retirada del sitio no vuelve sola. Publicar el grupo
                // seguiría editando su borrador, pero no la republica: para eso
                // está la acción explícita de republicar.
                var canPublish = request.Publish && row.Retired is null;

                // El panel envía SIEMPRE todas las claves del grupo, no solo las
                // editadas. Sin esta comparación, dos editores en campos distintos
                // del mismo grupo chocarían siempre: cada guardado tocaría las 27
                // filas y subiría sus versiones.
                var draftChanged = row.Draft != content;
                var publishChanged = canPublish && row.Published != content;
                if (!draftChanged && !publishChanged)
                {
                    continue;
                }

                row.Draft = content;
                if (canPublish)
                {
                    row.Published = content;
                }

                row.Version++;
                row.UpdatedBy = author;
                row.UpdatedAt = now;
                changed++;

                // Entra en el mismo SaveChanges que el cambio, de modo que no
                // puede quedar un historial contando algo que no ocurrio.
                await GuardasDelCms.RecordHistoryAsync(
                    dbContext, field.Key,
                    canPublish ? GuardasDelCms.AccionPublicado : GuardasDelCms.AccionGuardado,
                    content, author, now, cancellationToken);
            }

            if (changed == 0)
            {
                return Results.Ok(new { groupId, changed = 0, published = request.Publish });
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Version es token de concurrencia: si otra persona guardó esa misma
                // clave entre la lectura y la escritura, EF no encuentra la fila y
                // llega aquí. Se avisa en vez de sobrescribir su trabajo.
                return Results.Conflict(new
                {
                    message = "Otra persona modificó este grupo mientras usted lo editaba. Vuelva a cargarlo antes de guardar.",
                });
            }

            return Results.Ok(new { groupId, changed, published = request.Publish });
        })
        .WithName("SaveWebContentGroup");

        // ---------- Retirar y republicar (incremento 2D) ----------

        admin.MapPost("/{key}/retire", async (
            string key,
            RetireRequest? request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            // Retirar cambia lo que ve el visitante, igual que publicar. Mismo rol.
            if (!HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var row = await dbContext.ContenidoWeb.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (row is null)
            {
                return Results.NotFound(new { message = $"La clave '{key}' no existe en el catálogo." });
            }

            if (row.Retired is not null)
            {
                return Results.Ok(BuildStateResponse(row, changed: false));
            }

            if (string.IsNullOrEmpty(row.Published))
            {
                // Nunca estuvo visible: no hay nada que retirar. Marcarlo igual
                // dejaría un estado "retirado" que nadie provocó.
                return Results.Conflict(new
                {
                    message = $"La clave '{key}' no está publicada, así que no hay nada que retirar.",
                    state = ResolveState(row),
                });
            }

            // El borrador se conserva a propósito: retirar quita el texto del
            // sitio, no lo borra del editor.
            var retiradoEn = DateTime.UtcNow;
            row.Published = null;
            row.Retired = retiradoEn;
            row.Version++;
            row.UpdatedBy = ResolveAuthor(principal);
            row.UpdatedAt = retiradoEn;

            // Valor nulo: retirar no reescribe el borrador. Lo que cuenta es la accion.
            await GuardasDelCms.RecordHistoryAsync(
                dbContext, key, GuardasDelCms.AccionRetirado, null, ResolveAuthor(principal), retiradoEn, cancellationToken);

            var saved = await TrySaveAsync(dbContext, cancellationToken);
            return saved ?? Results.Ok(BuildStateResponse(row, changed: true, request?.Reason));
        })
        .WithName("RetireWebContent");

        admin.MapPost("/{key}/republish", async (
            string key,
            RetireRequest? request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var row = await dbContext.ContenidoWeb.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (row is null)
            {
                return Results.NotFound(new { message = $"La clave '{key}' no existe en el catálogo." });
            }

            if (row.Retired is null)
            {
                return Results.Conflict(new
                {
                    message = $"La clave '{key}' no está retirada.",
                    state = ResolveState(row),
                });
            }

            // Republicar publica el borrador vigente, que pudo editarse mientras
            // el texto estaba retirado. Por eso se revalida con la regla de 2A:
            // el límite pudo cambiar en el catálogo desde el último guardado.
            var content = row.Draft;
            if (content.Length > row.CharacterLimit)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [key] = [$"El borrador excede el límite de {row.CharacterLimit} caracteres (tiene {content.Length}). Corríjalo antes de republicar."],
                });
            }
            if (content.Contains('<'))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [key] = ["El borrador contiene el carácter '<', que no se admite."],
                });
            }

            var republicadoEn = DateTime.UtcNow;
            row.Published = content;
            row.Retired = null;
            row.Version++;
            row.UpdatedBy = ResolveAuthor(principal);
            row.UpdatedAt = republicadoEn;

            await GuardasDelCms.RecordHistoryAsync(
                dbContext, key, GuardasDelCms.AccionRepublicado, content, ResolveAuthor(principal), republicadoEn, cancellationToken);

            var saved = await TrySaveAsync(dbContext, cancellationToken);
            return saved ?? Results.Ok(BuildStateResponse(row, changed: true, request?.Reason));
        })
        .WithName("RepublishWebContent");

        return group;
    }

    public sealed record RetireRequest(string? Reason);

    /// <summary>
    /// Estado derivado de las dos columnas. No se guarda: una tercera columna que
    /// dijera lo mismo podría contradecir a las otras dos.
    /// </summary>
    private static string ResolveState(ContenidoWebRow row)
    {
        if (!string.IsNullOrEmpty(row.Published)) { return "publicado"; }
        return row.Retired is not null ? "retirado" : "no_publicado";
    }

    private static object BuildStateResponse(ContenidoWebRow row, bool changed, string? reason = null)
        => new { key = row.Key, state = ResolveState(row), version = row.Version, changed, reason };

    /// <summary>
    /// Guarda y traduce el choque de concurrencia a 409. Devuelve null si todo fue
    /// bien, para que el llamador construya su propia respuesta.
    /// </summary>
    private static async Task<IResult?> TrySaveAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message = "Otra persona modificó esta clave mientras usted la editaba. Vuelva a cargarla antes de reintentar.",
            });
        }
    }

    private static bool HasAnyRole(ClaimsPrincipal principal, string[] roles)
    {
        return principal.Identity?.IsAuthenticated == true
            && roles.Any(principal.IsInRole);
    }

    private static string ResolveAuthor(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(name) ? "Desconocido" : name.Trim();
    }
}
