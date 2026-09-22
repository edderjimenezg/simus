/*
    PNMC - Administracion extendida.
    Estructura preparada para permisos, textos del sitio, editorial propio,
    entidades colaboradoras e historial general de revision.
*/

IF OBJECT_ID(N'dbo.RegistrosRevisionHistorial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosRevisionHistorial (
        IdRevisionHistorial bigint IDENTITY(1,1) NOT NULL,
        ModuloId nvarchar(80) NOT NULL,
        RegistroId nvarchar(120) NOT NULL,
        EstadoAnterior nvarchar(80) NULL,
        EstadoNuevo nvarchar(80) NOT NULL,
        Accion nvarchar(80) NOT NULL,
        Comentario nvarchar(1200) NULL,
        MotivoRechazo nvarchar(1200) NULL,
        CamposObservados nvarchar(max) NULL,
        IdUsuario int NULL,
        IdEntidadColaboradora int NULL,
        Fecha datetime2(0) NOT NULL CONSTRAINT DF_RegistrosRevisionHistorial_Fecha DEFAULT (SYSUTCDATETIME()),
        MetadataJson nvarchar(max) NULL,
        CONSTRAINT PK_RegistrosRevisionHistorial PRIMARY KEY (IdRevisionHistorial),
        CONSTRAINT CK_RegistrosRevisionHistorial_EstadoNuevo CHECK (EstadoNuevo IN (
            N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'rechazado', N'archivado'
        ))
    );

    CREATE INDEX IX_RegistrosRevisionHistorial_ModuloRegistro
        ON dbo.RegistrosRevisionHistorial (ModuloId, RegistroId, Fecha DESC);

    CREATE INDEX IX_RegistrosRevisionHistorial_EstadoFecha
        ON dbo.RegistrosRevisionHistorial (EstadoNuevo, Fecha DESC);
END;

IF OBJECT_ID(N'dbo.TextosSitio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TextosSitio (
        IdTextoSitio int IDENTITY(1,1) NOT NULL,
        Clave nvarchar(160) NOT NULL,
        Pagina nvarchar(80) NOT NULL,
        Seccion nvarchar(120) NOT NULL,
        Bloque nvarchar(120) NOT NULL,
        TituloInterno nvarchar(240) NOT NULL,
        TituloVisible nvarchar(500) NULL,
        Subtitulo nvarchar(800) NULL,
        Cuerpo nvarchar(max) NULL,
        TextoBoton nvarchar(180) NULL,
        UrlBoton nvarchar(500) NULL,
        TextoAlternativo nvarchar(500) NULL,
        TipoContenido nvarchar(80) NOT NULL CONSTRAINT DF_TextosSitio_TipoContenido DEFAULT (N'texto'),
        LimiteCaracteresTitulo int NULL,
        LimiteCaracteresSubtitulo int NULL,
        LimiteCaracteresCuerpo int NULL,
        Estado nvarchar(80) NOT NULL CONSTRAINT DF_TextosSitio_Estado DEFAULT (N'borrador'),
        Version int NOT NULL CONSTRAINT DF_TextosSitio_Version DEFAULT (1),
        Publicado bit NOT NULL CONSTRAINT DF_TextosSitio_Publicado DEFAULT (0),
        CreadoPorUsuarioId int NULL,
        ActualizadoPorUsuarioId int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_TextosSitio_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,
        CONSTRAINT PK_TextosSitio PRIMARY KEY (IdTextoSitio),
        CONSTRAINT UQ_TextosSitio_Clave UNIQUE (Clave),
        CONSTRAINT CK_TextosSitio_Estado CHECK (Estado IN (
            N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'rechazado', N'archivado'
        ))
    );

    CREATE INDEX IX_TextosSitio_PaginaSeccion
        ON dbo.TextosSitio (Pagina, Seccion, Bloque);
END;

IF OBJECT_ID(N'dbo.TextosSitioHistorial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TextosSitioHistorial (
        IdTextoSitioHistorial bigint IDENTITY(1,1) NOT NULL,
        TextoSitioId int NOT NULL,
        Version int NOT NULL,
        ContenidoAnteriorJson nvarchar(max) NULL,
        ContenidoNuevoJson nvarchar(max) NOT NULL,
        Accion nvarchar(80) NOT NULL,
        Comentario nvarchar(1200) NULL,
        UsuarioId int NULL,
        Fecha datetime2(0) NOT NULL CONSTRAINT DF_TextosSitioHistorial_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TextosSitioHistorial PRIMARY KEY (IdTextoSitioHistorial),
        CONSTRAINT FK_TextosSitioHistorial_TextosSitio FOREIGN KEY (TextoSitioId) REFERENCES dbo.TextosSitio (IdTextoSitio)
    );
END;

