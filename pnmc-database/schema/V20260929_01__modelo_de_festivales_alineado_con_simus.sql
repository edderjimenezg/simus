/*
  Deja el modelo de Festivales y sus tablas maestras igual al que instala la línea de
  migraciones de la base SIMUS del Ministerio (pnmc-database/simus-ministerio).

  - Festivales pierde cinco columnas que ninguna pieza lee ni escribe: Activo, IdUsuarioCreador
    (la autoría vive en ProcedenciasDeRegistro), TipoOrganizadorId y Director (pertenecen a la
    edición) y FechaEnvioARevision.
  - Festival, versión y edición solo admiten estados editoriales, y la edición exige fuentes de
    financiación distintas.
  - Las tablas «…DeRegistro» y EnviosDeRevision usan el mismo ancho de ModuloId y RegistroId que
    revisiones, propuestas y procedencia. Una localización se distingue también por su zona, una
    entidad aliada tiene que ser identificable y cada catálogo tiene índice para su clave foránea.
  - Se retiran índices duplicados y se corrigen los nombres de restricciones que conservaban el
    nombre de la tabla anterior.
  - RegionesOcad toma la forma común de los catálogos; San Andrés (88) entra en la región Caribe.
  - Se completan datos de referencia en bases ya sembradas: tipo de organizador «Otro» y fichas
    conceptuales de todos los territorios y prácticas.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Festivales ------------------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Festivales_Estado_Activo' AND object_id = OBJECT_ID(N'dbo.Festivales'))
    DROP INDEX IX_Festivales_Estado_Activo ON dbo.Festivales;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Festivales_IdUsuarioCreador' AND object_id = OBJECT_ID(N'dbo.Festivales'))
    DROP INDEX IX_Festivales_IdUsuarioCreador ON dbo.Festivales;
IF OBJECT_ID(N'dbo.FK_Festivales_UsuarioCreador', N'F') IS NOT NULL
    ALTER TABLE dbo.Festivales DROP CONSTRAINT FK_Festivales_UsuarioCreador;
IF OBJECT_ID(N'dbo.FK_Festivales_TipoOrganizador', N'F') IS NOT NULL
    ALTER TABLE dbo.Festivales DROP CONSTRAINT FK_Festivales_TipoOrganizador;

DECLARE @Sql nvarchar(max);
SELECT @Sql = STRING_AGG(CAST(N'ALTER TABLE dbo.Festivales DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' AS nvarchar(max)), N' ')
FROM sys.default_constraints AS dc
JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Festivales')
  AND c.name IN (N'Activo', N'IdUsuarioCreador', N'TipoOrganizadorId', N'Director', N'FechaEnvioARevision');
IF @Sql IS NOT NULL EXEC sys.sp_executesql @Sql;

SELECT @Sql = STRING_AGG(CAST(N'ALTER TABLE dbo.Festivales DROP COLUMN ' + QUOTENAME(c.name) + N';' AS nvarchar(max)), N' ')
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'dbo.Festivales')
  AND c.name IN (N'Activo', N'IdUsuarioCreador', N'TipoOrganizadorId', N'Director', N'FechaEnvioARevision');
IF @Sql IS NOT NULL EXEC sys.sp_executesql @Sql;
GO

CREATE INDEX IX_Festivales_EstadoRegistro ON dbo.Festivales (EstadoRegistro);

ALTER TABLE dbo.Festivales WITH CHECK ADD CONSTRAINT CK_Festivales_EstadoRegistro
    CHECK (EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado'));

ALTER TABLE dbo.VersionesFestival WITH CHECK CHECK CONSTRAINT FK_VersionesFestival_EstadosContenido;
ALTER TABLE dbo.VersionesFestival WITH CHECK ADD CONSTRAINT CK_VersionesFestival_EstadoRegistro
    CHECK (EstadoRegistro IS NULL OR EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado'));

ALTER TABLE dbo.EdicionesFestival WITH CHECK ADD CONSTRAINT CK_EdicionesFestival_EstadoRegistro
    CHECK (EstadoRegistro IN (N'borrador', N'en_revision', N'ajustes_solicitados', N'aprobado', N'publicado', N'archivado', N'rechazado'));
ALTER TABLE dbo.EdicionesFestival WITH CHECK ADD CONSTRAINT CK_EdicionesFestival_FuentesDistintas
    CHECK (FuenteFinanciacionPrimariaId IS NULL OR FuenteFinanciacionSecundariaId IS NULL
        OR FuenteFinanciacionPrimariaId <> FuenteFinanciacionSecundariaId);
GO

-- Tablas «…DeRegistro» ---------------------------------------------------------------------------
/*
  ModuloId y RegistroId participan en la restricción única y en el CHECK de módulo, y FechaCreacion
  tiene un DEFAULT: los tres se quitan, se cambia el tipo de las columnas y se vuelven a crear.
*/
DECLARE @Tablas TABLE (Tabla sysname PRIMARY KEY, Unica nvarchar(400) NOT NULL, IndiceCatalogo nvarchar(400) NULL);
INSERT INTO @Tablas VALUES
    (N'TerritoriosSonorosDeRegistro',       N'ModuloId, RegistroId, IdTerritorioSonoro',       N'IdTerritorioSonoro'),
    (N'PracticasMusicalesDeRegistro',       N'ModuloId, RegistroId, IdPracticaMusical',        N'IdPracticaMusical'),
    (N'ExpresionesArtisticasDeRegistro',    N'ModuloId, RegistroId, IdExpresionArtistica',     N'IdExpresionArtistica'),
    (N'ModalidadesParticipacionDeRegistro', N'ModuloId, RegistroId, IdModalidadParticipacion', N'IdModalidadParticipacion'),
    (N'TiposIngresoDeRegistro',             N'ModuloId, RegistroId, IdTipoIngreso',            N'IdTipoIngreso'),
    (N'LocalizacionesDeRegistro',           N'ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio, ZonaUrbanoRuralId', NULL),
    (N'ArchivosDeRegistro',                 N'ModuloId, RegistroId, RolArchivo, OrdenVisualizacion', NULL),
    (N'EntidadesAliadasDeRegistro',         N'', NULL);

