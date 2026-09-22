/*
    Separa el buzón externo del institucional cuando una misma persona conserva ambos roles.
    El ámbito pertenece a la notificación, no se deduce de su texto ni del nombre del evento.
*/

IF COL_LENGTH(N'dbo.Notificaciones', N'AmbitoAcceso') IS NULL
BEGIN
    ALTER TABLE dbo.Notificaciones
        ADD AmbitoAcceso nvarchar(20) NULL;
END;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Notificaciones')
      AND name = N'AmbitoAcceso'
      AND is_nullable = 1
)
BEGIN
    UPDATE dbo.Notificaciones
       SET AmbitoAcceso = CASE
           WHEN TipoEvento = N'FestivalRecibidoParaRevision' THEN N'institutional'
           ELSE N'external'
       END
     WHERE AmbitoAcceso IS NULL;

    ALTER TABLE dbo.Notificaciones
        ALTER COLUMN AmbitoAcceso nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Notificaciones')
      AND name = N'DF_Notificaciones_AmbitoAcceso'
)
BEGIN
    ALTER TABLE dbo.Notificaciones
        ADD CONSTRAINT DF_Notificaciones_AmbitoAcceso DEFAULT (N'external') FOR AmbitoAcceso;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Notificaciones')
      AND name = N'CK_Notificaciones_AmbitoAcceso'
)
BEGIN
    ALTER TABLE dbo.Notificaciones
        ADD CONSTRAINT CK_Notificaciones_AmbitoAcceso
        CHECK (AmbitoAcceso IN (N'external', N'institutional'));
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Notificaciones')
      AND name = N'IX_Notificaciones_AmbitoBuzon'
)
BEGIN
    CREATE INDEX IX_Notificaciones_AmbitoBuzon
        ON dbo.Notificaciones (UsuarioDestinatarioId, AmbitoAcceso, FechaOcultacion, FechaCreacion DESC);
END;
