using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-014a. Tres pruebas de muestra sobre el motor real. NO son una migracion de la
/// suite: las 195 pruebas siguen corriendo sobre SQLite y no se han tocado. Estas tres
/// existen para demostrar que la via paralela funciona y para fijar por escrito que
/// clase de defecto se le escapa hoy al arnes de SQLite.
///
/// <para>
/// Estan omitidas salvo que se encienda <c>PNMC_PRUEBAS_SQLSERVER=1</c>. Ver
/// <see cref="ArnesSqlServer"/>.
/// </para>
/// </summary>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class MuestraSqlServerTests
{
    private readonly SqlServerFixture _base;

    public MuestraSqlServerTests(SqlServerFixture baseDesechable)
    {
        _base = baseDesechable;
    }

    /// <summary>
    /// Lo que el arnes de SQLite no puede tener: un disparador y un procedimiento
    /// almacenado.
    ///
    /// <para>
    /// <c>TR_RegistrosEcosistema_ValidarOrigen</c> y <c>sp_ActualizarMetricasMapa</c>
    /// viven en <c>pnmc-database/schema/V20260519_04__articulacion_lectura_comun.sql</c>
    /// y no existen en el modelo de EF. Con <c>UseSqlite</c> + <c>EnsureCreated</c> la
    /// base se fabrica desde ese modelo, asi que ninguno de los dos llega a existir y
    /// ninguna prueba de la suite puede romperse si alguien los borra, los rompe o
    /// cambia su semantica.
    /// </para>
    ///
    /// <para>
    /// La comprobacion de <c>DB_NAME()</c> no es adorno: confirma que la aplicacion
    /// habla con la base desechable de esta corrida y no con <c>PNMC_LOCAL</c>, que es
    /// la base de desarrollo del dueno.
    /// </para>
    /// </summary>
    [HechoSqlServer]
    public async Task El_arnes_monta_el_esquema_real_sobre_SQL_Server()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // Sin esto, la prueba pasaria en verde tambien sobre SQLite y no probaria nada.
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);

        var baseEnUso = Convert.ToString(await _base.EscalarAsync("SELECT DB_NAME();"));
        Assert.Equal(_base.NombreDeBase, baseEnUso);
        Assert.StartsWith(ArnesSqlServer.PrefijoBaseDesechable, baseEnUso, StringComparison.Ordinal);
        Assert.NotEqual(ArnesSqlServer.BaseProhibida, baseEnUso);

        // Los guiones de esquema reales se aplican en orden de nombre. No se fija el
        // número exacto para que añadir un guion nuevo no rompa la prueba.
        Assert.NotEmpty(_base.GuionesAplicados);
        Assert.Contains("V20260910_02__fichas_conceptuales_sin_cascada.sql", _base.GuionesAplicados);
        Assert.Contains("V20260911_01__importaciones_gobernadas_festivales.sql", _base.GuionesAplicados);
        Assert.Contains("V20260911_02__importaciones_sin_borrado_en_cascada.sql", _base.GuionesAplicados);
        Assert.Contains("V20260911_03__retencion_historial_importaciones.sql", _base.GuionesAplicados);

        var tablasImportacion = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'LotesImportacion', N'FilasImportacion', N'HallazgosImportacion', N'DecisionesImportacion');"));
        Assert.Equal(4, tablasImportacion);
        Assert.Equal(1, Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.LotesImportacion') AND name = N'FechaDepuracion';")));

        // El perfil vigente retiró la capa genérica del ecosistema y sus artefactos SQL.
        var disparadores = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.triggers WHERE name = N'TR_RegistrosEcosistema_ValidarOrigen';"));
        Assert.Equal(0, disparadores);

        var procedimientos = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.procedures WHERE name = N'sp_ActualizarMetricasMapa';"));
        Assert.Equal(0, procedimientos);

        // Comprobado el 21 ago 2026 sobre base nueva: 49 tablas y 61 restricciones CHECK.
        // Se comprueba con holgura para que anadir esquema no rompa la prueba, pero el
        // orden de magnitud deja constancia de cuanto no ve hoy la suite: el modelo de
        // EF declara UNA sola restriccion CHECK (CK_EquipoWeb_Unica).
        var restricciones = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.check_constraints;"));
        Assert.True(restricciones >= 40, $"Se esperaban las restricciones CHECK del esquema real; hay {restricciones}.");
    }

    /// <summary>
    /// Migrada de <c>ContenidoWebTests.La_siembra_corre_tambien_en_sqlite</c>.
    ///
    /// <para>
    /// Aquella prueba nacio de una regresion real: la siembra colgaba de la rama de SQL
    /// Server del bootstrapper y con SQLite nunca corria. Se arreglo y se protegio... la
    /// rama de SQLite. La rama de SQL Server —la que corre en la maquina del dueno y la
    /// que correria en produccion— sigue sin que ninguna prueba la ejecute: es la que
    /// crea trece tablas por DDL en crudo (ContenidoWeb, EquipoWeb, VersionesFestival,
    /// PropuestasDeCambio, CatalogoEditorial, UsuariosCodigosVerificacion...) que
    /// los guiones de esquema no traen. Esta prueba la ejecuta.
    /// </para>
    ///
    /// <para>
    /// La segunda mitad recorre la lectura publica por HTTP, como
    /// <c>ContenidoWebTests.La_lectura_publica_no_exige_sesion</c>, para que la muestra no
    /// se quede en el acceso a datos.
    /// </para>
    /// </summary>
    [HechoSqlServer]
    public async Task La_siembra_del_CMS_tambien_corre_en_la_rama_de_SQL_Server()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);

        var esperadas = SembradorDeContenidoWeb.LoadCatalog().Count;
        var sembradas = await db.ContenidoWeb.CountAsync();

        // Cero filas casi nunca significa "el seeder cuenta mal": significa que
        // EnsureReadyAsync se rompio y se lo trago, porque Database:ContinueOnStartupFailure
        // viene en true desde appsettings.json y deja la API en modo degradado sin ruido.
        // El detalle del fallo esta en la salida del servidor de pruebas.
        Assert.True(
            sembradas > 0,
            "ContenidoWeb quedo vacia: la rama de SQL Server de DatabaseBootstrapper no llego a "
            + "sembrar. Revisa el registro del arranque; el fallo va silenciado por "
            + "Database:ContinueOnStartupFailure.");
        Assert.Equal(esperadas, sembradas);

        // Sembrar no es publicar, igual que en el arnes de SQLite.
        var publicadasDeFabrica = await db.ContenidoWeb
            .CountAsync(fila => fila.Published != null && fila.UpdatedBy == "Sistema");
        Assert.Equal(0, publicadasDeFabrica);

        // La nomina es una fila unica; la crea EnsureWebTeamTableAsync con su CHECK.
        Assert.Equal(1, await db.EquipoWeb.CountAsync());

        using var anonimo = _base.CrearCliente();
        var respuesta = await anonimo.GetAsync("/api/v1/contenido-web");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<PublicContentResponse>());
    }

    /// <summary>
    /// Prueba nueva: la clase de defecto que el arnes de SQLite no puede ver.
    ///
    /// <para>
    /// El esquema real declara <c>CK_EntidadesAliadas_Estado CHECK (Estado IN
    /// ('activa','inactiva','pendiente','suspendida'))</c>. El modelo de EF no declara
    /// esa restriccion en ninguna parte, asi que la base que <c>EnsureCreated</c> fabrica
    /// para SQLite no la tiene: alli este mismo INSERT entra sin queja. Cualquier codigo
    /// que escriba un estado no previsto —una cadena mal normalizada, un valor nuevo que
    /// alguien olvido anadir a la restriccion— pasa las 195 pruebas en verde y falla en
    /// la primera escritura contra SQL Server.
    /// </para>
    ///
    /// <para>
    /// Se escribe por SQL en crudo a proposito: lo que se prueba es el motor, no el
    /// mapeo. El control positivo del final evita el otro fallo posible, que la prueba
    /// pase porque el INSERT esta mal escrito y no porque la restriccion actue.
    /// </para>
    /// </summary>
    /// <remarks>
    /// La muestra era <c>CK_EntidadesAliadas_Estado</c> hasta que el concepto de entidad
    /// aliada se retiro. Se sustituye por
    /// <c>CK_UsuariosEntidades_Rol</c>, que es mejor muestra: gobierna el rol DE ENTIDAD de la
    /// tabla puente persona-entidad, es decir el corazon del modelo nuevo, y es exactamente la
    /// clase de restriccion que SQLite no declara.
    /// </remarks>
    [HechoSqlServer]
    public async Task El_esquema_real_rechaza_un_rol_de_entidad_invalido()
    {
        using var alcance = _base.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);

        // Hace falta un usuario y una entidad reales: la tabla puente tiene FK contra las dos,
        // y sin ellas el INSERT fallaría por la FK y no por el CHECK, que es lo que se mide. Una
        // base desechable no depende de que la siembra opcional de cuentas técnicas esté activa.
        var idUsuario = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT TOP 1 IdUsuario FROM dbo.Usuarios ORDER BY IdUsuario;"));
        if (idUsuario == 0)
        {
            var usuario = new UserRow
            {
                FullName = "Usuario de muestra CHECK",
                Email = $"muestra-check-{Guid.NewGuid():N}@pnmc.local",
                PasswordHash = "no-utilizable",
                AccessChannel = "interno",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(usuario);
            await db.SaveChangesAsync();
            idUsuario = usuario.Id;
        }
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.Entidades (Nombre, TipoEntidad, EstadoRegistro, IdUsuarioCreador) VALUES (N'Entidad muestra CHECK', N'organizacion', N'activa', {idUsuario});");
        var idEntidad = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT TOP 1 IdEntidad FROM dbo.Entidades WHERE Nombre = N'Entidad muestra CHECK' ORDER BY IdEntidad DESC;"));

        var fallo = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dbo.UsuariosEntidades (IdUsuario, IdEntidad, RolEntidad) VALUES ({idUsuario}, {idEntidad}, N'rol_inventado');");
        });
        Assert.Contains("CK_UsuariosEntidades_Rol", fallo.Message, StringComparison.Ordinal);

        // Control positivo: con un rol del catalogo, la misma escritura entra.
        var insertadas = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO dbo.UsuariosEntidades (IdUsuario, IdEntidad, RolEntidad) VALUES ({idUsuario}, {idEntidad}, N'administrador');");
        Assert.Equal(1, insertadas);

        var rechazadas = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM dbo.UsuariosEntidades WHERE RolEntidad = N'rol_inventado';"));
        Assert.Equal(0, rechazadas);
    }

    /// <summary>
    /// Una base construida desde cero no tiene ni una tabla ni un rol del modelo de aliados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ESTA ES LA PRUEBA DE LA RETIRADA, y mira el motor real porque es el unico sitio donde
    /// la pregunta tiene sentido: el arnes de SQLite construye su esquema desde el modelo de
    /// EF, asi que alli las tablas nunca existieron y un aserto de ausencia pasaria sin haber
    /// probado nada.
    /// </para>
    /// <para>
    /// Cubre las DOS formas de reaparecer que tenia este concepto, y las dos son reales:
    /// </para>
    /// <list type="number">
    ///   <item><description>
    ///     POR NOMBRE ANTIGUO. Las tablas nacian en <c>V20260525_01</c> como
    ///     <c>EntidadesColaboradoras</c> y <c>UsuariosEntidadesColaboradoras</c>, y
    ///     <c>V20260525_02</c> solo las RENOMBRABA. Quien retirase el DDL del fichero que lleva
    ///     "aliados" en el nombre las veria reaparecer con el nombre viejo. Por eso el patron
    ///     de busqueda incluye <c>%Colaborad%</c>.
    ///   </description></item>
    ///   <item><description>
    ///     POR LA SEMILLA. El <c>MERGE</c> de <c>V20260519_03</c> sembraba los tres roles y
    ///     corre DESPUES de todo <c>schema/</c>: un <c>DELETE</c> colocado en una migracion
    ///     quedaba deshecho en la misma ejecucion, sin error y sin aviso.
    ///   </description></item>
    /// </list>
    /// </remarks>
    [HechoSqlServer]
    public async Task Una_base_nueva_no_tiene_nada_del_modelo_de_aliados()
    {
        var tablas = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN "
            + "(N'EntidadesAliadas', N'UsuariosEntidadesAliadas', N'EntidadesColaboradoras', N'UsuariosEntidadesColaboradoras');"));
        Assert.Equal(0, tablas);

        var roles = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM dbo.Roles WHERE NombreRol LIKE N'aliado[_]%';"));
        Assert.Equal(0, roles);

        // CONTROL POSITIVO. Sin el, los dos ceros de arriba podrian venir de una base vacia
        // —un fallo de montaje silencioso— en vez de una base bien construida.
        var rolesDePlataforma = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM dbo.Roles WHERE NombreRol IN (N'webmaster', N'gestor_interno', N'externo');"));
        Assert.Equal(3, rolesDePlataforma);

        // Y el catalogo no tiene nada mas: es el aserto que detecta un rol reintroducido.
        var rolesTotales = Convert.ToInt32(await _base.EscalarAsync("SELECT COUNT(*) FROM dbo.Roles;"));
        Assert.Equal(3, rolesTotales);

        // La columna de ambito de la solicitud de vinculacion apunta a la entidad del
        // ecosistema, no a una entidad aliada.
        Assert.NotNull(await _base.EscalarAsync(
            "SELECT COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadId');"));
        var columnaVieja = await _base.EscalarAsync(
            "SELECT COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadAliadaId');");
        Assert.True(columnaVieja is null or DBNull,
            "SolicitudesVinculacionRegistros conserva EntidadAliadaId sobre una base nueva.");

        // Y la relacion persona-entidad del modelo definitivo sigue en pie: es lo que MAS se
        // parece a lo que se retiro, y lo que un barrido por patron se llevaria por delante.
        Assert.NotNull(await _base.EscalarAsync("SELECT OBJECT_ID(N'dbo.UsuariosEntidades', N'U');"));
        Assert.NotNull(await _base.EscalarAsync("SELECT OBJECT_ID(N'dbo.Entidades', N'U');"));
    }
}
