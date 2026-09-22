/* PNMC · Reclamaciones administrativas reutilizables · Festivales (primer dominio) */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
IF COL_LENGTH(N'dbo.Entidades', N'EsInstitucional') IS NULL
BEGIN
 ALTER TABLE dbo.Entidades ADD EsInstitucional bit NOT NULL CONSTRAINT DF_Entidades_EsInstitucional DEFAULT(0) WITH VALUES;
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Entidades_EsInstitucional' AND object_id=OBJECT_ID(N'dbo.Entidades'))
EXEC(N'CREATE UNIQUE INDEX UX_Entidades_EsInstitucional ON dbo.Entidades(EsInstitucional) WHERE EsInstitucional=1;');
IF OBJECT_ID(N'dbo.BorradoresProceso', N'U') IS NULL
CREATE TABLE dbo.BorradoresProceso (
 IdBorradorProceso bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
 Dominio nvarchar(80) NOT NULL, OrganizacionId int NOT NULL, PersonaId int NOT NULL,
 Estado nvarchar(40) NOT NULL CONSTRAINT DF_BorradoresProceso_Estado DEFAULT N'borrador',
 DatosJson nvarchar(max) NOT NULL CONSTRAINT DF_BorradoresProceso_Datos DEFAULT N'{}',
 Version int NOT NULL CONSTRAINT DF_BorradoresProceso_Version DEFAULT 1,
 FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_BorradoresProceso_Creacion DEFAULT (SYSUTCDATETIME()),
 FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_BorradoresProceso_Actualizacion DEFAULT (SYSUTCDATETIME()),
 CONSTRAINT FK_BorradoresProceso_Organizacion FOREIGN KEY (OrganizacionId) REFERENCES dbo.Entidades(IdEntidad),
 CONSTRAINT FK_BorradoresProceso_Persona FOREIGN KEY (PersonaId) REFERENCES dbo.Usuarios(IdUsuario),
 CONSTRAINT CK_BorradoresProceso_Estado CHECK (Estado IN (N'borrador',N'cerrado',N'cancelado'))
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_BorradoresProceso_Abierto' AND object_id=OBJECT_ID(N'dbo.BorradoresProceso'))
CREATE UNIQUE INDEX UX_BorradoresProceso_Abierto ON dbo.BorradoresProceso(Dominio,OrganizacionId,PersonaId) WHERE Estado=N'borrador';

IF OBJECT_ID(N'dbo.ReclamacionesAdministracion', N'U') IS NULL
CREATE TABLE dbo.ReclamacionesAdministracion (
 IdReclamacion bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
 Dominio nvarchar(80) NOT NULL, RegistroCanonicoId nvarchar(120) NOT NULL, OrganizacionSolicitanteId int NOT NULL,
 PersonaSolicitanteId int NOT NULL, BorradorProcesoId bigint NULL, Estado nvarchar(40) NOT NULL,
 SenalesJson nvarchar(max) NOT NULL CONSTRAINT DF_Reclamaciones_Senales DEFAULT N'[]',
 Justificacion nvarchar(2400) NOT NULL, EvidenciasJson nvarchar(max) NULL,
 Decision nvarchar(40) NULL, MotivoDecision nvarchar(2400) NULL, DecisorId int NULL,
 FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Reclamaciones_Creacion DEFAULT (SYSUTCDATETIME()),
 FechaEnvio datetime2(0) NULL, FechaDecision datetime2(0) NULL, FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_Reclamaciones_Actualizacion DEFAULT (SYSUTCDATETIME()),
 Version int NOT NULL CONSTRAINT DF_Reclamaciones_Version DEFAULT 1,
 CONSTRAINT FK_Reclamaciones_Organizacion FOREIGN KEY (OrganizacionSolicitanteId) REFERENCES dbo.Entidades(IdEntidad),
 CONSTRAINT FK_Reclamaciones_Persona FOREIGN KEY (PersonaSolicitanteId) REFERENCES dbo.Usuarios(IdUsuario),
 CONSTRAINT FK_Reclamaciones_Borrador FOREIGN KEY (BorradorProcesoId) REFERENCES dbo.BorradoresProceso(IdBorradorProceso),
 CONSTRAINT CK_Reclamaciones_Estado CHECK (Estado IN (N'borrador',N'enviada',N'en_revision',N'requiere_aclaracion',N'aclaracion_enviada',N'aprobada',N'rechazada',N'cancelada',N'transferencia_ejecutada',N'historica'))
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reclamaciones_Activa' AND object_id=OBJECT_ID(N'dbo.ReclamacionesAdministracion'))
CREATE INDEX IX_Reclamaciones_Activa ON dbo.ReclamacionesAdministracion(Dominio,RegistroCanonicoId,OrganizacionSolicitanteId,Estado);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Reclamaciones_Bandeja' AND object_id=OBJECT_ID(N'dbo.ReclamacionesAdministracion'))
CREATE INDEX IX_Reclamaciones_Bandeja ON dbo.ReclamacionesAdministracion(Estado,FechaActualizacion DESC);

IF OBJECT_ID(N'dbo.ReclamacionesAclaraciones', N'U') IS NULL
CREATE TABLE dbo.ReclamacionesAclaraciones (
 IdAclaracion bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, ReclamacionId bigint NOT NULL,
 SolicitadaPorId int NOT NULL, Comentario nvarchar(2400) NOT NULL, Respuesta nvarchar(2400) NULL,
 RespondidaPorId int NULL, FechaSolicitud datetime2(0) NOT NULL CONSTRAINT DF_ReclamacionesAclaraciones_Solicitud DEFAULT (SYSUTCDATETIME()), FechaRespuesta datetime2(0) NULL,
 CONSTRAINT FK_ReclamacionesAclaraciones_Reclamacion FOREIGN KEY(ReclamacionId) REFERENCES dbo.ReclamacionesAdministracion(IdReclamacion)
);

IF OBJECT_ID(N'dbo.TransferenciasAdministracion', N'U') IS NULL
CREATE TABLE dbo.TransferenciasAdministracion (
 IdTransferencia bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, Dominio nvarchar(80) NOT NULL, RegistroCanonicoId nvarchar(120) NOT NULL,
 OrganizacionAnteriorId int NOT NULL, OrganizacionNuevaId int NOT NULL, ReclamacionId bigint NOT NULL, DecisorId int NOT NULL,
 Motivo nvarchar(2400) NULL, EvidenciasJson nvarchar(max) NULL, EstadoAnterior nvarchar(80) NULL, EstadoPosterior nvarchar(80) NULL,
 FechaEjecucion datetime2(0) NOT NULL CONSTRAINT DF_Transferencias_Ejecucion DEFAULT (SYSUTCDATETIME()),
 CONSTRAINT FK_Transferencias_Reclamacion FOREIGN KEY(ReclamacionId) REFERENCES dbo.ReclamacionesAdministracion(IdReclamacion)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_Transferencias_Reclamacion' AND object_id=OBJECT_ID(N'dbo.TransferenciasAdministracion'))
CREATE UNIQUE INDEX UX_Transferencias_Reclamacion ON dbo.TransferenciasAdministracion(ReclamacionId);

IF OBJECT_ID(N'dbo.ReclamacionesConciliaciones', N'U') IS NULL
CREATE TABLE dbo.ReclamacionesConciliaciones (
 IdConciliacion bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
 ReclamacionId bigint NOT NULL, ValoresHistoricosJson nvarchar(max) NOT NULL,
 ValoresBorradorJson nvarchar(max) NOT NULL, SeleccionesJson nvarchar(max) NULL,
 FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ReclamacionesConciliaciones_Creacion DEFAULT (SYSUTCDATETIME()),
 FechaResolucion datetime2(0) NULL,
 CONSTRAINT UQ_ReclamacionesConciliaciones_Reclamacion UNIQUE(ReclamacionId),
 CONSTRAINT FK_ReclamacionesConciliaciones_Reclamacion FOREIGN KEY(ReclamacionId) REFERENCES dbo.ReclamacionesAdministracion(IdReclamacion)
);
