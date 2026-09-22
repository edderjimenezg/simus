using PNMC.Api.Security;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Imágenes administrables del sitio público.
/// <para>
/// Gemelo de <see cref="ContenidoWebEndpoints"/> y con el mismo ciclo: borrador, publicado,
/// retirado, republicado, historial. Lo que cambia es que aquí el valor son bytes, y eso trae
/// tres cosas que los textos no tienen: un reconocedor de formato que decide si eso es una
/// imagen (<see cref="MediosWebContrato"/>), una ruta que sirve el archivo con su ETag, y una
/// subida por multipart.
/// </para>
/// <para>
/// <b>SEMÁNTICA DE «RETIRAR», que es distinta de la de los textos y conviene tenerla clara.</b>
/// Retirar una imagen no deja un hueco: el front cae en la imagen compilada, que es la que el
/// sitio tenía antes de que esto existiera. Es lo que se quiere de un botón «retirar» sobre una
/// portada —volver a la de fábrica—, y es la razón por la que <c>getWebImage</c> nunca devuelve
/// cadena vacía.
/// </para>
/// </summary>
public static class ImagenesWebEndpoints
{
    /// <summary>Roles que pueden subir y guardar un borrador. Los mismos que en textos.</summary>
    private static readonly string[] EditorRoles = ["webmaster", "gestor_interno"];

    /// <summary>
    /// Roles que pueden cambiar lo que ve el visitante. Más estrecho que el de edición, por el
    /// mismo motivo que en <see cref="ContenidoWebEndpoints"/>.
    /// </summary>
    private static readonly string[] PublisherRoles = ["webmaster"];

    public sealed record MediaVersionRequest(int? Version);

    public sealed record MediaRetireRequest(string? Reason);

