/*
    PNMC - Bases maestras estaticas
    Fase: reconstruccion local desde cero

    Criterios:
    - Nombres de tablas y campos en espanol.
    - Sin trazabilidad ni estados para estas maestras.
    - Sin tablas de staging/importacion.
    - Scripts idempotentes para ejecucion local repetible.
*/

IF OBJECT_ID(N'dbo.Divipola', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Divipola
    (
        CodigoDepartamento char(2) NOT NULL,
        NombreDepartamento nvarchar(120) NOT NULL,
        CodigoMunicipio char(5) NOT NULL,
        NombreMunicipio nvarchar(160) NOT NULL,
        TipoTerritorio nvarchar(80) NULL,
        Latitud decimal(9, 6) NULL,
        Longitud decimal(9, 6) NULL,
        CONSTRAINT PK_Divipola PRIMARY KEY (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT UQ_Divipola_CodigoMunicipio UNIQUE (CodigoMunicipio),
        CONSTRAINT CK_Divipola_CodigoDepartamento_Formato CHECK (CodigoDepartamento NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Divipola_CodigoMunicipio_Formato CHECK (CodigoMunicipio NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Divipola_CodigoMunicipio_Departamento CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Divipola_Departamento_NombreMunicipio' AND object_id = OBJECT_ID(N'dbo.Divipola'))
BEGIN
    CREATE INDEX IX_Divipola_Departamento_NombreMunicipio
    ON dbo.Divipola (CodigoDepartamento, NombreMunicipio);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Divipola_NombreDepartamento' AND object_id = OBJECT_ID(N'dbo.Divipola'))
BEGIN
    CREATE INDEX IX_Divipola_NombreDepartamento
    ON dbo.Divipola (NombreDepartamento);
END;

IF OBJECT_ID(N'dbo.TerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TerritoriosSonoros
    (
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
END;

IF OBJECT_ID(N'dbo.PracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PracticasMusicales
    (
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
END;

IF OBJECT_ID(N'dbo.TiposRegistroEcosistema', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TiposRegistroEcosistema
    (
        IdTipoRegistroEcosistema int IDENTITY(1,1) NOT NULL,
        CodigoTipoRegistro nvarchar(80) NOT NULL,
        NombreTipoRegistro nvarchar(140) NOT NULL,
        Descripcion nvarchar(800) NULL,
        CONSTRAINT PK_TiposRegistroEcosistema PRIMARY KEY (IdTipoRegistroEcosistema),
        CONSTRAINT UQ_TiposRegistroEcosistema_Codigo UNIQUE (CodigoTipoRegistro),
        CONSTRAINT UQ_TiposRegistroEcosistema_Nombre UNIQUE (NombreTipoRegistro),
        CONSTRAINT CK_TiposRegistroEcosistema_Codigo_Formato CHECK (
            CodigoTipoRegistro = LOWER(CodigoTipoRegistro)
            AND CodigoTipoRegistro NOT LIKE '% %'
        )
    );
END;
