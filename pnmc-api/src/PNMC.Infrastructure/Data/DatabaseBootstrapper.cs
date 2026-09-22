using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PNMC.Infrastructure.Data;

public static class DatabaseBootstrapper
{
    /// <summary>Una cuenta de arranque del entorno local.</summary>
    public sealed record CuentaDeArranque(string NombreCompleto, string Correo, string Rol, string Clave, string? Telefono);

    /// <summary>
    /// LAS CUENTAS DE ARRANQUE, EN UNA SOLA LISTA Y NO EN DOS.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POR QUE EXISTE ESTA LISTA. Habia dos: la rama de SQLite sembraba tres cuentas y la de SQL
    /// Server —<c>Database:SeedBootstrapUsers</c>— sembraba dos. La tercera, <c>externo@pnmc.local</c>,
    /// se quedo sin hash contra la base real y <c>POST /external/auth/login</c> devolvia 401 con la
    /// contrasena que la documentacion declara. Comprobado: <c>admin@</c> y
    /// <c>gestor@</c> con hash de 84 caracteres, las tres externas con el literal
    /// <c>pendiente_configurar_hash_seguro</c>, de 32.
    /// </para>
    /// <para>
    /// Con una sola lista ese desajuste no puede repetirse: anadir una cuenta la siembra en los dos
    /// caminos, y quitarla la quita de los dos. Es lo unico que impide que el proximo alta vuelva a
    /// existir solo en el carril rapido, que es donde el defecto era invisible.
    /// </para>
    /// <para>
    /// <c>sistema@pnmc.local</c> NO ESTA AQUI Y NO ES UN OLVIDO. Es la cuenta a la que se atribuyen
    /// las acciones automaticas, y <c>AdminAuthEndpoints.WebmastersQuePuedenEntrarAsync</c> cuenta
    /// con que NO pueda iniciar sesion: si pudiera, la guarda del ultimo webmaster contaria a
    /// alguien que nadie usa y dejaria desactivar al unico real.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<CuentaDeArranque> CuentasDeArranque =
    [
        new("Webmaster PNMC", "admin@pnmc.local", "webmaster", "admin", "3151234567"),
        new("Gestor Interno PNMC", "gestor@pnmc.local", "gestor_interno", "admin", "3207654321"),
        new("Colaborador Externo", "externo@pnmc.local", "externo", "admin", "3103332211"),
        new("Participante de Prueba 2", "participante.dos@pnmc.local", "externo", "admin", null),
        new("Participante de Prueba 3", "participante.tres@pnmc.local", "externo", "admin", null),
    ];

    public static async Task EnsureReadyAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseBootstrapper");
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var timeoutSeconds = Math.Clamp(options.StartupTimeoutSeconds, 5, 300);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var startupToken = timeoutCts.Token;

