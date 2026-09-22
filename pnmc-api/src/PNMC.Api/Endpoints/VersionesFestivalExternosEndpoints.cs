using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Las versiones de un Festival: leerlas enteras y escribirlas enteras, con los treinta y un campos
/// del modelo de SIMUS y sus seis listas.
/// </summary>
/// <remarks>
/// <para>
/// <b>⚠ ESTE GRUPO NO ESTA REGISTRADO, Y ES DELIBERADO DESDE EL 9 DE SEPTIEMBRE DE 2026.</b>
/// `Program.cs` lo retiró con este motivo: «transportaban fechas, financiación y otras propiedades
/// de una realización, que ahora pertenecen exclusivamente a EdicionesFestival. La tabla persiste
/// para historial y propuestas, nunca como formulario externo de una Edición». Sus cinco rutas
/// devuelven 404.
/// </para>
/// <para>
/// <b>EL FICHERO NO LO SABIA, Y ESO COSTO UN ERROR.</b> El 13 de septiembre se registró leyendo su
/// ausencia como un olvido: el fichero se describe a sí mismo como la forma de escribir los treinta
/// y un campos, sin una línea que dijera que ya no se usa. Queda escrito aquí para que la próxima
/// lectura empiece por esto.
/// </para>
/// <para>
/// <b>LO QUE HAY QUE DECIDIR.</b> `ficha-festival.component.ts` sigue llamando a estas rutas —y
/// llamando «edición» a lo que pide como «versión»—, así que el editor de perfil del panel está
/// apoyado en un camino retirado. O ese componente pasa a las diez rutas de
/// `EdicionesFestivalExternosEndpoints` —que `ediciones-temporales.component.ts` ya usa bien—, o
/// esta decisión se revisa. Mientras tanto, este fichero es código sin salida: 569 líneas que
/// ninguna ruta alcanza.
/// </para>
/// <para>
/// QUE FALTABA, Y CUANTO. Hasta el canal externo sabia leer y escribir
/// ONCE campos de un Festival. `ART_MUS_FESTIVALES_VERSION` tiene treinta y una columnas y seis
/// tablas puente; el resto del modelo estaba en la base —desde el 24 de agosto— y no habia ninguna
/// ruta que lo tocara. Es decir: veinte campos que la persona no podia escribir y que nadie podia
/// leer.
/// </para>
/// <para>
/// POR QUE UN FICHERO NUEVO Y NO DENTRO DE `FestivalesExternosEndpoints`. Ese fichero ya lleva 560
/// lineas y responde por la CABECERA del Festival —crearlo, listarlo, enviarlo a revision—. La
/// version es otra cosa con su propio ciclo: sus fechas, sus catalogos y SU estado de revision.
/// Meterlo ahi seria el septimo asunto del mismo fichero.
/// </para>
/// <para>
/// LA GUARDA ES LA MISMA DE SIEMPRE, y por eso se reusa `RolesDeEntidad.QueResponden` en vez de
/// escribir la comparacion otra vez: el 28 de agosto se cerro un defecto que venia de tener esa
/// regla copiada ocho veces, y una de las copias se habia quedado atras. Aqui se pregunta por la
/// organizacion DEL FESTIVAL, no por una que venga en la peticion: si viniera en la peticion,
/// cambiarla en el cuerpo abriria el Festival ajeno.
/// </para>
/// <para>
/// LO QUE NO SE PUEDE ESCRIBIR DESDE FUERA, y es deliberado: `EstadoRegistro`, `EsVigente` y
/// `ObservacionesRechazo` no estan en `GuardarVersionFestivalSolicitud`. El estado lo mueve la
/// revision institucional y la vigencia la decide la aprobacion. Un campo que la solicitud no arma
/// no se puede colar aunque llegue en el JSON.
/// </para>
/// </remarks>
public static class VersionesFestivalExternosEndpoints
{
    /// <summary>
    /// En que estados admite el API que se edite una version.
    /// </summary>
    /// <remarks>
    /// LOS MISMOS DOS QUE LA CABECERA (`FestivalesExternosEndpoints`, `EsEditable`): borrador y
    /// ajustes solicitados. En revision no, porque cambiaria lo que el funcionario esta leyendo; ni
    /// aprobado ni publicado, porque para eso existe el circuito de propuestas de cambio.
    /// </remarks>
    private static readonly string[] EstadosEditables = ["borrador", "ajustes_solicitados"];

