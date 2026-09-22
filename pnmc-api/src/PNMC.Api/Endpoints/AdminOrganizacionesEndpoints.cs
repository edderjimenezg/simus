using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Las organizaciones del ecosistema, con la persona que responde por cada una.
/// </summary>
/// <remarks>
/// <para>
/// La persona responsable —nombre, correo, teléfono, documento y autorización de datos— vive en
/// <c>EntidadesResponsable</c>, en una tabla aparte y por un motivo de privacidad que está escrito
/// en <see cref="EntidadResponsableRow"/>. No se deduce de la cuenta institucional asignada al
/// registro, porque esa cuenta puede ser quien lo revisa y no quien representa a la organización.
/// </para>
/// <para>
/// EL RESPONSABLE SE RESUELVE EN DOS INTENTOS, Y LA RESPUESTA DICE CUAL DE LOS DOS FUE.
/// <c>EntidadesResponsable</c> es la fuente buena, pero la tabla nació y
/// solo la escribe el alta externa: de las siete organizaciones de la base local, UNA tiene fila.
/// Si esta ruta leyera solo esa tabla, seis de siete saldrían «sin responsable» y la pantalla
/// parecería rota. El segundo intento es la cuenta vinculada en <c>UsuariosEntidades</c> con rol de
/// administrador o propietario. Los dos casos viajan con <c>Origen</c> —«declarado» o «cuenta»—
/// porque no valen lo mismo: el primero lo firmó una persona identificada, el segundo es una
/// deducción a partir de quién puede administrar.
/// </para>
/// <para>
/// LA CEDULA NO SALE. <c>EntidadesResponsable</c> guarda tipo y número de documento; aquí viaja
/// solo si HAY documento registrado, no cuál es. Un listado no necesita el número para hacer su
/// trabajo, y multiplicar la cédula de un ciudadano por cada fila de una tabla que se pinta en
/// pantalla es exactamente lo que la tabla aparte existe para evitar.
/// </para>
/// <para>
/// LOS PROCESOS SE CUENTAN CON SEIS CONSULTAS, NO CON UNA POR FILA. Cada tabla de proceso apunta a
/// su organización con una columna distinta —<c>Festivales.OrganizacionPrincipalId</c> y
/// <c>OrganizacionResponsableId</c> en las otras cinco—, así que se agrupa una vez por tabla sobre
/// los identificadores de la página.
/// </para>
/// </remarks>
public static class AdminOrganizacionesEndpoints
{
    private const string TipoOrganizacion = "organizacion";

    private const int TamanoPorOmision = 20;

    /// <summary>
    /// Tope duro de página. Cada página dispara ocho consultas de apoyo —responsables, cuentas y
    /// seis recuentos de proceso—, así que el servidor decide cuánto trabajo acepta pedirse.
    /// </summary>
    private const int TamanoMaximo = 100;

    /// <summary>El responsable salió de <c>EntidadesResponsable</c>: lo declaró una persona.</summary>
    public const string OrigenDeclarado = "declarado";

    /// <summary>El responsable se dedujo de la cuenta que administra la organización.</summary>
    public const string OrigenCuenta = "cuenta";

    /// <summary>
    /// Los roles de <c>UsuariosEntidades</c> que valen como responsable de respaldo.
    /// </summary>
    /// <remarks>
    /// ERA UNA LISTA PROPIA Y AHORA ES LA COMPARTIDA. Esta consola ya aceptaba los dos roles
    /// mientras el canal externo aceptaba solo «administrador», y esa discrepancia entre las dos
    /// mitades dejaba a doce responsables sin poder administrar su organización desde fuera. Se
    /// unificó en <see cref="RolesDeEntidad.QueResponden"/>, que lleva
    /// escrito el motivo entero.
    /// </remarks>
    private static readonly string[] RolesQueResponden = RolesDeEntidad.QueResponden;

    /// <summary>Las columnas por las que la tabla se deja ordenar. Lo que no este aqui no ordena.</summary>
    /// <remarks>
    /// SE ORDENA EN SQL, ANTES DE PAGINAR, Y ESO NO ES UN DETALLE. Esta ruta devuelve una pagina de
    /// como mucho <see cref="TamanoMaximo"/> filas: ordenar en el navegador ordenaria ESA pagina y
    /// dejaria fuera lo que no vino, con la tabla afirmando «primero por fecha» mientras la fila mas
    /// antigua se quedo en el servidor. Cuatro de las seis claves son columnas de <c>Entidades</c>;
    /// las otras dos —responsable y procesos— se calculan con subconsultas correlacionadas, para que
    /// el orden valga sobre TODO el resultado y no sobre lo que ya se habia traido.
    /// </remarks>
    /// <remarks>
    /// LO QUE CUESTA, DICHO. Ordenar por «procesos» obliga al motor a contar seis tablas por cada
    /// fila que entre en el orden, no por cada fila de la pagina. Con las organizaciones del
    /// ecosistema —siete en la base local, del orden de miles en el peor caso— eso es aceptable;
    /// si esta tabla creciera un orden de magnitud, el recuento tendria que precalcularse.
    /// </remarks>
    public const string OrdenNombre = "nombre";

