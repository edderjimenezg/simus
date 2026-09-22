namespace PNMC.Api.Tests;

/// <summary>
/// El catálogo de permisos y su reparto por rol, tal y como los siembra
/// <c>pnmc-database/schema/V20260824_01__usuarios_roles_y_permisos.sql</c>.
/// </summary>
/// <remarks>
/// <para>
/// POR QUÉ EXISTE — EL DEFECTO U8 DEL PLAN DE CONSTRUCCIÓN. La vía de SQLite del arnés fabrica su
/// base con <c>EnsureCreated</c>, que crea las tablas del modelo pero <b>no ejecuta ni una línea
/// de los guiones de <c>schema/</c></b>. Sin esto, <c>dbo.Permisos</c> y <c>dbo.RolesPermisos</c>
/// existen vacías en esa vía, y el día que una guarda empiece a consultarlas <b>todas las pruebas
/// de permiso pasarían por vacío</b>: no porque el reparto sea correcto, sino porque no hay
/// reparto. Es el peor de los tres modos de fallo que el plan enumera, porque no se ve.
/// </para>
/// <para>
/// ES UNA SEGUNDA COPIA, Y SE SABE. La primera vive en el guion de esquema. Dos copias de una
/// lista es como se llega a que una se quede atrás sin que nadie lo note —el mismo argumento con
/// el que existe <c>Permisos.cs</c>— así que la copia <b>no se deja sola</b>:
/// <see cref="CatalogoDePermisosTests"/> lee el fichero <c>.sql</c> y exige que las dos digan
/// exactamente lo mismo. Si alguien concede un permiso en el guion y no aquí, esa prueba se pone
/// roja y nombra el código que falta.
/// </para>
/// <para>
/// NO SE LEE EN PRODUCCIÓN. Hoy ninguna guarda del API consulta estas tablas: las listas de roles
/// siguen escritas en el código. Esto es el inventario que hace que la vía de SQLite sea un espejo
/// fiel de la base real, no una fuente de decisiones.
/// </para>
/// </remarks>
public static class CatalogoDePermisosDeReferencia
{
    /// <summary>Los quince permisos del catálogo: código y módulo.</summary>
    public static readonly (string Codigo, string Modulo, string Nombre)[] Permisos =
    [
        ("registros.revisar", "ecosistema", "Revisar registros"),
        ("registros.publicar", "ecosistema", "Publicar registros"),
        ("entidades.administrar", "ecosistema", "Administrar entidades"),
        ("gobernanza.resolver_vinculos", "ecosistema", "Resolver vinculaciones"),
        ("festivales.decidir", "festivales", "Decidir sobre Festivales"),
        ("propuestas.decidir", "festivales", "Decidir propuestas de cambio"),
        ("festivales.normalizar", "festivales", "Normalizar versiones"),
        ("cms.editar", "cms", "Editar textos del sitio"),
        ("cms.publicar", "cms", "Publicar textos del sitio"),
        ("cms.importar", "cms", "Importar textos"),
        ("equipo_web.editar", "cms", "Editar el equipo web"),
        ("equipo_web.publicar", "cms", "Publicar el equipo web"),
        ("usuarios.administrar", "administracion", "Administrar usuarios"),
        ("sistema.configurar", "administracion", "Configurar el sistema"),
        ("monitor.consultar", "administracion", "Consultar el monitor"),
    ];

    /// <summary>
    /// El reparto: qué rol tiene qué permiso.
    /// </summary>
    /// <remarks>
    /// <b>La diferencia entre los dos roles internos son tres filas</b> —<c>cms.publicar</c>,
    /// <c>usuarios.administrar</c> y <c>sistema.configurar</c>— y las tres están medidas en el
    /// código de hoy. <c>externo</c> se queda con cero concesiones a propósito: lo que puede hacer
    /// una persona externa lo decide su vínculo en <c>UsuariosEntidades</c>, no un permiso fino.
    /// </remarks>
    public static readonly (string Rol, string Codigo)[] Reparto =
    [
        ("webmaster", "registros.revisar"),
        ("webmaster", "registros.publicar"),
        ("webmaster", "entidades.administrar"),
        ("webmaster", "gobernanza.resolver_vinculos"),
        ("webmaster", "festivales.decidir"),
        ("webmaster", "propuestas.decidir"),
        ("webmaster", "festivales.normalizar"),
        ("webmaster", "cms.editar"),
        ("webmaster", "cms.publicar"),
        ("webmaster", "cms.importar"),
        ("webmaster", "equipo_web.editar"),
        ("webmaster", "equipo_web.publicar"),
        ("webmaster", "usuarios.administrar"),
        ("webmaster", "sistema.configurar"),
        ("webmaster", "monitor.consultar"),

        ("gestor_interno", "registros.revisar"),
        ("gestor_interno", "entidades.administrar"),
        ("gestor_interno", "gobernanza.resolver_vinculos"),
        ("gestor_interno", "festivales.decidir"),
        ("gestor_interno", "propuestas.decidir"),
        ("gestor_interno", "festivales.normalizar"),
        ("gestor_interno", "cms.editar"),
        ("gestor_interno", "equipo_web.editar"),
        ("gestor_interno", "monitor.consultar"),
    ];
}
