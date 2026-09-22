/*
    SIMUS · Festival · identificación flexible y visibilidad de Ediciones

    Una Edición es una realización concreta, no una versión anual. Por eso el
    año deja de ser obligatorio ni único: puede identificarse por número,
    nombre y/o fechas. La visibilidad también se nombra de forma explícita,
    separada del estado operativo de la realización.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.EdicionesFestival', N'NumeroEdicion') IS NULL
    ALTER TABLE dbo.EdicionesFestival ADD NumeroEdicion int NULL;
GO

IF COL_LENGTH(N'dbo.EdicionesFestival', N'EstadoVisibilidad') IS NULL
    EXEC(N'ALTER TABLE dbo.EdicionesFestival ADD EstadoVisibilidad nvarchar(40) NULL;');
GO

UPDATE dbo.EdicionesFestival
   SET EstadoVisibilidad = CASE LOWER(EstadoRegistro)
       WHEN N'publicado' THEN N'publicada'
       WHEN N'archivado' THEN N'archivada'
       ELSE N'borrador' END
 WHERE EstadoVisibilidad IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.EdicionesFestival') AND name = N'EstadoVisibilidad' AND is_nullable = 1)
    ALTER TABLE dbo.EdicionesFestival ALTER COLUMN EstadoVisibilidad nvarchar(40) NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_EdicionesFestival_FestivalAnio')
    ALTER TABLE dbo.EdicionesFestival DROP CONSTRAINT UQ_EdicionesFestival_FestivalAnio;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EdicionesFestival_FestivalId_Anio' AND object_id = OBJECT_ID(N'dbo.EdicionesFestival'))
    DROP INDEX IX_EdicionesFestival_FestivalId_Anio ON dbo.EdicionesFestival;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EdicionesFestival_Anio')
    ALTER TABLE dbo.EdicionesFestival DROP CONSTRAINT CK_EdicionesFestival_Anio;
GO

ALTER TABLE dbo.EdicionesFestival ALTER COLUMN Anio int NULL;
ALTER TABLE dbo.EdicionesFestival ALTER COLUMN Nombre nvarchar(240) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EdicionesFestival_Identificacion')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT CK_EdicionesFestival_Identificacion CHECK
    (Anio IS NOT NULL OR NumeroEdicion IS NOT NULL OR NULLIF(LTRIM(RTRIM(Nombre)), N'') IS NOT NULL OR FechaInicio IS NOT NULL OR FechaFin IS NOT NULL);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EdicionesFestival_AnioValido')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT CK_EdicionesFestival_AnioValido CHECK (Anio IS NULL OR Anio BETWEEN 1900 AND 2200);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EdicionesFestival_NumeroValido')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT CK_EdicionesFestival_NumeroValido CHECK (NumeroEdicion IS NULL OR NumeroEdicion > 0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_EdicionesFestival_EstadoVisibilidad')
    ALTER TABLE dbo.EdicionesFestival ADD CONSTRAINT CK_EdicionesFestival_EstadoVisibilidad CHECK (EstadoVisibilidad IN (N'borrador', N'publicada', N'archivada'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EdicionesFestival_Festival_Orden' AND object_id = OBJECT_ID(N'dbo.EdicionesFestival'))
    CREATE INDEX IX_EdicionesFestival_Festival_Orden ON dbo.EdicionesFestival (FestivalId, Anio DESC, NumeroEdicion DESC);
GO
