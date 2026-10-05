/*
  Catálogos que no pertenecen a ningún módulo: territorios sonoros y prácticas musicales con su
  ficha conceptual, regiones OCAD con su correspondencia por departamento, y los vocabularios de
  localización y de naturaleza de una entidad.

  Todos tienen la misma forma: nombre y slug únicos, descripción y orden de presentación. Los
  módulos los enlazan por las tablas «…DeRegistro», nunca con una copia propia del catálogo.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.TerritoriosSonoros (
    IdTerritorioSonoro int IDENTITY(1,1) NOT NULL,
    NombreTerritorioSonoro nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_TerritoriosSonoros PRIMARY KEY (IdTerritorioSonoro),
    CONSTRAINT UQ_TerritoriosSonoros_Nombre UNIQUE (NombreTerritorioSonoro),
    CONSTRAINT UQ_TerritoriosSonoros_Slug UNIQUE (Slug),
    CONSTRAINT CK_TerritoriosSonoros_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.FichasConceptualesTerritoriosSonoros (
    TerritorioSonoroId int NOT NULL,
    DefinicionBreve nvarchar(800) NULL,
    DefinicionAmpliada nvarchar(max) NULL,
    DescripcionConceptual nvarchar(max) NULL,
    Caracteristicas nvarchar(max) NULL,
    RelacionTerritorial nvarchar(max) NULL,
    Contextos nvarchar(max) NULL,
    Ejemplos nvarchar(max) NULL,
    Fuentes nvarchar(max) NULL,
    RecursoVisualUrl nvarchar(1000) NULL,
    TextoAlternativoRecurso nvarchar(500) NULL,
    FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_FichasConceptualesTerritoriosSonoros_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_FichasConceptualesTerritoriosSonoros PRIMARY KEY (TerritorioSonoroId),
    CONSTRAINT FK_FichasConceptualesTerritoriosSonoros_Territorio
        FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro)
);

CREATE TABLE dbo.PracticasMusicales (
    IdPracticaMusical int IDENTITY(1,1) NOT NULL,
    NombrePracticaMusical nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_PracticasMusicales PRIMARY KEY (IdPracticaMusical),
    CONSTRAINT UQ_PracticasMusicales_Nombre UNIQUE (NombrePracticaMusical),
    CONSTRAINT UQ_PracticasMusicales_Slug UNIQUE (Slug),
    CONSTRAINT CK_PracticasMusicales_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.FichasConceptualesPracticasMusicales (
    PracticaMusicalId int NOT NULL,
    DefinicionBreve nvarchar(800) NULL,
    DefinicionAmpliada nvarchar(max) NULL,
    DescripcionConceptual nvarchar(max) NULL,
    Caracteristicas nvarchar(max) NULL,
    Contextos nvarchar(max) NULL,
    Ejemplos nvarchar(max) NULL,
    Fuentes nvarchar(max) NULL,
    RecursoVisualUrl nvarchar(1000) NULL,
    TextoAlternativoRecurso nvarchar(500) NULL,
    FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_FichasConceptualesPracticasMusicales_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_FichasConceptualesPracticasMusicales PRIMARY KEY (PracticaMusicalId),
    CONSTRAINT FK_FichasConceptualesPracticas_Practica
        FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales (IdPracticaMusical)
);

CREATE TABLE dbo.RegionesOcad (
    IdRegionOcad int IDENTITY(1,1) NOT NULL,
    NombreRegionOcad nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_RegionesOcad PRIMARY KEY (IdRegionOcad),
    CONSTRAINT UQ_RegionesOcad_Nombre UNIQUE (NombreRegionOcad),
    CONSTRAINT UQ_RegionesOcad_Slug UNIQUE (Slug),
    CONSTRAINT CK_RegionesOcad_Orden CHECK (OrdenVisualizacion > 0)
);

-- La región OCAD no se captura: se deriva del departamento de cada localización.
CREATE TABLE dbo.DepartamentosRegionOcad (
    CodigoDepartamento char(2) NOT NULL,
    RegionOcadId int NOT NULL,
    CONSTRAINT PK_DepartamentosRegionOcad PRIMARY KEY (CodigoDepartamento),
    CONSTRAINT FK_DepartamentosRegionOcad_RegionesOcad FOREIGN KEY (RegionOcadId) REFERENCES dbo.RegionesOcad (IdRegionOcad),
    CONSTRAINT CK_DepartamentosRegionOcad_Codigo CHECK (CodigoDepartamento NOT LIKE '%[^0-9]%')
);
CREATE INDEX IX_DepartamentosRegionOcad_Region ON dbo.DepartamentosRegionOcad (RegionOcadId) INCLUDE (CodigoDepartamento);

CREATE TABLE dbo.ZonasUrbanoRural (
    IdZonaUrbanoRural int IDENTITY(1,1) NOT NULL,
    NombreZonaUrbanoRural nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_ZonasUrbanoRural PRIMARY KEY (IdZonaUrbanoRural),
    CONSTRAINT UQ_ZonasUrbanoRural_Nombre UNIQUE (NombreZonaUrbanoRural),
    CONSTRAINT UQ_ZonasUrbanoRural_Slug UNIQUE (Slug),
    CONSTRAINT CK_ZonasUrbanoRural_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.TitulacionesColectivas (
    IdTitulacionColectiva int IDENTITY(1,1) NOT NULL,
    NombreTitulacionColectiva nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_TitulacionesColectivas PRIMARY KEY (IdTitulacionColectiva),
    CONSTRAINT UQ_TitulacionesColectivas_Nombre UNIQUE (NombreTitulacionColectiva),
    CONSTRAINT UQ_TitulacionesColectivas_Slug UNIQUE (Slug),
    CONSTRAINT CK_TitulacionesColectivas_Orden CHECK (OrdenVisualizacion > 0)
);

CREATE TABLE dbo.NaturalezasEntidad (
    IdNaturalezaEntidad int IDENTITY(1,1) NOT NULL,
    NombreNaturalezaEntidad nvarchar(140) NOT NULL,
    Slug nvarchar(160) NOT NULL,
    Descripcion nvarchar(800) NULL,
    OrdenVisualizacion int NOT NULL,
    CONSTRAINT PK_NaturalezasEntidad PRIMARY KEY (IdNaturalezaEntidad),
    CONSTRAINT UQ_NaturalezasEntidad_Nombre UNIQUE (NombreNaturalezaEntidad),
    CONSTRAINT UQ_NaturalezasEntidad_Slug UNIQUE (Slug),
    CONSTRAINT CK_NaturalezasEntidad_Orden CHECK (OrdenVisualizacion > 0)
);
GO