    public const string OrdenResponsable = "responsable";

    public const string OrdenProcesos = "procesos";

    public const string OrdenTerritorio = "territorio";

    public const string OrdenEstado = "estado";

    public const string OrdenCreacion = "creacion";

    private static readonly string[] OrdenesValidos =
        [OrdenNombre, OrdenResponsable, OrdenProcesos, OrdenTerritorio, OrdenEstado, OrdenCreacion];


    /// <summary>
    /// Estado de la organización, en palabras.
    /// </summary>
    /// <remarks>
    /// LA LISTA VIVE EN <see cref="EstadosDeOrganizacion"/>. Hasta este
    /// fichero tenía su propio diccionario con los ocho códigos del catálogo de contenidos, seis de
    /// los cuales no significaban nada para una organización.
    /// </remarks>
    private static string EtiquetarEstado(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Sin estado" : EstadosDeOrganizacion.Etiquetar(codigo);

    public static RouteGroupBuilder MapAdminOrganizacionesEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin/organizaciones").WithTags("admin-organizaciones");
        admin.RequireAuthorization(SimusAuthentication.InstitutionalPolicy);
        admin.ExigeModulo("organizaciones");

        admin.MapGet("/", async (
            string? q,
            string? estado,
            string? departamento,
            string? orden,
            string? direccion,
            int? pagina,
            int? tamano,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var paginaPedida = Math.Max(1, pagina ?? 1);
            var tamanoPedido = Math.Clamp(tamano ?? TamanoPorOmision, 1, TamanoMaximo);
            var busqueda = (q ?? string.Empty).Trim();
            var estadoPedido = (estado ?? string.Empty).Trim().ToLowerInvariant();
            var departamentoPedido = (departamento ?? string.Empty).Trim().ToLowerInvariant();

            // UNA COLUMNA QUE NO EXISTE NO ES UN 400: es una peticion vieja, un enlace guardado o un
            // parametro mal escrito, y la tabla tiene que seguir mostrando el registro. Se cae al
            // orden por nombre, y la respuesta DICE cual quedo puesto para que la flechita de la
            // pantalla no acabe pintada sobre una cabecera que no ordeno nada.
            var ordenNormalizado = (orden ?? string.Empty).Trim().ToLowerInvariant();
            var ordenPedido = OrdenesValidos.Contains(ordenNormalizado) ? ordenNormalizado : OrdenNombre;
            var ascendente = !string.Equals((direccion ?? string.Empty).Trim(), "desc", StringComparison.OrdinalIgnoreCase);

            // NO SE FILTRA POR `Activo`. Una organización dada de baja tiene que seguir viéndose
            // en la consola que la administra: esconderla es lo que produce el «¿dónde se fue?»
            // que nadie sabe responder. Viaja con su marca y la tabla la distingue.
            var baseDeOrganizaciones = dbContext.EntityProfiles.AsNoTracking()
                .Where(fila => fila.EntityType == TipoOrganizacion);

            // CADA FILTRO SE APLICA POR SEPARADO PARA PODER DEJARLO FUERA DE SU PROPIA FACETA. Las
            // cifras de las pestañas de estado se cuentan con la búsqueda y el departamento puestos
            // pero SIN el filtro de estado; las del desplegable de territorio, al revés. Contarlas
            // sobre el resultado final dejaría en cero todas las opciones no elegidas, y contarlas
            // sobre el total prometería filas que el otro filtro ya descartó.
            IQueryable<EntityProfileRow> ConBusqueda(IQueryable<EntityProfileRow> origen) =>
                busqueda.Length == 0
                    ? origen
                    : origen.Where(fila =>
                        fila.Name.Contains(busqueda)
                        || (fila.LegalName != null && fila.LegalName.Contains(busqueda))
                        || (fila.ContactEmail != null && fila.ContactEmail.Contains(busqueda))
                        || (fila.IdentificationNumber != null && fila.IdentificationNumber.Contains(busqueda)));

            IQueryable<EntityProfileRow> ConEstado(IQueryable<EntityProfileRow> origen) =>
                estadoPedido.Length == 0 || estadoPedido == "todos"
                    ? origen
                    : origen.Where(fila => fila.StatusCode == estadoPedido);

            IQueryable<EntityProfileRow> ConDepartamento(IQueryable<EntityProfileRow> origen)
            {
                if (departamentoPedido.Length == 0 || departamentoPedido == "todos") return origen;

                // «sin_territorio» ES UNA OPCION Y NO UN HUECO. En la base local dos de las siete
                // organizaciones no tienen departamento —la entidad del Ministerio entre ellas—, y
                // sin este cajón no habría manera de pedirlas.
                if (departamentoPedido == OrganizacionTerritorioDto.SinTerritorio)
                {
                    return origen.Where(fila => fila.HeadquartersDepartmentCode == null || fila.HeadquartersDepartmentCode == "");
                }

                return origen.Where(fila => fila.HeadquartersDepartmentCode == departamentoPedido);
            }

            var estados = (await ConDepartamento(ConBusqueda(baseDeOrganizaciones))
                    .GroupBy(fila => fila.StatusCode)
                    .Select(g => new { Estado = g.Key, Total = g.Count() })
                    .ToListAsync(cancellationToken))
                .Select(x => new OrganizacionEstadoDto(x.Estado, EtiquetarEstado(x.Estado), x.Total))
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Etiqueta, StringComparer.Ordinal)
                .ToList();

            var opcionesDeTerritorio = await ContarPorDepartamentoAsync(
                dbContext,
                ConEstado(ConBusqueda(baseDeOrganizaciones)),
                cancellationToken);

            var consulta = ConDepartamento(ConEstado(ConBusqueda(baseDeOrganizaciones)));

            var total = await consulta.CountAsync(cancellationToken);

            var filas = await Ordenar(consulta, dbContext, ordenPedido, ascendente)
                .Skip((paginaPedida - 1) * tamanoPedido)
                .Take(tamanoPedido)
                .ToListAsync(cancellationToken);

            var identificadores = filas.Select(fila => fila.Id).ToList();

            var responsables = await ResolverResponsablesAsync(dbContext, identificadores, cancellationToken);
            var cuentas = await ResolverCuentasResponsablesAsync(dbContext, identificadores, cancellationToken);
            var procesos = await ContarProcesosAsync(dbContext, identificadores, cancellationToken);
            var territorios = await ResolverTerritoriosAsync(dbContext, filas, cancellationToken);
            // DE DONDE VINO CADA UNA, para toda la pagina en dos consultas y no dos por fila.
            var procedencias = await ProcedenciaDeRegistro.LeerVariasAsync(
                dbContext,
                Modulos.Organizaciones,
                identificadores.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList(),
                cancellationToken);

            var items = filas.Select(fila =>
            {
                responsables.TryGetValue(fila.Id, out var responsable);
                procesos.TryGetValue(fila.Id, out var conteo);
                territorios.TryGetValue(fila.Id, out var territorio);
                procedencias.TryGetValue(fila.Id.ToString(CultureInfo.InvariantCulture), out var procedencia);
                cuentas.TryGetValue(fila.Id, out var correos);

                return new OrganizacionAdminDto(
                    fila.Id.ToString(CultureInfo.InvariantCulture),
                    fila.Name,
                    fila.LegalName,
                    fila.IdentificationNumber,
                    fila.ContactEmail,
                    fila.ContactPhone,
                    fila.StatusCode,
                    EtiquetarEstado(fila.StatusCode),
                    fila.IsActive,
                    fila.IsInstitutional,
                    territorio ?? string.Empty,
                    responsable,
                    conteo ?? OrganizacionProcesosDto.Vacio,
                    procedencia,
                    correos?.AlgunoConfirmado ?? false,
                    correos?.Correos ?? [],
                    fila.CreatedAt,
                    fila.UpdatedAt);
            }).ToList();

            return Results.Ok(new OrganizacionesRespuestaDto(
                total,
                paginaPedida,
                tamanoPedido,
                estados,
                items,
                opcionesDeTerritorio,
                ordenPedido,
                ascendente ? "asc" : "desc"));
        });

