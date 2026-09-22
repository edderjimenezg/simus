/*
    SIMUS · Festival · Edición anual detallada

    Separa los datos de una realización anual del perfil público versionado.
    Es aditiva y segura para una base vacía: no copia ni borra filas históricas.
    La migración de datos, si llega a existir una fuente autorizada, se hará por
    un procedimiento revisable aparte; nunca por este DDL de arranque.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.EdicionesFestival', N'TipologiaFestivalId') IS NULL
BEGIN
    ALTER TABLE dbo.EdicionesFestival ADD
        TipologiaFestivalId int NULL,
        OtraTipologia nvarchar(120) NULL,
        FuenteFinanciacionPrimariaId int NULL,
        OtraFuenteFinanciacionPrimaria nvarchar(120) NULL,
        FuenteFinanciacionSecundariaId int NULL,
        OtraFuenteFinanciacionSecundaria nvarchar(120) NULL,
        UsaEstampillaProcultura bit NULL,
        PracticasMusicalesQueCongrega nvarchar(500) NULL,
        OtraModalidadParticipacion nvarchar(120) NULL,
        OtraExpresionArtistica nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EdicionesFestival_Tipologia')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT FK_EdicionesFestival_Tipologia
        FOREIGN KEY (TipologiaFestivalId) REFERENCES dbo.TipologiasFestival(IdTipologiaFestival);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EdicionesFestival_FuentePrimaria')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT FK_EdicionesFestival_FuentePrimaria
        FOREIGN KEY (FuenteFinanciacionPrimariaId) REFERENCES dbo.FuentesFinanciacion(IdFuenteFinanciacion);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EdicionesFestival_FuenteSecundaria')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT FK_EdicionesFestival_FuenteSecundaria
        FOREIGN KEY (FuenteFinanciacionSecundariaId) REFERENCES dbo.FuentesFinanciacion(IdFuenteFinanciacion);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalPracticasMusicales', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalPracticasMusicales (
    IdEdicionFestivalPracticaMusical int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    PracticaMusicalId int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalPracticas_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalPracticas_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalPracticas_Practica FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales(IdPracticaMusical),
    CONSTRAINT UQ_EdicionesFestivalPracticas UNIQUE (EdicionFestivalId, PracticaMusicalId)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalTerritoriosSonoros', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalTerritoriosSonoros (
    IdEdicionFestivalTerritorioSonoro int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    TerritorioSonoroId int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalTerritorios_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalTerritorios_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalTerritorios_Territorio FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros(IdTerritorioSonoro),
    CONSTRAINT UQ_EdicionesFestivalTerritorios UNIQUE (EdicionFestivalId, TerritorioSonoroId)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalExpresionesArtisticas', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalExpresionesArtisticas (
    IdEdicionFestivalExpresionArtistica int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    ExpresionArtisticaId int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalExpresiones_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalExpresiones_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalExpresiones_Expresion FOREIGN KEY (ExpresionArtisticaId) REFERENCES dbo.ExpresionesArtisticas(IdExpresionArtistica),
    CONSTRAINT UQ_EdicionesFestivalExpresiones UNIQUE (EdicionFestivalId, ExpresionArtisticaId)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalModalidadesParticipacion', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalModalidadesParticipacion (
    IdEdicionFestivalModalidadParticipacion int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    ModalidadParticipacionId int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalModalidades_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalModalidades_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalModalidades_Modalidad FOREIGN KEY (ModalidadParticipacionId) REFERENCES dbo.ModalidadesParticipacion(IdModalidadParticipacion),
    CONSTRAINT UQ_EdicionesFestivalModalidades UNIQUE (EdicionFestivalId, ModalidadParticipacionId)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalTiposIngreso', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalTiposIngreso (
    IdEdicionFestivalTipoIngreso int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    TipoIngresoId int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalIngresos_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalIngresos_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalIngresos_Tipo FOREIGN KEY (TipoIngresoId) REFERENCES dbo.TiposIngreso(IdTipoIngreso),
    CONSTRAINT UQ_EdicionesFestivalIngresos UNIQUE (EdicionFestivalId, TipoIngresoId)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalLocalizaciones', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalLocalizaciones (
    IdEdicionFestivalLocalizacion int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    CodigoDepartamento char(2) NOT NULL,
    CodigoMunicipio char(5) NOT NULL,
    ZonaUrbanoRuralId int NULL,
    TitulacionColectivaId int NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalLocalizaciones_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalLocalizaciones_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalLocalizaciones_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio) REFERENCES dbo.Divipola(CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT FK_EdicionesFestivalLocalizaciones_Zona FOREIGN KEY (ZonaUrbanoRuralId) REFERENCES dbo.ZonasUrbanoRural(IdZonaUrbanoRural),
    CONSTRAINT FK_EdicionesFestivalLocalizaciones_Titulacion FOREIGN KEY (TitulacionColectivaId) REFERENCES dbo.TitulacionesColectivas(IdTitulacionColectiva),
    CONSTRAINT UQ_EdicionesFestivalLocalizaciones UNIQUE (EdicionFestivalId, CodigoDepartamento, CodigoMunicipio)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalEntidadesAliadas', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalEntidadesAliadas (
    IdEdicionFestivalEntidadAliada int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    NombreEntidadAliada nvarchar(240) NULL,
    CorreoEntidadAliada nvarchar(180) NULL,
    NaturalezaEntidadId int NULL,
    EntidadId int NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalAliadas_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalAliadas_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalAliadas_Naturaleza FOREIGN KEY (NaturalezaEntidadId) REFERENCES dbo.NaturalezasEntidad(IdNaturalezaEntidad),
    CONSTRAINT FK_EdicionesFestivalAliadas_Entidad FOREIGN KEY (EntidadId) REFERENCES dbo.Entidades(IdEntidad)
);
GO

IF OBJECT_ID(N'dbo.EdicionesFestivalMateriales', N'U') IS NULL
CREATE TABLE dbo.EdicionesFestivalMateriales (
    IdEdicionFestivalMaterial int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EdicionFestivalId int NOT NULL,
    ArchivoId int NULL,
    Url nvarchar(1000) NULL,
    DescripcionArchivo nvarchar(1000) NULL,
    OrdenVisualizacion int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestivalMateriales_Fecha DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_EdicionesFestivalMateriales_Edicion FOREIGN KEY (EdicionFestivalId) REFERENCES dbo.EdicionesFestival(IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestivalMateriales_Archivo FOREIGN KEY (ArchivoId) REFERENCES dbo.Archivos(IdArchivo),
    CONSTRAINT CK_EdicionesFestivalMateriales_Orden CHECK (OrdenVisualizacion > 0)
);
GO
