using PNMC.Api.Security;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Observability;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// La nómina del equipo del PNMC (incremento 2B).
/// <para>
/// Va aparte de los 238 textos porque no es texto: es una lista ordenada de
/// personas con fotografía, que se guarda y se publica entera. Comparte con
/// aquellos las reglas que sí son comunes —quién edita, quién publica, borrador
/// separado de publicado, y no escribir si nada cambió—.
/// </para>
/// <para>
/// Deliberadamente no hay aquí retiro ni historial: el retiro del incremento 2D
/// se diseñó por clave de texto, y aplicarlo a una nómina entera es una decisión
/// distinta que merece su propia revisión.
/// </para>
/// </summary>
public static class EquipoWebEndpoints
{
    private static readonly string[] EditorRoles = ["webmaster", "gestor_interno"];
    private static readonly string[] PublisherRoles = ["webmaster"];

    /// <summary>
    /// PNMC-039. Categoria de registro de la escritura administrativa. Segundo de los tres
    /// endpoints instrumentados como patron: es una escritura que publica contenido al sitio
    /// abierto, es decir la clase de accion sobre la que alguien preguntara "¿quien puso esto?".
    /// </summary>
    private const string CategoriaRegistro = "PNMC.EscrituraAdministrativa";

    /// <summary>
    /// Lo que el panel envía. <c>Version</c> es anulable a propósito: si el cliente
    /// no la manda, se le responde 400 y se le pide recargar, en vez de aplicar el
    /// guardado a ciegas. Un 409 significaría «alguien se le adelantó», que no es
    /// lo que pasó.
    /// </summary>
    public sealed record GuardarEquipoWebSolicitud(
        IReadOnlyList<MiembroDelEquipoWeb?>? Members,
        bool Publish,
        int? Version);

    public static RouteGroupBuilder MapEquipoWebEndpoints(this RouteGroupBuilder group)
    {
        // ---------- Lectura pública ----------
        var publico = group.MapGroup("/equipo-web").WithTags("equipo-web");

        // ANONIMA A PROPOSITO (39 de 39). La nomina publicada del equipo, que alimenta la
        // pagina abierta "Equipo PNMC". Gemela de GET /contenido-web: el front-end la pide
        // sin sesion y solo lee la columna Published, es decir la version que un webmaster
        // decidio publicar. El borrador y el historial viven en /admin/equipo-web, que si
        // exige sesion (linea 65 de este fichero).
        //
        // DATOS DE CONTACTO — OCTAVA LECTURA, ANOTADA APARTE: MiembroDelEquipoWeb incluye Email
        // (ContratoDeEquipoWeb.cs), asi que esta ruta expone correos de personas sin
        // pedir sesion. Es distinta de las siete del catalogo: aqui el correo es
        // institucional y se publica a proposito, un webmaster tuvo que pulsar publicar
        // para que saliera. Se deja anonima y se anota igualmente, para que la decision
        // pendiente del dueno sobre que campos son publicos la contemple y no se le
        // escape por venir de otro fichero.
        publico.MapGet("/", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var published = await dbContext.EquipoWeb
                .AsNoTracking()
                .Select(x => x.Published)
                .FirstOrDefaultAsync(cancellationToken);

            // Contrato explícito para que el front-end distinga los dos casos que
            // de otro modo se verían iguales: `members: null` es «no hay nómina
            // publicada, use la compilada»; `members: []` es «se publicó una
            // nómina vacía a propósito, no muestre a nadie».
            if (published is null)
            {
                return Results.Ok(new { published = false, members = (object?)null });
            }

            return Results.Ok(new { published = true, members = ContratoDeEquipoWeb.Deserialize(published) });
        })
        .WithName("GetPublicWebTeam")
        .AllowAnonymous();

        // ---------- Consola de administración ----------
        var admin = group.MapGroup("/admin/equipo-web").WithTags("admin-equipo-web");
        admin.RequireAuthorization();
        admin.ExigeModulo("gestion-sitio");

