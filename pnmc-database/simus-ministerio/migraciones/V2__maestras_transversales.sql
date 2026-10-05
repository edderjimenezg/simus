/*
  Tablas maestras que comparten todos los módulos: estados, tipos de documento, DIVIPOLA,
  usuarios, organizaciones, archivos, procedencia de los registros y la correspondencia con los
  identificadores heredados de SIMUS.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.EstadosContenido (
    IdEstadoContenido int IDENTITY(1,1) NOT NULL,
    CodigoEstado nvarchar(80) NOT NULL,
    NombreEstado nvarchar(120) NOT NULL,
    DescripcionEstado nvarchar(500) NULL,
    CONSTRAINT PK_EstadosContenido PRIMARY KEY (IdEstadoContenido),
    CONSTRAINT UQ_EstadosContenido_CodigoEstado UNIQUE (CodigoEstado),
    CONSTRAINT UQ_EstadosContenido_NombreEstado UNIQUE (NombreEstado),
    CONSTRAINT CK_EstadosContenido_CodigoEstado_Formato
        CHECK (CodigoEstado = LOWER(CodigoEstado) AND CodigoEstado NOT LIKE '% %')
);

CREATE TABLE dbo.TiposDocumento (
    CodigoTipoDocumento nvarchar(20) NOT NULL,
    NombreTipoDocumento nvarchar(120) NOT NULL,
    OrdenVisualizacion int NOT NULL,
    Activo bit NOT NULL CONSTRAINT DF_TiposDocumento_Activo DEFAULT (1),
    CONSTRAINT PK_TiposDocumento PRIMARY KEY (CodigoTipoDocumento),
    CONSTRAINT UQ_TiposDocumento_Nombre UNIQUE (NombreTipoDocumento),
    CONSTRAINT CK_TiposDocumento_Orden CHECK (OrdenVisualizacion > 0),
    CONSTRAINT CK_TiposDocumento_CodigoSinRelleno
        CHECK (DATALENGTH(CodigoTipoDocumento) = DATALENGTH(LTRIM(RTRIM(CodigoTipoDocumento)))
               AND DATALENGTH(CodigoTipoDocumento) > 0),
    CONSTRAINT CK_TiposDocumento_CodigoEnMinuscula
        CHECK (CodigoTipoDocumento COLLATE Latin1_General_BIN2 = LOWER(CodigoTipoDocumento) COLLATE Latin1_General_BIN2)
);

-- El departamento se deduce de sus municipios: no hay tabla de departamentos.
CREATE TABLE dbo.Divipola (
    CodigoDepartamento char(2) NOT NULL,
    NombreDepartamento nvarchar(120) NOT NULL,
    CodigoMunicipio char(5) NOT NULL,
    NombreMunicipio nvarchar(160) NOT NULL,
    TipoTerritorio nvarchar(80) NULL,
    Latitud decimal(9,6) NULL,
    Longitud decimal(9,6) NULL,
    CONSTRAINT PK_Divipola PRIMARY KEY (CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT UQ_Divipola_CodigoMunicipio UNIQUE (CodigoMunicipio),
    CONSTRAINT CK_Divipola_CodigoDepartamento_Formato CHECK (CodigoDepartamento NOT LIKE '%[^0-9]%'),
    CONSTRAINT CK_Divipola_CodigoMunicipio_Formato CHECK (CodigoMunicipio NOT LIKE '%[^0-9]%'),
    CONSTRAINT CK_Divipola_CodigoMunicipio_Departamento CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento)
);
CREATE INDEX IX_Divipola_Departamento_NombreMunicipio ON dbo.Divipola (CodigoDepartamento, NombreMunicipio);
CREATE INDEX IX_Divipola_NombreDepartamento ON dbo.Divipola (NombreDepartamento);

CREATE TABLE dbo.CatalogosReferenciaFuente (
    CodigoCatalogo nvarchar(80) NOT NULL,
    VersionFuente nvarchar(80) NOT NULL,
    UrlOrigen nvarchar(1000) NOT NULL,
    FechaIncorporacion datetime2(0) NOT NULL,
    CantidadDepartamentos int NULL,
    CantidadMunicipios int NULL,
    CONSTRAINT PK_CatalogosReferenciaFuente PRIMARY KEY (CodigoCatalogo)
);

CREATE TABLE dbo.Usuarios (
    IdUsuario int IDENTITY(1,1) NOT NULL,
    NombreCompleto nvarchar(180) NOT NULL,
    CorreoElectronico nvarchar(180) NOT NULL,
    HashContrasena nvarchar(500) NOT NULL,
    CanalAcceso nvarchar(40) NOT NULL CONSTRAINT DF_Usuarios_CanalAcceso DEFAULT (N'interno'),
    TipoPerfil nvarchar(80) NULL,
    Activo bit NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT (1),
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    UltimoAcceso datetime2(0) NULL,
    Telefono nvarchar(80) NULL,
    Identificacion nvarchar(60) NULL,
    CodigoTipoDocumento nvarchar(20) NULL,
    CorreoConfirmado bit NOT NULL CONSTRAINT DF_Usuarios_CorreoConfirmado DEFAULT (0),
    FechaConfirmacionCorreo datetime2(0) NULL,
    PrimerNombre nvarchar(80) NULL,
    SegundoNombre nvarchar(80) NULL,
    PrimerApellido nvarchar(80) NULL,
    SegundoApellido nvarchar(80) NULL,
    DebeCambiarContrasena bit NOT NULL CONSTRAINT DF_Usuarios_DebeCambiarContrasena DEFAULT (0),
    PerfilCompletado bit NOT NULL CONSTRAINT DF_Usuarios_PerfilCompletado DEFAULT (1),
    CONSTRAINT PK_Usuarios PRIMARY KEY (IdUsuario),
    CONSTRAINT UQ_Usuarios_CorreoElectronico UNIQUE (CorreoElectronico),
    CONSTRAINT FK_Usuarios_TiposDocumento FOREIGN KEY (CodigoTipoDocumento) REFERENCES dbo.TiposDocumento (CodigoTipoDocumento),
    CONSTRAINT CK_Usuarios_Documento_Completo
        CHECK ((Identificacion IS NULL AND CodigoTipoDocumento IS NULL) OR (Identificacion IS NOT NULL AND CodigoTipoDocumento IS NOT NULL)),
    CONSTRAINT CK_Usuarios_Identificacion_Formato
        CHECK (Identificacion IS NULL OR (LEN(Identificacion) >= 3 AND Identificacion NOT LIKE '%[^0-9A-Za-z-]%')),
    CONSTRAINT CK_Usuarios_CorreoElectronico_Formato CHECK (CorreoElectronico LIKE '%_@_%._%')
);
CREATE UNIQUE INDEX UQ_Usuarios_Documento ON dbo.Usuarios (CodigoTipoDocumento, Identificacion) WHERE Identificacion IS NOT NULL;

CREATE TABLE dbo.Archivos (
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
    Contenido varbinary(max) NULL,
    Huella char(64) NULL,
    Ancho int NULL,
    Alto int NULL,
    OrganizacionId int NULL,
    CONSTRAINT PK_Archivos PRIMARY KEY (IdArchivo),
    CONSTRAINT UQ_Archivos_RutaAlmacenamiento UNIQUE (RutaAlmacenamiento),
    CONSTRAINT FK_Archivos_Usuarios FOREIGN KEY (IdUsuarioCarga) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT CK_Archivos_PesoBytes CHECK (PesoBytes IS NULL OR PesoBytes >= 0),
    CONSTRAINT CK_Archivos_PesoPorTipo
        CHECK (PesoBytes <= CASE WHEN TipoMime = N'application/pdf' THEN 20971520 ELSE 2097152 END)
);
CREATE INDEX IX_Archivos_IdUsuarioCarga ON dbo.Archivos (IdUsuarioCarga);
CREATE INDEX IX_Archivos_TipoMime ON dbo.Archivos (TipoMime);
CREATE INDEX IX_Archivos_Huella ON dbo.Archivos (Huella);
CREATE INDEX IX_Archivos_Organizacion ON dbo.Archivos (OrganizacionId) WHERE OrganizacionId IS NOT NULL;

CREATE TABLE dbo.Entidades (
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
    Direccion nvarchar(300) NULL,
    EstadoRegistro nvarchar(80) NOT NULL CONSTRAINT DF_Entidades_EstadoRegistro DEFAULT (N'pendiente_de_confirmacion'),
    Activo bit NOT NULL CONSTRAINT DF_Entidades_Activo DEFAULT (1),
    IdUsuarioCreador int NOT NULL,
    IdUsuarioResponsable int NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Entidades_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    FechaRevision datetime2(0) NULL,
    FechaAprobacion datetime2(0) NULL,
    FechaPublicacion datetime2(0) NULL,
    NumeroIdentificacion nvarchar(60) NULL,
    TipoIdentificacion nvarchar(40) NULL,
    EsInstitucional bit NOT NULL CONSTRAINT DF_Entidades_EsInstitucional DEFAULT (0),
    CodigoDepartamentoSede char(2) NULL,
    CodigoMunicipioSede char(5) NULL,
    ArchivoFotoId int NULL,
    CONSTRAINT PK_Entidades PRIMARY KEY (IdEntidad),
    CONSTRAINT FK_Entidades_EstadosContenido FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado),
    CONSTRAINT FK_Entidades_UsuarioCreador FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_Entidades_UsuarioResponsable FOREIGN KEY (IdUsuarioResponsable) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_Entidades_ArchivoFoto FOREIGN KEY (ArchivoFotoId) REFERENCES dbo.Archivos (IdArchivo),
    CONSTRAINT FK_Entidades_Sede_Divipola FOREIGN KEY (CodigoDepartamentoSede, CodigoMunicipioSede)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT CK_Entidades_Tipo CHECK (TipoEntidad = N'organizacion'),
    CONSTRAINT CK_Entidades_EstadoRegistro
        CHECK (EstadoRegistro IN (N'pendiente_de_confirmacion', N'activa', N'inactiva', N'eliminada')),
    CONSTRAINT CK_Entidades_VigenciaCoherente
        CHECK ((EstadoRegistro IN (N'pendiente_de_confirmacion', N'activa') AND Activo = 1)
            OR (EstadoRegistro IN (N'inactiva', N'eliminada') AND Activo = 0)),
    CONSTRAINT CK_Entidades_Sede_Completa
        CHECK ((CodigoDepartamentoSede IS NULL AND CodigoMunicipioSede IS NULL)
            OR (CodigoDepartamentoSede IS NOT NULL AND CodigoMunicipioSede IS NOT NULL))
);
CREATE INDEX IX_Entidades_Tipo_Estado ON dbo.Entidades (TipoEntidad, EstadoRegistro, Activo);
CREATE UNIQUE INDEX UQ_Entidades_NumeroIdentificacion ON dbo.Entidades (NumeroIdentificacion) WHERE NumeroIdentificacion IS NOT NULL;
CREATE UNIQUE INDEX UX_Entidades_CorreoContacto ON dbo.Entidades (CorreoContacto) WHERE CorreoContacto IS NOT NULL;
-- Una sola entidad institucional: la que responde por lo que ninguna organización ha reclamado.
CREATE UNIQUE INDEX UQ_Entidades_EsInstitucional ON dbo.Entidades (EsInstitucional) WHERE EsInstitucional = 1;

ALTER TABLE dbo.Archivos ADD CONSTRAINT FK_Archivos_Organizacion
    FOREIGN KEY (OrganizacionId) REFERENCES dbo.Entidades (IdEntidad);

CREATE TABLE dbo.EntidadesResponsable (
    IdEntidad int NOT NULL,
    ResponsableNombre nvarchar(240) NOT NULL,
    ResponsableTipoDocumento nvarchar(40) NOT NULL,
    ResponsableNumeroDocumento nvarchar(40) NOT NULL,
    ResponsableCorreo nvarchar(320) NOT NULL,
    ResponsableTelefono nvarchar(80) NULL,
    ResponsableDesde datetime2(0) NOT NULL CONSTRAINT DF_EntidadesResponsable_ResponsableDesde DEFAULT (SYSUTCDATETIME()),
    ResponsableAutorizacionDatos bit NOT NULL CONSTRAINT DF_EntidadesResponsable_ResponsableAutorizacionDatos DEFAULT (0),
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EntidadesResponsable_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    ResponsablePrimerNombre nvarchar(80) NULL,
    ResponsableSegundoNombre nvarchar(80) NULL,
    ResponsablePrimerApellido nvarchar(80) NULL,
    ResponsableSegundoApellido nvarchar(80) NULL,
    CONSTRAINT PK_EntidadesResponsable PRIMARY KEY (IdEntidad),
    CONSTRAINT FK_EntidadesResponsable_Entidades FOREIGN KEY (IdEntidad) REFERENCES dbo.Entidades (IdEntidad)
);
CREATE INDEX IX_EntidadesResponsable_Correo ON dbo.EntidadesResponsable (ResponsableCorreo);

CREATE TABLE dbo.UsuariosEntidades (
    IdUsuarioEntidad int IDENTITY(1,1) NOT NULL,
    IdUsuario int NOT NULL,
    IdEntidad int NOT NULL,
    RolEntidad nvarchar(80) NOT NULL,
    Activo bit NOT NULL CONSTRAINT DF_UsuariosEntidades_Activo DEFAULT (1),
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_UsuariosEntidades_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_UsuariosEntidades PRIMARY KEY (IdUsuarioEntidad),
    CONSTRAINT UQ_UsuariosEntidades UNIQUE (IdUsuario, IdEntidad, RolEntidad),
    CONSTRAINT FK_UsuariosEntidades_Usuarios FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_UsuariosEntidades_Entidades FOREIGN KEY (IdEntidad) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT CK_UsuariosEntidades_Rol
        CHECK (RolEntidad IN (N'propietario', N'administrador', N'editor', N'cargador', N'lector'))
);

-- Procedencia: desde qué contexto y por qué organización y usuario entró cada registro.
CREATE TABLE dbo.ProcedenciasDeRegistro (
    IdProcedencia bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    ContextoOrigen nvarchar(20) NOT NULL,
    OrganizacionProcedenciaId int NULL,
    UsuarioCreadorId int NULL,
    FechaRegistro datetime2(0) NOT NULL CONSTRAINT DF_ProcedenciasDeRegistro_FechaRegistro DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ProcedenciasDeRegistro PRIMARY KEY (IdProcedencia),
    CONSTRAINT UQ_ProcedenciasDeRegistro UNIQUE (ModuloId, RegistroId),
    CONSTRAINT FK_ProcedenciasDeRegistro_Organizacion FOREIGN KEY (OrganizacionProcedenciaId) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_ProcedenciasDeRegistro_Usuario FOREIGN KEY (UsuarioCreadorId) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT CK_ProcedenciasDeRegistro_Contexto
        CHECK (ContextoOrigen IN (N'administrativo', N'externo', N'importacion', N'siembra', N'historico', N'prueba'))
);
CREATE INDEX IX_ProcedenciasDeRegistro_Organizacion ON dbo.ProcedenciasDeRegistro (OrganizacionProcedenciaId, ModuloId);
CREATE INDEX IX_ProcedenciasDeRegistro_Usuario ON dbo.ProcedenciasDeRegistro (UsuarioCreadorId, FechaRegistro DESC);

-- Equivalencia entre un identificador de las tablas ART_MUS_* y el registro que lo sustituye.
CREATE TABLE dbo.CorrespondenciasHeredadas (
    IdCorrespondencia int IDENTITY(1,1) NOT NULL,
    TablaHeredada sysname NOT NULL,
    IdHeredado nvarchar(64) NOT NULL,
    TablaDestino sysname NOT NULL,
    IdDestino nvarchar(120) NOT NULL,
    FechaRegistro datetime2(0) NOT NULL CONSTRAINT DF_CorrespondenciasHeredadas_FechaRegistro DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_CorrespondenciasHeredadas PRIMARY KEY (IdCorrespondencia),
    CONSTRAINT UQ_CorrespondenciasHeredadas UNIQUE (TablaHeredada, IdHeredado, TablaDestino)
);
CREATE INDEX IX_CorrespondenciasHeredadas_Destino ON dbo.CorrespondenciasHeredadas (TablaDestino, IdDestino);
GO