        try
        {
            if (db.Database.IsSqlServer())
            {
                var canConnect = await db.Database.CanConnectAsync(startupToken);
                if (!canConnect)
                {
                    throw new InvalidOperationException("No fue posible conectar con SQL Server.");
                }

                // El esquema de SQL Server pertenece exclusivamente a PNMC.Migrador (DbUp).
                // El proceso del API valida la conexión y solo escribe datos de arranque
                // expresamente habilitados; no crea ni altera tablas durante el inicio.
                if (options.SeedBootstrapUsers)
                {
                    if (!environment.IsDevelopment()
                        && !environment.IsEnvironment("Local")
                        && !environment.IsEnvironment("Test"))
                    {
                        throw new InvalidOperationException(
                            "Database:SeedBootstrapUsers solo puede habilitarse en Development, Local o Test.");
                    }

                    await EnsureBootstrapUsersAsync(db, startupToken);
                }

                logger.LogInformation(
                    "SQL Server connection established. Schema changes are managed exclusively by PNMC.Migrador.");
            }
            else
            {
                await db.Database.EnsureCreatedAsync(startupToken);
                await EnsureLocalSupportTablesAsync(db, startupToken);
                await EnsureLocalDevelopmentSeedAsync(db, startupToken);
                logger.LogInformation("Database initialized with EnsureCreated for non-SQL provider.");
            }

            // EstadosContenido es un catálogo técnico del dominio, no contenido de
            // demostración. Festival, Organización y los demás registros que lo usan
            // tienen una llave foránea contra él; por tanto debe existir también en
            // SQL Server. Antes solo se completaba al iniciar SQLite y un Festival
            // nuevo fallaba con 500 al intentar guardar el estado "borrador".
            await EnsureCoreContentStatusesAsync(db, startupToken);

            if (options.SeedWebConfiguration)
            {
                // La siembra del CMS va DESPUES del if/else, no dentro de la rama de
                // SQL Server. Esa rama terminaba en `return`, de modo que cualquier
                // paso colgado de ella nunca corria con SQLite —el proveedor que usan
                // las pruebas—, y la suite habria pasado en verde sobre una tabla vacia.
                // El renombrado va ANTES de sembrar y no despues: el sembrador
                // inserta lo que falta, de modo que si se invirtiera el orden las
                // claves nuevas ya existirian cuando llegara el traslado y toda fila
                // vieja pareceria un conflicto.
                await RenombradoContenidoWeb.EnsureRenamedAsync(db, logger, startupToken);
                await SembradorDeContenidoWeb.EnsureSeededAsync(db, logger, startupToken);
                await SembradorDeEquipoWeb.EnsureSeededAsync(db, logger, startupToken);
                // Las ranuras de imagen. Nacen sin bytes: sembrar no es publicar, y el dia que esto
                // entro el sitio se veia exactamente igual. Ver MediosWebSeeder.
                await MediosWebSeeder.EnsureSeededAsync(db, logger, startupToken);

                // La poda va la ULTIMA de las tres. El renombrado y la retirada
                // escriben historial, y podar antes que ellos dejaria por encima del
                // tope justo las claves que este arranque acaba de tocar.
                await PodaDelHistorialContenidoWeb.PodarTodoAsync(db, logger, startupToken);
            }
            else
            {
                logger.LogInformation("Web configuration seeding is disabled for this database profile.");
            }
        }
        catch (Exception exception) when (options.ContinueOnStartupFailure)
        {
            logger.LogWarning(
                exception,
                "Database bootstrap failed at startup. The API will continue running in degraded mode. TimeoutSeconds={TimeoutSeconds}",
                timeoutSeconds);
        }
    }

    private static async Task EnsureBootstrapUsersAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        await EnsureRoleAsync(db, "webmaster", "Control total de usuarios, modulos, datos, configuracion, revision, publicacion y mantenimiento.", cancellationToken);
        await EnsureRoleAsync(db, "gestor_interno", "Segundo nivel general de administracion institucional.", cancellationToken);
        await EnsureRoleAsync(db, "externo", "Participante publico sin acceso administrativo privilegiado.", cancellationToken);

        foreach (var cuenta in CuentasDeArranque)
        {
            await EnsureBootstrapUserAsync(
                db, cuenta.NombreCompleto, cuenta.Correo, cuenta.Rol, cuenta.Clave, cuenta.Telefono, cancellationToken);
        }
    }

    private static async Task EnsureRoleAsync(
        PnmcDbContext db,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (role is null)
        {
            db.Roles.Add(new RoleRow
            {
                Name = name,
                Description = description
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        role.Description = description;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureBootstrapUserAsync(
        PnmcDbContext db,
        string fullName,
        string email,
        string roleName,
        string password,
        string? phone,
        CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstAsync(item => item.Name == roleName, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        var isNew = user is null;

        user ??= new UserRow
        {
            FullName = fullName,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        user.FullName = fullName;
        // Dos canales, no tres. El tercero, "aliado", desaparecio con el concepto el 22 de
        // agosto de 2026. CanalAcceso no tiene CHECK en la base, asi que ninguna fila vieja
        // con "aliado" rompe nada; simplemente ya no se escribe.
        user.AccessChannel = roleName == "externo" ? "externo" : "interno";
        user.ProfileType = roleName == "externo" ? "organizacion" : user.ProfileType;
        user.Telefono = phone;
        user.IsActive = true;
        // UNA CUENTA DE ARRANQUE NO PASA POR EL RECORRIDO DE PRIMER INGRESO. Ese recorrido existe
        // porque una cuenta se ENTREGA: alguien eligió su contraseña y la persona todavía no ha
        // dicho quién es. Estas las crea el propio sistema, con su nombre y sus credenciales
        // técnicas. Se dice aquí además de ser el valor por omisión de la entidad, porque una
        // cuenta de arranque que ya existiera con las marcas puestas tiene que volver a su sitio.
        user.DebeCambiarContrasena = false;
        user.PerfilCompletado = true;
        user.UpdatedAt = DateTime.UtcNow;

        if (isNew || !LooksLikeAspNetPasswordHash(user.PasswordHash))
        {
            var hasher = new PasswordHasher<UserRow>();
            user.PasswordHash = hasher.HashPassword(user, password);
        }

        if (isNew)
        {
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(cancellationToken);

        // LA FILA DE dbo.UsuariosRoles, QUE ES LO QUE DA EL ROL DESDE LA TRANSICION.
        //
        // Sin esto, una base arrancada por el bootstrapper —la de desarrollo local y la que
        // fabrica el arnes de SQLite— dejaba a las cuentas semilla con `IdRol` puesto y CERO
        // filas de asignacion, es decir sin poder iniciar sesion: exactamente el sintoma que el
        // defecto U3 del plan describia para la siembra de SQL Server, en el otro camino.
        //
        // Desde la transicion es lo UNICO que da el rol: `Usuarios.IdRol` ya no existe. Va DESPUES
        // del guardado porque un usuario recien creado no tiene Id hasta que EF lo escribe.
        //
        // NO ACUMULA: deja EXACTAMENTE el rol pedido y retira cualquier otro. Un bootstrapper que
        // acumulara convertiria cada arranque en una concesion mas.
        var actuales = await db.UsuariosRoles
            .Where(asignacion => asignacion.UserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var sobrante in actuales.Where(asignacion => asignacion.RoleId != role.Id))
        {
            db.UsuariosRoles.Remove(sobrante);
        }

        if (!actuales.Any(asignacion => asignacion.RoleId == role.Id))
        {
            db.UsuariosRoles.Add(new UsuarioRolRow
            {
                UserId = user.Id,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await AsegurarModulosDeConsolaAsync(db, user.Id, roleName, cancellationToken);
    }

    /// <summary>
    /// Deja a una cuenta interna con todos los módulos de la consola activados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNA CUENTA TECNICA NACE PUDIENDO TRABAJAR.</b> Desde los
    /// permisos de la consola se conceden por cuenta y el servidor los aplica: una cuenta sin
    /// módulos entra a una consola de dos pantallas. Las cuentas de arranque existen justamente
    /// para recorrer el sistema entero, así que se siembran con todo, igual que hace la migración
    /// con las cuentas que ya existían.
    /// </para>
    /// <para>
    /// <b>SOLO LAS INTERNAS.</b> Una cuenta externa no entra a la consola: darle módulos no le
    /// abriría nada y ensuciaría la pantalla de permisos con filas que no significan nada.
    /// </para>
    /// <para>
    /// NO ACUMULA NI RETIRA lo que alguien haya decidido después: solo añade lo que falte. Un
    /// arranque no es el sitio donde se revocan permisos concedidos a mano.
    /// </para>
    /// </remarks>
    private static async Task AsegurarModulosDeConsolaAsync(
        PnmcDbContext db, int idUsuario, string roleName, CancellationToken cancellationToken)
    {
        if (!string.Equals(roleName, "webmaster", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(roleName, "gestor_interno", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var yaTiene = await db.ModulosPorCuenta
            .Where(x => x.IdUsuario == idUsuario)
            .Select(x => x.CodigoModulo)
            .ToListAsync(cancellationToken);

        var faltan = ModulosDeConsolaQueSeSiembran
            .Where(modulo => !yaTiene.Contains(modulo, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (faltan.Count == 0) return;

        var ahora = DateTime.UtcNow;
        foreach (var modulo in faltan)
        {
            db.ModulosPorCuenta.Add(new ModuloPorCuentaRow
            {
                IdUsuario = idUsuario,
                CodigoModulo = modulo,
                FechaOtorgado = ahora,
                IdUsuarioQueOtorgo = null,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Los módulos que se siembran. Son todos menos los dos que van siempre activados.
    /// </summary>
    /// <remarks>
    /// «monitor» y «solicitudes» NO se guardan nunca: los concede el catálogo del servidor, y
    /// guardarlos sería poder borrarlos.
    /// </remarks>
    private static readonly string[] ModulosDeConsolaQueSeSiembran =
    [
        "ecosistema", "mercados", "organizaciones", "catalogo-editorial", "agenda", "noticias",
        "categorias", "banco-de-archivos", "gestion-sitio", "boletin", "analisis",
        "auditoria", "usuarios", "sistema",
    ];

    private static async Task EnsureLocalDevelopmentSeedAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        await EnsureRoleAsync(db, "webmaster", "Control total de usuarios, modulos, datos, configuracion, revision, publicacion y mantenimiento.", cancellationToken);
        await EnsureRoleAsync(db, "gestor_interno", "Segundo nivel general de administracion institucional.", cancellationToken);
        await EnsureRoleAsync(db, "externo", "Participante publico sin acceso administrativo privilegiado.", cancellationToken);

        // LA MISMA LISTA QUE LA RAMA DE SQL SERVER, por el mismo motivo: dos listas divergen y la
        // divergencia solo se ve en el carril que ninguna prueba recorre.
        foreach (var cuenta in CuentasDeArranque)
        {
            await EnsureBootstrapUserAsync(
                db, cuenta.NombreCompleto, cuenta.Correo, cuenta.Rol, cuenta.Clave, cuenta.Telefono, cancellationToken);
        }

        await EnsureDivipolaLocationAsync(db, "05", "Antioquia", "05001", "Medellin", cancellationToken);
        await EnsureDivipolaLocationAsync(db, "11", "Bogota, D.C.", "11001", "Bogota, D.C.", cancellationToken);
        await EnsureDivipolaLocationAsync(db, "13", "Bolivar", "13001", "Cartagena de Indias", cancellationToken);
        await EnsureDivipolaLocationAsync(db, "25", "Cundinamarca", "25754", "Soacha", cancellationToken);
        await EnsureDivipolaLocationAsync(db, "76", "Valle del Cauca", "76001", "Cali", cancellationToken);
        await EnsurePoliticaDeDatosAsync(db, "tratamiento", PoliticasDeDatosSembradas.VersionVigente,
            PoliticasDeDatosSembradas.TituloTratamiento, PoliticasDeDatosSembradas.TextoTratamiento,
            PoliticasDeDatosSembradas.UrlTratamiento, "PL-GSI-002 v0", cancellationToken);

        await EnsurePoliticaDeDatosAsync(db, "terminos", PoliticasDeDatosSembradas.VersionVigente,
            PoliticasDeDatosSembradas.TituloTerminos, PoliticasDeDatosSembradas.TextoTerminos,
            PoliticasDeDatosSembradas.UrlTerminos, "PL-GSI-001 v0", cancellationToken);

        // EL BOLETIN NO TIENE PDF INSTITUCIONAL, y no se le inventa uno. Su texto se basta: dice
        // quien trata, que dato, para que y como se revoca.
        await EnsurePoliticaDeDatosAsync(db, "boletin", PoliticasDeDatosSembradas.VersionVigente,
            PoliticasDeDatosSembradas.TituloBoletin, PoliticasDeDatosSembradas.TextoBoletin,
            null, null, cancellationToken);
    }

    private static async Task EnsureLocalSupportTablesAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS RegistrosRevisionHistorial (
                IdRevisionHistorial INTEGER PRIMARY KEY AUTOINCREMENT,
                ModuloId TEXT NOT NULL,
                RegistroId TEXT NOT NULL,
                EstadoAnterior TEXT NULL,
                EstadoNuevo TEXT NOT NULL,
                Accion TEXT NOT NULL,
                Comentario TEXT NULL,
                MotivoRechazo TEXT NULL,
                CamposObservados TEXT NULL,
                IdUsuario INTEGER NULL,
                Fecha TEXT NOT NULL,
                MetadataJson TEXT NULL
            );
            """, cancellationToken);
    }

    private static async Task EnsureCoreContentStatusesAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        await EnsureContentStatusAsync(db, "borrador", "Borrador", "Registro en elaboración.", cancellationToken);
        await EnsureContentStatusAsync(db, "en_revision", "En revisión", "Registro enviado para revisión institucional.", cancellationToken);
        await EnsureContentStatusAsync(db, "ajustes_solicitados", "Ajustes solicitados", "Registro devuelto para correcciones.", cancellationToken);
        await EnsureContentStatusAsync(db, "aprobado", "Aprobado", "Registro validado institucionalmente.", cancellationToken);
        await EnsureContentStatusAsync(db, "publicado", "Publicado", "Registro visible públicamente.", cancellationToken);
        await EnsureContentStatusAsync(db, "rechazado", "Rechazado", "Registro no aprobado.", cancellationToken);
        await EnsureContentStatusAsync(db, "archivado", "Archivado", "Registro retirado del flujo activo.", cancellationToken);
        // LOS CUATRO ESTADOS DE UNA ORGANIZACION, que comparten este catálogo por la foránea de
        // dbo.Entidades y NO son los de un contenido: una organización no se publica ni se aprueba.
        // Son un ciclo de vida en un solo eje, y los fija EstadosDeOrganizacion.
        await EnsureContentStatusAsync(db, "pendiente_de_confirmacion", "Pendiente de confirmación", "Organización cuya cuenta responsable todavía no ha confirmado su correo.", cancellationToken);
        await EnsureContentStatusAsync(db, "activa", "Activa", "Organización que opera con normalidad en la plataforma.", cancellationToken);
        await EnsureContentStatusAsync(db, "inactiva", "Inactiva", "Organización suspendida: conserva su ficha y no puede entrar.", cancellationToken);
        await EnsureContentStatusAsync(db, "eliminada", "Eliminada", "Organización retirada del ecosistema; sus procesos vuelven al Programa.", cancellationToken);

        await EnsureTiposDeDocumentoAsync(db, cancellationToken);
        await EnsureVocabulariosDeMercadoAsync(db, cancellationToken);
    }

    /// <summary>
    /// El alcance y la modalidad de un mercado musical, que son vocabulario controlado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SE SIEMBRAN AQUI PORQUE LA BASE DE PRUEBAS NO PASA POR DbUp.</b> La crea EF con
    /// <c>EnsureCreated</c>, que levanta las tablas y ninguna de sus filas: un desplegable vacío no
    /// deja guardar a nadie, y la prueba fallaría por un motivo que no tiene que ver con lo que
    /// mide. Es la misma razón por la que se siembran los tipos de documento.
    /// </para>
    /// <para>
    /// <b>LA TABLA HEREDADA LOS GUARDABA COMO TEXTO LIBRE</b> —<c>alcance</c> y <c>modalidad</c>,
    /// <c>varchar</c> sin lista ni CHECK—, así que «Nacional», «nacional» y «NACIONAL» eran tres
    /// alcances distintos y nadie podía contar mercados por alcance. La regla del proyecto es que
    /// todo vocabulario controlado se captura con una lista.
    /// </para>
    /// </remarks>
    private static async Task EnsureVocabulariosDeMercadoAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.AlcancesMercado.AnyAsync(cancellationToken))
        {
            db.AlcancesMercado.AddRange(
                new AlcanceMercadoRow { Nombre = "Local", Slug = "local", OrdenVisualizacion = 1 },
                new AlcanceMercadoRow { Nombre = "Regional", Slug = "regional", OrdenVisualizacion = 2 },
                new AlcanceMercadoRow { Nombre = "Nacional", Slug = "nacional", OrdenVisualizacion = 3 },
                new AlcanceMercadoRow { Nombre = "Internacional", Slug = "internacional", OrdenVisualizacion = 4 });
        }

        if (!await db.ModalidadesMercado.AnyAsync(cancellationToken))
        {
            db.ModalidadesMercado.AddRange(
                new ModalidadMercadoRow { Nombre = "Presencial", Slug = "presencial", OrdenVisualizacion = 1 },
                new ModalidadMercadoRow { Nombre = "Virtual", Slug = "virtual", OrdenVisualizacion = 2 },
                new ModalidadMercadoRow { Nombre = "Mixta", Slug = "mixta", OrdenVisualizacion = 3 });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Los tipos de documento de identidad del país, que son vocabulario controlado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SE SIEMBRAN PORQUE LA TABLA ESTABA VACIA DONDE NO SE HABIA MIGRADO.</b> `dbo.TiposDocumento`
    /// tenía sus filas en la base local, pero ninguna siembra las creaba: cualquier base nueva —la de
    /// pruebas, una instalación limpia— nacía con el catálogo vacío, y un desplegable vacío no deja
    /// guardar a nadie.
    /// </para>
    /// <para>
    /// <b>SON DIEZ Y NO OCHO, y ese fue un hallazgo.</b> La copia que el alta externa llevaba escrita
    /// a mano tenía dos que la tabla no: «Documento de identificación extranjero» y «Carné
    /// diplomático». Es decir, el alta aceptaba dos códigos que el catálogo no reconocía, y una
    /// pantalla que leyera de la tabla enseñaría el código en crudo. Se añaden a la tabla, que es la
    /// fuente, en vez de quitarlos del alta: son tipos de documento reales.
    /// </para>
    /// <para>
    /// NO PISA LO QUE YA HAY: si la fila existe, se deja como está. Cambiar un nombre a mano en la
    /// base es una decisión de alguien y un arranque no es el sitio para deshacerla.
    /// </para>
    /// </remarks>
    private static async Task EnsureTiposDeDocumentoAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        var catalogo = new (string Codigo, string Nombre, int Orden)[]
        {
            ("cc", "Cédula de ciudadanía", 1),
            ("ce", "Cédula de extranjería", 2),
            ("ti", "Tarjeta de identidad", 3),
            ("rc", "Registro civil de nacimiento", 4),
            ("nuip", "Número único de identificación personal", 5),
            ("pa", "Pasaporte", 6),
            ("pep", "Permiso especial de permanencia", 7),
            ("ppt", "Permiso por protección temporal", 8),
            ("die", "Documento de identificación extranjero", 9),
            ("cd", "Carné diplomático", 10),
        };

        var existentes = await db.TiposDocumento.Select(t => t.Codigo).ToListAsync(cancellationToken);
        var faltan = catalogo.Where(t => !existentes.Contains(t.Codigo, StringComparer.OrdinalIgnoreCase)).ToList();
        if (faltan.Count == 0) return;

        foreach (var (codigo, nombre, orden) in faltan)
        {
            db.TiposDocumento.Add(new TipoDocumentoRow
            {
                Codigo = codigo,
                Nombre = nombre,
                OrdenVisualizacion = orden,
                Activo = true,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureContentStatusAsync(
        PnmcDbContext db,
        string code,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        var status = await db.ContentStatuses.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (status is null)
        {
            db.ContentStatuses.Add(new ContentStatusRow
            {
                Code = code,
                Name = name,
                Description = description
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        status.Name = name;
        status.Description = description;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDivipolaLocationAsync(
        PnmcDbContext db,
        string departmentCode,
        string departmentName,
        string municipalityCode,
        string municipalityName,
        CancellationToken cancellationToken)
    {
        var location = await db.DivipolaLocations.FirstOrDefaultAsync(
            item => item.DepartmentCode == departmentCode && item.MunicipalityCode == municipalityCode,
            cancellationToken);
        if (location is null)
        {
            db.DivipolaLocations.Add(new DivipolaLocationRow
            {
                DepartmentCode = departmentCode,
                DepartmentName = departmentName,
                MunicipalityCode = municipalityCode,
                MunicipalityName = municipalityName,
                LocationType = "MUNICIPALITY"
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        location.DepartmentName = departmentName;
        location.MunicipalityName = municipalityName;
        location.LocationType = "MUNICIPALITY";
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Deja puesta la politica vigente de una finalidad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PARA QUE SIRVE ESTO SI LA MIGRACION YA LAS SIEMBRA.</b> `V20260912_07` las escribe en las
    /// bases que pasan por DbUp. Las bases de prueba no pasan: se levantan desde el modelo. Sin
    /// esta siembra, un alta de organizacion en pruebas no encontraria texto que copiar y la
    /// autorizacion nacería vacia, que es exactamente lo que esta tabla existe para impedir.
    /// </para>
    /// <para>
    /// <b>ACTUALIZA EL TEXTO DE LA VERSION, NO LO VERSIONA.</b> Aqui se repone una redaccion
    /// conocida, no se publica una nueva: si el texto cambia de verdad, cambia la version y entra
    /// como otra fila por migracion. Reescribir el texto de una version ya aceptada por alguien
    /// seria cambiarle lo que dice su evidencia, y por eso las autorizaciones guardan su propia
    /// copia y no leen de aqui.
    /// </para>
    /// </remarks>
    private static async Task EnsurePoliticaDeDatosAsync(
        PnmcDbContext db, string clave, string version, string titulo, string texto,
        string? urlOficial, string? referenciaOficial, CancellationToken cancellationToken)
    {
        var politica = await db.PoliticasDeDatos
            .FirstOrDefaultAsync(item => item.Clave == clave && item.Version == version, cancellationToken);

        if (politica is null)
        {
            db.PoliticasDeDatos.Add(new PoliticaDeDatosRow
            {
                Clave = clave,
                Version = version,
                Titulo = titulo,
                Texto = texto,
                UrlOficial = urlOficial,
                ReferenciaOficial = referenciaOficial,
                Vigente = true,
                FechaPublicacion = DateTime.UtcNow,
                FechaCreacion = DateTime.UtcNow,
            });
        }
        else
        {
            politica.Titulo = titulo;
            politica.Texto = texto;
            politica.UrlOficial = urlOficial;
            politica.ReferenciaOficial = referenciaOficial;
            politica.Vigente = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureLocalEcosystemSeedAsync(PnmcDbContext db, CancellationToken cancellationToken)
    {
        if (await db.FestivalRecords.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // TRES ASIGNACIONES DE AQUI NO GUARDABAN NADA, y es exactamente por lo que se retiraron las
        // propiedades que las recibían. `StatusId`, `CreatedByUserId` y `PublishedAt` estaban
        // declaradas en `FestivalRow` y marcadas con `Ignore()` en el mapeo: el código las escribía,
        // el compilador no se quejaba y la base no recibía nada. El estado de verdad viaja en
        // `StatusCode`, que sí es una columna.
        db.FestivalRecords.Add(new FestivalRow
        {
            Name = "Festival Demo PNMC",
            Description = "Registro local de referencia para validar mapa, revision y publicacion.",
            OrganizerDisplayName = "Agrupacion Demo PNMC",
            CoverageLevel = "municipal",
            DepartmentCode = "05",
            MunicipalityCode = "05001",
            StatusCode = "publicado",
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool LooksLikeAspNetPasswordHash(string passwordHash)
    {
        return !string.IsNullOrWhiteSpace(passwordHash)
            && passwordHash.StartsWith("AQAAAA", StringComparison.Ordinal);
    }

}
