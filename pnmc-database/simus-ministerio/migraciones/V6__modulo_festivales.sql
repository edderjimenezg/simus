/*
  Módulo de Festivales.

  Festival: registro permanente, propiedad de una organización.
  EdicionesFestival: cada realización, con su caracterización, financiación y estado operativo.
  VersionesFestival: instantáneas numeradas de la ficha pública; una sola vigente por festival.

  Las listas de valores y las relaciones de un registro (territorios, prácticas, expresiones,
  modalidades, tipos de ingreso, localizaciones, entidades aliadas y archivos) viven en tablas
  «…DeRegistro», identificadas por módulo y registro, para que cualquier módulo las reutilice.
  El ciclo de revisión y las propuestas de cambio usan la misma identificación.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.TipologiasFestival (
    IdTipologiaFestival int IDENTITY(1,1) NOT NULL,
    NombreTipologiaFestival nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_TipologiasFestival PRIMARY KEY (IdTipologiaFestival),
    CONSTRAINT UQ_TipologiasFestival_Nombre UNIQUE (NombreTipologiaFestival),
    CONSTRAINT UQ_TipologiasFestival_Slug UNIQUE (Slug),
    CONSTRAINT CK_TipologiasFestival_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.ExpresionesArtisticas (
    IdExpresionArtistica int IDENTITY(1,1) NOT NULL,
    NombreExpresionArtistica nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_ExpresionesArtisticas PRIMARY KEY (IdExpresionArtistica),
    CONSTRAINT UQ_ExpresionesArtisticas_Nombre UNIQUE (NombreExpresionArtistica),
    CONSTRAINT UQ_ExpresionesArtisticas_Slug UNIQUE (Slug),
    CONSTRAINT CK_ExpresionesArtisticas_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.FuentesFinanciacion (
    IdFuenteFinanciacion int IDENTITY(1,1) NOT NULL,
    NombreFuenteFinanciacion nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_FuentesFinanciacion PRIMARY KEY (IdFuenteFinanciacion),
    CONSTRAINT UQ_FuentesFinanciacion_Nombre UNIQUE (NombreFuenteFinanciacion),
    CONSTRAINT UQ_FuentesFinanciacion_Slug UNIQUE (Slug),
    CONSTRAINT CK_FuentesFinanciacion_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.ModalidadesParticipacion (
    IdModalidadParticipacion int IDENTITY(1,1) NOT NULL,
    NombreModalidadParticipacion nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_ModalidadesParticipacion PRIMARY KEY (IdModalidadParticipacion),
    CONSTRAINT UQ_ModalidadesParticipacion_Nombre UNIQUE (NombreModalidadParticipacion),
    CONSTRAINT UQ_ModalidadesParticipacion_Slug UNIQUE (Slug),
    CONSTRAINT CK_ModalidadesParticipacion_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.TiposIngreso (
    IdTipoIngreso int IDENTITY(1,1) NOT NULL,
    NombreTipoIngreso nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_TiposIngreso PRIMARY KEY (IdTipoIngreso),
    CONSTRAINT UQ_TiposIngreso_Nombre UNIQUE (NombreTipoIngreso),
    CONSTRAINT UQ_TiposIngreso_Slug UNIQUE (Slug),
    CONSTRAINT CK_TiposIngreso_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.TiposOrganizador (
    IdTipoOrganizador int IDENTITY(1,1) NOT NULL,
    NombreTipoOrganizador nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_TiposOrganizador PRIMARY KEY (IdTipoOrganizador),
    CONSTRAINT UQ_TiposOrganizador_Nombre UNIQUE (NombreTipoOrganizador),
    CONSTRAINT UQ_TiposOrganizador_Slug UNIQUE (Slug),
    CONSTRAINT CK_TiposOrganizador_Orden CHECK (OrdenVisualizacion > 0)
);
GO

CREATE TABLE dbo.Festivales (
    IdFestival int IDENTITY(1,1) NOT NULL,
    NombreFestival nvarchar(220) NOT NULL,
    NumeroVersiones int NULL,
    FechaUltimaVersion date NULL,
    Descripcion nvarchar(max) NULL,
    Organizador nvarchar(220) NULL,
    CorreoOrganizador nvarchar(180) NULL,
    TelefonoOrganizador nvarchar(80) NULL,
    SitioWebOrganizador nvarchar(500) NULL,
    CorreoFestival nvarchar(180) NULL,
    InstagramFestival nvarchar(500) NULL,
    FacebookFestival nvarchar(500) NULL,
    SitioWebFestival nvarchar(500) NULL,
    OtroEnlaceFestival nvarchar(500) NULL,
    TelefonoFestival nvarchar(80) NULL,
    TieneVersionVigenteAnoActual bit NOT NULL CONSTRAINT DF_Festivales_TieneVersionVigenteAnoActual DEFAULT (0),
    EstadoVersionAnoActual nvarchar(80) NULL,
    FechaInicioVersionActual date NULL,
    FechaFinVersionActual date NULL,
    NivelCobertura nvarchar(40) NOT NULL,
    CodigoDepartamento char(2) NULL,
    CodigoMunicipio char(5) NULL,
    EstadoRegistro nvarchar(80) NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Festivales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    OrganizacionPrincipalId int NOT NULL,
    Periodicidad nvarchar(80) NULL,
    ObservacionesContacto nvarchar(600) NULL,
    PeriodicidadDetalle nvarchar(600) NULL,
    CONSTRAINT PK_Festivales PRIMARY KEY (IdFestival),
    CONSTRAINT FK_Festivales_OrganizacionPrincipal FOREIGN KEY (OrganizacionPrincipalId) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_Festivales_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT FK_Festivales_EstadosContenido FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado),
    CONSTRAINT CK_Festivales_EstadoRegistro
        CHECK (EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado')),
    CONSTRAINT CK_Festivales_NumeroVersiones CHECK (NumeroVersiones IS NULL OR NumeroVersiones >= 0),
    CONSTRAINT CK_Festivales_FechasVersionActual
        CHECK (FechaFinVersionActual IS NULL OR FechaInicioVersionActual IS NULL OR FechaFinVersionActual >= FechaInicioVersionActual),
    CONSTRAINT CK_Festivales_NivelCobertura
        CHECK (NivelCobertura IN (N'nacional', N'departamental', N'municipal') AND (
               (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
            OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
            OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)))
);
CREATE INDEX IX_Festivales_Territorio ON dbo.Festivales (CodigoDepartamento, CodigoMunicipio);
CREATE INDEX IX_Festivales_EstadoRegistro ON dbo.Festivales (EstadoRegistro);
CREATE INDEX IX_Festivales_OrganizacionPrincipalId_EstadoRegistro ON dbo.Festivales (OrganizacionPrincipalId, EstadoRegistro);

CREATE TABLE dbo.VersionesFestival (
    IdVersionFestival int IDENTITY(1,1) NOT NULL,
    FestivalOrigenId int NOT NULL,
    NumeroVersion int NOT NULL,
    EsVigente bit NOT NULL,
    Nombre nvarchar(240) NOT NULL,
    Descripcion nvarchar(1200) NULL,
    NivelCobertura nvarchar(40) NOT NULL,
    CodigoDepartamento char(2) NULL,
    CodigoMunicipio char(5) NULL,
    Periodicidad nvarchar(80) NULL,
    CorreoContacto nvarchar(180) NULL,
    FechaPublicacion datetime2(0) NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_VersionesFestival_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaInicio date NULL,
    FechaFin date NULL,
    TipologiaFestivalId int NULL,
    FuenteFinanciacionPrimariaId int NULL,
    FuenteFinanciacionSecundariaId int NULL,
    UsaEstampillaProcultura bit NULL,
    TelefonoContacto nvarchar(80) NULL,
    Instagram nvarchar(500) NULL,
    Facebook nvarchar(500) NULL,
    SitioWeb nvarchar(500) NULL,
    OtroEnlace nvarchar(500) NULL,
    Director nvarchar(240) NULL,
    TipoOrganizadorId int NULL,
    PracticasMusicalesQueCongrega nvarchar(500) NULL,
    OtraTipologia nvarchar(200) NULL,
    OtraModalidadParticipacion nvarchar(200) NULL,
    OtraExpresionArtistica nvarchar(200) NULL,
    OtraFuenteFinanciacionPrimaria nvarchar(200) NULL,
    OtraFuenteFinanciacionSecundaria nvarchar(200) NULL,
    PerteneceAOrganizacionColectiva bit NULL,
    NombreOrganizacionColectiva nvarchar(500) NULL,
    OtroTipoOrganizador nvarchar(200) NULL,
    ObservacionesContacto nvarchar(600) NULL,
    ObservacionesRechazo nvarchar(4000) NULL,
    EstadoRegistro nvarchar(80) NULL,
    PeriodicidadDetalle nvarchar(600) NULL,
    CONSTRAINT PK_VersionesFestival PRIMARY KEY (IdVersionFestival),
    CONSTRAINT UQ_VersionesFestival_Festival_Numero UNIQUE (FestivalOrigenId, NumeroVersion),
    CONSTRAINT FK_VersionesFestival_Festival FOREIGN KEY (FestivalOrigenId) REFERENCES dbo.Festivales (IdFestival),
    CONSTRAINT FK_VersionesFestival_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT FK_VersionesFestival_TipologiaFestival FOREIGN KEY (TipologiaFestivalId) REFERENCES dbo.TipologiasFestival (IdTipologiaFestival),
    CONSTRAINT FK_VersionesFestival_FuenteFinanciacionPrimaria FOREIGN KEY (FuenteFinanciacionPrimariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion),
    CONSTRAINT FK_VersionesFestival_FuenteFinanciacionSecundaria FOREIGN KEY (FuenteFinanciacionSecundariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion),
    CONSTRAINT FK_VersionesFestival_TipoOrganizador FOREIGN KEY (TipoOrganizadorId) REFERENCES dbo.TiposOrganizador (IdTipoOrganizador),
    CONSTRAINT FK_VersionesFestival_EstadosContenido FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado),
    CONSTRAINT CK_VersionesFestival_EstadoRegistro
        CHECK (EstadoRegistro IS NULL OR EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado')),
    CONSTRAINT CK_VersionesFestival_FechasEdicion CHECK (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio),
    CONSTRAINT CK_VersionesFestival_FuentesDistintas
        CHECK (FuenteFinanciacionPrimariaId IS NULL OR FuenteFinanciacionSecundariaId IS NULL
            OR FuenteFinanciacionPrimariaId <> FuenteFinanciacionSecundariaId)
);
CREATE UNIQUE INDEX UX_VersionesFestival_Festival_Vigente ON dbo.VersionesFestival (FestivalOrigenId) WHERE EsVigente = 1;
CREATE INDEX IX_VersionesFestival_TipologiaFestivalId ON dbo.VersionesFestival (TipologiaFestivalId);

CREATE TABLE dbo.EdicionesFestival (
    IdEdicionFestival int IDENTITY(1,1) NOT NULL,
    FestivalId int NOT NULL,
    Anio int NULL,
    Nombre nvarchar(240) NULL,
    Descripcion nvarchar(max) NULL,
    FechaInicio date NULL,
    FechaFin date NULL,
    Estado nvarchar(40) NOT NULL CONSTRAINT DF_EdicionesFestival_Estado DEFAULT (N'en_preparacion'),
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EdicionesFestival_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    Director nvarchar(240) NULL,
    EstadoRegistro nvarchar(80) NOT NULL,
    TipologiaFestivalId int NULL,
    OtraTipologia nvarchar(120) NULL,
    FuenteFinanciacionPrimariaId int NULL,
    OtraFuenteFinanciacionPrimaria nvarchar(120) NULL,
    FuenteFinanciacionSecundariaId int NULL,
    OtraFuenteFinanciacionSecundaria nvarchar(120) NULL,
    UsaEstampillaProcultura bit NULL,
    PracticasMusicalesQueCongrega nvarchar(500) NULL,
    OtraModalidadParticipacion nvarchar(120) NULL,
    OtraExpresionArtistica nvarchar(120) NULL,
    NumeroEdicion int NULL,
    EstadoVisibilidad nvarchar(40) NOT NULL,
    CONSTRAINT PK_EdicionesFestival PRIMARY KEY (IdEdicionFestival),
    CONSTRAINT FK_EdicionesFestival_Festivales FOREIGN KEY (FestivalId) REFERENCES dbo.Festivales (IdFestival),
    CONSTRAINT FK_EdicionesFestival_EstadosContenido FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado),
    CONSTRAINT FK_EdicionesFestival_Tipologia FOREIGN KEY (TipologiaFestivalId) REFERENCES dbo.TipologiasFestival (IdTipologiaFestival),
    CONSTRAINT FK_EdicionesFestival_FuentePrimaria FOREIGN KEY (FuenteFinanciacionPrimariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion),
    CONSTRAINT FK_EdicionesFestival_FuenteSecundaria FOREIGN KEY (FuenteFinanciacionSecundariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion),
    CONSTRAINT CK_EdicionesFestival_EstadoRegistro
        CHECK (EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado')),
    CONSTRAINT CK_EdicionesFestival_Estado CHECK (Estado IN (N'en_preparacion', N'programada', N'realizada', N'cancelada')),
    CONSTRAINT CK_EdicionesFestival_EstadoVisibilidad CHECK (EstadoVisibilidad IN (N'borrador', N'publicada', N'archivada')),
    CONSTRAINT CK_EdicionesFestival_Identificacion
        CHECK (Anio IS NOT NULL OR NumeroEdicion IS NOT NULL OR NULLIF(LTRIM(RTRIM(Nombre)), N'') IS NOT NULL
            OR FechaInicio IS NOT NULL OR FechaFin IS NOT NULL),
    CONSTRAINT CK_EdicionesFestival_AnioValido CHECK (Anio IS NULL OR (Anio >= 1900 AND Anio <= 2200)),
    CONSTRAINT CK_EdicionesFestival_NumeroValido CHECK (NumeroEdicion IS NULL OR NumeroEdicion > 0),
    CONSTRAINT CK_EdicionesFestival_Fechas CHECK (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio),
    CONSTRAINT CK_EdicionesFestival_FuentesDistintas
        CHECK (FuenteFinanciacionPrimariaId IS NULL OR FuenteFinanciacionSecundariaId IS NULL
            OR FuenteFinanciacionPrimariaId <> FuenteFinanciacionSecundariaId)
);
CREATE INDEX IX_EdicionesFestival_Festival_Orden ON dbo.EdicionesFestival (FestivalId, Anio DESC, NumeroEdicion DESC);
CREATE INDEX IX_EdicionesFestival_EstadoRegistro ON dbo.EdicionesFestival (EstadoRegistro, FestivalId);

CREATE TABLE dbo.FestivalesReferenciasHistoricas (
    IdReferenciaHistoricaFestival int IDENTITY(1,1) NOT NULL,
    IdOrganizacion int NOT NULL,
    IdFestival int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FestivalesReferenciasHistoricas_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_FestivalesReferenciasHistoricas PRIMARY KEY (IdReferenciaHistoricaFestival),
    CONSTRAINT UQ_FestivalesReferenciasHistoricas_Organizacion_Festival UNIQUE (IdOrganizacion, IdFestival),
    CONSTRAINT FK_FestivalesReferenciasHistoricas_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_FestivalesReferenciasHistoricas_Festival FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival)
);
GO

/*
  Clasificaciones de un registro. ModuloId dice qué tabla es la dueña y RegistroId su
  identificador; el CHECK limita los módulos que pueden usarlas.
*/
CREATE TABLE dbo.TerritoriosSonorosDeRegistro (
    IdTerritorioSonoroDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    IdTerritorioSonoro int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_TerritoriosSonorosDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TerritoriosSonorosDeRegistro PRIMARY KEY (IdTerritorioSonoroDeRegistro),
    CONSTRAINT UQ_TerritoriosSonorosDeRegistro UNIQUE (ModuloId, RegistroId, IdTerritorioSonoro),
    CONSTRAINT FK_TerritoriosSonorosDeRegistro_Catalogo FOREIGN KEY (IdTerritorioSonoro) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
    CONSTRAINT CK_TerritoriosSonorosDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_TerritoriosSonorosDeRegistro_Catalogo ON dbo.TerritoriosSonorosDeRegistro (IdTerritorioSonoro);

CREATE TABLE dbo.PracticasMusicalesDeRegistro (
    IdPracticaMusicalDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    IdPracticaMusical int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_PracticasMusicalesDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_PracticasMusicalesDeRegistro PRIMARY KEY (IdPracticaMusicalDeRegistro),
    CONSTRAINT UQ_PracticasMusicalesDeRegistro UNIQUE (ModuloId, RegistroId, IdPracticaMusical),
    CONSTRAINT FK_PracticasMusicalesDeRegistro_Catalogo FOREIGN KEY (IdPracticaMusical) REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
    CONSTRAINT CK_PracticasMusicalesDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_PracticasMusicalesDeRegistro_Catalogo ON dbo.PracticasMusicalesDeRegistro (IdPracticaMusical);

CREATE TABLE dbo.ExpresionesArtisticasDeRegistro (
    IdExpresionArtisticaDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    IdExpresionArtistica int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ExpresionesArtisticasDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ExpresionesArtisticasDeRegistro PRIMARY KEY (IdExpresionArtisticaDeRegistro),
    CONSTRAINT UQ_ExpresionesArtisticasDeRegistro UNIQUE (ModuloId, RegistroId, IdExpresionArtistica),
    CONSTRAINT FK_ExpresionesArtisticasDeRegistro_Catalogo FOREIGN KEY (IdExpresionArtistica) REFERENCES dbo.ExpresionesArtisticas (IdExpresionArtistica),
    CONSTRAINT CK_ExpresionesArtisticasDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_ExpresionesArtisticasDeRegistro_Catalogo ON dbo.ExpresionesArtisticasDeRegistro (IdExpresionArtistica);

CREATE TABLE dbo.ModalidadesParticipacionDeRegistro (
    IdModalidadParticipacionDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    IdModalidadParticipacion int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ModalidadesParticipacionDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ModalidadesParticipacionDeRegistro PRIMARY KEY (IdModalidadParticipacionDeRegistro),
    CONSTRAINT UQ_ModalidadesParticipacionDeRegistro UNIQUE (ModuloId, RegistroId, IdModalidadParticipacion),
    CONSTRAINT FK_ModalidadesParticipacionDeRegistro_Catalogo FOREIGN KEY (IdModalidadParticipacion) REFERENCES dbo.ModalidadesParticipacion (IdModalidadParticipacion),
    CONSTRAINT CK_ModalidadesParticipacionDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_ModalidadesParticipacionDeRegistro_Catalogo ON dbo.ModalidadesParticipacionDeRegistro (IdModalidadParticipacion);

CREATE TABLE dbo.TiposIngresoDeRegistro (
    IdTipoIngresoDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    IdTipoIngreso int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_TiposIngresoDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TiposIngresoDeRegistro PRIMARY KEY (IdTipoIngresoDeRegistro),
    CONSTRAINT UQ_TiposIngresoDeRegistro UNIQUE (ModuloId, RegistroId, IdTipoIngreso),
    CONSTRAINT FK_TiposIngresoDeRegistro_Catalogo FOREIGN KEY (IdTipoIngreso) REFERENCES dbo.TiposIngreso (IdTipoIngreso),
    CONSTRAINT CK_TiposIngresoDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_TiposIngresoDeRegistro_Catalogo ON dbo.TiposIngresoDeRegistro (IdTipoIngreso);

-- Un municipio puede figurar una vez por zona: urbana y rural son localizaciones distintas.
CREATE TABLE dbo.LocalizacionesDeRegistro (
    IdLocalizacionDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    CodigoDepartamento char(2) NOT NULL,
    CodigoMunicipio char(5) NOT NULL,
    ZonaUrbanoRuralId int NULL,
    TitulacionColectivaId int NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_LocalizacionesDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_LocalizacionesDeRegistro PRIMARY KEY (IdLocalizacionDeRegistro),
    CONSTRAINT UQ_LocalizacionesDeRegistro UNIQUE (ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio, ZonaUrbanoRuralId),
    CONSTRAINT FK_LocalizacionesDeRegistro_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
    CONSTRAINT FK_LocalizacionesDeRegistro_Zona FOREIGN KEY (ZonaUrbanoRuralId) REFERENCES dbo.ZonasUrbanoRural (IdZonaUrbanoRural),
    CONSTRAINT FK_LocalizacionesDeRegistro_Titulacion FOREIGN KEY (TitulacionColectivaId) REFERENCES dbo.TitulacionesColectivas (IdTitulacionColectiva),
    CONSTRAINT CK_LocalizacionesDeRegistro_Municipio_Departamento CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento),
    CONSTRAINT CK_LocalizacionesDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_LocalizacionesDeRegistro_Municipio ON dbo.LocalizacionesDeRegistro (CodigoDepartamento, CodigoMunicipio);

CREATE TABLE dbo.EntidadesAliadasDeRegistro (
    IdEntidadAliadaDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    NombreEntidadAliada nvarchar(300) NULL,
    CorreoEntidadAliada nvarchar(180) NULL,
    NaturalezaEntidadId int NULL,
    EntidadId int NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EntidadesAliadasDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_EntidadesAliadasDeRegistro PRIMARY KEY (IdEntidadAliadaDeRegistro),
    CONSTRAINT FK_EntidadesAliadasDeRegistro_Entidad FOREIGN KEY (EntidadId) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_EntidadesAliadasDeRegistro_Naturaleza FOREIGN KEY (NaturalezaEntidadId) REFERENCES dbo.NaturalezasEntidad (IdNaturalezaEntidad),
    CONSTRAINT CK_EntidadesAliadasDeRegistro_Identificable
        CHECK (EntidadId IS NOT NULL OR NULLIF(LTRIM(RTRIM(NombreEntidadAliada)), N'') IS NOT NULL),
    CONSTRAINT CK_EntidadesAliadasDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_EntidadesAliadasDeRegistro_Registro ON dbo.EntidadesAliadasDeRegistro (ModuloId, RegistroId);

CREATE TABLE dbo.ArchivosDeRegistro (
    IdArchivoDeRegistro bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    ArchivoId int NULL,
    Url nvarchar(1000) NULL,
    RolArchivo nvarchar(80) NOT NULL CONSTRAINT DF_ArchivosDeRegistro_RolArchivo DEFAULT (N'material'),
    DescripcionArchivo nvarchar(1000) NULL,
    OrdenVisualizacion int NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ArchivosDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_ArchivosDeRegistro PRIMARY KEY (IdArchivoDeRegistro),
    CONSTRAINT UQ_ArchivosDeRegistro UNIQUE (ModuloId, RegistroId, RolArchivo, OrdenVisualizacion),
    CONSTRAINT FK_ArchivosDeRegistro_Archivo FOREIGN KEY (ArchivoId) REFERENCES dbo.Archivos (IdArchivo),
    CONSTRAINT CK_ArchivosDeRegistro_Orden CHECK (OrdenVisualizacion > 0),
    CONSTRAINT CK_ArchivosDeRegistro_Destino CHECK (ArchivoId IS NOT NULL OR Url IS NOT NULL),
    CONSTRAINT CK_ArchivosDeRegistro_Modulo
        CHECK (ModuloId IN (N'festivales', N'versiones-festival', N'ediciones-festival', N'ediciones-mercado'))
);
CREATE INDEX IX_ArchivosDeRegistro_Archivo ON dbo.ArchivosDeRegistro (ArchivoId) WHERE ArchivoId IS NOT NULL;
GO

/*
  Ciclo de revisión institucional, común a todos los módulos: una revisión viva por registro,
  observaciones por campo, el envío que la originó y el historial de cambios de estado.
*/
CREATE TABLE dbo.RevisionesDeRegistro (
    IdRevision bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    Estado nvarchar(40) NOT NULL CONSTRAINT DF_RevisionesDeRegistro_Estado DEFAULT (N'borrador'),
    IdUsuarioRevisor int NOT NULL,
    RevisorNombre nvarchar(480) NULL,
    IdUsuarioDestinatario int NULL,
    DestinatarioNombre nvarchar(480) NULL,
    IdOrganizacion int NULL,
    OrganizacionNombre nvarchar(480) NULL,
    ObservacionGeneral nvarchar(2400) NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RevisionesDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    FechaEnvio datetime2(0) NULL,
    FechaCierre datetime2(0) NULL,
    CONSTRAINT PK_RevisionesDeRegistro PRIMARY KEY (IdRevision),
    CONSTRAINT FK_RevisionesDeRegistro_Revisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_RevisionesDeRegistro_Destinatario FOREIGN KEY (IdUsuarioDestinatario) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_RevisionesDeRegistro_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT CK_RevisionesDeRegistro_Estado CHECK (Estado IN (N'borrador', N'enviada', N'cerrada')),
    CONSTRAINT CK_RevisionesDeRegistro_FechaEnvio
        CHECK ((Estado = N'borrador' AND FechaEnvio IS NULL) OR (Estado <> N'borrador' AND FechaEnvio IS NOT NULL)),
    CONSTRAINT CK_RevisionesDeRegistro_FechaCierre
        CHECK ((Estado = N'cerrada' AND FechaCierre IS NOT NULL) OR (Estado <> N'cerrada' AND FechaCierre IS NULL)),
    CONSTRAINT CK_RevisionesDeRegistro_Registro CHECK (LEN(LTRIM(RTRIM(ModuloId))) > 0 AND LEN(LTRIM(RTRIM(RegistroId))) > 0)
);
CREATE UNIQUE INDEX UQ_RevisionesDeRegistro_Viva ON dbo.RevisionesDeRegistro (ModuloId, RegistroId) WHERE Estado <> N'cerrada';
CREATE INDEX IX_RevisionesDeRegistro_Registro ON dbo.RevisionesDeRegistro (ModuloId, RegistroId, IdRevision DESC);

CREATE TABLE dbo.RevisionesDeRegistroObservaciones (
    IdObservacion bigint IDENTITY(1,1) NOT NULL,
    IdRevision bigint NOT NULL,
    Ambito nvarchar(40) NOT NULL,
    SubregistroId nvarchar(120) NULL,
    SeccionId nvarchar(80) NOT NULL,
    CampoId nvarchar(120) NOT NULL,
    CampoEtiqueta nvarchar(240) NOT NULL,
    ValorObservado nvarchar(max) NULL,
    Nota nvarchar(2400) NOT NULL,
    Estado nvarchar(40) NOT NULL CONSTRAINT DF_RevisionesDeRegistroObservaciones_Estado DEFAULT (N'pendiente'),
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RevisionesDeRegistroObservaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    FechaAtencion datetime2(0) NULL,
    IdUsuarioAtiende int NULL,
    CONSTRAINT PK_RevisionesDeRegistroObservaciones PRIMARY KEY (IdObservacion),
    CONSTRAINT UQ_RevisionesDeRegistroObservaciones_Campo UNIQUE (IdRevision, Ambito, SubregistroId, CampoId),
    CONSTRAINT FK_RevisionesDeRegistroObservaciones_Revision FOREIGN KEY (IdRevision) REFERENCES dbo.RevisionesDeRegistro (IdRevision),
    CONSTRAINT FK_RevisionesDeRegistroObservaciones_Atiende FOREIGN KEY (IdUsuarioAtiende) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT CK_RevisionesDeRegistroObservaciones_Ambito CHECK (Ambito IN (N'principal', N'subregistro')),
    CONSTRAINT CK_RevisionesDeRegistroObservaciones_Estado CHECK (Estado IN (N'pendiente', N'atendida')),
    CONSTRAINT CK_RevisionesDeRegistroObservaciones_Subregistro
        CHECK ((Ambito = N'subregistro' AND SubregistroId IS NOT NULL) OR (Ambito = N'principal' AND SubregistroId IS NULL)),
    CONSTRAINT CK_RevisionesDeRegistroObservaciones_Nota CHECK (LEN(LTRIM(RTRIM(Nota))) > 0)
);
CREATE INDEX IX_RevisionesDeRegistroObservaciones_Revision ON dbo.RevisionesDeRegistroObservaciones (IdRevision, SeccionId, IdObservacion);

CREATE TABLE dbo.EnviosDeRevision (
    IdEnvioDeRevision bigint IDENTITY(1,1) NOT NULL,
    NumeroEnvio int NOT NULL,
    IdUsuarioRemitente int NOT NULL,
    IdOrganizacion int NOT NULL,
    IdRevisionOrigen bigint NULL,
    EstadoAnterior nvarchar(40) NOT NULL,
    DatosJson nvarchar(max) NOT NULL,
    FechaEnvio datetime2(0) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    CONSTRAINT PK_EnviosDeRevision PRIMARY KEY (IdEnvioDeRevision),
    CONSTRAINT FK_EnviosDeRevision_Usuario FOREIGN KEY (IdUsuarioRemitente) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_EnviosDeRevision_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_EnviosDeRevision_RevisionDeRegistro FOREIGN KEY (IdRevisionOrigen) REFERENCES dbo.RevisionesDeRegistro (IdRevision),
    CONSTRAINT CK_EnviosDeRevision_Numero CHECK (NumeroEnvio > 0),
    CONSTRAINT CK_EnviosDeRevision_Datos CHECK (ISJSON(DatosJson) = 1)
);
CREATE UNIQUE INDEX UQ_EnviosDeRevision_Numero ON dbo.EnviosDeRevision (ModuloId, RegistroId, NumeroEnvio);
CREATE INDEX IX_EnviosDeRevision_Fecha ON dbo.EnviosDeRevision (ModuloId, RegistroId, FechaEnvio DESC);

CREATE TABLE dbo.PropuestasDeCambio (
    IdPropuesta bigint IDENTITY(1,1) NOT NULL,
    ModuloId nvarchar(80) NOT NULL,
    RegistroId nvarchar(120) NOT NULL,
    Estado nvarchar(40) NOT NULL CONSTRAINT DF_PropuestasDeCambio_Estado DEFAULT (N'borrador'),
    IdOrganizacion int NOT NULL,
    OrganizacionNombre nvarchar(480) NULL,
    IdUsuarioProponente int NULL,
    ProponenteNombre nvarchar(480) NULL,
    Motivo nvarchar(2400) NULL,
    IdUsuarioDecide int NULL,
    DecideNombre nvarchar(480) NULL,
    MotivoDeLaDecision nvarchar(2400) NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_PropuestasDeCambio_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    FechaEnvio datetime2(0) NULL,
    FechaDecision datetime2(0) NULL,
    SubregistroId nvarchar(120) NULL,
    SubregistroResultanteId nvarchar(120) NULL,
    CONSTRAINT PK_PropuestasDeCambio PRIMARY KEY (IdPropuesta),
    CONSTRAINT FK_PropuestasDeCambio_Organizacion FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),
    CONSTRAINT FK_PropuestasDeCambio_Proponente FOREIGN KEY (IdUsuarioProponente) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT FK_PropuestasDeCambio_Decide FOREIGN KEY (IdUsuarioDecide) REFERENCES dbo.Usuarios (IdUsuario),
    CONSTRAINT CK_PropuestasDeCambio_Estado
        CHECK (Estado IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aplicada', N'rechazada')),
    CONSTRAINT CK_PropuestasDeCambio_FechaEnvio
        CHECK ((Estado = N'borrador' AND FechaEnvio IS NULL) OR (Estado <> N'borrador' AND FechaEnvio IS NOT NULL)),
    CONSTRAINT CK_PropuestasDeCambio_FechaDecision
        CHECK ((Estado IN (N'aplicada', N'rechazada') AND FechaDecision IS NOT NULL)
            OR (Estado NOT IN (N'aplicada', N'rechazada') AND FechaDecision IS NULL)),
    CONSTRAINT CK_PropuestasDeCambio_MotivoDelRechazo
        CHECK (Estado <> N'rechazada' OR LEN(LTRIM(RTRIM(ISNULL(MotivoDeLaDecision, N'')))) > 0),
    CONSTRAINT CK_PropuestasDeCambio_Registro CHECK (LEN(LTRIM(RTRIM(ModuloId))) > 0 AND LEN(LTRIM(RTRIM(RegistroId))) > 0)
);
CREATE UNIQUE INDEX UQ_PropuestasDeCambio_Viva ON dbo.PropuestasDeCambio (ModuloId, RegistroId)
    WHERE Estado <> N'aplicada' AND Estado <> N'rechazada';
CREATE INDEX IX_PropuestasDeCambio_Registro ON dbo.PropuestasDeCambio (ModuloId, RegistroId, IdPropuesta DESC);
CREATE INDEX IX_PropuestasDeCambio_Estado ON dbo.PropuestasDeCambio (Estado, FechaEnvio);

CREATE TABLE dbo.PropuestasDeCambioCampos (
    IdCampoPropuesto bigint IDENTITY(1,1) NOT NULL,
    IdPropuesta bigint NOT NULL,
    SeccionId nvarchar(80) NOT NULL,
    CampoId nvarchar(120) NOT NULL,
    CampoEtiqueta nvarchar(240) NOT NULL,
    ValorAnterior nvarchar(max) NULL,
    ValorPropuesto nvarchar(max) NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_PropuestasDeCambioCampos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    FechaActualizacion datetime2(0) NULL,
    CONSTRAINT PK_PropuestasDeCambioCampos PRIMARY KEY (IdCampoPropuesto),
    CONSTRAINT UQ_PropuestasDeCambioCampos_Campo UNIQUE (IdPropuesta, CampoId),
    CONSTRAINT FK_PropuestasDeCambioCampos_Propuesta FOREIGN KEY (IdPropuesta) REFERENCES dbo.PropuestasDeCambio (IdPropuesta),
    CONSTRAINT CK_PropuestasDeCambioCampos_Campo CHECK (LEN(LTRIM(RTRIM(CampoId))) > 0 AND LEN(LTRIM(RTRIM(CampoEtiqueta))) > 0)
);

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
    Fecha datetime2(0) NOT NULL CONSTRAINT DF_RegistrosRevisionHistorial_Fecha DEFAULT (SYSUTCDATETIME()),
    MetadataJson nvarchar(max) NULL,
    OrganizacionId int NULL,
    OrganizacionNombre nvarchar(240) NULL,
    ResponsableNombre nvarchar(240) NULL,
    ActorNombre nvarchar(240) NULL,
    CONSTRAINT PK_RegistrosRevisionHistorial PRIMARY KEY (IdRevisionHistorial),
    CONSTRAINT CK_RegistrosRevisionHistorial_EstadoNuevo
        CHECK (EstadoNuevo IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'rechazado',
                               N'archivado', N'pendiente_revision', N'validada', N'observada', N'retirado'))
);
CREATE INDEX IX_RegistrosRevisionHistorial_ModuloRegistro ON dbo.RegistrosRevisionHistorial (ModuloId, RegistroId, Fecha DESC);
CREATE INDEX IX_RegistrosRevisionHistorial_EstadoFecha ON dbo.RegistrosRevisionHistorial (EstadoNuevo, Fecha DESC);
GO
