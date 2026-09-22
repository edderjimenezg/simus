/*
    SIMUS · Estado de registro y dirección artística de EdicionesFestival

    Una Edición temporal tiene dos estados diferentes y ambos son necesarios:
      - EstadoRegistro: su ciclo de gobierno (borrador, revisión, ajustes, publicado...).
      - Estado: su realización operativa (en preparación, programada, realizada, cancelada).

    No se crea otra tabla de estados. EstadoRegistro reutiliza EstadosContenido, igual que
    Festivales y VersionesFestival. El guion es aditivo: conserva todos los valores históricos y
    evita copiar datos entre EdicionesFestival y VersionesFestival.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.EdicionesFestival', N'Director') IS NULL
BEGIN
    ALTER TABLE dbo.EdicionesFestival ADD Director nvarchar(240) NULL;
END;
GO

IF COL_LENGTH(N'dbo.EdicionesFestival', N'EstadoRegistro') IS NULL
BEGIN
    ALTER TABLE dbo.EdicionesFestival ADD EstadoRegistro nvarchar(80) NULL;
END;
GO

-- Las filas existentes son registros ya creados pero sin circuito histórico. Se conservan como
-- borradores para que ninguna publicación se infiera por accidente.
UPDATE dbo.EdicionesFestival
SET EstadoRegistro = N'borrador'
WHERE EstadoRegistro IS NULL OR LTRIM(RTRIM(EstadoRegistro)) = N'';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EdicionesFestival_EstadosContenido'
)
BEGIN
    ALTER TABLE dbo.EdicionesFestival WITH CHECK
        ADD CONSTRAINT FK_EdicionesFestival_EstadosContenido
        FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado);
END;
GO

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'EdicionesFestival'
      AND COLUMN_NAME = N'EstadoRegistro' AND IS_NULLABLE = N'YES'
)
BEGIN
    ALTER TABLE dbo.EdicionesFestival ALTER COLUMN EstadoRegistro nvarchar(80) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EdicionesFestival_EstadoRegistro' AND object_id = OBJECT_ID(N'dbo.EdicionesFestival', N'U')
)
BEGIN
    CREATE INDEX IX_EdicionesFestival_EstadoRegistro
        ON dbo.EdicionesFestival (EstadoRegistro, FestivalId);
END;
GO