    public static RouteGroupBuilder MapImagenesWebEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapPublico(group);
        MapAdmin(group);
        return group;
    }

    // =============================================================================================
    // LECTURA PÚBLICA
    // =============================================================================================
    private static void MapPublico(RouteGroupBuilder group)
    {
        var publico = group.MapGroup("/imagenes-web").WithTags("imagenes-web");

        // ANONIMA A PROPOSITO. Gemela de GET /contenido-web: el front la pide al arrancar CUALQUIER
        // pagina, para saber que imagenes hay publicadas antes de pintar la primera. Exigir sesion
        // dejaria el sitio entero en sus imagenes compiladas para todo visitante.
        //
        // NO LLEVA BYTES. Son dieciseis entradas de metadatos, unos 2 KB. Los bytes se piden uno a
        // uno por la ruta de abajo, y solo los que la pagina pinta.
        publico.MapGet("/", async (PnmcDbContext dbContext, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            // Proyeccion explicita: no se nombra ni una columna varbinary. En SQL Server un valor
            // mayor de 8.000 bytes vive en paginas LOB aparte, y un SELECT que no lo nombra no las
            // lee. Materializar la entidad entera traeria aqui los treinta y dos blobs.
            var publicadas = await dbContext.ImagenesWeb
                .AsNoTracking()
                // FILTRA POR LA HUELLA Y NO POR EL CONTENIDO, y no es cosmetico. `WHERE
                // PublicadoContenido IS NOT NULL` NOMBRA la columna varbinary en el SQL; que SQL
                // Server pueda resolver esa comparacion sin bajar a las paginas LOB es una
                // propiedad del motor que este codigo no controla. `PublicadoHuella` es char(64),
                // vive siempre en fila, y el CHECK CK_MediosWeb_PublicadoCompleto garantiza que es
                // NULL exactamente cuando lo es el contenido: la condicion es la misma y la base
                // la impone. Lo destapo la prueba que lee el SQL emitido.
                .Where(x => x.PublishedHash != null)
                .Select(x => new
                {
                    x.Key,
                    x.Version,
                    x.PublishedMime,
                    x.PublishedHash,
                    x.PublishedWidth,
                    x.PublishedHeight,
                    x.PublishedBytes,
                    x.AltText,
                    x.Use,
                    x.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            // FIRMA SEPARADA DE LA DE LOS TEXTOS, a proposito. Si compartieran ETag, publicar una
            // tilde del pie invalidaria el manifiesto de imagenes y al reves. Los tres agregados
            // son baratos y ninguno toca un blob.
            var firma = publicadas.Count == 0
                ? "vacio"
                : string.Create(CultureInfo.InvariantCulture,
                    $"{publicadas.Count}-{publicadas.Max(x => x.UpdatedAt).Ticks}-{publicadas.Sum(x => x.Version)}");
            var etag = $"\"medios-{firma}\"";

            if (SiCoincideElEtag(httpContext, etag))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }

            httpContext.Response.Headers.ETag = etag;
            httpContext.Response.Headers.CacheControl = "no-cache";

            return Results.Ok(new
            {
                images = publicadas.ToDictionary(
                    x => x.Key,
                    x => new
                    {
                        url = $"/api/v1/imagenes-web/{x.Key}?v={x.Version}",
                        version = x.Version,
                        mime = x.PublishedMime,
                        hash = x.PublishedHash,
                        width = x.PublishedWidth,
                        height = x.PublishedHeight,
                        bytes = x.PublishedBytes,
                        alt = x.AltText,
                        use = x.Use,
                    }),
                count = publicadas.Count,
            });
        })
        .WithName("GetPublicWebMedia")
        .AllowAnonymous();

        // ANONIMA A PROPOSITO. Es la URL que va dentro de <img src>: pedirle sesion romperia todas
        // las imagenes del sitio para cualquier visitante.
        publico.MapGet("/{clave}", async (
            string clave,
            int? v,
            PnmcDbContext dbContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var fila = await dbContext.ImagenesWeb
                .AsNoTracking()
                // Misma razon que en el manifiesto para filtrar por la huella. Aqui el SELECT si
                // trae los bytes —es su trabajo—, pero el filtro no tiene por que.
                .Where(x => x.Key == clave && x.PublishedHash != null)
                .Select(x => new { x.PublishedContent, x.PublishedMime, x.PublishedHash, x.Version })
                .FirstOrDefaultAsync(cancellationToken);

            // 404 IGUAL EN LOS TRES CASOS —clave inexistente, nunca publicada, retirada— y es
            // deliberado: distinguirlos convertiria esta ruta anonima en un comprobador de que
            // claves existen en el catalogo y en que estado esta cada una.
            if (fila is null)
            {
                return Results.NotFound();
            }

            // El ETag lleva la huella Y la version. Hacen falta las dos: la huella cambia si
            // cambia el archivo; la version cambia si se retira y se republica EL MISMO archivo.
            var etag = $"\"{fila.PublishedHash![..16]}-{fila.Version.ToString("x", CultureInfo.InvariantCulture)}\"";

            // Con ?v la URL nombra un recurso que no puede cambiar —publicar sube la version y
            // cambia la URL—, asi que el navegador no tiene por que revalidar en un ano. Sin ?v se
            // guarda pero se revalida, igual que los textos: publicar se ve enseguida.
            httpContext.Response.Headers.CacheControl = v.HasValue
                ? "public, max-age=31536000, immutable"
                : "no-cache";
            httpContext.Response.Headers.ContentDisposition = "inline";
            // Sin scripts ni subrecursos: aunque un dia entrara algo que el reconocedor no vio, el
            // navegador no lo ejecutaria desde este origen.
            httpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

            // El Content-Type sale SIEMPRE de la columna, que la escribio el reconocedor, nunca de
            // lo que declaro el cliente al subir.
            // Esta sobrecarga resuelve If-None-Match y responde 304 sin cuerpo por si sola.
            return Results.File(fila.PublishedContent!, fila.PublishedMime!, entityTag: new EntityTagHeaderValue(etag));
        })
        .WithName("GetPublicWebMediaFile")
        .AllowAnonymous();
    }

    // =============================================================================================
    // CONSOLA DE ADMINISTRACIÓN
    // =============================================================================================
    private static void MapAdmin(RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin/imagenes-web").WithTags("admin-imagenes-web");
        admin.RequireAuthorization();
        admin.ExigeModulo("gestion-sitio");

        // NO HAY RUTA DE CSRF NUEVA. El token sale del mismo IAntiforgery y ya se pide en
        // GET /api/v1/admin/contenido-web/csrf. Una segunda seria una segunda cosa que mantener
        // diciendo lo mismo.

        MapGrupo(admin);
        MapPrevisualizacion(admin);
        MapSubida(admin);
        MapPublicar(admin);
        MapRetirar(admin);
        MapRepublicar(admin);
        MapHistorial(admin);
    }

    private static void MapGrupo(RouteGroupBuilder admin)
    {
        admin.MapGet("/groups/{groupId}", async (
            string groupId,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!GuardasDelCms.HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            // NO TRAE BYTES, y esa es la propiedad que hace usable el panel. Con dieciseis claves
            // a 2 MiB por mitad, materializar la entidad serian 64 MiB por cada vez que la
            // editora pulsa un grupo. Lo mide una prueba, no este comentario.
            var filas = await dbContext.ImagenesWeb
                .AsNoTracking()
                .Where(x => x.GroupId == groupId)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Key,
                    x.Label,
                    x.GroupLabel,
                    x.Section,
                    x.Use,
                    x.Editable,
                    x.AltText,
                    x.SuggestedWidth,
                    x.SuggestedHeight,
                    x.DraftMime,
                    x.DraftBytes,
                    x.DraftWidth,
                    x.DraftHeight,
                    x.DraftHash,
                    x.PublishedMime,
                    x.PublishedBytes,
                    x.PublishedWidth,
                    x.PublishedHeight,
                    x.PublishedHash,
                    x.Retired,
                    x.Version,
                    x.UpdatedBy,
                    x.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            if (filas.Count == 0)
            {
                return Results.NotFound(new { message = $"El grupo '{groupId}' no existe en el catálogo de imágenes." });
            }

            return Results.Ok(new
            {
                groupId,
                groupLabel = filas[0].GroupLabel,
                section = filas[0].Section,
                images = filas.Select(x => new
                {
                    key = x.Key,
                    label = x.Label,
                    use = x.Use,
                    editable = x.Editable,
                    alt = x.AltText,
                    suggestedWidth = x.SuggestedWidth,
                    suggestedHeight = x.SuggestedHeight,
                    draft = x.DraftHash is null ? null : new
                    {
                        mime = x.DraftMime,
                        bytes = x.DraftBytes,
                        width = x.DraftWidth,
                        height = x.DraftHeight,
                        hash = x.DraftHash,
                    },
                    published = x.PublishedHash is null ? null : new
                    {
                        mime = x.PublishedMime,
                        bytes = x.PublishedBytes,
                        width = x.PublishedWidth,
                        height = x.PublishedHeight,
                        hash = x.PublishedHash,
                    },
                    state = ResolverEstado(x.PublishedHash is not null, x.Retired),
                    version = x.Version,
                    updatedBy = x.UpdatedBy,
                    updatedAt = x.UpdatedAt,
                }),
                // LOS TOPES VIAJAN EN LA RESPUESTA y el panel no los lleva escritos a mano. Es la
                // correccion que ya hubo que hacer en la nomina, donde el panel se quedo en un
                // limite y el servidor aceptaba otro: la editora veia «cabe» y recibia un 400.
                limits = new
                {
                    maxBytes = MediosWebContrato.MaxBytes,
                    maxThumbnailBytes = MediosWebContrato.MaxThumbnailBytes,
                    maxAltLength = MediosWebContrato.MaxAltLength,
                    maxDimension = MediosWebContrato.MaxDimension,
                    allowedTypes = MediosWebContrato.TiposPermitidos,
                },
            });
        })
        .WithName("GetWebMediaGroup");
    }

    private static void MapPrevisualizacion(RouteGroupBuilder admin)
    {
        // Lo que pintan las <img> de la cuadricula del panel. Con miniatura=true son 24 KiB por
        // ranura en vez del original entero: dieciseis miniaturas pesan lo que una sola portada.
        admin.MapGet("/{clave}/preview/{estado}", async (
            string clave,
            string estado,
            bool? miniatura,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!GuardasDelCms.HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            if (estado is not ("borrador" or "publicado"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["estado"] = ["Debe ser 'borrador' o 'publicado'."],
                });
            }

            var esBorrador = estado == "borrador";
            var pequena = miniatura == true;

            var fila = await dbContext.ImagenesWeb
                .AsNoTracking()
                .Where(x => x.Key == clave)
                .Select(x => new
                {
                    Contenido = esBorrador
                        ? (pequena ? x.DraftThumbnail : x.DraftContent)
                        : (pequena ? x.PublishedThumbnail : x.PublishedContent),
                    Tipo = esBorrador ? x.DraftMime : x.PublishedMime,
                    Huella = esBorrador ? x.DraftHash : x.PublishedHash,
                    x.Version,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (fila?.Contenido is null || fila.Huella is null)
            {
                return Results.NotFound();
            }

            // La miniatura es siempre WebP; el original lleva el tipo que dijo el reconocedor.
            var tipo = pequena ? "image/webp" : fila.Tipo!;
            var etag = $"\"{fila.Huella[..16]}-{(pequena ? "m" : "o")}-{fila.Version.ToString("x", CultureInfo.InvariantCulture)}\"";

            // private: es material sin publicar. No debe quedar en una cache compartida.
            httpContext.Response.Headers.CacheControl = "private, no-cache";
            httpContext.Response.Headers.ContentDisposition = "inline";
            httpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

            return Results.File(fila.Contenido, tipo, entityTag: new EntityTagHeaderValue(etag));
        })
        .WithName("GetWebMediaPreview");
    }

    private static void MapSubida(RouteGroupBuilder admin)
    {
        admin.MapPost("/{clave}", async (
            string clave,
            // ENLAZA IFormCollection Y NO HttpRequest, y la diferencia no es de estilo. Enlazar el
            // formulario es lo que hace que .NET cuelgue del endpoint sus metadatos antiforgery
            // automaticos, y por tanto lo que hace que el .DisableAntiforgery() de mas abajo
            // signifique algo. Con HttpRequest y un ReadFormAsync a mano, esa linea era inerte: un
            // mutante que la borraba dejaba las veintiuna pruebas en verde.
            IFormCollection form,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!GuardasDelCms.HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var publicar = string.Equals(form["publish"], "true", StringComparison.OrdinalIgnoreCase);

            if (publicar && !GuardasDelCms.HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            if (!int.TryParse(form["version"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
            {
                // 400 y no 409: nadie se adelanto, es que la peticion no dice desde que version
                // parte. Sin este campo, la comprobacion de concurrencia no existe.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["version"] = ["Debe enviar la versión desde la que edita."],
                });
            }

            var archivo = form.Files["file"];
            if (archivo is null || archivo.Length == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Debe adjuntar un archivo."],
                });
            }

            // EL TOPE SE MIRA ANTES DE LEER EL FLUJO. Leer primero y medir despues es reservar la
            // memoria que se queria evitar.
            if (archivo.Length > MediosWebContrato.MaxBytes)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = [$"El archivo pesa {archivo.Length} bytes y el máximo es {MediosWebContrato.MaxBytes}."],
                });
            }

            var fila = await dbContext.ImagenesWeb.FirstOrDefaultAsync(x => x.Key == clave, cancellationToken);
            if (fila is null)
            {
                return Results.NotFound(new { message = $"La clave '{clave}' no existe en el catálogo de imágenes." });
            }

            // 403 y no 400: no es que el dato este mal, es que esa ranura no se toca. Son las
            // marcas institucionales ajenas. La base lo impide ademas por CHECK.
            if (!fila.Editable)
            {
                return Results.Forbid();
            }

            var bytes = await LeerAsync(archivo, cancellationToken);
            var formato = MediosWebContrato.Reconocer(bytes);
            if (formato is null)
            {
                // SIN DECIR EN QUE PASO FALLO. Un mensaje detallado seria un manual para el
                // siguiente intento.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["El archivo no es una imagen WebP, PNG o JPEG válida."],
                });
            }

            // El tipo declarado se IGNORA para lo que se guarda. Pero si viene y no coincide con
            // el reconocido, se rechaza: un cliente que miente sobre el tipo es una senal, no un
            // descuido que haya que corregirle en silencio.
            var declarado = archivo.ContentType;
            if (!string.IsNullOrWhiteSpace(declarado)
                && !string.Equals(declarado, formato.Value.Mime, StringComparison.OrdinalIgnoreCase))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["El tipo declarado no coincide con el contenido del archivo."],
                });
            }

            byte[]? miniatura = null;
            var miniaturaSubida = form.Files["thumbnail"];
            if (miniaturaSubida is not null && miniaturaSubida.Length > 0)
            {
                if (miniaturaSubida.Length > MediosWebContrato.MaxThumbnailBytes)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["thumbnail"] = [$"La miniatura pesa {miniaturaSubida.Length} bytes y el máximo es {MediosWebContrato.MaxThumbnailBytes}."],
                    });
                }

                miniatura = await LeerAsync(miniaturaSubida, cancellationToken);
                // LA MINIATURA PASA POR EL MISMO RECONOCEDOR. Es un dato de cliente: sin validar
                // seria el hueco por el que entra un archivo cualquiera mientras el principal se
                // mira con lupa.
                var formatoMiniatura = MediosWebContrato.Reconocer(miniatura);
                if (formatoMiniatura?.Mime != "image/webp")
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["thumbnail"] = ["La miniatura debe ser un WebP válido."],
                    });
                }
            }

            var alt = form["alt"].ToString();
            if (form.ContainsKey("alt"))
            {
                if (alt.Length > MediosWebContrato.MaxAltLength)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["alt"] = [$"Excede el límite de {MediosWebContrato.MaxAltLength} caracteres."],
                    });
                }

                if (alt.Contains('<', StringComparison.Ordinal))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["alt"] = ["El texto alternativo no admite el carácter '<'."],
                    });
                }
            }

            if (fila.Version != version)
            {
                return Results.Conflict(new
                {
                    message = "Otra persona modificó esta imagen mientras usted la editaba. Vuelva a cargarla antes de guardar.",
                });
            }

            var huella = MediosWebContrato.Huella(bytes);

            // MISMO PREDICADO QUE EN TEXTOS (ContenidoWebEndpoints.cs): una fila retirada no
            // vuelve sola. Subir con publish=true sobre una clave retirada guarda el borrador y NO
            // la resucita; para eso esta republicar, que es una accion explicita.
            var puedePublicar = publicar && fila.Retired is null;

            var cambiaElBorrador = fila.DraftHash != huella;
            var cambiaLoPublicado = puedePublicar && fila.PublishedHash != huella;
            var cambiaElAlt = form.ContainsKey("alt") && fila.AltText != alt;

            if (!cambiaElBorrador && !cambiaLoPublicado && !cambiaElAlt)
            {
                // Ni se escribe ni sube la version: subir dos veces el mismo archivo no es un
                // cambio, y contarlo como tal gastaria una entrada de historial por nada.
                return Results.Ok(EstadoDeLaFila(fila, cambiado: false));
            }

            var autor = GuardasDelCms.ResolveAuthor(principal);
            var ahora = DateTime.UtcNow;

            if (cambiaElAlt)
            {
                fila.AltText = alt;
            }

            fila.DraftContent = bytes;
            fila.DraftMime = formato.Value.Mime;
            fila.DraftBytes = bytes.Length;
            fila.DraftWidth = formato.Value.Width;
            fila.DraftHeight = formato.Value.Height;
            fila.DraftHash = huella;
            fila.DraftThumbnail = miniatura;

            if (puedePublicar)
            {
                CopiarBorradorAPublicado(fila);
            }

            fila.Version++;
            fila.UpdatedBy = autor;
            fila.UpdatedAt = ahora;

            await GuardasDelCms.RecordMediaHistoryAsync(
                dbContext, clave,
                puedePublicar ? GuardasDelCms.AccionPublicado : GuardasDelCms.AccionGuardado,
                bytes.Length, formato.Value, huella, autor, ahora, cancellationToken);

            var fallo = await GuardarAsync(dbContext, cancellationToken);
            return fallo ?? Results.Ok(EstadoDeLaFila(fila, cambiado: true));
        })
        .WithName("UploadWebMedia")
        // ESTO DESACTIVA EL FILTRO AUTOMATICO DEL FRAMEWORK, NO LA COMPROBACION.
        //
        // Desde .NET 8, un endpoint de minimal API que enlaza un formulario recibe metadatos
        // antiforgery automaticos, y su filtro EXIGE que app.UseAntiforgery() este en la tuberia.
        // COMPROBADO: no lo esta. Program.cs solo tiene AddAntiforgery (:87); no hay UseAntiforgery
        // en ninguna parte. Sin esta linea, la primera subida muere con InvalidOperationException
        // en tiempo de ejecucion, no al compilar.
        //
        // La comprobacion ocurre dentro del manejador, en GuardasDelCms.ValidateAntiforgeryAsync,
        // que es el UNICO mecanismo antiforgery de este repositorio y el que ya ejercitan las
        // pruebas del CMS. Anadir UseAntiforgery() seria un segundo mecanismo para lo mismo, con
        // dos sitios donde desactivarlo por error.
        //
        // ESTA LINEA CARGA PESO Y SE PUEDE MEDIR: un mutante que la borra pone en rojo las siete
        // pruebas que suben un archivo, con el InvalidOperationException de arriba. Antes no era
        // asi —el manejador leia el formulario a mano y el framework no colgaba nada—, y por eso
        // el parametro es IFormCollection.
        .DisableAntiforgery()
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(3 * 1024 * 1024))
        .RequireRateLimiting("web-media-upload");
    }

    private static void MapPublicar(RouteGroupBuilder admin)
    {
        // «Desplegar el borrador» sin subir nada. Es la accion que el panel ofrece cuando ya hay
        // un borrador guardado y solo falta que salga al sitio.
        admin.MapPost("/{clave}/publish", async (
            string clave,
            MediaVersionRequest? request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!GuardasDelCms.HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var fila = await dbContext.ImagenesWeb.FirstOrDefaultAsync(x => x.Key == clave, cancellationToken);
            if (fila is null)
            {
                return Results.NotFound(new { message = $"La clave '{clave}' no existe en el catálogo de imágenes." });
            }

            if (fila.DraftContent is null)
            {
                return Results.Conflict(new
                {
                    message = $"La clave '{clave}' no tiene borrador que publicar.",
                    state = ResolverEstado(fila.PublishedContent is not null, fila.Retired),
                });
            }

            if (fila.Retired is not null)
            {
                return Results.Conflict(new
                {
                    message = $"La clave '{clave}' está retirada. Use republicar.",
                    state = "retirado",
                });
            }

            if (request?.Version is int esperada && fila.Version != esperada)
            {
                return Results.Conflict(new
                {
                    message = "Otra persona modificó esta imagen mientras usted la editaba. Vuelva a cargarla antes de publicar.",
                });
            }

            if (fila.PublishedHash == fila.DraftHash)
            {
                return Results.Ok(EstadoDeLaFila(fila, cambiado: false));
            }

            var autor = GuardasDelCms.ResolveAuthor(principal);
            var ahora = DateTime.UtcNow;
            CopiarBorradorAPublicado(fila);
            fila.Version++;
            fila.UpdatedBy = autor;
            fila.UpdatedAt = ahora;

            await GuardasDelCms.RecordMediaHistoryAsync(
                dbContext, clave, GuardasDelCms.AccionPublicado,
                fila.DraftBytes, FormatoDelBorrador(fila), fila.DraftHash!, autor, ahora, cancellationToken);

            var fallo = await GuardarAsync(dbContext, cancellationToken);
            return fallo ?? Results.Ok(EstadoDeLaFila(fila, cambiado: true));
        })
        .WithName("PublishWebMedia");
    }

    private static void MapRetirar(RouteGroupBuilder admin)
    {
        admin.MapPost("/{clave}/retire", async (
            string clave,
            MediaRetireRequest? request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            // Retirar cambia lo que ve el visitante, igual que publicar. Mismo rol.
            if (!GuardasDelCms.HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var fila = await dbContext.ImagenesWeb.FirstOrDefaultAsync(x => x.Key == clave, cancellationToken);
            if (fila is null)
            {
                return Results.NotFound(new { message = $"La clave '{clave}' no existe en el catálogo de imágenes." });
            }

            if (fila.Retired is not null)
            {
                return Results.Ok(EstadoDeLaFila(fila, cambiado: false));
            }

            if (fila.PublishedContent is null)
            {
                // Nunca estuvo visible: no hay nada que retirar. Marcarlo igual dejaria un estado
                // «retirado» que nadie provoco.
                return Results.Conflict(new
                {
                    message = $"La clave '{clave}' no está publicada, así que no hay nada que retirar.",
                    state = ResolverEstado(false, fila.Retired),
                });
            }

            var autor = GuardasDelCms.ResolveAuthor(principal);
            var ahora = DateTime.UtcNow;

            // EL BORRADOR SE CONSERVA. Retirar quita la imagen del sitio —el visitante vuelve a
            // ver la compilada— y no la borra del editor.
            fila.PublishedContent = null;
            fila.PublishedMime = null;
            fila.PublishedBytes = null;
            fila.PublishedWidth = null;
            fila.PublishedHeight = null;
            fila.PublishedHash = null;
            fila.PublishedThumbnail = null;
            fila.Retired = ahora;
            fila.Version++;
            fila.UpdatedBy = autor;
            fila.UpdatedAt = ahora;

            // Sin archivo: retirar no reescribe el borrador. Lo que cuenta es la accion.
            await GuardasDelCms.RecordMediaHistoryAsync(
                dbContext, clave, GuardasDelCms.AccionRetirado,
                null, null, null, autor, ahora, cancellationToken);

            var fallo = await GuardarAsync(dbContext, cancellationToken);
            return fallo ?? Results.Ok(EstadoDeLaFila(fila, cambiado: true, request?.Reason));
        })
        .WithName("RetireWebMedia");
    }

    private static void MapRepublicar(RouteGroupBuilder admin)
    {
        admin.MapPost("/{clave}/republish", async (
            string clave,
            MediaRetireRequest? request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!GuardasDelCms.HasAnyRole(principal, PublisherRoles))
            {
                return Results.Forbid();
            }

            var fila = await dbContext.ImagenesWeb.FirstOrDefaultAsync(x => x.Key == clave, cancellationToken);
            if (fila is null)
            {
                return Results.NotFound(new { message = $"La clave '{clave}' no existe en el catálogo de imágenes." });
            }

            if (fila.Retired is null)
            {
                return Results.Conflict(new
                {
                    message = $"La clave '{clave}' no está retirada.",
                    state = ResolverEstado(fila.PublishedContent is not null, null),
                });
            }

            if (fila.DraftContent is null)
            {
                return Results.Conflict(new
                {
                    message = $"La clave '{clave}' no tiene borrador que republicar.",
                    state = "retirado",
                });
            }

            // SE REVALIDA EL BORRADOR contra los topes vigentes, por el mismo motivo que en textos:
            // el borrador pudo guardarse cuando el tope era otro, y republicar es publicar.
            if (fila.DraftBytes > MediosWebContrato.MaxBytes
                || MediosWebContrato.Reconocer(fila.DraftContent) is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [clave] = ["El borrador ya no cumple los límites vigentes. Vuelva a subir la imagen antes de republicar."],
                });
            }

            var autor = GuardasDelCms.ResolveAuthor(principal);
            var ahora = DateTime.UtcNow;
            CopiarBorradorAPublicado(fila);
            fila.Retired = null;
            fila.Version++;
            fila.UpdatedBy = autor;
            fila.UpdatedAt = ahora;

            await GuardasDelCms.RecordMediaHistoryAsync(
                dbContext, clave, GuardasDelCms.AccionRepublicado,
                fila.DraftBytes, FormatoDelBorrador(fila), fila.DraftHash!, autor, ahora, cancellationToken);

            var fallo = await GuardarAsync(dbContext, cancellationToken);
            return fallo ?? Results.Ok(EstadoDeLaFila(fila, cambiado: true, request?.Reason));
        })
        .WithName("RepublishWebMedia");
    }

    private static void MapHistorial(RouteGroupBuilder admin)
    {
        admin.MapGet("/{clave}/history", async (
            string clave,
            int? limit,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!GuardasDelCms.HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var take = Math.Clamp(limit ?? 20, 1, 100);

            // EL HISTORIAL NO GUARDA EL ARCHIVO desde V20260829_03: solo quien, cuando, que
            // accion y las senas de lo que estuvo ahi. No hay ruta de restaurar y no puede
            // haberla: reemplazar una imagen borra la anterior, que es lo que se pidio.
            var entradas = await dbContext.HistorialDeImagenesWeb
                .AsNoTracking()
                .Where(x => x.Key == clave)
                .OrderByDescending(x => x.At)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .Select(x => new
                {
                    id = x.Id,
                    action = x.Action,
                    mime = x.Mime,
                    bytes = x.Bytes,
                    width = x.Width,
                    height = x.Height,
                    hash = x.Hash,
                    user = x.User,
                    at = x.At,
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { key = clave, entries = entradas });
        })
        .WithName("GetWebMediaHistory");
    }

    // =============================================================================================
    // AYUDANTES
    // =============================================================================================

    /// <summary>
    /// Publicar es copiar las siete columnas de una mitad a la otra. En un solo sitio, para que no
    /// pueda copiarse seis en una ruta y siete en otra.
    /// </summary>
    private static void CopiarBorradorAPublicado(ImagenWebRow fila)
    {
        fila.PublishedContent = fila.DraftContent;
        fila.PublishedMime = fila.DraftMime;
        fila.PublishedBytes = fila.DraftBytes;
        fila.PublishedWidth = fila.DraftWidth;
        fila.PublishedHeight = fila.DraftHeight;
        fila.PublishedHash = fila.DraftHash;
        fila.PublishedThumbnail = fila.DraftThumbnail;
        fila.Retired = null;
    }

    private static MediosWebContrato.Formato? FormatoDelBorrador(ImagenWebRow fila)
        => fila.DraftMime is null
            ? null
            : new MediosWebContrato.Formato(fila.DraftMime, fila.DraftWidth ?? 0, fila.DraftHeight ?? 0);

    /// <summary>
    /// Estado derivado de las dos columnas. No se guarda: una tercera columna que dijera lo mismo
    /// podría contradecir a las otras dos. Igual que en <see cref="ContenidoWebEndpoints"/>.
    /// </summary>
    private static string ResolverEstado(bool hayPublicado, DateTime? retirada)
    {
        if (hayPublicado) { return "publicado"; }
        return retirada is not null ? "retirado" : "no_publicado";
    }

    private static object EstadoDeLaFila(ImagenWebRow fila, bool cambiado, string? razon = null)
        => new
        {
            key = fila.Key,
            state = ResolverEstado(fila.PublishedContent is not null, fila.Retired),
            version = fila.Version,
            changed = cambiado,
            hash = fila.DraftHash,
            mime = fila.DraftMime,
            width = fila.DraftWidth,
            height = fila.DraftHeight,
            bytes = fila.DraftBytes,
            reason = razon,
        };

    private static async Task<byte[]> LeerAsync(IFormFile archivo, CancellationToken cancellationToken)
    {
        using var memoria = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }

    /// <summary>
    /// Guarda y traduce el choque de concurrencia a 409. Devuelve null si todo fue bien, para que
    /// el llamador construya su propia respuesta.
    /// </summary>
    private static async Task<IResult?> GuardarAsync(PnmcDbContext dbContext, CancellationToken cancellationToken)
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
                message = "Otra persona modificó esta imagen mientras usted la editaba. Vuelva a cargarla antes de reintentar.",
            });
        }
    }

    private static bool SiCoincideElEtag(HttpContext httpContext, string etag)
    {
        var pedido = httpContext.Request.GetTypedHeaders().IfNoneMatch;
        return pedido.Count > 0 && pedido.Any(x => string.Equals(x.Tag.Value, etag, StringComparison.Ordinal));
    }
}