    public static RouteGroupBuilder MapVersionesFestivalExternosEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo").WithTags("versiones-festival-externas");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        // -----------------------------------------------------------------------------------
        // La lista de versiones de un Festival
        // -----------------------------------------------------------------------------------
        externo.MapGet("/festivales/{festivalId:int}/versiones", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            // UN FESTIVAL SIN ORGANIZACION NO LO ADMINISTRA NADIE desde fuera. `OrganizacionPrincipalId`
            // es nulable en la fila -hay filas historicas sin ella- y `Forbid` es la respuesta correcta:
            // no es que falte el recurso, es que por ese Festival no responde ninguna organizacion.
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorElFestivalAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            var etiquetas = await EtiquetasDeEstadoAsync(dbContext, cancellationToken);

            // ORDEN DESCENDENTE POR NUMERO: la edicion mas reciente arriba, que es la que se abre.
            var versiones = await dbContext.VersionesFestival.AsNoTracking()
                .Where(item => item.FestivalOrigenId == festivalId)
                .OrderByDescending(item => item.NumeroVersion)
                .ToListAsync(cancellationToken);

            return Results.Ok(versiones.Select(item => new ResumenVersionFestivalDto(
                item.Id, item.NumeroVersion, item.Nombre, item.FechaInicio, item.FechaFin,
                item.EsVigente, item.EstadoRegistro, Etiqueta(etiquetas, item.EstadoRegistro))).ToList());
        });

        // -----------------------------------------------------------------------------------
        // La ficha completa de una version
        // -----------------------------------------------------------------------------------
        externo.MapGet("/versiones/{versionId:int}", async (
            int versionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            var version = await dbContext.VersionesFestival.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);
            if (version is null) return Results.NotFound();

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == version.FestivalOrigenId, cancellationToken);
            if (festival is null) return Results.NotFound();
            // UN FESTIVAL SIN ORGANIZACION NO LO ADMINISTRA NADIE desde fuera. `OrganizacionPrincipalId`
            // es nulable en la fila -hay filas historicas sin ella- y `Forbid` es la respuesta correcta:
            // no es que falte el recurso, es que por ese Festival no responde ninguna organizacion.
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorElFestivalAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            return Results.Ok(await ADtoAsync(version, festival.Name, dbContext, cancellationToken));
        });

        // -----------------------------------------------------------------------------------
        // Crear una version
        // -----------------------------------------------------------------------------------
        externo.MapPost("/festivales/{festivalId:int}/versiones", async (
            int festivalId,
            GuardarVersionFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            // UN FESTIVAL SIN ORGANIZACION NO LO ADMINISTRA NADIE desde fuera. `OrganizacionPrincipalId`
            // es nulable en la fila -hay filas historicas sin ella- y `Forbid` es la respuesta correcta:
            // no es que falte el recurso, es que por ese Festival no responde ninguna organizacion.
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorElFestivalAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            var errores = await ValidarAsync(solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            // EL NUMERO LO PONE EL SERVIDOR Y NO LA PETICION. `UQ_VersionesFestival_Festival_Numero`
            // lo exige unico por Festival; dejarlo llegar de fuera convertiria un choque de dos
            // pestanas abiertas en un 500 de clave duplicada en vez de en una version nueva.
            var ultimo = await dbContext.VersionesFestival
                .Where(item => item.FestivalOrigenId == festivalId)
                .MaxAsync(item => (int?)item.NumeroVersion, cancellationToken) ?? 0;

            var ahora = DateTime.UtcNow;
            var version = new VersionFestivalRow
            {
                FestivalOrigenId = festivalId,
                NumeroVersion = ultimo + 1,
                // NACE COMO BORRADOR Y NO VIGENTE. La vigencia la da la aprobacion; una version
                // recien escrita que naciera vigente reemplazaria la ficha publica sin revision.
                EsVigente = false,
                EstadoRegistro = "borrador",
                NivelCobertura = festival.CoverageLevel,
                FechaPublicacion = ahora,
                FechaCreacion = ahora,
            };
            AplicarSolicitud(version, solicitud);
            dbContext.VersionesFestival.Add(version);
            await dbContext.SaveChangesAsync(cancellationToken);

            await ReemplazarListasAsync(version.Id, solicitud, dbContext, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created(
                $"/api/v1/externo/versiones/{version.Id.ToString(CultureInfo.InvariantCulture)}",
                await ADtoAsync(version, festival.Name, dbContext, cancellationToken));
        });

        // -----------------------------------------------------------------------------------
        // Guardar una version
        // -----------------------------------------------------------------------------------
        externo.MapPut("/versiones/{versionId:int}", async (
            int versionId,
            GuardarVersionFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var version = await dbContext.VersionesFestival
                .FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);
            if (version is null) return Results.NotFound();

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == version.FestivalOrigenId, cancellationToken);
            if (festival is null) return Results.NotFound();
            // UN FESTIVAL SIN ORGANIZACION NO LO ADMINISTRA NADIE desde fuera. `OrganizacionPrincipalId`
            // es nulable en la fila -hay filas historicas sin ella- y `Forbid` es la respuesta correcta:
            // no es que falte el recurso, es que por ese Festival no responde ninguna organizacion.
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorElFestivalAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            // 409 Y NO 403: no es que esta persona no pueda, es que la version no admite cambios en
            // este estado. Un 403 la mandaria a buscar un permiso que no le falta.
            if (!EsEditable(version.EstadoRegistro))
                return Results.Conflict(new { mensaje = "Esta version no admite cambios mientras esta en revision o publicada." });

            var errores = await ValidarAsync(solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            AplicarSolicitud(version, solicitud);
            await ReemplazarListasAsync(version.Id, solicitud, dbContext, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ADtoAsync(version, festival.Name, dbContext, cancellationToken));
        });

        return group;
    }

    // =============================================================================================
    // Lo que comparten las cuatro rutas
    // =============================================================================================

    internal static bool EsEditable(string? estado) =>
        estado is not null && EstadosEditables.Contains(estado, StringComparer.Ordinal);

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId)
            ? personaId : null;

    private static async Task<bool> RespondePorElFestivalAsync(
        PnmcDbContext dbContext, int personaId, int organizacionId, CancellationToken cancellationToken) =>
    // LA REGLA VIVE EN `AdministracionDeOrganizacion`. Esta copia NO comprobaba si la organización
    // sigue activa: una organización dada de baja seguía pudiendo actuar por aquí.
        await AdministracionDeOrganizacion.PuedeAdministrarAsync(dbContext, personaId, organizacionId, cancellationToken);

    /// <remarks>
    /// INTERNAL DESDE EL 29 DE AGOSTO DE 2026: la revision institucional lee las mismas ediciones
    /// que la organizacion, y la unica forma de que las dos respuestas tengan la MISMA forma es que
    /// salgan del mismo codigo. Copiar la proyeccion daria dos verdades sobre la misma fila, y la
    /// copia se queda atras en cuanto se anada una columna.
    /// </remarks>
    internal static async Task<Dictionary<string, string>> EtiquetasDeEstadoAsync(
        PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.ContentStatuses.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, item => item.Name, cancellationToken);

    internal static string? Etiqueta(Dictionary<string, string> etiquetas, string? codigo) =>
        codigo is not null && etiquetas.TryGetValue(codigo, out var nombre) ? nombre : codigo;

    /// <summary>Copia los campos escribibles de la solicitud a la fila.</summary>
    /// <remarks>
    /// EL RECORTE DE ESPACIOS VA AQUI Y NO EN CADA RUTA: crear y guardar tienen que dejar la fila
    /// igual para la misma entrada, y dos copias de esta asignacion es como dejan de hacerlo.
    /// </remarks>
    private static void AplicarSolicitud(VersionFestivalRow version, GuardarVersionFestivalSolicitud solicitud)
    {
        version.Nombre = solicitud.Nombre.Trim();
        version.Descripcion = Limpiar(solicitud.Descripcion);
        version.FechaInicio = solicitud.FechaInicio;
        version.FechaFin = solicitud.FechaFin;

        version.TipologiaFestivalId = solicitud.TipologiaFestivalId;
        version.OtraTipologia = Limpiar(solicitud.OtraTipologia);
        version.FuenteFinanciacionPrimariaId = solicitud.FuenteFinanciacionPrimariaId;
        version.OtraFuenteFinanciacionPrimaria = Limpiar(solicitud.OtraFuenteFinanciacionPrimaria);
        version.FuenteFinanciacionSecundariaId = solicitud.FuenteFinanciacionSecundariaId;
        version.OtraFuenteFinanciacionSecundaria = Limpiar(solicitud.OtraFuenteFinanciacionSecundaria);
        version.UsaEstampillaProcultura = solicitud.UsaEstampillaProcultura;

        // El director puede variar entre ediciones. Organizacion y contacto se escriben una sola
        // vez en la cabecera del Festival; no se aceptan aqui aunque subsistan columnas legadas.
        version.Director = Limpiar(solicitud.Director);

        version.PracticasMusicalesQueCongrega = Limpiar(solicitud.PracticasMusicalesQueCongrega);
        version.OtraModalidadParticipacion = Limpiar(solicitud.OtraModalidadParticipacion);
        version.OtraExpresionArtistica = Limpiar(solicitud.OtraExpresionArtistica);
    }

    private static string? Limpiar(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    /// <summary>Borra y vuelve a escribir las ocho listas de la version.</summary>
    /// <remarks>
    /// BORRAR Y REESCRIBIR, Y NO CALCULAR LA DIFERENCIA. Las ocho son listas cortas —cinco de
    /// catalogo, mas localizaciones, aliadas y archivos— y ninguna lleva dato propio que se pierda
    /// al reescribirla: son puentes con su identidad y su fecha. Calcular altas y bajas seria mas
    /// codigo para el mismo resultado y un sitio mas donde equivocarse.
    /// </remarks>
    private static async Task ReemplazarListasAsync(
        int versionId, GuardarVersionFestivalSolicitud solicitud, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var ahora = DateTime.UtcNow;

        await dbContext.BorrarAsync<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarAsync<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarAsync<ExpresionArtisticaDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarAsync<ModalidadParticipacionDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarAsync<TipoIngresoDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<LocalizacionDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<EntidadAliadaDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<ArchivoDeRegistroRow>(Modulos.VersionesDeFestival, versionId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // `Distinct()` EN LAS CINCO DE CATALOGO: cada una tiene un UNIQUE por (módulo, registro,
        // valor), y una lista con el mismo identificador dos veces —que el front puede mandar sin
        // querer— moriria en la base con un mensaje que no nombra ningun campo de la pantalla. Lo
        // aplica `Agregar`, que es por donde pasan las cinco.
        dbContext.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, versionId, solicitud.PracticasMusicalesIds, ahora);
        dbContext.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, versionId, solicitud.TerritoriosSonorosIds, ahora);
        dbContext.Agregar<ExpresionArtisticaDeRegistroRow>(Modulos.VersionesDeFestival, versionId, solicitud.ExpresionesArtisticasIds, ahora);
        dbContext.Agregar<ModalidadParticipacionDeRegistroRow>(Modulos.VersionesDeFestival, versionId, solicitud.ModalidadesParticipacionIds, ahora);
        dbContext.Agregar<TipoIngresoDeRegistroRow>(Modulos.VersionesDeFestival, versionId, solicitud.TiposIngresoIds, ahora);

        foreach (var lugar in solicitud.Localizaciones)
            dbContext.LocalizacionesDeRegistro.Add(new LocalizacionDeRegistroRow
            {
                ModuloId = Modulos.VersionesDeFestival,
                RegistroId = Clasificaciones.Clave(versionId),
                CodigoDepartamento = lugar.CodigoDepartamento,
                CodigoMunicipio = lugar.CodigoMunicipio,
                ZonaUrbanoRuralId = lugar.ZonaUrbanoRuralId,
                TitulacionColectivaId = lugar.TitulacionColectivaId,
                FechaCreacion = ahora,
            });

        foreach (var aliada in solicitud.EntidadesAliadas)
            dbContext.EntidadesAliadasDeRegistro.Add(new EntidadAliadaDeRegistroRow
            {
                ModuloId = Modulos.VersionesDeFestival,
                RegistroId = Clasificaciones.Clave(versionId),
                Nombre = Limpiar(aliada.Nombre),
                Correo = Limpiar(aliada.Correo),
                NaturalezaEntidadId = aliada.NaturalezaEntidadId,
                EntidadId = aliada.EntidadId,
                FechaCreacion = ahora,
            });

        // EMPIEZA EN 1 Y NO EN 0. `CK_VersionesFestivalArchivos_Orden` exige `OrdenVisualizacion > 0`,
        // asi que la primera pieza de material moria con un 500 y el formulario no podia guardar
        // ningun archivo. Lo encontro `CamposDelVolcadoDeFestivalTests`; las
        // doce pruebas anteriores de versiones no mandaban archivos, y la suite de SQLite no declara
        // restricciones CHECK, asi que alli habria pasado en verde igual.
        var orden = 1;
        foreach (var archivo in solicitud.Archivos)
            dbContext.ArchivosDeRegistro.Add(new ArchivoDeRegistroRow
            {
                ModuloId = Modulos.VersionesDeFestival,
                RegistroId = Clasificaciones.Clave(versionId),
                // «material» CUANDO NO LLEGA: `RolArchivo` es nuestro y no del volcado de SIMUS,
                // que solo tiene URL_ARCHIVO y DESCRIPCION_ARCHIVO. Exigirlo obligaba al formulario
                // a ofrecer un desplegable de tres opciones inventadas.
                RolArchivo = string.IsNullOrWhiteSpace(archivo.RolArchivo) ? RolesDeArchivo.Material : archivo.RolArchivo.Trim(),
                DescripcionArchivo = Limpiar(archivo.DescripcionArchivo),
                Url = Limpiar(archivo.Url),
                OrdenVisualizacion = orden++,
                FechaCreacion = ahora,
            });
    }

    /// <summary>Lo que se comprueba ANTES de enviar, para que la base no conteste por el formulario.</summary>
    /// <remarks>
    /// SE ADELANTA A LA BASE, NO LA SUSTITUYE. Un identificador de catalogo que no existe muere en
    /// la clave ajena con un mensaje que nombra la restriccion y no el campo; aqui se convierte en
    /// un 422 con la clave del campo, que es lo que el formulario sabe pintar al lado del control.
    /// </remarks>
    private static async Task<Dictionary<string, string[]>> ValidarAsync(
        GuardarVersionFestivalSolicitud solicitud, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(solicitud.Nombre))
            errores["nombre"] = ["El nombre de la version es obligatorio."];

        // LAS DOS FECHAS SE COMPARAN AQUI porque la base no las compara: `dbo.VersionesFestival` no
        // tiene ningun CHECK que ate `FechaFin` a `FechaInicio`, asi que un festival que termina
        // antes de empezar se guardaria sin queja.
        if (solicitud.FechaInicio is not null && solicitud.FechaFin is not null && solicitud.FechaFin < solicitud.FechaInicio)
            errores["fechaFin"] = ["La fecha de cierre no puede ser anterior a la de apertura."];

        await ExigirDelCatalogoAsync(errores, "tipologiaFestivalId", solicitud.TipologiaFestivalId,
            id => dbContext.TipologiasFestival.AnyAsync(item => item.Id == id, cancellationToken));
        await ExigirDelCatalogoAsync(errores, "fuenteFinanciacionPrimariaId", solicitud.FuenteFinanciacionPrimariaId,
            id => dbContext.FuentesFinanciacion.AnyAsync(item => item.Id == id, cancellationToken));
        await ExigirDelCatalogoAsync(errores, "fuenteFinanciacionSecundariaId", solicitud.FuenteFinanciacionSecundariaId,
            id => dbContext.FuentesFinanciacion.AnyAsync(item => item.Id == id, cancellationToken));

        await ExigirListaDelCatalogoAsync(errores, "practicasMusicalesIds", solicitud.PracticasMusicalesIds,
            ids => dbContext.PracticasMusicales.CountAsync(item => ids.Contains(item.Id), cancellationToken));
        await ExigirListaDelCatalogoAsync(errores, "territoriosSonorosIds", solicitud.TerritoriosSonorosIds,
            ids => dbContext.TerritoriosSonoros.CountAsync(item => ids.Contains(item.Id), cancellationToken));
        await ExigirListaDelCatalogoAsync(errores, "expresionesArtisticasIds", solicitud.ExpresionesArtisticasIds,
            ids => dbContext.ExpresionesArtisticas.CountAsync(item => ids.Contains(item.Id), cancellationToken));
        await ExigirListaDelCatalogoAsync(errores, "modalidadesParticipacionIds", solicitud.ModalidadesParticipacionIds,
            ids => dbContext.ModalidadesParticipacion.CountAsync(item => ids.Contains(item.Id), cancellationToken));
        await ExigirListaDelCatalogoAsync(errores, "tiposIngresoIds", solicitud.TiposIngresoIds,
            ids => dbContext.TiposIngreso.CountAsync(item => ids.Contains(item.Id), cancellationToken));

        // EL MUNICIPIO SE COMPRUEBA CONTRA DIVIPOLA, y con el par completo: `FK_..._Divipola` es
        // compuesta, asi que un municipio de otro departamento pasa la comprobacion de existencia
        // por separado y muere en la clave ajena.
        foreach (var lugar in solicitud.Localizaciones)
        {
            var existe = await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(
                item => item.DepartmentCode == lugar.CodigoDepartamento && item.MunicipalityCode == lugar.CodigoMunicipio,
                cancellationToken);
            if (!existe)
            {
                errores["localizaciones"] = ["Uno o mas municipios no existen en DIVIPOLA, o no pertenecen al departamento indicado."];
                break;
            }
        }

        return errores;
    }

    private static async Task ExigirDelCatalogoAsync(
        Dictionary<string, string[]> errores, string clave, int? id, Func<int, Task<bool>> existe)
    {
        if (id is null) return;
        if (!await existe(id.Value)) errores[clave] = ["El valor elegido no esta en el catalogo."];
    }

    private static async Task ExigirListaDelCatalogoAsync(
        Dictionary<string, string[]> errores, string clave, IReadOnlyList<int> ids, Func<int[], Task<int>> contar)
    {
        var distintos = ids.Distinct().ToArray();
        if (distintos.Length == 0) return;
        if (await contar(distintos) != distintos.Length)
            errores[clave] = ["Uno o mas valores elegidos no estan en el catalogo."];
    }

    internal static async Task<FichaVersionFestivalDto> ADtoAsync(
        VersionFestivalRow version, string festivalNombre, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var etiquetas = await EtiquetasDeEstadoAsync(dbContext, cancellationToken);

        var practicas = await dbContext.De<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .Join(dbContext.PracticasMusicales.AsNoTracking(), r => r.ValorId, c => c.Id,
                (_, c) => new { c.Id, c.Nombre })
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var territorios = await dbContext.De<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .Join(dbContext.TerritoriosSonoros.AsNoTracking(), r => r.ValorId, c => c.Id,
                (_, c) => new { c.Id, c.Nombre })
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var expresiones = await dbContext.De<ExpresionArtisticaDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .Join(dbContext.ExpresionesArtisticas.AsNoTracking(), r => r.ValorId, c => c.Id,
                (_, c) => new { c.Id, c.Nombre, c.Orden })
            .OrderBy(item => item.Orden)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(cancellationToken);
        var modalidades = await dbContext.De<ModalidadParticipacionDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .Join(dbContext.ModalidadesParticipacion.AsNoTracking(), r => r.ValorId, c => c.Id,
                (_, c) => new { c.Id, c.Nombre, c.Orden })
            .OrderBy(item => item.Orden)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(cancellationToken);
        var ingresos = await dbContext.De<TipoIngresoDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .Join(dbContext.TiposIngreso.AsNoTracking(), r => r.ValorId, c => c.Id,
                (_, c) => new { c.Id, c.Nombre, c.Orden })
            .OrderBy(item => item.Orden)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(cancellationToken);

        // EL NOMBRE DEL MUNICIPIO SE RESUELVE AQUI Y VIAJA CON LA FILA. Un codigo DIVIPOLA en
        // pantalla no es un dato: es el identificador con el que la base guarda a Medellin. Es la
        // misma decision que ya tomo el perfil de la organizacion, que manda `NombreDepartamento`.
        var filasLugar = await dbContext.Relacion<LocalizacionDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .ToListAsync(cancellationToken);
        var zonas = await dbContext.ZonasUrbanoRural.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Nombre, cancellationToken);
        var titulaciones = await dbContext.TitulacionesColectivas.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Nombre, cancellationToken);
        var localizaciones = new List<LocalizacionVersionDto>();
        foreach (var lugar in filasLugar)
        {
            var divipola = await dbContext.DivipolaLocations.AsNoTracking().FirstOrDefaultAsync(
                item => item.DepartmentCode == lugar.CodigoDepartamento && item.MunicipalityCode == lugar.CodigoMunicipio,
                cancellationToken);
            localizaciones.Add(new LocalizacionVersionDto(
                lugar.Id, lugar.CodigoDepartamento, divipola?.DepartmentName,
                lugar.CodigoMunicipio, divipola?.MunicipalityName,
                lugar.ZonaUrbanoRuralId, lugar.ZonaUrbanoRuralId is int z && zonas.TryGetValue(z, out var nz) ? nz : null,
                lugar.TitulacionColectivaId, lugar.TitulacionColectivaId is int tc && titulaciones.TryGetValue(tc, out var nt) ? nt : null));
        }

        var naturalezas = await dbContext.NaturalezasEntidad.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Nombre, cancellationToken);
        var aliadas = (await dbContext.Relacion<EntidadAliadaDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .ToListAsync(cancellationToken))
            .Select(item => new EntidadAliadaVersionDto(
                item.Id, item.Nombre, item.Correo, item.NaturalezaEntidadId,
                item.NaturalezaEntidadId is int n && naturalezas.TryGetValue(n, out var nn) ? nn : null))
            .ToList();

        var archivos = await dbContext.Relacion<ArchivoDeRegistroRow>(Modulos.VersionesDeFestival, version.Id).AsNoTracking()
            .OrderBy(item => item.OrdenVisualizacion)
            .Select(item => new ArchivoVersionDto(item.Id, item.RolArchivo, item.Url, item.OrdenVisualizacion, item.DescripcionArchivo))
            .ToListAsync(cancellationToken);

        var tipologia = version.TipologiaFestivalId is int tid
            ? await dbContext.TipologiasFestival.AsNoTracking().Where(item => item.Id == tid).Select(item => item.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        var fuente1 = version.FuenteFinanciacionPrimariaId is int f1
            ? await dbContext.FuentesFinanciacion.AsNoTracking().Where(item => item.Id == f1).Select(item => item.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        var fuente2 = version.FuenteFinanciacionSecundariaId is int f2
            ? await dbContext.FuentesFinanciacion.AsNoTracking().Where(item => item.Id == f2).Select(item => item.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new FichaVersionFestivalDto(
            version.Id, version.FestivalOrigenId, festivalNombre, version.NumeroVersion, version.EsVigente,
            version.Nombre, version.Descripcion, version.FechaInicio, version.FechaFin,
            version.TipologiaFestivalId, tipologia, version.OtraTipologia,
            version.FuenteFinanciacionPrimariaId, fuente1, version.OtraFuenteFinanciacionPrimaria,
            version.FuenteFinanciacionSecundariaId, fuente2, version.OtraFuenteFinanciacionSecundaria,
            version.UsaEstampillaProcultura,
            version.Director,
            version.PracticasMusicalesQueCongrega, version.OtraModalidadParticipacion, version.OtraExpresionArtistica,
            practicas, territorios, expresiones, modalidades, ingresos,
            localizaciones, aliadas, archivos,
            version.EstadoRegistro, Etiqueta(etiquetas, version.EstadoRegistro), version.ObservacionesRechazo,
            EsEditable(version.EstadoRegistro), version.FechaCreacion);
    }
}