DECLARE @Tabla sysname, @Unica nvarchar(400), @IndiceCatalogo nvarchar(400), @Sql nvarchar(max), @Modulo nvarchar(max);
SET @Modulo = N'(ModuloId IN (N''festivales'', N''versiones-festival'', N''ediciones-festival'', N''ediciones-mercado''))';

DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR SELECT Tabla, Unica, IndiceCatalogo FROM @Tablas;
OPEN tablas;
FETCH NEXT FROM tablas INTO @Tabla, @Unica, @IndiceCatalogo;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'';
    SELECT @Sql += N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
    FROM sys.default_constraints AS dc
    JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.' + @Tabla) AND c.name = N'FechaCreacion';

    IF OBJECT_ID(N'dbo.UQ_' + @Tabla, N'UQ') IS NOT NULL
        SET @Sql += N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' DROP CONSTRAINT ' + QUOTENAME(N'UQ_' + @Tabla) + N';';
    IF OBJECT_ID(N'dbo.CK_' + @Tabla + N'_Modulo', N'C') IS NOT NULL
        SET @Sql += N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' DROP CONSTRAINT ' + QUOTENAME(N'CK_' + @Tabla + N'_Modulo') + N';';

    SET @Sql += N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' ALTER COLUMN ModuloId nvarchar(80) NOT NULL;'
              + N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' ALTER COLUMN RegistroId nvarchar(120) NOT NULL;'
              + N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' ALTER COLUMN FechaCreacion datetime2(0) NOT NULL;'
              + N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' ADD CONSTRAINT ' + QUOTENAME(N'DF_' + @Tabla + N'_FechaCreacion')
              + N' DEFAULT (SYSUTCDATETIME()) FOR FechaCreacion;'
              + N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(N'CK_' + @Tabla + N'_Modulo')
              + N' CHECK ' + @Modulo + N';';
    IF @Unica <> N''
        SET @Sql += N'ALTER TABLE dbo.' + QUOTENAME(@Tabla) + N' ADD CONSTRAINT ' + QUOTENAME(N'UQ_' + @Tabla) + N' UNIQUE (' + @Unica + N');';
    IF @IndiceCatalogo IS NOT NULL
        SET @Sql += N'CREATE INDEX ' + QUOTENAME(N'IX_' + @Tabla + N'_Catalogo') + N' ON dbo.' + QUOTENAME(@Tabla) + N' (' + @IndiceCatalogo + N');';

    EXEC sys.sp_executesql @Sql;
    FETCH NEXT FROM tablas INTO @Tabla, @Unica, @IndiceCatalogo;
