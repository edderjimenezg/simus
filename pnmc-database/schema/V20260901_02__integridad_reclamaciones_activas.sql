/* Integridad de concurrencia para reclamaciones administrativas. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.ReclamacionesAdministracion', N'EsActiva') IS NULL
BEGIN
    ALTER TABLE dbo.ReclamacionesAdministracion ADD EsActiva AS
        (CONVERT(bit, CASE WHEN Estado IN (N'borrador',N'enviada',N'en_revision',N'requiere_aclaracion',N'aclaracion_enviada',N'aprobada') THEN 1 ELSE 0 END)) PERSISTED;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ReclamacionesAdministracion') AND name=N'IX_ReclamacionesAdministracion_Activa')
BEGIN
    CREATE INDEX IX_ReclamacionesAdministracion_Activa
        ON dbo.ReclamacionesAdministracion(Dominio,RegistroCanonicoId,EsActiva);
END;
