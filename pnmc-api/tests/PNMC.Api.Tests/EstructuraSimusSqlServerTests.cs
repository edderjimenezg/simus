using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La estructura que trajo la migración de SIMUS existe, y existe **con la forma decidida**.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE HACE FALTA ESTA PRUEBA, Y POR QUE SOBRE SQL SERVER. Los dos guiones de la migración
/// —<c>V20260824_01</c> y <c>V20260824_02</c>— añaden 20 tablas, 3 foráneas a <c>dbo.Divipola</c>
/// y un catálogo de permisos, y <b>ninguna prueba las miraba</b>. No es un descuido de quien las
/// escribió: es que no había forma de mirarlas desde el carril normal.
/// </para>
/// <list type="bullet">
///   <item><description>
///     El arnés de SQLite fabrica su base <b>desde el modelo de EF</b>, y el modelo no mapea
///     ninguna de las 20 tablas nuevas: allí sencillamente no existen, y un aserto sobre ellas
///     no probaría nada.
///   </description></item>
///   <item><description>
///     <see cref="ParidadEsquemaSinArranqueTests"/> recorre el modelo de EF y comprueba que la
///     base lo satisfaga. Va en esa dirección y sólo en esa: lo que está en <c>schema/</c> y
///     <b>no</b> en el modelo le es invisible por construcción.
///   </description></item>
/// </list>
/// <para>
/// Es decir: sin esta prueba, borrar entero <c>V20260824_02</c> —quince tablas— dejaba la suite
/// en verde. Está comprobado con el mutante.
/// </para>
/// <para>
/// LO QUE COMPRUEBA, Y ES DELIBERADAMENTE POCO. No prueba comportamiento, porque todavía no hay
/// comportamiento: ninguna ruta del API lee estas tablas. Comprueba <b>las decisiones que costó
/// tomar y que un cambio distraído desharía sin ruido</b>: que las tablas están, que no hay
/// cascadas, que el reparto de permisos es el espejo exacto de las guardas de hoy, y que las dos
/// tablas que D11.4 mandó endurecer quedaron endurecidas. Cada aserto tiene detrás un defecto
/// que la refutación encontró y que costó trabajo cerrar; la prueba existe para que no vuelva.
/// </para>
/// <para>
/// Mutantes demostrados (24 ago 2026): quitando <c>V20260824_02</c> falla el primer hecho
/// nombrando las quince tablas ausentes; devolviendo un <c>ON DELETE CASCADE</c> a una puente
/// falla el segundo nombrando la foránea; concediendo <c>cms.publicar</c> a
/// <c>gestor_interno</c> falla el tercero nombrando el permiso de más.
/// </para>
/// </remarks>
[Collection(ColeccionSqlServer.Nombre)]
public sealed class EstructuraSimusSqlServerTests
{
    private readonly SqlServerFixture _base;

    public EstructuraSimusSqlServerTests(SqlServerFixture baseDesechable)
    {
        _base = baseDesechable;
    }

    /// <summary>Las tablas que traen los guiones de esquema, nombradas una a una.</summary>
    /// <remarks>
    /// <para>
    /// Se nombran en vez de contarlas. Un recuento se satisface con veinte tablas cualesquiera y
    /// además obliga a tocar la prueba cada vez que el proyecto crece por otro sitio.
    /// </para>
    /// <para>
    /// LAS SEIS PUENTES POR VERSION YA NO SE NOMBRAN, y no es que falten: el 18 de septiembre de
    /// 2026 las veinte tablas de relación del circuito de Festivales —una por proceso y por
    /// vocabulario— quedaron en ocho compartidas por todo el Ecosistema, con el dueño en
    /// <c>ModuloId</c> + <c>RegistroId</c>. Lo que se comprueba aquí son las ocho, que es donde
    /// vive ahora aquel contenido.
    /// </para>
    /// </remarks>
    private static readonly string[] TablasEsperadas =
    [
        // V20260824_01 — usuarios, roles y permisos (D10, D12)
        "UsuariosRoles", "TiposDocumento", "Permisos", "RolesPermisos", "MenuElementos",
        // V20260824_02 — los nueve catálogos del módulo de festivales
        "TipologiasFestival", "TiposOrganizador", "FuentesFinanciacion", "ExpresionesArtisticas",
        "ModalidadesParticipacion", "NaturalezasEntidad", "ZonasUrbanoRural",
        "TitulacionesColectivas", "TiposIngreso",
        // V20260918_02 y V20260918_03 — las ocho relaciones compartidas
        "PracticasMusicalesDeRegistro", "TerritoriosSonorosDeRegistro",
        "ExpresionesArtisticasDeRegistro", "ModalidadesParticipacionDeRegistro",
        "TiposIngresoDeRegistro", "LocalizacionesDeRegistro",
        "EntidadesAliadasDeRegistro", "ArchivosDeRegistro",
    ];