END
CLOSE tablas;
DEALLOCATE tablas;
GO

CREATE INDEX IX_LocalizacionesDeRegistro_Municipio ON dbo.LocalizacionesDeRegistro (CodigoDepartamento, CodigoMunicipio);
CREATE INDEX IX_EntidadesAliadasDeRegistro_Registro ON dbo.EntidadesAliadasDeRegistro (ModuloId, RegistroId);
CREATE INDEX IX_ArchivosDeRegistro_Archivo ON dbo.ArchivosDeRegistro (ArchivoId) WHERE ArchivoId IS NOT NULL;
ALTER TABLE dbo.EntidadesAliadasDeRegistro WITH CHECK ADD CONSTRAINT CK_EntidadesAliadasDeRegistro_Identificable
    CHECK (EntidadId IS NOT NULL OR NULLIF(LTRIM(RTRIM(NombreEntidadAliada)), N'') IS NOT NULL);
GO

-- Envíos de revisión y procedencia ---------------------------------------------------------------
DROP INDEX UQ_EnviosDeRevision_Numero ON dbo.EnviosDeRevision;
DROP INDEX IX_EnviosDeRevision_Fecha ON dbo.EnviosDeRevision;
ALTER TABLE dbo.EnviosDeRevision ALTER COLUMN ModuloId nvarchar(80) NOT NULL;
ALTER TABLE dbo.EnviosDeRevision ALTER COLUMN RegistroId nvarchar(120) NOT NULL;
CREATE UNIQUE INDEX UQ_EnviosDeRevision_Numero ON dbo.EnviosDeRevision (ModuloId, RegistroId, NumeroEnvio);
CREATE INDEX IX_EnviosDeRevision_Fecha ON dbo.EnviosDeRevision (ModuloId, RegistroId, FechaEnvio DESC);
EXEC sys.sp_rename N'dbo.CK_EnviosRevisionFestival_Numero', N'CK_EnviosDeRevision_Numero', N'OBJECT';
EXEC sys.sp_rename N'dbo.CK_EnviosRevisionFestival_Datos', N'CK_EnviosDeRevision_Datos', N'OBJECT';

-- IX_ProcedenciasDeRegistro_Modulo repetía exactamente la restricción única.
DROP INDEX IX_ProcedenciasDeRegistro_Modulo ON dbo.ProcedenciasDeRegistro;
EXEC sys.sp_rename N'dbo.ProcedenciasDeRegistro.IX_ProcedenciaDeRegistros_Usuario', N'IX_ProcedenciasDeRegistro_Usuario', N'INDEX';
EXEC sys.sp_rename N'dbo.CK_ProcedenciaDeRegistros_Contexto', N'CK_ProcedenciasDeRegistro_Contexto', N'OBJECT';
EXEC sys.sp_rename N'dbo.FK_ProcedenciaDeRegistros_Organizacion', N'FK_ProcedenciasDeRegistro_Organizacion', N'OBJECT';
EXEC sys.sp_rename N'dbo.FK_ProcedenciaDeRegistros_Usuario', N'FK_ProcedenciasDeRegistro_Usuario', N'OBJECT';
GO

-- Entidades --------------------------------------------------------------------------------------
-- UX_Entidades_EsInstitucional repetía UQ_Entidades_EsInstitucional. El DEFAULT 'borrador' no
-- cumplía CK_Entidades_EstadoRegistro: el valor por omisión pasa a ser el estado inicial real.
DROP INDEX UX_Entidades_EsInstitucional ON dbo.Entidades;

