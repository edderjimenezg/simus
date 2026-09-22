/*
    PNMC - Datos iniciales minimos para administracion y control.
*/

/*
    LOS TRES ROLES DE PLATAFORMA. Este MERGE corre DESPUES de todo schema/ —lo hacen asi
    seed-local-db.sh y el arnes de SQL Server—, de modo que es la ultima palabra sobre
    dbo.Roles. Cuando aqui figuraban aliado_admin/editor/lector, cualquier DELETE colocado
    en una migracion quedaba deshecho en la misma ejecucion, sin error y sin aviso: el MERGE
    no falla, simplemente reinserta. Se retiraron, en el mismo
    cambio que la migracion de retirada.
*/
MERGE dbo.Roles AS destino
USING (VALUES
    (N'webmaster', N'Control total de usuarios, modulos, datos, configuracion, revision, publicacion y mantenimiento.'),
    (N'gestor_interno', N'Segundo nivel general de administracion institucional.'),
    (N'externo', N'Persona del ecosistema. Es a la vez la persona registrada y el usuario externo que representa a un actor: los distingue el vinculo en UsuariosEntidades, no el rol.')
) AS origen (NombreRol, DescripcionRol)
ON destino.NombreRol = origen.NombreRol
WHEN MATCHED THEN
    UPDATE SET DescripcionRol = origen.DescripcionRol
WHEN NOT MATCHED THEN
    INSERT (NombreRol, DescripcionRol)
    VALUES (origen.NombreRol, origen.DescripcionRol);

MERGE dbo.EstadosContenido AS destino
USING (VALUES
    (N'borrador', N'Borrador', N'Contenido en elaboracion interna.'),
    (N'en_revision', N'En revision', N'Contenido enviado a revision editorial o tecnica.'),
    (N'ajustes_solicitados', N'Ajustes solicitados', N'Contenido devuelto al responsable para corregir campos u observaciones.'),
    (N'aprobado', N'Aprobado', N'Contenido aprobado, pendiente de publicacion o activacion.'),
    (N'publicado', N'Publicado', N'Contenido visible para usuarios finales.'),
    (N'archivado', N'Archivado', N'Contenido retirado de la vista publica sin eliminarlo.'),
    (N'rechazado', N'Rechazado', N'Contenido revisado y no aprobado para publicacion.'),
    -- Estado de una Entidad recien dada de alta por el canal externo, no de un contenido.
    -- ExternalOrganizationEndpoints lo escribia desde el principio y la
    -- tabla no lo tenia, de modo que FK_Entidades_EstadosContenido rechazaba el alta: crear
    -- una organizacion era imposible contra SQL Server. Comprobado.
    (N'registrada', N'Registrada', N'Entidad dada de alta por su responsable, activa y sin aprobacion ministerial previa.')
) AS origen (CodigoEstado, NombreEstado, DescripcionEstado)
ON destino.CodigoEstado = origen.CodigoEstado
WHEN MATCHED THEN
    UPDATE SET
        NombreEstado = origen.NombreEstado,
        DescripcionEstado = origen.DescripcionEstado
