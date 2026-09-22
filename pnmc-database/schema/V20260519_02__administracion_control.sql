/*
    PNMC - Administracion y control
    Fase: reconstruccion local desde cero

    Incluye usuarios, roles, estados de contenido, categorias, etiquetas,
    archivos y bitacora de auditoria.
*/

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        IdRol int IDENTITY(1,1) NOT NULL,
        NombreRol nvarchar(80) NOT NULL,
        DescripcionRol nvarchar(500) NULL,
        CONSTRAINT PK_Roles PRIMARY KEY (IdRol),
        CONSTRAINT UQ_Roles_NombreRol UNIQUE (NombreRol)
    );
END;

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios
    (
        IdUsuario int IDENTITY(1,1) NOT NULL,
        NombreCompleto nvarchar(180) NOT NULL,
        CorreoElectronico nvarchar(180) NOT NULL,
        HashContrasena nvarchar(500) NOT NULL,
        IdRol int NOT NULL,
        CanalAcceso nvarchar(40) NOT NULL CONSTRAINT DF_Usuarios_CanalAcceso DEFAULT (N'interno'),
        TipoPerfil nvarchar(80) NULL,
        Activo bit NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        UltimoAcceso datetime2(0) NULL,
        CONSTRAINT PK_Usuarios PRIMARY KEY (IdUsuario),
        CONSTRAINT UQ_Usuarios_CorreoElectronico UNIQUE (CorreoElectronico),
        CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (IdRol) REFERENCES dbo.Roles (IdRol),
        CONSTRAINT CK_Usuarios_CorreoElectronico_Formato CHECK (CorreoElectronico LIKE '%_@_%._%')
    );
END;

IF COL_LENGTH(N'dbo.Usuarios', N'CanalAcceso') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios
    ADD CanalAcceso nvarchar(40) NOT NULL
        CONSTRAINT DF_Usuarios_CanalAcceso DEFAULT (N'interno') WITH VALUES;
END;

IF COL_LENGTH(N'dbo.Usuarios', N'TipoPerfil') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios
    ADD TipoPerfil nvarchar(80) NULL;
END;

/*
    GUARDA DE COMPATIBILIDAD.  `dbo.Usuarios.IdRol` ya no existe: la retira la seccion 11 de
    V20260824_01__usuarios_roles_y_permisos.sql.  Este bloque solo tiene sentido mientras exista,
    es decir, en la PRIMERA construccion de una base -donde este guion la crea unas lineas mas
    arriba- y en una base antigua que todavia no se haya migrado.

    Y VA DENTRO DE EXEC(N'...'), NO SOLO DETRAS DEL IF.  SQL Server enlaza las columnas de una
    tabla existente al COMPILAR el lote, no al ejecutarlo: sin el EXEC, el lote entero muere con
    Msg 207 sobre `IdRol` y ni siquiera llega a evaluar el COL_LENGTH que debia protegerlo.

    POR QUE HACE FALTA.  El guion debe poder aplicarse tanto sobre una base recien creada
    -donde `IdRol` existe porque este mismo fichero la acaba de crear- como sobre una ya migrada,
    donde la columna ya no esta.  Sin la guarda, el segundo caso muere con Msg 1911 en el indice
    y Msg 207 en los UPDATE.
*/
IF COL_LENGTH(N'dbo.Usuarios', N'IdRol') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Usuarios_IdRol' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
BEGIN
    EXEC(N'CREATE INDEX IX_Usuarios_IdRol ON dbo.Usuarios (IdRol);');
END;