/*
    RETIRADO EL 22 DE AGOSTO DE 2026: dbo.EntidadesColaboradoras y
    dbo.UsuariosEntidadesColaboradoras.

    Aqui nacian las dos tablas del modelo de entidades aliadas. El fichero que lleva
    "aliados" en el nombre —V20260525_02— no las creaba: solo las RENOMBRABA con sp_rename,
    y sus propios CREATE TABLE, guardados por IF OBJECT_ID(...) IS NULL, nunca llegaban a
    ejecutarse sobre una base limpia. Por eso la retirada empieza aqui y no alli: quien
    borrara solo el DDL de _02 veria las tablas reaparecer en cada base nueva, con el nombre
    antiguo y sin que ningun guion se quejara.

    Que se conserva: NADA de esta pareja. La relacion persona-entidad del modelo definitivo
    es dbo.UsuariosEntidades, creada en V20260521_01__entidades_administrativas.sql, que se
    le parece mucho —tabla puente con columna de rol y bandera Activo— y NO tiene nada que
    ver con esto.

    Las bases que ya existen las suelta V20260823_01__retirada_aliados.sql.
*/

IF OBJECT_ID(N'dbo.RecursosEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecursosEditoriales (
        IdRecursoEditorial int IDENTITY(1,1) NOT NULL,
        Titulo nvarchar(500) NOT NULL,
        Subtitulo nvarchar(500) NULL,
        Resumen nvarchar(max) NULL,
        Descripcion nvarchar(max) NULL,
        Autores nvarchar(max) NULL,
        Editores nvarchar(max) NULL,
        EntidadResponsable nvarchar(240) NULL,
        Anio int NULL,
        FechaPublicacion date NULL,
        TipoRecurso nvarchar(120) NULL,
        Coleccion nvarchar(180) NULL,
        Categoria nvarchar(180) NULL,
        ISBN nvarchar(80) NULL,
        ISMN nvarchar(80) NULL,
        ISSN nvarchar(80) NULL,
        DOI nvarchar(180) NULL,
        PalabrasClave nvarchar(max) NULL,
        PracticasMusicales nvarchar(max) NULL,
        TerritoriosSonoros nvarchar(max) NULL,
        Departamento nvarchar(120) NULL,
        Municipio nvarchar(120) NULL,
        CoberturaTerritorial nvarchar(80) NULL,
        UrlExterna nvarchar(500) NULL,
        Idioma nvarchar(80) NULL,
        NumeroPaginas int NULL,
        DerechosLicencia nvarchar(240) NULL,
        Estado nvarchar(80) NOT NULL CONSTRAINT DF_RecursosEditoriales_Estado DEFAULT (N'borrador'),
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_RecursosEditoriales_Orden DEFAULT (0),
        CreadoPorUsuarioId int NULL,
        ActualizadoPorUsuarioId int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RecursosEditoriales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_RecursosEditoriales PRIMARY KEY (IdRecursoEditorial),
        CONSTRAINT CK_RecursosEditoriales_Estado CHECK (Estado IN (
            N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'rechazado', N'archivado'
        ))
    );

    CREATE INDEX IX_RecursosEditoriales_EstadoFecha
        ON dbo.RecursosEditoriales (Estado, FechaActualizacion DESC);
END;

IF OBJECT_ID(N'dbo.RecursosEditorialesArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecursosEditorialesArchivos (
        IdRecursoEditorialArchivo int IDENTITY(1,1) NOT NULL,
        RecursoEditorialId int NOT NULL,
        TipoArchivo nvarchar(80) NOT NULL,
        ArchivoId int NULL,
        Url nvarchar(500) NULL,
        MetadataJson nvarchar(max) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RecursosEditorialesArchivos_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RecursosEditorialesArchivos PRIMARY KEY (IdRecursoEditorialArchivo),
        CONSTRAINT FK_RecursosEditorialesArchivos_Recurso FOREIGN KEY (RecursoEditorialId) REFERENCES dbo.RecursosEditoriales (IdRecursoEditorial)
    );
END;

IF OBJECT_ID(N'dbo.ExtraccionesAsistidas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExtraccionesAsistidas (
        IdExtraccionAsistida bigint IDENTITY(1,1) NOT NULL,
        ModuloId nvarchar(80) NOT NULL,
        NombreArchivo nvarchar(260) NOT NULL,
        TipoMime nvarchar(120) NULL,
        PesoBytes bigint NULL,
        Estado nvarchar(80) NOT NULL CONSTRAINT DF_ExtraccionesAsistidas_Estado DEFAULT (N'pendiente'),
        ResultadoJson nvarchar(max) NULL,
        ErrorCodigo nvarchar(80) NULL,
        ErrorMensaje nvarchar(800) NULL,
        UsuarioId int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ExtraccionesAsistidas_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ExtraccionesAsistidas PRIMARY KEY (IdExtraccionAsistida)
    );
END;
