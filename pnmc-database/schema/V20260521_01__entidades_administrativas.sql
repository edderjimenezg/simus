/*
    PNMC - Entidades administrativas

    Capa comun para perfiles administrables, usuarios responsables,
    relaciones entre entidades y vinculos hacia tablas fuente existentes.
*/

IF OBJECT_ID(N'dbo.Entidades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Entidades
    (
        IdEntidad int IDENTITY(1,1) NOT NULL,
        TipoEntidad nvarchar(80) NOT NULL,
        Nombre nvarchar(240) NOT NULL,
        NombreLegal nvarchar(240) NULL,
        Descripcion nvarchar(max) NULL,
        CorreoContacto nvarchar(180) NULL,
        TelefonoContacto nvarchar(80) NULL,
        SitioWeb nvarchar(500) NULL,
        Facebook nvarchar(500) NULL,
        Instagram nvarchar(500) NULL,
        OtroEnlace nvarchar(500) NULL,
        NivelCobertura nvarchar(40) NOT NULL CONSTRAINT DF_Entidades_NivelCobertura DEFAULT (N'municipal'),
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Direccion nvarchar(300) NULL,
        Latitud decimal(9,6) NULL,
        Longitud decimal(9,6) NULL,
        EstadoRegistro nvarchar(80) NOT NULL CONSTRAINT DF_Entidades_EstadoRegistro DEFAULT (N'borrador'),
        Activo bit NOT NULL CONSTRAINT DF_Entidades_Activo DEFAULT (1),
        IdUsuarioCreador int NOT NULL,
        IdUsuarioResponsable int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Entidades_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaRevision datetime2(0) NULL,
        FechaAprobacion datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,
        CONSTRAINT PK_Entidades PRIMARY KEY (IdEntidad),
        CONSTRAINT FK_Entidades_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_Entidades_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT FK_Entidades_UsuarioCreador FOREIGN KEY (IdUsuarioCreador)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_Entidades_UsuarioResponsable FOREIGN KEY (IdUsuarioResponsable)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_Entidades_Tipo CHECK (TipoEntidad IN (
            N'organizacion', N'escuela_musica', N'lutier', N'festival',
            N'mercado_musical', N'espacio', N'colectivo', N'individuo'
        )),
        CONSTRAINT CK_Entidades_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Entidades_Tipo_Estado' AND object_id = OBJECT_ID(N'dbo.Entidades'))
BEGIN
    CREATE INDEX IX_Entidades_Tipo_Estado ON dbo.Entidades (TipoEntidad, EstadoRegistro, Activo);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Entidades_Territorio' AND object_id = OBJECT_ID(N'dbo.Entidades'))
BEGIN
    CREATE INDEX IX_Entidades_Territorio ON dbo.Entidades (CodigoDepartamento, CodigoMunicipio);
END;

IF OBJECT_ID(N'dbo.UsuariosEntidades', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuariosEntidades
    (
        IdUsuarioEntidad int IDENTITY(1,1) NOT NULL,
        IdUsuario int NOT NULL,
        IdEntidad int NOT NULL,
        RolEntidad nvarchar(80) NOT NULL,
        Activo bit NOT NULL CONSTRAINT DF_UsuariosEntidades_Activo DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_UsuariosEntidades_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_UsuariosEntidades PRIMARY KEY (IdUsuarioEntidad),
        CONSTRAINT FK_UsuariosEntidades_Usuarios FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_UsuariosEntidades_Entidades FOREIGN KEY (IdEntidad)
            REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT UQ_UsuariosEntidades UNIQUE (IdUsuario, IdEntidad, RolEntidad),
        CONSTRAINT CK_UsuariosEntidades_Rol CHECK (RolEntidad IN (
            N'propietario', N'administrador', N'editor', N'cargador', N'lector'
        ))
    );
END;

IF OBJECT_ID(N'dbo.EntidadesRelaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntidadesRelaciones
    (
        IdEntidadRelacion int IDENTITY(1,1) NOT NULL,
        IdEntidadOrigen int NOT NULL,
        IdEntidadDestino int NOT NULL,
        TipoRelacion nvarchar(80) NOT NULL,
        Notas nvarchar(800) NULL,
        Activo bit NOT NULL CONSTRAINT DF_EntidadesRelaciones_Activo DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EntidadesRelaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EntidadesRelaciones PRIMARY KEY (IdEntidadRelacion),
        CONSTRAINT FK_EntidadesRelaciones_Origen FOREIGN KEY (IdEntidadOrigen)
            REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_EntidadesRelaciones_Destino FOREIGN KEY (IdEntidadDestino)
            REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT UQ_EntidadesRelaciones UNIQUE (IdEntidadOrigen, IdEntidadDestino, TipoRelacion),
        CONSTRAINT CK_EntidadesRelaciones_Distintas CHECK (IdEntidadOrigen <> IdEntidadDestino),
        CONSTRAINT CK_EntidadesRelaciones_Tipo CHECK (TipoRelacion IN (
            N'administra', N'depende_de', N'organiza', N'aliada', N'pertenece_a', N'representa'
        ))
    );
END;

IF OBJECT_ID(N'dbo.EntidadesRegistrosFuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntidadesRegistrosFuente
    (
        IdEntidadRegistroFuente int IDENTITY(1,1) NOT NULL,
        IdEntidad int NOT NULL,
        TablaFuente nvarchar(120) NOT NULL,
        IdRegistroFuente int NOT NULL,
        IdRegistroEcosistema int NULL,
        EsPrincipal bit NOT NULL CONSTRAINT DF_EntidadesRegistrosFuente_EsPrincipal DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EntidadesRegistrosFuente_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EntidadesRegistrosFuente PRIMARY KEY (IdEntidadRegistroFuente),
        CONSTRAINT FK_EntidadesRegistrosFuente_Entidades FOREIGN KEY (IdEntidad)
            REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_EntidadesRegistrosFuente_RegistrosEcosistema FOREIGN KEY (IdRegistroEcosistema)
            REFERENCES dbo.RegistrosEcosistema (IdRegistroEcosistema),
        CONSTRAINT UQ_EntidadesRegistrosFuente_Tabla_Id UNIQUE (TablaFuente, IdRegistroFuente),
        CONSTRAINT CK_EntidadesRegistrosFuente_Tabla CHECK (TablaFuente IN (
            N'Festivales', N'EscuelasMusica', N'MercadosMusicales',
            N'RedesDocumentacion', N'Lutieres'
        ))
    );
END;

IF OBJECT_ID(N'dbo.EntidadesHistorialRevision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntidadesHistorialRevision
    (
        IdHistorialRevision int IDENTITY(1,1) NOT NULL,
        IdEntidad int NOT NULL,
        IdUsuario int NOT NULL,
        Accion nvarchar(80) NOT NULL,
        Comentario nvarchar(1200) NULL,
        FechaAccion datetime2(0) NOT NULL CONSTRAINT DF_EntidadesHistorialRevision_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EntidadesHistorialRevision PRIMARY KEY (IdHistorialRevision),
        CONSTRAINT FK_EntidadesHistorialRevision_Entidades FOREIGN KEY (IdEntidad)
            REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_EntidadesHistorialRevision_Usuarios FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_EntidadesHistorialRevision_Accion CHECK (Accion IN (
            N'crear', N'actualizar', N'enviar_revision', N'aprobar',
            N'rechazar', N'publicar', N'archivar', N'comentar'
        ))
    );
END;