WHEN NOT MATCHED THEN
    INSERT (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (origen.CodigoEstado, origen.NombreEstado, origen.DescripcionEstado);

MERGE dbo.Categorias AS destino
USING (VALUES
    (N'agenda', N'Convocatoria', N'convocatoria', N'Eventos o llamados abiertos a participacion.', 1),
    (N'agenda', N'Encuentro', N'encuentro', N'Espacios de reunion, articulacion o intercambio.', 2),
    (N'agenda', N'Formacion', N'formacion', N'Actividades pedagogicas, talleres o procesos formativos.', 3),
    (N'agenda', N'Circulacion', N'circulacion', N'Actividades de circulacion, muestras, conciertos o programacion.', 4),
    (N'noticias', N'Institucional', N'institucional', N'Noticias y comunicaciones institucionales.', 1),
    (N'noticias', N'Territorio', N'territorio', N'Noticias relacionadas con procesos territoriales.', 2),
    (N'noticias', N'Convocatorias', N'convocatorias', N'Noticias asociadas a convocatorias o invitaciones publicas.', 3),
    (N'noticias', N'Memoria', N'memoria', N'Noticias relacionadas con memoria, documentacion e investigacion.', 4),
    (N'galeria', N'Archivo fotografico', N'archivo-fotografico', N'Albumes o registros fotograficos.', 1),
    (N'galeria', N'Video', N'video', N'Albumes o registros audiovisuales.', 2),
    (N'galeria', N'Evento', N'evento', N'Galerias asociadas a eventos especificos.', 3),
    (N'editorial', N'Publicacion', N'publicacion', N'Recursos editoriales publicados por el PNMC o aliados.', 1),
    (N'editorial', N'Investigacion', N'investigacion', N'Recursos asociados a investigacion, memoria o documentacion.', 2),
    (N'editorial', N'Material pedagogico', N'material-pedagogico', N'Recursos pedagogicos, guias, cartillas o metodologias.', 3)
) AS origen (CodigoModulo, NombreCategoria, Slug, Descripcion, OrdenVisualizacion)
ON destino.CodigoModulo = origen.CodigoModulo AND destino.Slug = origen.Slug
WHEN MATCHED THEN
    UPDATE SET
        NombreCategoria = origen.NombreCategoria,
        Descripcion = origen.Descripcion,
        OrdenVisualizacion = origen.OrdenVisualizacion
WHEN NOT MATCHED THEN
    INSERT (CodigoModulo, NombreCategoria, Slug, Descripcion, OrdenVisualizacion)
    VALUES (origen.CodigoModulo, origen.NombreCategoria, origen.Slug, origen.Descripcion, origen.OrdenVisualizacion);

MERGE dbo.Etiquetas AS destino
USING (VALUES
    (N'PNMC', N'pnmc'),
    (N'Formacion', N'formacion'),
    (N'Circulacion', N'circulacion'),
    (N'Memoria', N'memoria'),
    (N'Territorio', N'territorio'),
    (N'Participacion', N'participacion')
) AS origen (NombreEtiqueta, Slug)
ON destino.Slug = origen.Slug
WHEN MATCHED THEN
    UPDATE SET NombreEtiqueta = origen.NombreEtiqueta
WHEN NOT MATCHED THEN
    INSERT (NombreEtiqueta, Slug)
    VALUES (origen.NombreEtiqueta, origen.Slug);

/*
    EL ROL VA EN dbo.UsuariosRoles, NO EN UNA COLUMNA.

    Una version anterior de esta seccion escribia `Usuarios.IdRol`.  Esa columna ya no existe: la retira
    `schema/V20260824_01__usuarios_roles_y_permisos.sql` seccion 11, y `schema/` corre ENTERO antes
    que `seed/`, de modo que cuando este fichero se ejecuta la columna se ha ido.  Escribirla aqui
    fallaria con Msg 207 y se llevaria por delante toda la siembra que viene detras.

    DOS PASOS, Y EN ESTE ORDEN: primero la cuenta, despues su rol.  La fila de UsuariosRoles
    necesita el IdUsuario, y ese lo asigna la base al insertar.
*/
DECLARE @IdRolWebmaster int = (
    SELECT IdRol FROM dbo.Roles WHERE NombreRol = N'webmaster'
);

IF @IdRolWebmaster IS NULL
BEGIN
    RAISERROR(N'Falta el rol base webmaster en dbo.Roles.', 16, 1);
END;

MERGE dbo.Usuarios AS destino
USING (VALUES
    (N'Sistema PNMC', N'sistema@pnmc.local', N'pendiente_configurar_hash_seguro', CAST(1 AS bit))
) AS origen (NombreCompleto, CorreoElectronico, HashContrasena, Activo)
ON destino.CorreoElectronico = origen.CorreoElectronico
WHEN MATCHED THEN
    UPDATE SET
        NombreCompleto = origen.NombreCompleto,
        Activo = origen.Activo,
        FechaActualizacion = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (NombreCompleto, CorreoElectronico, HashContrasena, Activo)
    VALUES (origen.NombreCompleto, origen.CorreoElectronico, origen.HashContrasena, origen.Activo);

/*
    Y SU ROL.  Solo INSERT: si alguien le concedio otro rol a esta cuenta desde la consola, volver
    a sembrar no debe retirarselo en silencio.  Es la misma regla que el reparto de permisos de
    schema/V20260824_01, y el reverso de la que aprendio V20260519_01: converger, no reescribir.
*/
MERGE dbo.UsuariosRoles AS destino
USING (
    SELECT u.IdUsuario, @IdRolWebmaster AS IdRol
    FROM dbo.Usuarios AS u
    WHERE u.CorreoElectronico = N'sistema@pnmc.local'
) AS origen
ON destino.IdUsuario = origen.IdUsuario AND destino.IdRol = origen.IdRol
WHEN NOT MATCHED THEN
    INSERT (IdUsuario, IdRol) VALUES (origen.IdUsuario, origen.IdRol);

DECLARE @IdUsuarioSistema int = (
    SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'sistema@pnmc.local'
);

/*
    AQUI SE SEMBRABA UN ARCHIVO DE PRUEBA, Y NO DEBIA.

    Insertaba una fila `placeholder.txt` con `PesoBytes = 0` y sin contenido, descrita en la propia
    semilla como «Archivo temporal de prueba … para validar metadatos de archivos». El Banco de
    archivos de la consola la listaba como un archivo real y su previsualizacion contestaba 404,
    porque `GET /publico/archivos/{id}` no tiene bytes que servir. Comprobado
    de 2026 recorriendo la consola: era el archivo 5.

    Se retira de la semilla para que una instalacion nueva no la reciba. Las bases que ya la tienen
    conservan la fila —no se borran datos de paso— y deja de molestar porque el listado del banco
    excluye las filas sin bytes; ver `BancoDeArchivosEndpoints.ListarInstitucional`.
*/

IF NOT EXISTS (
    SELECT 1
    FROM dbo.BitacoraAuditoria
    WHERE TablaAfectada = N'AdministracionControl'
      AND IdRegistroAfectado = N'seed-inicial'
      AND Accion = N'crear'
)
BEGIN
    INSERT INTO dbo.BitacoraAuditoria
        (IdUsuario, TablaAfectada, IdRegistroAfectado, Accion, ValoresAnteriores, ValoresNuevos)
    VALUES
        (@IdUsuarioSistema, N'AdministracionControl', N'seed-inicial', N'crear', NULL, N'{"mensaje":"Carga inicial de administracion y control"}');
END;

/*
    LA ORGANIZACION INSTITUCIONAL (, 25 ago 2026).

    Quien responde por un festival mientras ninguna organizacion de la comunidad lo reclame.
    No es un relleno: es la respuesta a la pregunta que la ficha publica tiene que poder
    contestar —«¿quien responde por esto?»— cuando nadie lo ha reclamado todavia.

    VA AQUI Y NO EN UNA SEMILLA POSTERIOR por orden de dependencias: `V20260519_06` inserta los
    festivales y necesita apuntar a ella, y `Entidades.IdUsuarioCreador` necesita a su vez que
    los usuarios existan, lo cual ocurre unas lineas mas arriba en este mismo fichero.

    Se marca con `EsInstitucional = 1`, que tiene indice unico filtrado: solo puede haber una.
    El codigo la resuelve por esa marca y no por su identificador, para no atar la aplicacion al
    orden en que se sembro una base concreta.

    MERGE y no INSERT: la siembra tiene que poder correr dos veces. Y si alguien le corrigio el
    nombre o el correo desde la consola, converger no debe deshacerselo en silencio — por eso el
    UPDATE solo toca la marca institucional y el tipo, que son lo estructural.
*/
/*
    EL IDENTIFICADOR ES FIJO Y BAJO A PROPOSITO. `seed/V20260519_07` es dueña del rango >= 100 y
    lo limpia con `DELETE FROM dbo.Entidades WHERE IdEntidad >= 100` en cada pasada. Si la
    institucional cayera ahi, ese DELETE intentaria borrar la entidad a la que apuntan TODOS los
    festivales, moriria con Msg 547 y se llevaria por delante la siembra entera detras. Paso el
    25 ago 2026, con la institucional en 116.
*/
IF NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE EsInstitucional = 1)
BEGIN
    SET IDENTITY_INSERT dbo.Entidades ON;

    /*
        SIN `NivelCobertura` NI SEDE, Y CON EL ESTADO DE UNA ORGANIZACION.

        Esta inserción nombraba tres columnas que `schema/V20260909_02` retiró de `dbo.Entidades`
        —`NivelCobertura`, `CodigoDepartamento`, `CodigoMunicipio`— porque el alcance territorial
        es de cada PROCESO y no de la entidad. Y escribía `EstadoRegistro = 'publicado'`, que es un
        estado de CONTENIDO: una organización no se publica, se activa. `CK_Entidades_EstadoRegistro`
        solo admite `pendiente_de_confirmacion`, `activa`, `inactiva` y `eliminada`.

        Llevaba así desde y no lo vio nadie porque la siembra entera
        estaba rota antes de llegar aquí: `seed-local-db.sh` moría en su primer fichero.
    */
    INSERT INTO dbo.Entidades
        (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, SitioWeb,
         EstadoRegistro, Activo,
         EsInstitucional, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion)
    VALUES
        (1,
         N'organizacion',
         N'Plan Nacional de Música para la Convivencia',
         N'Plan Nacional de Música para la Convivencia',
         N'Organización responsable por defecto de los registros que ninguna organización del ecosistema ha reclamado todavía.',
         N'contacto@pnmc.local',
         N'https://pnmc.local',
         N'activa', 1,
         1,
         @IdUsuarioSistema, @IdUsuarioSistema, SYSUTCDATETIME());

    SET IDENTITY_INSERT dbo.Entidades OFF;
END;
