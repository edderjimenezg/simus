using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PNMC.Api.Security;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// El banco de archivos: subir una imagen una vez y vincularla desde donde haga falta.
///
/// <para>
/// <b>POR QUE HACIA FALTA.</b> <c>dbo.Archivos</c> existe desde el principio —con su texto
/// alternativo, su pie y su crédito— y <b>ninguna ruta del API escribía en ella</b>. Los
/// Festivales solo vinculaban identificadores que nadie podía crear, y los paneles de alta de
/// Agenda, Noticias y Catálogo Editorial pedían la imagen como una RUTA QUE ALGUIEN TECLEABA.
/// Eso no es subir un archivo: es confiar en que el fichero ya esté puesto y bien escrito.
/// </para>
/// <para>
/// <b>EL ORDEN DE LAS COMPROBACIONES ES EL ORDEN EN QUE SE LE DICEN LOS PROBLEMAS A QUIEN SUBE</b>,
/// y está tomado del desarrollo de septiembre porque allí está bien pensado:
/// </para>
/// <list type="number">
///   <item>El <b>tope se mira antes de reconocer</b>. Leer primero y medir después es reservar la
///   memoria que se quería evitar.</item>
///   <item>El <b>texto alternativo se exige antes de mirar los bytes</b>. Que el archivo sea
///   válido no salva a una imagen sin alternativa textual.</item>
///   <item>El <b>formato se reconoce por la firma</b>, no por lo que el cliente declare. Es lo que
///   separa una imagen de un ejecutable con la cabecera pegada delante.</item>
///   <item>Si el cliente <b>declara un tipo y no coincide</b>, se rechaza: mentir sobre el tipo es
///   una señal, no un descuido que haya que corregirle en silencio.</item>
/// </list>
/// <para>
/// <b>LOS BYTES VAN A LA BASE</b>, como ya hace <c>MediosWeb</c>. No se inventa un segundo
/// almacenamiento, y el reconocedor es el mismo: <see cref="MediosWebContrato"/>.
/// </para>
/// </summary>
public static class BancoDeArchivosEndpoints
{
    private static readonly string[] AdjunteUnArchivo = ["Adjunte un archivo."];
    private static readonly string[] EscribaElTextoAlternativo =
        ["Escriba el texto alternativo: describe la imagen para quien no puede verla."];
    private static readonly string[] NoEsImagenValida =
        ["El archivo no es una imagen WebP, PNG o JPEG válida."];
    private static readonly string[] NoEsPdfValido =
        ["El archivo no es un PDF válido."];
    private static readonly string[] TipoDeclaradoNoCoincide =
        ["El tipo declarado no coincide con el contenido del archivo."];

    private const int LargoMaximoDelAlternativo = 300;
    private const int LargoMaximoDelPie = 500;
    private const int LargoMaximoDelCredito = 200;

    public static RouteGroupBuilder MapBancoDeArchivosEndpoints(this RouteGroupBuilder api)
    {
        // LA LECTURA ES ANONIMA Y LA ESCRITURA NO. Un archivo vinculado a un evento publicado
        // tiene que poder verlo cualquiera; subirlo, solo quien administra.
        var publico = api.MapGroup("/publico/archivos").WithTags("archivos-publico").AllowAnonymous();
        publico.MapGet("/{id:int}", Servir).WithName("ServirArchivo");

        var consola = api.MapGroup("/institucional/archivos")
            .WithTags("archivos")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("banco-de-archivos");
        consola.MapPost(string.Empty, Subir).WithName("SubirArchivo").DisableAntiforgery();
        // LOS DOCUMENTOS ENTRAN POR SU PROPIA PUERTA, y no relajando la de las imágenes.
        consola.MapPost("/documentos", SubirDocumento).WithName("SubirDocumento").DisableAntiforgery();
        consola.MapGet("/{id:int}", Describir).WithName("DescribirArchivo");
        // LA GESTION DEL BANCO, QUE NO EXISTIA. Se podía subir y servir, y nada más: ni ver qué
        // hay dentro, ni saber a qué está vinculado cada archivo, ni retirar uno subido por error.
        consola.MapGet(string.Empty, ListarInstitucional).WithName("ListarArchivos");
        consola.MapDelete("/{id:int}", RetirarInstitucional).WithName("RetirarArchivo");

        // ── EL MISMO BANCO, PARA LAS ORGANIZACIONES ───────────────────────────────────────────
        //
        // POR QUE SE ABRE. La historia de usuario pide adjuntar el programa, el afiche y el logo de
        // cada Edición, y la foto de perfil de la organización; con el banco cerrado al canal
        // externo, lo único que podía aportar quien registra era una URL a un sitio ajeno. Abierto
        // a petición de la dirección de producto.
        //
        // ES EL MISMO MANEJADOR Y NO UNA COPIA. Toda la cadena de comprobaciones —el tope antes de
        // leer, el texto alternativo, el formato POR SU FIRMA y no por lo que el cliente declare, y
        // el rechazo si el tipo declarado miente— es exactamente la misma. Duplicarla para el canal
        // externo sería duplicar la superficie donde una de las dos copias se queda corta.
        //
        // LO QUE CAMBIA SON DOS GUARDAS QUE EL CANAL INSTITUCIONAL NO NECESITA, y están en
        // `GuardasDelCanalExterno`: el correo confirmado y la cuota por organización.
        var externo = api.MapGroup("/externo/archivos")
            .WithTags("archivos")
            .RequireAuthorization(SimusAuthentication.ExternalPolicy);
        externo.MapPost(string.Empty, SubirDesdeOrganizacion).WithName("SubirArchivoExterno").DisableAntiforgery();
        externo.MapGet("/{id:int}", DescribirDeLaOrganizacion).WithName("DescribirArchivoExterno");
        externo.MapGet(string.Empty, ListarDeLaOrganizacion).WithName("ListarArchivosExternos");
        externo.MapDelete("/{id:int}", RetirarDeLaOrganizacion).WithName("RetirarArchivoExterno");
        return api;
    }