        /// <summary>
        /// LA FICHA DE UNA ORGANIZACIÓN, no solo su fila. está definido el 2 de septiembre de
        /// 2026: «abrir ficha; consultar procesos administrados; revisar solicitudes; revisar
        /// vinculaciones; consultar historial». La fila de la tabla ya trae cuántos procesos
        /// administra -<see cref="OrganizacionProcesosDto"/>-; esta ruta trae CUÁLES, con nombre, más
        /// las solicitudes y reclamaciones que la involucran y su propio historial de auditoría.
        /// </summary>
        admin.MapGet("/{id:int}", async (
            int id,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var fila = await dbContext.EntityProfiles.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.EntityType == TipoOrganizacion, cancellationToken);
            if (fila is null) return Results.NotFound();

            var responsables = await ResolverResponsablesAsync(dbContext, [id], cancellationToken);
            var cuentas = await ResolverCuentasResponsablesAsync(dbContext, [id], cancellationToken);
            var correos = cuentas.GetValueOrDefault(id);
            var procesos = await ContarProcesosAsync(dbContext, [id], cancellationToken);
            var territorios = await ResolverTerritoriosAsync(dbContext, [fila], cancellationToken);
            responsables.TryGetValue(id, out var responsable);
            procesos.TryGetValue(id, out var conteo);
            territorios.TryGetValue(id, out var territorio);
            var procedencia = await ProcedenciaDeRegistro.LeerAsync(
                dbContext, Modulos.Organizaciones, id.ToString(CultureInfo.InvariantCulture), cancellationToken);

            var organizacion = new OrganizacionAdminDto(
                fila.Id.ToString(CultureInfo.InvariantCulture),
                fila.Name,
                fila.LegalName,
                fila.IdentificationNumber,
                fila.ContactEmail,
                fila.ContactPhone,
                fila.StatusCode,
                EtiquetarEstado(fila.StatusCode),
                fila.IsActive,
                fila.IsInstitutional,
                territorio ?? string.Empty,
                responsable,
                conteo ?? OrganizacionProcesosDto.Vacio,
                procedencia,
                correos?.AlgunoConfirmado ?? false,
                correos?.Correos ?? [],
                fila.CreatedAt,
                fila.UpdatedAt);

            // CUÁLES FESTIVALES, EN QUÉ ESTADO, Y CUÁLES SE QUEDARÍAN SIN NADIE. Festival es el único
            // proceso habilitado en esta etapa; los demás directorios no forman parte de la frontera
            // administrativa actual. La consulta es la misma que mira el cierre, para que la ficha no
            // pueda enseñar una cuenta y el cierre otra.
            var festivales = (await CustodiaDeProcesos.DeLaOrganizacionAsync(dbContext, id, cancellationToken))
                .Select(proceso => new ProcesoDeOrganizacionDto(
                    proceso.Id.ToString(CultureInfo.InvariantCulture),
                    proceso.Nombre,
                    proceso.Estado,
                    proceso.DejaHuerfano))
                .ToList();

            // SOLICITUDES Y VINCULACIONES QUE INVOLUCRAN A ESTA ORGANIZACIÓN, CON EL NOMBRE DEL
            // FESTIVAL Y NO SOLO SU ID -mismo motivo que ya corrigió la bandeja de Solicitudes-.
            var solicitudesCrudo = await dbContext.RecordLinkRequests.AsNoTracking()
                .Where(item => item.EntidadId == id)
                .OrderByDescending(item => item.UpdatedAt)
                .ToListAsync(cancellationToken);
            var idsDeFestivalSolicitud = solicitudesCrudo
                .Where(item => int.TryParse(item.RecordId, out _))
                .Select(item => int.Parse(item.RecordId, CultureInfo.InvariantCulture))
                .Distinct().ToArray();
            var nombresDeFestivalSolicitud = idsDeFestivalSolicitud.Length == 0
                ? new Dictionary<int, string>()
                : await dbContext.FestivalRecords.AsNoTracking()
                    .Where(item => idsDeFestivalSolicitud.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
            var solicitudes = solicitudesCrudo.Select(item =>
            {
                var nombreRegistro = int.TryParse(item.RecordId, out var festivalId) && nombresDeFestivalSolicitud.TryGetValue(festivalId, out var nombre) ? nombre : null;
                return new RecordLinkRequestDto(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.ModuloId,
                    item.RecordId,
                    item.RequestingUserId.ToString(CultureInfo.InvariantCulture),
                    item.EntidadId?.ToString(CultureInfo.InvariantCulture),
                    item.RequestedScope,
                    item.Reason,
                    item.EvidenceText ?? string.Empty,
                    item.Status,
                    item.ReviewComment ?? string.Empty,
                    item.CreatedAt,
                    item.UpdatedAt,
                    nombreRegistro,
                    fila.Name);
            }).ToList();

            // RECLAMACIONES: LA ORGANIZACIÓN COMO SOLICITANTE. Reclamar la administración de un
            // Festival es justamente una de las «vinculaciones» que se define poder revisar
            // desde la ficha.
            var reclamacionesCrudo = await dbContext.AdministrationClaims.AsNoTracking()
                .Where(item => item.RequestingOrganizationId == id)
                .OrderByDescending(item => item.UpdatedAt)
                .ToListAsync(cancellationToken);
            var idsDeFestivalReclamacion = reclamacionesCrudo
                .Where(item => int.TryParse(item.CanonicalRecordId, out _))
                .Select(item => int.Parse(item.CanonicalRecordId, CultureInfo.InvariantCulture))
                .Distinct().ToArray();
            var nombresDeFestivalReclamacion = idsDeFestivalReclamacion.Length == 0
                ? new Dictionary<int, string>()
                : await dbContext.FestivalRecords.AsNoTracking()
                    .Where(item => idsDeFestivalReclamacion.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
            var reclamaciones = reclamacionesCrudo.Select(item =>
            {
                var registroNombre = int.TryParse(item.CanonicalRecordId, out var festivalId) && nombresDeFestivalReclamacion.TryGetValue(festivalId, out var nombre) ? nombre : null;
                return new ReclamacionAdministracionDto(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.ModuloId,
                    item.CanonicalRecordId,
                    item.RequestingOrganizationId.ToString(CultureInfo.InvariantCulture),
                    item.Status,
                    item.Justification,
                    item.SignalsJson,
                    item.DecisionReason,
                    item.CreatedAt,
                    item.SubmittedAt,
                    item.DecidedAt,
                    registroNombre,
                    fila.Name);
            }).ToList();

            // EL HISTORIAL NO VIAJA EN LA FICHA DESDE EL 12 DE SEPTIEMBRE DE 2026. Esta consulta
            // leía la misma bitácora que `/admin/auditoria` y la devolvía con otra forma, para que
            // la pantalla la pintara con una lista escrita a mano. Dos lecturas del mismo dato son
            // dos sitios donde decidir qué cuenta como actuación, y por eso una de las dos acabó
            // enseñando el verbo técnico en vez de lo que hizo la persona. Ahora la consola abre el
            // componente compartido, igual que en Noticias, Agenda y Catálogo Editorial.

            return Results.Ok(new OrganizacionFichaDto(
                organizacion,
                festivales,
                solicitudes,
                reclamaciones));
        });

        return group;
    }

