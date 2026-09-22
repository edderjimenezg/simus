SET QUOTED_IDENTIFIER ON;
GO

/*
    Una previsualización es material temporal durante 30 días; al expirar conserva
    cabecera y conteos, pero no filas ni datos normalizados. Un lote aplicado conserva evidencia
    administrativa, aunque la copia redundante de correo y teléfono se retira al aplicarlo.

    El plazo vive en configuración y no en un CHECK: cambiarlo no reescribe la historia. La fecha
    de depuración sí queda en la fila para que la interfaz y la auditoría expliquen qué ocurrió.
*/

IF COL_LENGTH(N'dbo.LotesImportacion', N'FechaDepuracion') IS NULL
    ALTER TABLE dbo.LotesImportacion ADD FechaDepuracion datetime2(3) NULL;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LotesImportacion_Estado')
    ALTER TABLE dbo.LotesImportacion DROP CONSTRAINT CK_LotesImportacion_Estado;
GO

ALTER TABLE dbo.LotesImportacion WITH CHECK ADD CONSTRAINT CK_LotesImportacion_Estado
    CHECK (Estado IN (N'previsualizado', N'aplicando', N'aplicado', N'depurando', N'expirado', N'conflicto'));
GO
