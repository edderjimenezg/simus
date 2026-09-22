/*
    Limpiar un buzón no borra el hecho administrativo. Esta fecha deja de mostrar el aviso
    únicamente para su destinatario, después de haberlo leído, y conserva la traza original.
*/
IF COL_LENGTH(N'dbo.Notificaciones', N'FechaOcultacion') IS NULL
BEGIN
    ALTER TABLE dbo.Notificaciones ADD FechaOcultacion datetime2(0) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Notificaciones')
      AND name = N'IX_Notificaciones_BuzonVisible'
)
BEGIN
    CREATE INDEX IX_Notificaciones_BuzonVisible
        ON dbo.Notificaciones (UsuarioDestinatarioId, FechaOcultacion, FechaCreacion DESC);
END;
