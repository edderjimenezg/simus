/*
  Revisión por campo de la edición temporal.
  No reutiliza RevisionesFestival: aquella tabla registra el expediente del Festival y del perfil
  versionado; una edición puede tener una revisión independiente y simultánea.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RevisionesEdicionesFestival', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesEdicionesFestival
    (
        IdRevisionEdicionFestival bigint IDENTITY(1,1) NOT NULL,
        IdEdicionFestival int NOT NULL,
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_RevisionesEdicionesFestival_Estado DEFAULT(N'borrador'),
        IdUsuarioRevisor int NOT NULL,
        RevisorNombre nvarchar(480) NULL,
        ObservacionGeneral nvarchar(2400) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RevisionesEdicionesFestival_FechaCreacion DEFAULT(SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaEnvio datetime2(0) NULL,
        FechaCierre datetime2(0) NULL,
        CONSTRAINT PK_RevisionesEdicionesFestival PRIMARY KEY (IdRevisionEdicionFestival),
        CONSTRAINT FK_RevisionesEdicionesFestival_Edicion FOREIGN KEY (IdEdicionFestival) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
        CONSTRAINT FK_RevisionesEdicionesFestival_Revisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT CK_RevisionesEdicionesFestival_Estado CHECK (Estado IN (N'borrador', N'enviada', N'cerrada')),
        CONSTRAINT CK_RevisionesEdicionesFestival_Cierre CHECK ((Estado = N'cerrada' AND FechaCierre IS NOT NULL) OR (Estado <> N'cerrada' AND FechaCierre IS NULL))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_RevisionesEdicionesFestival_Viva' AND object_id = OBJECT_ID(N'dbo.RevisionesEdicionesFestival'))
    CREATE UNIQUE INDEX UQ_RevisionesEdicionesFestival_Viva ON dbo.RevisionesEdicionesFestival(IdEdicionFestival) WHERE Estado <> N'cerrada';
GO

IF OBJECT_ID(N'dbo.RevisionesEdicionesFestivalObservaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesEdicionesFestivalObservaciones
    (
        IdObservacion bigint IDENTITY(1,1) NOT NULL,
        IdRevisionEdicionFestival bigint NOT NULL,
        SeccionId nvarchar(80) NOT NULL,
        CampoId nvarchar(120) NOT NULL,
        CampoEtiqueta nvarchar(240) NOT NULL,
        ValorObservado nvarchar(max) NULL,
        Nota nvarchar(2400) NOT NULL,
        Estado nvarchar(40) NOT NULL CONSTRAINT DF_RevisionesEdicionesFestivalObservaciones_Estado DEFAULT(N'pendiente'),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RevisionesEdicionesFestivalObservaciones_FechaCreacion DEFAULT(SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaAtencion datetime2(0) NULL,
        IdUsuarioAtiende int NULL,
        CONSTRAINT PK_RevisionesEdicionesFestivalObservaciones PRIMARY KEY (IdObservacion),
        CONSTRAINT FK_RevisionesEdicionesFestivalObservaciones_Revision FOREIGN KEY (IdRevisionEdicionFestival) REFERENCES dbo.RevisionesEdicionesFestival(IdRevisionEdicionFestival),
        CONSTRAINT FK_RevisionesEdicionesFestivalObservaciones_Atiende FOREIGN KEY (IdUsuarioAtiende) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT CK_RevisionesEdicionesFestivalObservaciones_Estado CHECK (Estado IN (N'pendiente', N'atendida')),
        CONSTRAINT CK_RevisionesEdicionesFestivalObservaciones_Nota CHECK (LEN(LTRIM(RTRIM(Nota))) > 0),
        CONSTRAINT UQ_RevisionesEdicionesFestivalObservaciones_Campo UNIQUE(IdRevisionEdicionFestival, CampoId)
    );
END;
GO