IF OBJECT_ID(N'dbo.EstadosContenido', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EstadosContenido
    (
        IdEstadoContenido int IDENTITY(1,1) NOT NULL,
        CodigoEstado nvarchar(80) NOT NULL,
        NombreEstado nvarchar(120) NOT NULL,
        DescripcionEstado nvarchar(500) NULL,
        CONSTRAINT PK_EstadosContenido PRIMARY KEY (IdEstadoContenido),
        CONSTRAINT UQ_EstadosContenido_CodigoEstado UNIQUE (CodigoEstado),
        CONSTRAINT UQ_EstadosContenido_NombreEstado UNIQUE (NombreEstado),
        CONSTRAINT CK_EstadosContenido_CodigoEstado_Formato CHECK (
            CodigoEstado = LOWER(CodigoEstado)
            AND CodigoEstado NOT LIKE '% %'
        )
    );
END;

IF OBJECT_ID(N'dbo.Categorias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categorias
    (
        IdCategoria int IDENTITY(1,1) NOT NULL,
        CodigoModulo nvarchar(80) NOT NULL,
        NombreCategoria nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(600) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_Categorias PRIMARY KEY (IdCategoria),
        CONSTRAINT UQ_Categorias_Modulo_Slug UNIQUE (CodigoModulo, Slug),
        CONSTRAINT UQ_Categorias_Modulo_Nombre UNIQUE (CodigoModulo, NombreCategoria),
        CONSTRAINT CK_Categorias_CodigoModulo_Formato CHECK (
            CodigoModulo = LOWER(CodigoModulo)
            AND CodigoModulo NOT LIKE '% %'
        ),
        CONSTRAINT CK_Categorias_Orden CHECK (OrdenVisualizacion > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Categorias_CodigoModulo_Orden' AND object_id = OBJECT_ID(N'dbo.Categorias'))
BEGIN
    CREATE INDEX IX_Categorias_CodigoModulo_Orden
    ON dbo.Categorias (CodigoModulo, OrdenVisualizacion);
END;

IF OBJECT_ID(N'dbo.Etiquetas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Etiquetas
    (
        IdEtiqueta int IDENTITY(1,1) NOT NULL,
        NombreEtiqueta nvarchar(120) NOT NULL,
        Slug nvarchar(140) NOT NULL,
        CONSTRAINT PK_Etiquetas PRIMARY KEY (IdEtiqueta),
        CONSTRAINT UQ_Etiquetas_NombreEtiqueta UNIQUE (NombreEtiqueta),
        CONSTRAINT UQ_Etiquetas_Slug UNIQUE (Slug)
    );
END;

IF OBJECT_ID(N'dbo.Archivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Archivos
    (
        IdArchivo int IDENTITY(1,1) NOT NULL,
        NombreOriginal nvarchar(260) NOT NULL,
        NombreAlmacenado nvarchar(260) NOT NULL,
        Extension nvarchar(20) NULL,
        TipoMime nvarchar(120) NOT NULL,
        PesoBytes bigint NULL,
        RutaAlmacenamiento nvarchar(700) NOT NULL,
        UrlPublica nvarchar(1000) NULL,
        TextoAlternativo nvarchar(300) NULL,
        Pie nvarchar(500) NULL,
        Credito nvarchar(250) NULL,
        IdUsuarioCarga int NOT NULL,
        FechaCarga datetime2(0) NOT NULL CONSTRAINT DF_Archivos_FechaCarga DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Archivos PRIMARY KEY (IdArchivo),
        CONSTRAINT UQ_Archivos_RutaAlmacenamiento UNIQUE (RutaAlmacenamiento),
        CONSTRAINT FK_Archivos_Usuarios FOREIGN KEY (IdUsuarioCarga) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_Archivos_PesoBytes CHECK (PesoBytes IS NULL OR PesoBytes >= 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_IdUsuarioCarga' AND object_id = OBJECT_ID(N'dbo.Archivos'))
BEGIN
    CREATE INDEX IX_Archivos_IdUsuarioCarga ON dbo.Archivos (IdUsuarioCarga);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Archivos_TipoMime' AND object_id = OBJECT_ID(N'dbo.Archivos'))
BEGIN
    CREATE INDEX IX_Archivos_TipoMime ON dbo.Archivos (TipoMime);
END;

IF OBJECT_ID(N'dbo.BitacoraAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BitacoraAuditoria
    (
        IdAuditoria bigint IDENTITY(1,1) NOT NULL,
        IdUsuario int NULL,
        TablaAfectada nvarchar(160) NOT NULL,
        IdRegistroAfectado nvarchar(120) NOT NULL,
        Accion nvarchar(40) NOT NULL,
        ValoresAnteriores nvarchar(max) NULL,
        ValoresNuevos nvarchar(max) NULL,
        FechaAccion datetime2(0) NOT NULL CONSTRAINT DF_BitacoraAuditoria_FechaAccion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_BitacoraAuditoria PRIMARY KEY (IdAuditoria),
        CONSTRAINT FK_BitacoraAuditoria_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_BitacoraAuditoria_Accion CHECK (Accion IN (
            N'crear', N'actualizar', N'eliminar', N'publicar', N'archivar',
            N'aprobar', N'rechazar', N'iniciar_sesion', N'cerrar_sesion',
            N'iniciar_sesion_externa', N'cerrar_sesion_externa',
            N'crear_organizacion', N'asignar_administrador_inicial'
        )),
        CONSTRAINT CK_BitacoraAuditoria_ValoresAnteriores_JSON CHECK (
            ValoresAnteriores IS NULL OR ISJSON(ValoresAnteriores) = 1
        ),
        CONSTRAINT CK_BitacoraAuditoria_ValoresNuevos_JSON CHECK (
            ValoresNuevos IS NULL OR ISJSON(ValoresNuevos) = 1
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BitacoraAuditoria_Tabla_Registro' AND object_id = OBJECT_ID(N'dbo.BitacoraAuditoria'))
BEGIN
    CREATE INDEX IX_BitacoraAuditoria_Tabla_Registro
    ON dbo.BitacoraAuditoria (TablaAfectada, IdRegistroAfectado, FechaAccion DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BitacoraAuditoria_IdUsuario_Fecha' AND object_id = OBJECT_ID(N'dbo.BitacoraAuditoria'))
BEGIN
    CREATE INDEX IX_BitacoraAuditoria_IdUsuario_Fecha
    ON dbo.BitacoraAuditoria (IdUsuario, FechaAccion DESC);
END;