    /// <summary>
    /// Pone el orden pedido en SQL, antes de paginar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL DESEMPATE POR <c>Id</c> ESTA EN LAS SEIS RAMAS Y NO ES ADORNO. Cinco de las siete
    /// organizaciones de la base local se crearon en el mismo segundo y las cinco viven en el mismo
    /// municipio: con la clave empatada, el motor puede devolverlas en distinto orden en dos
    /// peticiones, y entonces una fila sale dos veces mientras otra no sale nunca.
    /// </para>
    /// <para>
    /// LAS DOS CLAVES CALCULADAS REPITEN UNA LOGICA QUE YA VIVE EN
    /// <see cref="ResolverResponsablesAsync"/> Y <see cref="ContarProcesosAsync"/>, y esa es la
    /// deuda de este método: son dos escrituras de la misma regla, una en SQL para ordenar y otra en
    /// C# para pintar, y pueden separarse sin que el compilador diga nada. Lo que las mantiene
    /// juntas es una prueba —«El_Orden_Por_Responsable_Coincide_Con_El_Nombre_Que_Se_Pinta»— que
    /// compara la clave con el valor devuelto: si se separan, esa prueba se pone en rojo.
    /// </para>
    /// <para>
    /// EL TERRITORIO SE ORDENA POR MUNICIPIO Y DESPUES POR DEPARTAMENTO, que es como se lee la
    /// celda: dice «Medellín, Antioquia». Ordenar por departamento pondría en orden un texto que
    /// nadie ve primero, y la columna parecería desordenada. Agrupar por departamento es lo que
    /// hace el filtro, no el orden.
    /// </para>
    /// </remarks>
    private static IQueryable<EntityProfileRow> Ordenar(
        IQueryable<EntityProfileRow> consulta,
        PnmcDbContext dbContext,
        string orden,
        bool ascendente)
    {
        switch (orden)
        {
            case OrdenEstado:
                return ascendente
                    ? consulta.OrderBy(fila => fila.StatusCode).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(fila => fila.StatusCode).ThenBy(fila => fila.Id);

            case OrdenCreacion:
                return ascendente
                    ? consulta.OrderBy(fila => fila.CreatedAt).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(fila => fila.CreatedAt).ThenBy(fila => fila.Id);

            case OrdenTerritorio:
                return ascendente
                    ? consulta.OrderBy(ClaveDeMunicipio(dbContext)).ThenBy(ClaveDeDepartamento(dbContext)).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(ClaveDeMunicipio(dbContext)).ThenByDescending(ClaveDeDepartamento(dbContext)).ThenBy(fila => fila.Id);

            case OrdenResponsable:
                return ascendente
                    ? consulta.OrderBy(ClaveDeResponsable(dbContext)).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(ClaveDeResponsable(dbContext)).ThenBy(fila => fila.Id);

            case OrdenProcesos:
                return ascendente
                    ? consulta.OrderBy(ClaveDeProcesos(dbContext)).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(ClaveDeProcesos(dbContext)).ThenBy(fila => fila.Id);

            default:
                return ascendente
                    ? consulta.OrderBy(fila => fila.Name).ThenBy(fila => fila.Id)
                    : consulta.OrderByDescending(fila => fila.Name).ThenBy(fila => fila.Id);
        }
    }