DECLARE @Sql nvarchar(max);
SELECT @Sql = N'ALTER TABLE dbo.Entidades DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints AS dc
JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Entidades') AND c.name = N'EstadoRegistro';
IF @Sql IS NOT NULL EXEC sys.sp_executesql @Sql;

ALTER TABLE dbo.Entidades ADD CONSTRAINT DF_Entidades_EstadoRegistro DEFAULT (N'pendiente_de_confirmacion') FOR EstadoRegistro;
GO

-- Regiones OCAD ----------------------------------------------------------------------------------
DECLARE @Sql nvarchar(max);
SELECT @Sql = N'ALTER TABLE dbo.RegionesOcad DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints AS dc
JOIN sys.columns AS c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.RegionesOcad') AND c.name = N'OrdenVisualizacion';
IF @Sql IS NOT NULL EXEC sys.sp_executesql @Sql;

ALTER TABLE dbo.RegionesOcad DROP CONSTRAINT UQ_RegionesOcad_Slug;
ALTER TABLE dbo.RegionesOcad ALTER COLUMN NombreRegionOcad nvarchar(140) NOT NULL;
ALTER TABLE dbo.RegionesOcad ALTER COLUMN Slug nvarchar(160) NOT NULL;
ALTER TABLE dbo.RegionesOcad ALTER COLUMN Descripcion nvarchar(800) NULL;
GO

UPDATE dbo.RegionesOcad SET OrdenVisualizacion = IdRegionOcad WHERE OrdenVisualizacion <= 0;
ALTER TABLE dbo.RegionesOcad ADD CONSTRAINT UQ_RegionesOcad_Nombre UNIQUE (NombreRegionOcad);
ALTER TABLE dbo.RegionesOcad ADD CONSTRAINT UQ_RegionesOcad_Slug UNIQUE (Slug);
ALTER TABLE dbo.RegionesOcad WITH CHECK ADD CONSTRAINT CK_RegionesOcad_Orden CHECK (OrdenVisualizacion > 0);
ALTER TABLE dbo.DepartamentosRegionOcad WITH CHECK ADD CONSTRAINT CK_DepartamentosRegionOcad_Codigo
    CHECK (CodigoDepartamento NOT LIKE '%[^0-9]%');

INSERT INTO dbo.DepartamentosRegionOcad (CodigoDepartamento, RegionOcadId)
SELECT '88', IdRegionOcad FROM dbo.RegionesOcad
WHERE Slug = N'caribe' AND NOT EXISTS (SELECT 1 FROM dbo.DepartamentosRegionOcad WHERE CodigoDepartamento = '88');
GO

-- Datos de referencia en bases ya sembradas ------------------------------------------------------
IF EXISTS (SELECT 1 FROM dbo.TiposOrganizador) AND NOT EXISTS (SELECT 1 FROM dbo.TiposOrganizador WHERE Slug = N'otro')
    INSERT INTO dbo.TiposOrganizador (NombreTipoOrganizador, Slug, OrdenVisualizacion)
    SELECT N'Otro', N'otro', MAX(OrdenVisualizacion) + 1 FROM dbo.TiposOrganizador;

INSERT INTO dbo.FichasConceptualesTerritoriosSonoros (TerritorioSonoroId)
SELECT t.IdTerritorioSonoro FROM dbo.TerritoriosSonoros AS t
WHERE NOT EXISTS (SELECT 1 FROM dbo.FichasConceptualesTerritoriosSonoros AS f WHERE f.TerritorioSonoroId = t.IdTerritorioSonoro);

INSERT INTO dbo.FichasConceptualesPracticasMusicales (PracticaMusicalId)
SELECT p.IdPracticaMusical FROM dbo.PracticasMusicales AS p
WHERE NOT EXISTS (SELECT 1 FROM dbo.FichasConceptualesPracticasMusicales AS f WHERE f.PracticaMusicalId = p.IdPracticaMusical);
GO