    [HechoSqlServer]
    public async Task Las_Tablas_De_La_Migracion_Existen()
    {
        var faltan = new List<string>();
        foreach (var tabla in TablasEsperadas)
        {
            var existe = Convert.ToInt32(await _base.EscalarAsync(
                $"SELECT COUNT(*) FROM sys.tables WHERE name = N'{tabla}';"));
            if (existe == 0)
            {
                faltan.Add(tabla);
            }
        }

        Assert.True(
            faltan.Count == 0,
            "Estas tablas de la migracion de SIMUS no las crea ningun guion de schema/:\n  "
            + string.Join("\n  ", faltan));
    }

    /// <summary>
    /// Cero foráneas con cascada en toda la base, y las tres nuevas hacia <c>Divipola</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LA CASCADA (defectos F2 y U4). El diseño puso <c>ON DELETE CASCADE</c> en siete foráneas
    /// nuevas, presentándolo como convención del proyecto. No lo era: PNMC tiene <b>cero</b>. Lo
    /// grave no era la cascada, era la incoherencia — <c>VersionesFestival</c> ya tenía dos
    /// puentes sin ella, así que el mismo <c>DELETE</c> habría hecho dos cosas distintas según la
    /// tabla. El aserto es sobre la base entera, no sobre las siete: la regla es «PNMC no usa
    /// cascadas», y si algún día deja de serlo hay que decidirlo, no descubrirlo.
    /// </para>
    /// <para>
    /// LAS FORÁNEAS QUE SE NOMBRAN (D11.4, revertida). <c>VersionesFestival</c> y
    /// <c>PropuestasCambioFestival</c> eran las dos únicas tablas territorializadas sin foránea a
    /// <c>Divipola</c>, y la decisión de dejarlas así se tomó sobre una medición equivocada: se
    /// midió el modelo de EF, no la base. A ellas se sumó la de
    /// <c>LocalizacionesDeRegistro</c>, que heredó la que tenían las localizaciones de versión. <c>PropuestasCambioFestival</c>
    /// salió de la lista, cuando la tabla se retiró: su circuito vive
    /// en <c>PropuestasDeCambio</c>, que guarda lo que cambia campo por campo y no un par
    /// territorial propio. El aserto de abajo —que ninguna tabla con el par se quede sin foránea—
    /// es el que de verdad protege la regla, y ese no caduca.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task Ninguna_Foranea_Tiene_Cascada_Y_Las_Territorializadas_Apuntan_A_Divipola()
    {
        var conCascada = Convert.ToString(await _base.EscalarAsync(
            "SELECT ISNULL(STRING_AGG(name, N', '), N'') FROM sys.foreign_keys "
            + "WHERE delete_referential_action <> 0 OR update_referential_action <> 0;"));

        Assert.True(
            string.IsNullOrEmpty(conCascada),
            "PNMC no usa foraneas con cascada, y estas la tienen. Una cascada borra filas en "
            + "silencio y deja el mismo DELETE comportandose de dos maneras segun la tabla:\n  "
            + conCascada);

        foreach (var foranea in new[]
                 {
                     "FK_VersionesFestival_Divipola",
                     "FK_LocalizacionesDeRegistro_Divipola",
                 })
        {
            var existe = Convert.ToInt32(await _base.EscalarAsync(
                $"SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'{foranea}';"));
            Assert.True(existe == 1, $"Falta {foranea}. Es la decision D11.4, que se revirtio el 24 ago 2026.");
        }

        // Y ninguna tabla con el par territorial se queda fuera. Este es el aserto que no caduca:
        // no fija un numero, fija que no sobre ninguna. Ver validar_migracion_simus.sql, B3.
        var sinForanea = Convert.ToString(await _base.EscalarAsync(
            "SELECT ISNULL(STRING_AGG(t.name, N', '), N'') FROM sys.tables t "
            + "WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = N'CodigoDepartamento') "
            + "  AND EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = N'CodigoMunicipio') "
            + "  AND t.name <> N'Divipola' "
            + "  AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = t.object_id "
            + "                    AND fk.referenced_object_id = OBJECT_ID(N'dbo.Divipola'));"));

        Assert.True(
            string.IsNullOrEmpty(sinForanea),
            "Estas tablas guardan el par departamento/municipio y no tienen foranea a dbo.Divipola:\n  "
            + sinForanea);
    }