    /// <summary>El nombre del municipio, o cadena vacía si la organización no tiene ninguno.</summary>
    /// <remarks>
    /// SE DEVUELVE CADENA VACIA Y NO NULO A PROPOSITO. SQL Server y SQLite no colocan los nulos en
    /// el mismo sitio al ordenar; con la cadena vacía, las organizaciones sin territorio quedan
    /// juntas al principio en ascendente y al final en descendente, igual en los dos motores.
    /// </remarks>
    private static System.Linq.Expressions.Expression<Func<EntityProfileRow, string>> ClaveDeMunicipio(PnmcDbContext dbContext) =>
        fila => dbContext.DivipolaLocations
            .Where(lugar => lugar.MunicipalityCode == fila.HeadquartersMunicipalityCode)
            .Select(lugar => lugar.MunicipalityName)
            .FirstOrDefault() ?? string.Empty;

    private static System.Linq.Expressions.Expression<Func<EntityProfileRow, string>> ClaveDeDepartamento(PnmcDbContext dbContext) =>
        fila => dbContext.DivipolaLocations
            .Where(lugar => lugar.DepartmentCode == fila.HeadquartersDepartmentCode)
            .Select(lugar => lugar.DepartmentName)
            .FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// El nombre de quien responde, resuelto en SQL con los dos mismos intentos y en el mismo orden
    /// que <see cref="ResolverResponsablesAsync"/>: primero la fila declarada, después la cuenta
    /// vinculada más antigua.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<EntityProfileRow, string>> ClaveDeResponsable(PnmcDbContext dbContext) =>
        fila => (dbContext.EntidadesResponsable
                    .Where(declarado => declarado.IdEntidad == fila.Id)
                    .Select(declarado => declarado.ResponsableNombre)
                    .FirstOrDefault()
                ?? dbContext.UserEntities
                    .Where(vinculo => vinculo.EntityId == fila.Id
                        && vinculo.IsActive
                        && RolesQueResponden.Contains(vinculo.EntityRole))
                    .OrderBy(vinculo => vinculo.CreatedAt)
                    .Join(
                        dbContext.Users,
                        vinculo => vinculo.UserId,
                        usuario => usuario.Id,
                        (vinculo, usuario) => usuario.FullName)
                    .FirstOrDefault())
            ?? string.Empty;