    /// <summary>
    /// Sirve el archivo con su ETag.
    /// </summary>
    /// <remarks>
    /// LA HUELLA ES EL ETAG, y por eso se guarda. Un navegador que ya tiene la imagen recibe un
    /// 304 y no vuelve a bajar los bytes; sin ETag, cada carga del portal se trae el acervo entero.
    /// </remarks>
    private static async Task<IResult> Servir(
        PnmcDbContext db, HttpContext contexto, ClaimsPrincipal principal, int id, CancellationToken ct)
    {
        var fila = await db.Files.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Content, x.MimeType, x.Fingerprint, x.OrganizacionId })
            .FirstOrDefaultAsync(ct);

        if (fila?.Content is null || fila.Content.Length == 0) return Results.NotFound();

        // ── UN ARCHIVO SE SIRVE A QUIEN PUEDE VERLO, Y NO A CUALQUIERA QUE ACIERTE EL NUMERO ──
        //
        // QUE PASABA, MEDIDO EL 13 DE SEPTIEMBRE DE 2026. La ruta era anónima para TODO el banco y
        // los identificadores son enteros consecutivos: `GET /api/v1/publico/archivos/1..N` bajaba
        // el acervo entero. Entre lo que devolvía estaba el archivo 1005, «Afiche de la edición
        // 2027», subido por una organización a una edición que nunca se publicó —y que de hecho
        // había quedado huérfana, porque el formulario se abandonó sin guardar—. El material de un
        // proceso que su dueño todavía no ha enseñado no es público, y menos aún el de uno que no
        // llegó a existir.
        //
        // LA REGLA ES LA VINCULACION, NO UNA BANDERA. Un archivo es público si está usado en algo
        // que el público ya ve. No se guarda un `EsPublico` que habría que mantener a mano y que
        // se quedaría desincronizado la primera vez que alguien retire una noticia.
        //
        // QUIEN TIENE SESION NO PASA POR AHI: el equipo del PNMC ve el banco entero, y una
        // organización ve lo suyo. Es lo mismo que ya hace el resto del sistema con un borrador.
        // LA SESION EXTERNA HAY QUE PEDIRLA A PROPOSITO, Y ESO COSTO UN 404 EN PRUEBAS.
        //
        // Esta ruta es anónima, así que el canalizador solo resuelve el esquema POR OMISION —el de
        // la consola— y el `principal` que llega no trae la sesión de una organización aunque su
        // cookie venga en la petición. Comprobado: con sesión institucional el archivo 1005 se servía;
        // con la sesión de la organización DUEÑA del mismo archivo, 404. Es decir: quien lo subió
        // era justo el único que no podía volver a verlo.
        //
        // NO SE CAMBIA EL ESQUEMA POR OMISION —eso alcanzaría a todo el API—: se pregunta por el
        // externo solo aquí, y solo cuando el que ya vino no sirve.
        var quienPide = principal;
        if (Actor(quienPide) <= 0)
        {
            var externa = await contexto.AuthenticateAsync(SimusAuthentication.ExternalScheme);
            if (externa.Succeeded && externa.Principal is not null) { quienPide = externa.Principal; }
        }

        if (!await PuedeVerloAsync(db, quienPide, id, fila.OrganizacionId, ct))
        {
            // 404 Y NO 403: un 403 confirma que el archivo existe, que es justo lo que no hay que
            // confirmarle a quien está recorriendo identificadores.
            return Results.NotFound();
        }

        var etiqueta = string.IsNullOrWhiteSpace(fila.Fingerprint)
            ? null
            : new EntityTagHeaderValue($"\"{fila.Fingerprint}\"");

        return Results.File(fila.Content, fila.MimeType, entityTag: etiqueta);
    }

    /// <summary>
    /// Dónde está usado un archivo, mirando las seis tablas que lo pueden vincular.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES LA PREGUNTA QUE SOSTIENE TODO LO DEMAS: si se puede servir a cualquiera, si se puede
    /// retirar, y qué se rompería al hacerlo. Sin ella, el banco es una caja donde entran cosas y
    /// no se sabe qué hay dentro.
    /// </para>
    /// <para>
    /// SEIS TABLAS, Y SE ENUMERAN A PROPOSITO. Son las seis que tienen una foránea a
    /// <c>dbo.Archivos</c>, comprobado contra el catálogo de la base y no de memoria. Si algún día
    /// aparece una séptima, esta consulta se queda corta en silencio: por eso existe
    /// <c>Las_tablas_que_vinculan_un_archivo_son_las_que_esta_consulta_mira</c>, que compara esta
    /// lista con las foráneas reales y se pone en rojo cuando dejan de coincidir.
    /// </para>
    /// </remarks>
    private static async Task<List<VinculoDeArchivo>> VinculosAsync(PnmcDbContext db, int archivoId, CancellationToken ct)
    {
        var vinculos = new List<VinculoDeArchivo>();

        if (await db.EntityProfiles.AsNoTracking().AnyAsync(x => x.ArchivoFotoId == archivoId, ct))
        {
            // LA FOTO DE PERFIL ES PUBLICA POR DECISION de la dirección de producto // de 2026: acompaña a la organización en el portal.
            vinculos.Add(new VinculoDeArchivo("organizacion", "Foto de perfil de una organización", true));
        }

        var materiales = await db.ArchivosDeRegistro.AsNoTracking()
            .Where(x => x.ModuloId == Modulos.EdicionesDeFestival && x.ArchivoId == archivoId)
            .Join(db.EdicionesFestival.AsNoTracking(),
                m => m.RegistroId, e => e.Id.ToString(), (m, e) => e.EstadoVisibilidad)
            .ToListAsync(ct);
        foreach (var visibilidad in materiales)
        {
            vinculos.Add(new VinculoDeArchivo("edicion", "Material de una Edición de Festival", visibilidad == "publicada"));
        }

        var versiones = await db.ArchivosDeRegistro.AsNoTracking()
            .Where(x => x.ModuloId == Modulos.VersionesDeFestival && x.ArchivoId == archivoId).CountAsync(ct);
        for (var i = 0; i < versiones; i++)
        {
            // UNA VERSION DE FESTIVAL SOLO EXISTE CUANDO EL FESTIVAL SE PUBLICO: es la foto de lo
            // que se publicó, así que su material es público.
            vinculos.Add(new VinculoDeArchivo("festival", "Material de una versión publicada de Festival", true));
        }

        var noticias = await db.NoticiasArchivos.AsNoTracking()
            .Where(x => x.ArchivoId == archivoId)
            .Join(db.Noticias.AsNoTracking(), n => n.NoticiaId, x => x.Id, (n, x) => x.Estado)
            .ToListAsync(ct);
        foreach (var estado in noticias)
        {
            vinculos.Add(new VinculoDeArchivo("noticia", "Imagen de una noticia", estado == "publicado"));
        }

        var eventos = await db.EventosAgendaArchivos.AsNoTracking()
            .Where(x => x.ArchivoId == archivoId)
            .Join(db.EventosAgenda.AsNoTracking(), a => a.EventoAgendaId, e => e.Id, (a, e) => e.Estado)
            .ToListAsync(ct);
        foreach (var estado in eventos)
        {
            vinculos.Add(new VinculoDeArchivo("agenda", "Imagen de un evento de la agenda", estado == "publicado"));
        }

        var accesos = await db.AccesosEditoriales.AsNoTracking()
            .Where(x => x.ArchivoId == archivoId)
            .Select(x => x.DerechosPermitePublicarArchivo)
            .ToListAsync(ct);
        foreach (var permite in accesos)
        {
            // EL ARCHIVO DE UNA PUBLICACION NECESITA SU PROPIO PERMISO: enseñar la ficha y repartir
            // el fichero son dos autorizaciones distintas, y el Catálogo las separa.
            vinculos.Add(new VinculoDeArchivo("editorial", "Archivo de una publicación del Catálogo", permite));
        }

        return vinculos;
    }

    /// <summary>
    /// Si quien pide el archivo puede verlo: porque está publicado, porque es suyo, o porque
    /// administra.
    /// </summary>
    private static async Task<bool> PuedeVerloAsync(
        PnmcDbContext db, ClaimsPrincipal principal, int archivoId, int? organizacionDelArchivo, CancellationToken ct)
    {
        // 1. EL EQUIPO DEL PNMC VE EL BANCO ENTERO: es quien responde por él.
        if (Permisos.EsFuncionario(principal)) return true;

        // 2. LA ORGANIZACION VE LO SUYO, esté publicado o no. Es su material.
        if (organizacionDelArchivo is int duena)
        {
            var personaId = Actor(principal);
            if (personaId > 0 && await db.UserEntities.AsNoTracking().AnyAsync(
                    v => v.UserId == personaId && v.EntityId == duena && v.IsActive
                      && RolesDeEntidad.QueResponden.Contains(v.EntityRole), ct))
            {
                return true;
            }
        }

        // 3. Y CUALQUIERA, SI ESTA USADO EN ALGO QUE YA SE VE.
        var vinculos = await VinculosAsync(db, archivoId, ct);
        return vinculos.Any(v => v.EsPublico);
    }

    /// <summary>Un uso concreto de un archivo, y si ese uso lo hace visible al público.</summary>
    private sealed record VinculoDeArchivo(string Modulo, string Descripcion, bool EsPublico);

    /// <summary>Los datos del archivo sin sus bytes, para que un panel pueda describir lo vinculado.</summary>
    private static async Task<IResult> Describir(PnmcDbContext db, int id, CancellationToken ct)
    {
        var fila = await db.Files.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                id = x.Id,
                nombre = x.OriginalName,
                tipo = x.MimeType,
                bytes = x.FileSizeBytes,
                alt = x.AltText,
                pie = x.Caption,
                credito = x.Credit,
                ancho = x.Width,
                alto = x.Height,
                url = "/api/v1/publico/archivos/" + x.Id.ToString(CultureInfo.InvariantCulture),
            })
            .FirstOrDefaultAsync(ct);

        return fila is null ? Results.NotFound() : Results.Ok(fila);
    }

    /// <summary>
    /// Lo que hay en el banco, con su procedencia y con dónde está usado cada archivo.
    /// </summary>
    /// <remarks>
    /// <b>SIN ESTO NO HABIA GESTION, SOLO SUBIDA.</b> El banco admitía archivos y los servía, y no
    /// existía ninguna forma de ver qué contenía: para saber de un archivo había que conocer ya su
    /// identificador. Un almacén en el que no se puede mirar acumula lo que nadie recuerda.
    /// </remarks>
    private static async Task<IResult> ListarInstitucional(
        PnmcDbContext db, string? procedencia, bool? soloHuerfanos, int? pagina, int? tamano, CancellationToken ct) =>
        await ListarAsync(db, null, procedencia, soloHuerfanos, pagina, tamano, ct);

    /// <summary>Los archivos de la organización de quien pregunta, y cuánto le queda de cuota.</summary>
    private static async Task<IResult> ListarDeLaOrganizacion(
        PnmcDbContext db, ClaimsPrincipal principal, bool? soloHuerfanos, int? pagina, int? tamano, CancellationToken ct)
    {
        var organizacionId = await OrganizacionDeAsync(db, principal, ct);
        if (organizacionId is null) return Results.Forbid();
        return await ListarAsync(db, organizacionId, null, soloHuerfanos, pagina, tamano, ct);
    }

    private static async Task<IResult> ListarAsync(
        PnmcDbContext db, int? organizacionId, string? procedencia, bool? soloHuerfanos,
        int? pagina, int? tamano, CancellationToken ct)
    {
        var numero = Math.Max(1, pagina ?? 1);
        var porPagina = Math.Clamp(tamano ?? 24, 1, 100);

        // UNA FILA SIN BYTES NO ES UN ARCHIVO, Y EL BANCO NO LA OFRECE.
        //
        // QUE PASABA, MEDIDO EL 14 DE SEPTIEMBRE DE 2026 recorriendo la consola: el Banco de
        // archivos listaba el archivo 5 y su previsualizacion contestaba 404. La fila existe en
        // `dbo.Archivos` con `Contenido` nulo: la creo la semilla
        // `V20260519_03__administracion_control_seed.sql` como «Archivo temporal de prueba» para
        // validar metadatos, y nunca tuvo contenido que servir.
        //
        // SE EXCLUYE EN EL LISTADO Y NO SE BORRA LA FILA. Borrar datos es una accion que en este
        // proyecto no se toma de paso, y no hace falta: `Servir` ya devuelve 404 cuando no hay
        // bytes, asi que lo unico roto era ofrecer en la lista algo que no se puede abrir. Con esta
        // guarda, cualquier fila corrupta que aparezca en el futuro —una subida que falle despues
        // de escribir la fila— deja de ensuciar el banco por si sola.
        //
        // SE MIRA `PesoBytes` Y NO EL PROPIO BLOB. `Content.Length > 0` no se puede traducir —EF lo
        // baja a `Enumerable.Any()` y la consulta revienta con un 500, comprobado— y
        // `EF.Functions.DataLength` solo existe en SQL Server, mientras que buena parte de las
        // pruebas corren sobre SQLite. `PesoBytes` lo escribe cada subida con `datos.Length`, asi
        // que es el mismo hecho expresado en una columna que cualquier proveedor sabe comparar.
        // Un peso nulo no descarta la fila: lo que se excluye es un cero declarado.
        var consulta = db.Files.AsNoTracking()
            .Where(x => x.Content != null && (x.FileSizeBytes == null || x.FileSizeBytes > 0));
        if (organizacionId is int propia) { consulta = consulta.Where(x => x.OrganizacionId == propia); }
        else if (procedencia == "institucional") { consulta = consulta.Where(x => x.OrganizacionId == null); }
        else if (procedencia == "organizaciones") { consulta = consulta.Where(x => x.OrganizacionId != null); }

        var total = await consulta.CountAsync(ct);
        var filas = await consulta
            .OrderByDescending(x => x.Id)
            .Skip((numero - 1) * porPagina).Take(porPagina)
            .Select(x => new
            {
                x.Id, x.OriginalName, x.MimeType, x.FileSizeBytes, x.AltText,
                x.Width, x.Height, x.CreatedAt, x.OrganizacionId, x.UploadedByUserId,
            })
            .ToListAsync(ct);

        // LOS NOMBRES, NO LOS NUMEROS. Una lista que dice «organización 2162» obliga a ir a
        // buscar cuál es, que es justo lo que una lista existe para evitar.
        var organizaciones = filas.Select(x => x.OrganizacionId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var nombresDeOrganizacion = await db.EntityProfiles.AsNoTracking()
            .Where(e => organizaciones.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var personas = filas.Select(x => x.UploadedByUserId).Where(x => x > 0).Distinct().ToList();
        var nombresDePersona = await db.Users.AsNoTracking()
            .Where(u => personas.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var items = new List<object>(filas.Count);
        foreach (var fila in filas)
        {
            var vinculos = await VinculosAsync(db, fila.Id, ct);
            if (soloHuerfanos == true && vinculos.Count > 0) { continue; }
            items.Add(new
            {
                id = fila.Id,
                nombre = fila.OriginalName,
                tipo = fila.MimeType,
                bytes = fila.FileSizeBytes,
                alt = fila.AltText,
                ancho = fila.Width,
                alto = fila.Height,
                fechaCarga = fila.CreatedAt,
                // LA PROCEDENCIA, SEPARADA DE QUIEN LO SUBIO: son dos cosas distintas y el proyecto
                // no las confunde en ningún módulo.
                procedencia = fila.OrganizacionId is null ? "institucional" : "organizacion",
                organizacionId = fila.OrganizacionId,
                organizacionNombre = fila.OrganizacionId is int o && nombresDeOrganizacion.TryGetValue(o, out var nombre) ? nombre : null,
                subidoPor = nombresDePersona.TryGetValue(fila.UploadedByUserId, out var quien) ? quien : null,
                url = "/api/v1/publico/archivos/" + fila.Id.ToString(CultureInfo.InvariantCulture),
                usos = vinculos.Select(v => new { modulo = v.Modulo, descripcion = v.Descripcion, esPublico = v.EsPublico }).ToList(),
                // HUERFANO ES EL QUE NO USA NADIE, y es la cifra que de verdad hace falta: son los
                // que ocupan cuota sin que nadie los eche de menos.
                huerfano = vinculos.Count == 0,
                visiblePublicamente = vinculos.Any(v => v.EsPublico),
            });
        }

        long? usados = null;
        if (organizacionId is int deQuien)
        {
            usados = await db.Files.AsNoTracking().Where(x => x.OrganizacionId == deQuien)
                .SumAsync(x => (long?)x.FileSizeBytes ?? 0L, ct);
        }

        return Results.Ok(new
        {
            items,
            pagina = numero,
            tamano = porPagina,
            total,
            totalPaginas = total == 0 ? 0 : (int)Math.Ceiling(total / (double)porPagina),
            cuotaBytes = organizacionId is null ? (long?)null : CuotaPorOrganizacion,
            usadoBytes = usados,
        });
    }

    /// <summary>
    /// Retira un archivo del banco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SOLO SI NO LO USA NADIE, Y ESO NO ES UNA PRECAUCION: ES LA UNICA FORMA DE QUE SEA
    /// SEGURO.</b> Seis tablas pueden apuntar a un archivo. Borrarlo mientras alguna lo hace deja
    /// una noticia con una imagen rota o revienta contra la foránea; comprobarlo antes convierte la
    /// operación en una que no puede estropear nada.
    /// </para>
    /// <para>
    /// EL MENSAJE DE CUOTA PROMETIA ESTO Y NO EXISTIA. Cuando una organización llenaba su espacio
    /// se le decía «retira material que ya no uses», y no había ninguna forma de retirarlo: la
    /// única salida era escribir al Programa. Una promesa que el sistema no puede cumplir es peor
    /// que no hacerla.
    /// </para>
    /// </remarks>
    private static async Task<IResult> RetirarInstitucional(PnmcDbContext db, int id, CancellationToken ct)
    {
        var fila = await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (fila is null) return Results.NotFound();
        return await RetirarAsync(db, fila, ct);
    }

    private static async Task<IResult> RetirarDeLaOrganizacion(
        PnmcDbContext db, ClaimsPrincipal principal, int id, CancellationToken ct)
    {
        var organizacionId = await OrganizacionDeAsync(db, principal, ct);
        if (organizacionId is null) return Results.Forbid();

        var fila = await db.Files.FirstOrDefaultAsync(x => x.Id == id, ct);
        // LO AJENO NO SE ENCUENTRA. Un 403 sobre un archivo de otra organización confirmaría que
        // existe; con 404 no se puede saber ni eso recorriendo identificadores.
        if (fila is null || fila.OrganizacionId != organizacionId) return Results.NotFound();
        return await RetirarAsync(db, fila, ct);
    }

    private static async Task<IResult> RetirarAsync(PnmcDbContext db, FileRow fila, CancellationToken ct)
    {
        var vinculos = await VinculosAsync(db, fila.Id, ct);
        if (vinculos.Count > 0)
        {
            return Results.Conflict(new
            {
                motivo = "archivo_en_uso",
                message = "Este archivo se está usando y por eso no se puede retirar. Quítalo primero de donde está.",
                usos = vinculos.Select(v => v.Descripcion).Distinct().ToList(),
            });
        }

        db.Files.Remove(fila);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>La organización por la que responde quien pregunta, si responde por alguna.</summary>
    private static async Task<int?> OrganizacionDeAsync(PnmcDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var personaId = Actor(principal);
        if (personaId <= 0) return null;
        return await db.UserEntities.AsNoTracking()
            .Where(v => v.UserId == personaId && v.IsActive && RolesDeEntidad.QueResponden.Contains(v.EntityRole))
            .Select(v => (int?)v.EntityId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Describe un archivo de la propia organización.
    /// </summary>
    /// <remarks>
    /// ANTES DESCRIBIA CUALQUIERA. Una cuenta externa podía pedir <c>/externo/archivos/4</c> y
    /// recibir el nombre, el texto alternativo, el pie y el crédito de un archivo institucional con
    /// el que no tiene nada que ver. No son los bytes, pero son datos de otro.
    /// </remarks>
    private static async Task<IResult> DescribirDeLaOrganizacion(
        PnmcDbContext db, ClaimsPrincipal principal, int id, CancellationToken ct)
    {
        var organizacionId = await OrganizacionDeAsync(db, principal, ct);
        if (organizacionId is null) return Results.Forbid();

        var fila = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (fila is null || fila.OrganizacionId != organizacionId) return Results.NotFound();
        return Results.Ok(Descripcion(fila));
    }

    /// <summary>El tope de lo que una organización puede acumular en el banco.</summary>
    /// <remarks>
    /// <para>
    /// <b>EL CANAL INSTITUCIONAL NO TIENE TOPE Y EL EXTERNO SI, y la diferencia no es desconfianza:
    /// es quién responde.</b> Quien carga desde la consola es el equipo del PNMC y sus subidas se
    /// miran; una cuenta externa la abre cualquiera, y sin cuota el banco queda expuesto a que se
    /// llene a dos megas por vez. Los bytes viven en la base, así que llenarlo no es «ocupar disco»:
    /// es hacer más lento todo lo demás.
    /// </para>
    /// <para>
    /// CINCUENTA MEGAS SON VEINTICINCO ARCHIVOS AL TOPE. Una organización con tres Festivales y sus
    /// ediciones —programa, afiche y logo cada una, más su foto de perfil— no se acerca. Quien lo
    /// alcance es que tiene un problema distinto, y lo que corresponde entonces es hablarlo, no que
    /// el sistema siga tragando en silencio.
    /// </para>
    /// </remarks>
    private const long CuotaPorOrganizacion = 50L * 1024 * 1024;

    /// <summary>
    /// La misma subida, por la puerta de las organizaciones.
    /// </summary>
    /// <remarks>
    /// <b>NO REPITE NI UNA COMPROBACION DE FORMATO.</b> Pone las dos guardas propias del canal
    /// —correo confirmado y cuota— y delega en <see cref="Subir"/>, que es donde vive la cadena que
    /// separa una imagen de un ejecutable con la cabecera pegada delante.
    /// </remarks>
    private static async Task<IResult> SubirDesdeOrganizacion(
        HttpRequest peticion, PnmcDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var personaId = Actor(principal);
        if (personaId <= 0) return Results.Unauthorized();

        // DE QUE ORGANIZACION ES. Sin esto no hay ni procedencia ni cuota, y una subida externa
        // quedaría indistinguible de una del Programa.
        var organizacionId = await db.UserEntities.AsNoTracking()
            .Where(vinculo => vinculo.UserId == personaId && vinculo.IsActive
                && RolesDeEntidad.QueResponden.Contains(vinculo.EntityRole))
            .Select(vinculo => (int?)vinculo.EntityId)
            .FirstOrDefaultAsync(ct);
        if (organizacionId is null) return Results.Forbid();

        // 1. EL CORREO CONFIRMADO, que es la regla del canal: sin él una organización no registra
        //    procesos, y subir el afiche de un Festival es parte de registrarlo.
        if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                db, organizacionId.Value, "adjuntar archivos a tus procesos", ct) is { } sinConfirmar)
        {
            return sinConfirmar;
        }

        // 2. LA CUOTA, ANTES DE LEER LOS BYTES. Medir después de cargar es reservar la memoria que
        //    se quería evitar, que es el mismo motivo por el que el tope por archivo va primero.
        //
        //    CUENTA LO QUE ENTRA, Y ANTES NO. La comprobación era «lo acumulado ya llega al tope»,
        //    así que una organización a 49,9 MB podía subir un archivo de 2 MB y quedarse en 51,9.
        //    El tamaño viaja en la cabecera del formulario, así que se sabe sin leer un solo byte.
        var entrante = peticion.HasFormContentType
            ? ((await peticion.ReadFormAsync(ct)).Files["file"]?.Length ?? 0L)
            : 0L;
        var acumulado = await db.Files.AsNoTracking()
            .Where(x => x.OrganizacionId == organizacionId.Value)
            .SumAsync(x => (long?)x.FileSizeBytes ?? 0L, ct);
        if (acumulado + entrante > CuotaPorOrganizacion)
        {
            return Results.Conflict(new
            {
                motivo = "cuota_de_archivos_agotada",
                message = "Tu organización alcanzó el espacio disponible para archivos. "
                    + "Retira material que ya no uses o escríbenos para revisarlo.",
                usadoBytes = acumulado,
                cuotaBytes = CuotaPorOrganizacion,
                archivoBytes = entrante,
            });
        }

        return await Subir(peticion, db, principal, ct, organizacionId.Value);
    }

    private static async Task<IResult> Subir(
        HttpRequest peticion, PnmcDbContext db, ClaimsPrincipal principal, CancellationToken ct,
        int? organizacionId = null)
    {
        if (!peticion.HasFormContentType)
        {
            return Results.BadRequest(new { file = AdjunteUnArchivo });
        }

        var formulario = await peticion.ReadFormAsync(ct);
        var archivo = formulario.Files["file"];
        if (archivo is null || archivo.Length == 0)
        {
            return Results.BadRequest(new { file = AdjunteUnArchivo });
        }

        // 1. EL TOPE, ANTES DE LEER NADA. Medir después de cargar es reservar la memoria que se
        //    quería evitar.
        if (archivo.Length > MediosWebContrato.MaxBytes)
        {
            return Results.BadRequest(new
            {
                file = new[]
                {
                    $"El archivo pesa {archivo.Length.ToString(CultureInfo.InvariantCulture)} bytes y el máximo es {MediosWebContrato.MaxBytes.ToString(CultureInfo.InvariantCulture)}.",
                },
            });
        }

        // 2. EL TEXTO ALTERNATIVO, ANTES DE MIRAR LOS BYTES. Que el archivo sea válido no salva a
        //    una imagen sin alternativa textual.
        var alt = (formulario["alt"].ToString() ?? string.Empty).Trim();
        if (alt.Length == 0)
        {
            return Results.BadRequest(new { alt = EscribaElTextoAlternativo });
        }
        if (alt.Length > LargoMaximoDelAlternativo)
        {
            return Results.BadRequest(new
            {
                alt = new[] { $"El texto alternativo admite {LargoMaximoDelAlternativo.ToString(CultureInfo.InvariantCulture)} caracteres y tiene {alt.Length.ToString(CultureInfo.InvariantCulture)}." },
            });
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        var datos = memoria.ToArray();

        // 3. EL FORMATO, POR SU FIRMA. Sin decir en qué paso falló: un mensaje detallado sería un
        //    manual para el siguiente intento.
        var formato = MediosWebContrato.Reconocer(datos);
        if (formato is null)
        {
            return Results.BadRequest(new { file = NoEsImagenValida });
        }

        // 4. EL TIPO DECLARADO, SI VIENE, TIENE QUE COINCIDIR.
        var declarado = (archivo.ContentType ?? string.Empty).Trim();
        if (declarado.Length > 0 && !string.Equals(declarado, formato.Value.Mime, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { file = TipoDeclaradoNoCoincide });
        }

        var huella = MediosWebContrato.Huella(datos);

        // EL MISMO ARCHIVO NO SE GUARDA DOS VECES, PERO SOLO DENTRO DE SU MISMO DUEÑO.
        //
        // LA DEDUPLICACION ERA GLOBAL Y ESO CRUZABA ORGANIZACIONES. Si la organización B subía
        // exactamente los mismos bytes que ya había subido A, se le devolvía el archivo DE A: con
        // el texto alternativo de A, con el crédito de A y con la procedencia de A. B acababa
        // enseñando en su ficha una descripción que no escribió, no podía corregirla, y la cuota se
        // le seguía cobrando a A. Tres cosas mal por ahorrar unos kilobytes.
        //
        // DENTRO DE UN MISMO DUEÑO SI TIENE SENTIDO: reutilizar la portada de un evento en otro no
        // debe duplicar dos megas, y el texto alternativo lo escribió la misma persona.
        var yaEstaba = await db.Files.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Fingerprint == huella && x.OrganizacionId == organizacionId, ct);
        if (yaEstaba is not null)
        {
            return Results.Ok(Descripcion(yaEstaba));
        }

        var ahora = DateTime.UtcNow;
        var fila = new FileRow
        {
            OriginalName = Recortar(archivo.FileName, 300) ?? "archivo",
            StoredName = (organizacionId?.ToString(CultureInfo.InvariantCulture) ?? "pnmc") + "-" + huella + ExtensionDe(formato.Value.Mime),
            MimeType = formato.Value.Mime,
            Extension = ExtensionDe(formato.Value.Mime),
            FileSizeBytes = datos.Length,
            // LA RUTA ES UNICA POR ARCHIVO, y no la de lectura a secas.
            //
            // `UQ_Archivos_RutaAlmacenamiento` lo exige, y SQLite no replica esa restricción: con
            // la ruta común a todos, las pruebas pasaban en verde y el segundo archivo que se
            // subía contra SQL Server real reventaba con un 500. Es la misma clase de fallo que
            // trajo `CatalogoEditorialEsquemaSqlServerTests`, y aquí la fija su gemela.
            //
            // El nombre almacenado ya es la huella más la extensión, así que es único por
            // contenido: dos ficheros iguales ni siquiera llegan aquí —se reutiliza el existente—.
            // LA RUTA LLEVA AL DUEÑO PORQUE LA HUELLA YA NO ES UNICA EN LA TABLA. Al deduplicar
            // por dueño, los mismos bytes pueden estar dos veces —una por organización— y
            // `UQ_Archivos_RutaAlmacenamiento` rechazaría la segunda. El prefijo distingue sin
            // tocar la restricción, que es la que impide el descuido de verdad.
            StoragePath = "/api/v1/publico/archivos/" + (organizacionId?.ToString(CultureInfo.InvariantCulture) ?? "pnmc")
                + "-" + huella + ExtensionDe(formato.Value.Mime),
            PublicUrl = null,
            AltText = alt,
            Caption = Recortar(formulario["caption"].ToString(), LargoMaximoDelPie),
            Credit = Recortar(formulario["credit"].ToString(), LargoMaximoDelCredito),
            UploadedByUserId = Actor(principal),
            // NULO ES INSTITUCIONAL. Es la procedencia, y no lo mismo que quién lo subió.
            OrganizacionId = organizacionId,
            CreatedAt = ahora,
            Content = datos,
            Fingerprint = huella,
            Width = formato.Value.Width,
            Height = formato.Value.Height,
        };

        db.Files.Add(fila);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/institucional/archivos/{fila.Id.ToString(CultureInfo.InvariantCulture)}", Descripcion(fila));
    }

    /// <summary>
    /// Sube un documento —hoy, un PDF— al banco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES UNA PUERTA APARTE Y NO UN PARAMETRO DE LA DE IMAGENES, a propósito. Las guardas de
    /// <see cref="Subir"/> están pensadas para imágenes: exigen texto alternativo, miden dimensiones
    /// y toleran 2 MiB. Añadir aquí un interruptor «acepta PDF» habría relajado esas tres cosas para
    /// todo el mundo, que es como una excepción se convierte en la regla sin que nadie lo decida.
    /// </para>
    /// <para>
    /// QUE CAMBIA RESPECTO DE UNA IMAGEN: el tope sube a 20 MiB —una guía escaneada pesa diez veces
    /// lo que un hero y no por estar mal exportada—, no se pide texto alternativo —un PDF no se
    /// describe, se abre— y el formato se comprueba con <see cref="MediosWebContrato.EsPdf"/>, que
    /// mira las dos puntas del fichero. Lo que NO cambia: el formato se reconoce por su contenido y
    /// no por lo que el cliente declare, y el tope se mide ANTES de leer nada.
    /// </para>
    /// <para>
    /// SOLO CANAL INSTITUCIONAL. El Catálogo Editorial se carga desde el Espacio de Gestión
    /// Administrativa y solo desde ahí; abrir documentos al canal externo sería conceder una
    /// capacidad que nadie ha pedido.
    /// </para>
    /// </remarks>
    private static async Task<IResult> SubirDocumento(
        HttpRequest peticion, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        if (!peticion.HasFormContentType)
        {
            return Results.BadRequest(new { file = AdjunteUnArchivo });
        }

        var formulario = await peticion.ReadFormAsync(ct);
        var archivo = formulario.Files["file"];
        if (archivo is null || archivo.Length == 0)
        {
            return Results.BadRequest(new { file = AdjunteUnArchivo });
        }

        // EL TOPE, ANTES DE LEER NADA.
        if (archivo.Length > MediosWebContrato.MaxBytesDocumento)
        {
            return Results.BadRequest(new
            {
                file = new[]
                {
                    $"El documento pesa {archivo.Length.ToString(CultureInfo.InvariantCulture)} bytes y el máximo es {MediosWebContrato.MaxBytesDocumento.ToString(CultureInfo.InvariantCulture)}.",
                },
            });
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        var datos = memoria.ToArray();

        if (!MediosWebContrato.EsPdf(datos))
        {
            return Results.BadRequest(new { file = NoEsPdfValido });
        }

        var declarado = (archivo.ContentType ?? string.Empty).Trim();
        if (declarado.Length > 0 && !string.Equals(declarado, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { file = TipoDeclaradoNoCoincide });
        }

        var huella = MediosWebContrato.Huella(datos);
        var yaEstaba = await db.Files.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Fingerprint == huella && x.OrganizacionId == null, ct);
        if (yaEstaba is not null)
        {
            return Results.Ok(Descripcion(yaEstaba));
        }

        var ahora = DateTime.UtcNow;
        var fila = new FileRow
        {
            OriginalName = Recortar(archivo.FileName, 300) ?? "documento.pdf",
            StoredName = "pnmc-" + huella + ".pdf",
            MimeType = "application/pdf",
            Extension = ".pdf",
            FileSizeBytes = datos.Length,
            StoragePath = "/api/v1/publico/archivos/pnmc-" + huella + ".pdf",
            PublicUrl = null,
            // EL NOMBRE DEL FICHERO HACE DE DESCRIPCION, y no se inventa una: un PDF se identifica
            // por cómo se llama, no por una frase que describa su aspecto.
            AltText = Recortar(archivo.FileName, LargoMaximoDelAlternativo) ?? "documento",
            Caption = Recortar(formulario["caption"].ToString(), LargoMaximoDelPie),
            Credit = Recortar(formulario["credit"].ToString(), LargoMaximoDelCredito),
            UploadedByUserId = Actor(principal),
            OrganizacionId = null,
            CreatedAt = ahora,
            Content = datos,
            Fingerprint = huella,
            // Un documento no tiene dimensiones en píxeles. Inventarlas sería mentir en la tabla.
            Width = null,
            Height = null,
        };

        db.Files.Add(fila);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/institucional/archivos/{fila.Id.ToString(CultureInfo.InvariantCulture)}", Descripcion(fila));
    }

    private static object Descripcion(FileRow fila) => new
    {
        id = fila.Id,
        nombre = fila.OriginalName,
        tipo = fila.MimeType,
        bytes = fila.FileSizeBytes,
        alt = fila.AltText,
        pie = fila.Caption,
        credito = fila.Credit,
        ancho = fila.Width,
        alto = fila.Height,
        url = "/api/v1/publico/archivos/" + fila.Id.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>La extensión que corresponde al tipo reconocido, no a la que traía el nombre.</summary>
    private static string ExtensionDe(string mime) => mime switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin",
    };

    private static string? Recortar(string? valor, int largo)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0) return null;
        return limpio.Length <= largo ? limpio : limpio[..largo];
    }

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;
}