    /// <summary>
    /// El reparto de permisos es el <b>espejo exacto</b> de las guardas que el código aplica hoy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DEFECTO U2, QUE ERA EL MAS PELIGROSO DE LOS CINCO ALTOS. La primera versión del reparto
    /// concedía a <c>gestor_interno</c> tres capacidades que hoy son <b>sólo</b> de
    /// <c>webmaster</c>: <c>cms.importar</c>, <c>equipo_web.administrar</c> y
    /// <c>registros.publicar</c>. Eso es «más roles significan más puertas», que es lo que D12.1
    /// define como defecto y no como función — y habría entrado como parte de una migración de
    /// estructura, que es la forma más silenciosa de cambiar una política.
    /// </para>
    /// <para>
    /// Los nueve de <c>gestor_interno</c> se fijan uno a uno y por nombre. Un recuento no
    /// serviría: cambiar uno por otro mantiene el nueve.
    /// </para>
    /// </remarks>
    [HechoSqlServer]
    public async Task El_Reparto_De_Permisos_No_Le_Abre_Ninguna_Puerta_Nueva_A_Gestor_Interno()
    {
        var deGestor = Convert.ToString(await _base.EscalarAsync(
            "SELECT ISNULL(STRING_AGG(p.CodigoPermiso, N', ') WITHIN GROUP (ORDER BY p.CodigoPermiso), N'') "
            + "FROM dbo.RolesPermisos rp "
            + "JOIN dbo.Roles r ON r.IdRol = rp.IdRol "
            + "JOIN dbo.Permisos p ON p.IdPermiso = rp.IdPermiso "
            + "WHERE r.NombreRol = N'gestor_interno';"));

        Assert.Equal(
            "cms.editar, entidades.administrar, equipo_web.editar, festivales.decidir, "
            + "festivales.normalizar, gobernanza.resolver_vinculos, monitor.consultar, "
            + "propuestas.decidir, registros.revisar",
            deGestor);

        // externo no tiene NINGUNO, y esa es la mitad que mas facil se olvida.
        var deExterno = Convert.ToInt32(await _base.EscalarAsync(
            "SELECT COUNT(*) FROM dbo.RolesPermisos rp JOIN dbo.Roles r ON r.IdRol = rp.IdRol "
            + "WHERE r.NombreRol = N'externo';"));
        Assert.Equal(0, deExterno);

        // Control positivo: sin esto, los dos asertos de arriba pasarian con las tablas vacias.
        var catalogo = Convert.ToInt32(await _base.EscalarAsync("SELECT COUNT(*) FROM dbo.Permisos;"));
        Assert.True(catalogo >= 15, $"El catalogo de permisos tiene {catalogo} filas; se esperaban 15 o mas.");
    }
}