    /// <summary>
    /// Cuántos festivales administra una organización.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<EntityProfileRow, int>> ClaveDeProcesos(PnmcDbContext dbContext) =>
        fila => dbContext.FestivalRecords.Count(proceso => proceso.OrganizacionPrincipalId == fila.Id);

    /// <summary>
    /// Las opciones del filtro de territorio, con cuántas organizaciones hay en cada departamento.
    /// </summary>
    /// <remarks>
    /// SOLO SALEN LOS DEPARTAMENTOS QUE TIENEN ALGUNA. Un desplegable con los treinta y tres del
    /// país obliga a probar uno por uno para descubrir que treinta y uno están vacíos; este trae
    /// los que existen y dice cuántas hay en cada uno antes de elegir.
    /// </remarks>
    private static async Task<List<OrganizacionTerritorioDto>> ContarPorDepartamentoAsync(
        PnmcDbContext dbContext,
        IQueryable<EntityProfileRow> consulta,
        CancellationToken cancellationToken)
    {
        var conteo = await consulta
            .GroupBy(fila => fila.HeadquartersDepartmentCode)
            .Select(g => new { Codigo = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        // El nulo y la cadena vacía son el mismo caso para quien mira la pantalla: no hay
        // territorio. En SQL son dos grupos distintos, así que se suman aquí.
        var sinTerritorio = conteo.Where(x => string.IsNullOrWhiteSpace(x.Codigo)).Sum(x => x.Total);
        var conCodigo = conteo.Where(x => !string.IsNullOrWhiteSpace(x.Codigo)).ToList();

        var codigos = conCodigo.Select(x => x.Codigo!).Distinct().ToList();
        var nombres = codigos.Count == 0
            ? new Dictionary<string, string>()
            : (await dbContext.DivipolaLocations.AsNoTracking()
                    .Where(lugar => codigos.Contains(lugar.DepartmentCode))
                    .Select(lugar => new { lugar.DepartmentCode, lugar.DepartmentName })
                    .Distinct()
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.DepartmentCode)
                .ToDictionary(g => g.Key, g => g.First().DepartmentName, StringComparer.Ordinal);

        var opciones = conCodigo
            .Select(x => new OrganizacionTerritorioDto(
                x.Codigo!,
                nombres.TryGetValue(x.Codigo!, out var nombre) && nombre.Length > 0
                    ? nombre
                    // Un código que Divipola no conoce no se esconde: se muestra con su código, que
                    // es lo único cierto que hay de él.
                    : "Departamento " + x.Codigo,
                x.Total))
            .OrderBy(x => x.Etiqueta, OrdenDeTexto.Espanol)
            .ToList();

        // «Sin territorio» va al final: es el cajón de lo que falta, no un departamento más.
        if (sinTerritorio > 0)
        {
            opciones.Add(new OrganizacionTerritorioDto(
                OrganizacionTerritorioDto.SinTerritorio,
                "Sin territorio",
                sinTerritorio));
        }

        return opciones;
    }

    /// <summary>Las direcciones de las cuentas responsables y si alguna está comprobada.</summary>
    /// <remarks>
    /// <para>
    /// <b>ES APARTE DE <see cref="ResolverResponsablesAsync"/> A PROPOSITO.</b> Aquel resuelve
    /// «quién responde», y para la mayoría de organizaciones contesta con la fila declarada de
    /// <c>EntidadesResponsable</c>, donde el correo es el que la organización escribió en su ficha
    /// —puede ser el de la oficina—. La confirmación recae sobre otra cosa: la cuenta con la que se
    /// entra. Mezclarlos haría que la consola ofreciera comprobar una dirección que no abre ninguna
    /// sesión.
    /// </para>
    /// <para>
    /// <b>«ALGUNA» Y NO «TODAS»</b>, igual que la puerta: lo que la regla protege es que exista una
    /// dirección comprobada a la que escribir, no que todas las cuentas secundarias lo estén.
    /// </para>
    /// UNA CONSULTA PARA TODA LA PAGINA, no una por fila.
    /// </remarks>
    private static async Task<Dictionary<int, CuentasResponsables>> ResolverCuentasResponsablesAsync(
        PnmcDbContext dbContext,
        List<int> identificadores,
        CancellationToken cancellationToken)
    {
        if (identificadores.Count == 0) return [];

        var filas = await dbContext.UserEntities.AsNoTracking()
            .Where(vinculo => identificadores.Contains(vinculo.EntityId)
                && vinculo.IsActive
                && RolesQueResponden.Contains(vinculo.EntityRole))
            .Join(
                dbContext.Users.AsNoTracking().Where(usuario => usuario.IsActive),
                vinculo => vinculo.UserId,
                usuario => usuario.Id,
                (vinculo, usuario) => new { vinculo.EntityId, usuario.Email, usuario.CorreoConfirmado, usuario.Id })
            .ToListAsync(cancellationToken);

        return filas
            .GroupBy(fila => fila.EntityId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => new CuentasResponsables(
                    grupo.OrderBy(x => x.Id).Select(x => x.Email).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    grupo.Any(x => x.CorreoConfirmado)));
    }

    /// <summary>Las cuentas con las que se entra a una organización, y si alguna comprobó su correo.</summary>
    private sealed record CuentasResponsables(IReadOnlyList<string> Correos, bool AlgunoConfirmado);

    /// <summary>
    /// Quién responde por cada organización de la página: dos consultas, no dos por fila.
    /// </summary>
    private static async Task<Dictionary<int, OrganizacionResponsableDto>> ResolverResponsablesAsync(
        PnmcDbContext dbContext,
        List<int> identificadores,
        CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<int, OrganizacionResponsableDto>();
        if (identificadores.Count == 0) return resultado;

        var declarados = await dbContext.EntidadesResponsable.AsNoTracking()
            .Where(fila => identificadores.Contains(fila.IdEntidad))
            .ToListAsync(cancellationToken);

        foreach (var fila in declarados)
        {
            resultado[fila.IdEntidad] = new OrganizacionResponsableDto(
                fila.ResponsableNombre,
                fila.ResponsableCorreo,
                fila.ResponsableTelefono,
                // El tipo de documento sí viaja y el número no: «Cédula de ciudadanía registrada»
                // le dice a quien revisa que la identidad se capturó, sin repartir el número.
                fila.ResponsableTipoDocumento,
                TieneDocumento: !string.IsNullOrWhiteSpace(fila.ResponsableNumeroDocumento),
                fila.ResponsableAutorizacionDatos,
                fila.ResponsableDesde,
                null,
                OrigenDeclarado);
        }

        // SEGUNDO INTENTO, SOLO PARA LAS QUE NO TIENEN FILA DECLARADA. Pedir las cuentas de todas
        // traería quince vínculos para descartarlos después.
        var sinDeclarar = identificadores.Where(id => !resultado.ContainsKey(id)).ToList();
        if (sinDeclarar.Count == 0) return resultado;

        var cuentas = await dbContext.UserEntities.AsNoTracking()
            .Where(vinculo => sinDeclarar.Contains(vinculo.EntityId)
                && vinculo.IsActive
                && RolesQueResponden.Contains(vinculo.EntityRole))
            .Join(
                dbContext.Users.AsNoTracking(),
                vinculo => vinculo.UserId,
                usuario => usuario.Id,
                (vinculo, usuario) => new
                {
                    vinculo.EntityId,
                    vinculo.EntityRole,
                    vinculo.CreatedAt,
                    usuario.FullName,
                    usuario.Email,
                    usuario.Telefono,
                })
            .ToListAsync(cancellationToken);

        foreach (var grupo in cuentas.GroupBy(cuenta => cuenta.EntityId))
        {
            // Si hay varias, manda la más antigua: es la que fundó el vínculo.
            var cuenta = grupo.OrderBy(x => x.CreatedAt).First();
            resultado[grupo.Key] = new OrganizacionResponsableDto(
                cuenta.FullName,
                cuenta.Email,
                cuenta.Telefono,
                null,
                TieneDocumento: false,
                AutorizacionDatos: false,
                cuenta.CreatedAt,
                cuenta.EntityRole,
                OrigenCuenta);
        }

        return resultado;
    }

    /// <summary>
    /// Cuántos festivales administra cada organización.
    /// </summary>
    /// <summary>
    /// Cuántos procesos administra cada organización, y cuántos de ellos dependen de ella.
    /// </summary>
    /// <remarks>
    /// UNA CONSULTA PARA TODA LA PAGINA. Se agrupa por organización y estado en la base y se suma
    /// aquí: pedir el desglose por fila serían tantas consultas como organizaciones enseñe la tabla.
    /// </remarks>
    private static async Task<Dictionary<int, OrganizacionProcesosDto>> ContarProcesosAsync(
        PnmcDbContext dbContext,
        List<int> identificadores,
        CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<int, OrganizacionProcesosDto>();
        if (identificadores.Count == 0) return resultado;

        var porEstado = await dbContext.FestivalRecords.AsNoTracking()
            .Where(fila => fila.OrganizacionPrincipalId != null && identificadores.Contains(fila.OrganizacionPrincipalId.Value))
            .GroupBy(fila => new { Organizacion = fila.OrganizacionPrincipalId!.Value, fila.StatusCode })
            .Select(g => new { g.Key.Organizacion, g.Key.StatusCode, Total = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var id in identificadores)
        {
            var suyos = porEstado.Where(fila => fila.Organizacion == id).ToList();
            var total = suyos.Sum(fila => fila.Total);
            var publicados = 0;
            var dependientes = 0;

            foreach (var fila in suyos)
            {
                // LAS DOS GRAFIAS. `publicado` y `Publicado` conviven en la base desde la
                // importación del acervo; normalizar aquí evita contar de menos.
                var estado = EstadosFestival.DesdeContrato(fila.StatusCode) ?? (fila.StatusCode ?? string.Empty);
                if (string.Equals(estado, EstadosFestival.Publicado, StringComparison.OrdinalIgnoreCase))
                {
                    publicados += fila.Total;
                }

                if (Array.Exists(CustodiaDeProcesos.EstadosQueDejanHuerfano,
                        e => string.Equals(e, estado, StringComparison.OrdinalIgnoreCase)))
                {
                    dependientes += fila.Total;
                }
            }

            resultado[id] = new OrganizacionProcesosDto(total, publicados, dependientes - publicados, dependientes);
        }

        return resultado;
    }


    /// <summary>El territorio en palabras, de una consulta para toda la página.</summary>
    private static async Task<Dictionary<int, string>> ResolverTerritoriosAsync(
        PnmcDbContext dbContext,
        IReadOnlyList<EntityProfileRow> filas,
        CancellationToken cancellationToken)
    {
        var resultado = new Dictionary<int, string>();
        var departamentos = filas.Select(f => f.HeadquartersDepartmentCode).Where(c => c != null).Select(c => c!).Distinct().ToList();
        var municipios = filas.Select(f => f.HeadquartersMunicipalityCode).Where(c => c != null).Select(c => c!).Distinct().ToList();
        if (departamentos.Count == 0 && municipios.Count == 0) return resultado;

        var divipola = await dbContext.DivipolaLocations.AsNoTracking()
            .Where(fila => departamentos.Contains(fila.DepartmentCode) || municipios.Contains(fila.MunicipalityCode))
            .Select(fila => new { fila.DepartmentCode, fila.DepartmentName, fila.MunicipalityCode, fila.MunicipalityName })
            .ToListAsync(cancellationToken);

        var nombreDepartamento = divipola
            .GroupBy(x => x.DepartmentCode)
            .ToDictionary(g => g.Key, g => g.First().DepartmentName);
        var nombreMunicipio = divipola
            .GroupBy(x => x.MunicipalityCode)
            .ToDictionary(g => g.Key, g => g.First().MunicipalityName);

        foreach (var fila in filas)
        {
            var departamento = fila.HeadquartersDepartmentCode is not null
                ? nombreDepartamento.GetValueOrDefault(fila.HeadquartersDepartmentCode, string.Empty)
                : string.Empty;
            var municipio = fila.HeadquartersMunicipalityCode is not null
                ? nombreMunicipio.GetValueOrDefault(fila.HeadquartersMunicipalityCode, string.Empty)
                : string.Empty;

            var texto = (municipio.Length > 0, departamento.Length > 0) switch
            {
                (true, true) => municipio + ", " + departamento,
                (true, false) => municipio,
                (false, true) => departamento,
                _ => string.Empty,
            };

            if (texto.Length > 0) resultado[fila.Id] = texto;
        }

        return resultado;
    }
}