        admin.MapGet("/", async (
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var row = await dbContext.EquipoWeb.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                // La siembra la crea al arrancar. Si falta, el editor no debe ver
                // una nómina vacía que al guardar pise lo que hubiera: se avisa.
                return Results.NotFound(new
                {
                    message = "La nómina todavía no está inicializada en la base de datos.",
                });
            }

            return Results.Ok(new
            {
                members = ContratoDeEquipoWeb.Deserialize(row.Draft),
                publishedMembers = row.Published is null ? null : ContratoDeEquipoWeb.Deserialize(row.Published),
                state = row.Published is null ? "no_publicado" : "publicado",
                version = row.Version,
                updatedBy = row.UpdatedBy,
                updatedAt = row.UpdatedAt,
                // Los topes viajan con la nómina para que el panel no tenga que
                // repetirlos. Los tres de longitud se añadieron en agosto de
                // 2026: el panel los llevaba escritos a mano y se había quedado
                // en 120 para los tres cuando el servidor ya aceptaba 160 en el
                // cargo y 180 en el correo. El efecto era que un cargo
                // institucional largo no se podía ni teclear, y nada lo delataba
                // porque las dos validaciones nunca se comparan entre sí.
                limits = new
                {
                    maxMembers = ContratoDeEquipoWeb.MaxMembers,
                    maxPhotoChars = ContratoDeEquipoWeb.MaxPhotoChars,
                    maxSerializedChars = ContratoDeEquipoWeb.MaxSerializedChars,
                    maxNameLength = ContratoDeEquipoWeb.MaxNameLength,
                    maxRoleLength = ContratoDeEquipoWeb.MaxRoleLength,
                    maxEmailLength = ContratoDeEquipoWeb.MaxEmailLength,
                },
            });
        })
        .WithName("GetWebTeamDraft");

        admin.MapPost("/", async (
            GuardarEquipoWebSolicitud request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            ILoggerFactory registros,
            CancellationToken cancellationToken) =>
        {
            // PNMC-039. PATRON DE INSTRUMENTACION, ESCRITURA ADMINISTRATIVA.
            //
            // QUE SE REGISTRA: quien (seudonimo), que accion (guardar o publicar), de que version
            // a cual, cuantos miembros quedaron y cuanto tardo. Y, con el mismo detalle, los
            // rechazos: un 403 por rol y un 409 por choque de versiones son exactamente las dos
            // cosas de las que alguien se va a quejar por telefono, y hasta hoy no dejaban rastro.
            //
            // QUE NO SE REGISTRA: el CONTENIDO guardado. La nomina lleva nombres, cargos, correos
            // institucionales y fotografias en base64 — volcar el cuerpo aqui duplicaria un
            // fichero de datos personales en el registro, con megabytes por guardado. Se registra
            // el HECHO y su forma (cuantos, que version), nunca el dato. Esa es la regla general
            // de la guia: el registro dice que paso, la base de datos dice que hay.
            //
            // EL AUTOR VA SEUDONIMIZADO aunque sea personal institucional: row.UpdatedBy si
            // guarda el nombre en claro, porque el panel lo muestra y para eso existe. Un registro
            // no tiene ese permiso, y ademas el seudonimo basta para lo unico que se le pide
            // —agrupar las acciones de la misma persona—.
            var registro = registros.CreateLogger(CategoriaRegistro);
            var reloj = Stopwatch.StartNew();
            var autor = RedaccionDatosPersonales.Persona(ResolveAuthor(principal));

            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null)
            {
                registro.LogWarning(
                    "Guardado de la nomina rechazado. Motivo {Motivo}, autor {Autor}, {DuracionMs} ms",
                    "antiforgery_invalido",
                    autor,
                    reloj.ElapsedMilliseconds);

                return csrf;
            }

            if (!HasAnyRole(principal, EditorRoles))
            {
                registro.LogWarning(
                    "Guardado de la nomina rechazado. Motivo {Motivo}, autor {Autor}, {DuracionMs} ms",
                    "sin_rol_de_edicion",
                    autor,
                    reloj.ElapsedMilliseconds);

                return Results.Forbid();
            }

            if (request.Publish && !HasAnyRole(principal, PublisherRoles))
            {
                registro.LogWarning(
                    "Guardado de la nomina rechazado. Motivo {Motivo}, autor {Autor}, {DuracionMs} ms",
                    "sin_rol_de_publicacion",
                    autor,
                    reloj.ElapsedMilliseconds);

                return Results.Forbid();
            }

            if (request.Version is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["version"] = ["Falta la versión de la nómina que está editando. Vuelva a cargarla antes de guardar."],
                });
            }

            // Validar antes de leer la fila: un cuerpo desmedido se rechaza sin
            // haber tocado la base de datos.
            var errors = ContratoDeEquipoWeb.Validate(request.Members, out var members);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var row = await dbContext.EquipoWeb.FirstOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                return Results.NotFound(new
                {
                    message = "La nómina todavía no está inicializada en la base de datos.",
                });
            }

            if (row.Version != request.Version.Value)
            {
                registro.LogWarning(
                    "Guardado de la nomina rechazado. Motivo {Motivo}, autor {Autor}, version en curso {VersionEnServidor}, version enviada {VersionEnviada}, {DuracionMs} ms",
                    "choque_de_versiones",
                    autor,
                    row.Version,
                    request.Version.Value,
                    reloj.ElapsedMilliseconds);

                return Results.Conflict(new
                {
                    message = "Otra persona modificó la nómina mientras usted la editaba. Vuelva a cargarla antes de guardar.",
                    version = row.Version,
                });
            }

            var content = ContratoDeEquipoWeb.Serialize(members);

            // Mismo predicado que en los 238 textos: el panel envía la nómina
            // entera en cada guardado, así que sin esta comparación cada clic
            // subiría la versión y el siguiente editor chocaría sin motivo.
            var draftChanged = row.Draft != content;
            var publishChanged = request.Publish && row.Published != content;
            if (!draftChanged && !publishChanged)
            {
                return Results.Ok(new
                {
                    changed = false,
                    published = request.Publish,
                    count = members.Count,
                    version = row.Version,
                });
            }

            var versionAnterior = row.Version;
            row.Draft = content;
            if (request.Publish)
            {
                row.Published = content;
            }
            row.Version++;
            row.UpdatedBy = ResolveAuthor(principal);
            row.UpdatedAt = DateTime.UtcNow;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                registro.LogWarning(
                    "Guardado de la nomina rechazado. Motivo {Motivo}, autor {Autor}, version en curso {VersionEnServidor}, {DuracionMs} ms",
                    "choque_de_concurrencia_al_grabar",
                    autor,
                    versionAnterior,
                    reloj.ElapsedMilliseconds);

                return Results.Conflict(new
                {
                    message = "Otra persona modificó la nómina mientras usted la editaba. Vuelva a cargarla antes de guardar.",
                });
            }

            if (registro.IsEnabled(LogLevel.Information))
            {
                registro.LogInformation(
                    "Nomina del equipo escrita. Accion {Accion}, autor {Autor}, miembros {Miembros}, version {VersionAnterior} a {VersionNueva}, {DuracionMs} ms",
                    request.Publish ? "publicar" : "guardar_borrador",
                    autor,
                    members.Count,
                    versionAnterior,
                    row.Version,
                    reloj.ElapsedMilliseconds);
            }

            return Results.Ok(new
            {
                changed = true,
                published = request.Publish,
                count = members.Count,
                version = row.Version,
            });
        })
        .WithName("SaveWebTeam")
        // Cada guardado puede traer megabytes de fotografías. El tope de tamaño ya
        // acota una peticion; esto acota cuantas.
        .RequireRateLimiting("web-team-save");

        return group;
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
