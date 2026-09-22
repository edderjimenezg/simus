/*
    PNMC - Vinculacion progresiva, duplicados y calidad de datos.
    Estructura incremental no destructiva para registros sin responsable,
    solicitudes de vinculacion, candidatos duplicados y banderas de calidad.
*/

IF OBJECT_ID(N'dbo.SolicitudesVinculacionRegistros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SolicitudesVinculacionRegistros (
        IdSolicitudVinculacionRegistro bigint IDENTITY(1,1) NOT NULL,
        ModuloId nvarchar(80) NOT NULL,
        RegistroId nvarchar(120) NOT NULL,
        UsuarioSolicitanteId int NOT NULL,
        /* Antes EntidadAliadaId, con FK contra dbo.EntidadesAliadas. Al retirarse el
           concepto (22 ago 2026) el ancla pasa a ser la entidad del ecosistema —agrupacion,
           escuela de musica, agente, escenario—, alcanzada por dbo.UsuariosEntidades. Es
           NULL cuando quien pide es una persona registrada que aun no representa a nadie:
           ese es el caso normal de entrada, no un error. */
        EntidadId int NULL,
        AlcanceSolicitado nvarchar(40) NOT NULL CONSTRAINT DF_SolicitudesVinculacionRegistros_Alcance DEFAULT (N'responsable'),
        Justificacion nvarchar(1200) NOT NULL,
        EvidenciaTexto nvarchar(2000) NULL,
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_SolicitudesVinculacionRegistros_Estado DEFAULT (N'pendiente'),
        UsuarioRevisorId int NULL,
        ComentarioRevision nvarchar(1200) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_SolicitudesVinculacionRegistros_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_SolicitudesVinculacionRegistros_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SolicitudesVinculacionRegistros PRIMARY KEY (IdSolicitudVinculacionRegistro),
        CONSTRAINT FK_SolicitudesVinculacionRegistros_Usuario FOREIGN KEY (UsuarioSolicitanteId) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_SolicitudesVinculacionRegistros_Entidad FOREIGN KEY (EntidadId) REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT CK_SolicitudesVinculacionRegistros_Estado CHECK (Estado IN (N'pendiente', N'en_revision', N'ajustes_solicitados', N'aprobada', N'rechazada', N'cancelada')),
        CONSTRAINT CK_SolicitudesVinculacionRegistros_Alcance CHECK (AlcanceSolicitado IN (N'responsable', N'editor'))
    );

    CREATE INDEX IX_SolicitudesVinculacionRegistros_Estado ON dbo.SolicitudesVinculacionRegistros (Estado, FechaActualizacion DESC);
    CREATE INDEX IX_SolicitudesVinculacionRegistros_Registro ON dbo.SolicitudesVinculacionRegistros (ModuloId, RegistroId);
    CREATE INDEX IX_SolicitudesVinculacionRegistros_Usuario ON dbo.SolicitudesVinculacionRegistros (UsuarioSolicitanteId, FechaCreacion DESC);
END;

IF OBJECT_ID(N'dbo.RegistrosDuplicadosCandidatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosDuplicadosCandidatos (
        IdDuplicadoCandidato bigint IDENTITY(1,1) NOT NULL,
        ModuloId nvarchar(80) NOT NULL,
        RegistroOrigenId nvarchar(120) NOT NULL,
        RegistroCandidatoId nvarchar(120) NOT NULL,
        NivelCoincidencia nvarchar(20) NOT NULL,
        PuntajeCoincidencia decimal(5,2) NULL,
        EvidenciaJson nvarchar(max) NOT NULL CONSTRAINT DF_RegistrosDuplicadosCandidatos_Evidencia DEFAULT (N'{}'),
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_RegistrosDuplicadosCandidatos_Estado DEFAULT (N'pendiente'),
        Decision nvarchar(40) NULL,
        ComentarioDecision nvarchar(1200) NULL,
        UsuarioRevisorId int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RegistrosDuplicadosCandidatos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_RegistrosDuplicadosCandidatos_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RegistrosDuplicadosCandidatos PRIMARY KEY (IdDuplicadoCandidato),
        CONSTRAINT CK_RegistrosDuplicadosCandidatos_Nivel CHECK (NivelCoincidencia IN (N'alta', N'media', N'baja')),
        CONSTRAINT CK_RegistrosDuplicadosCandidatos_Estado CHECK (Estado IN (N'pendiente', N'resuelto')),
        CONSTRAINT CK_RegistrosDuplicadosCandidatos_Decision CHECK (Decision IS NULL OR Decision IN (N'fusionar', N'mantener_separados', N'no_duplicado', N'pendiente'))
    );

    CREATE INDEX IX_RegistrosDuplicadosCandidatos_Estado ON dbo.RegistrosDuplicadosCandidatos (Estado, FechaActualizacion DESC);
    CREATE INDEX IX_RegistrosDuplicadosCandidatos_Registro ON dbo.RegistrosDuplicadosCandidatos (ModuloId, RegistroOrigenId, RegistroCandidatoId);
END;

IF OBJECT_ID(N'dbo.RegistrosCalidadDatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosCalidadDatos (
        IdRegistroCalidadDatos bigint IDENTITY(1,1) NOT NULL,
        ModuloId nvarchar(80) NOT NULL,
        RegistroId nvarchar(120) NOT NULL,
        TipoBandera nvarchar(80) NOT NULL,
        Severidad nvarchar(20) NOT NULL CONSTRAINT DF_RegistrosCalidadDatos_Severidad DEFAULT (N'media'),
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_RegistrosCalidadDatos_Estado DEFAULT (N'abierta'),
        Detalle nvarchar(1200) NULL,
        CreadoPorUsuarioId int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RegistrosCalidadDatos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_RegistrosCalidadDatos_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_RegistrosCalidadDatos PRIMARY KEY (IdRegistroCalidadDatos),
        CONSTRAINT CK_RegistrosCalidadDatos_Severidad CHECK (Severidad IN (N'baja', N'media', N'alta')),
        CONSTRAINT CK_RegistrosCalidadDatos_Estado CHECK (Estado IN (N'abierta', N'en_revision', N'resuelta', N'descartada'))
    );

    CREATE INDEX IX_RegistrosCalidadDatos_Estado ON dbo.RegistrosCalidadDatos (Estado, FechaActualizacion DESC);
    CREATE INDEX IX_RegistrosCalidadDatos_Registro ON dbo.RegistrosCalidadDatos (ModuloId, RegistroId);
END;
