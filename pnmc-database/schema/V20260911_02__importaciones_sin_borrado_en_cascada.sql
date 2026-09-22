SET QUOTED_IDENTIFIER ON;
GO

/*
    Las trazas de importación son evidencia administrativa. Ninguna relación las borra en
    cascada: retirar un lote o una fila exige una operación de retención explícita y auditable.
*/

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FilasImportacion_Lote')
BEGIN
    ALTER TABLE dbo.FilasImportacion DROP CONSTRAINT FK_FilasImportacion_Lote;
    ALTER TABLE dbo.FilasImportacion WITH CHECK ADD CONSTRAINT FK_FilasImportacion_Lote
        FOREIGN KEY (IdLoteImportacion) REFERENCES dbo.LotesImportacion (IdLoteImportacion);
END;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_HallazgosImportacion_Fila')
BEGIN
    ALTER TABLE dbo.HallazgosImportacion DROP CONSTRAINT FK_HallazgosImportacion_Fila;
    ALTER TABLE dbo.HallazgosImportacion WITH CHECK ADD CONSTRAINT FK_HallazgosImportacion_Fila
        FOREIGN KEY (IdFilaImportacion) REFERENCES dbo.FilasImportacion (IdFilaImportacion);
END;
GO

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DecisionesImportacion_Lote')
BEGIN
    ALTER TABLE dbo.DecisionesImportacion DROP CONSTRAINT FK_DecisionesImportacion_Lote;
    ALTER TABLE dbo.DecisionesImportacion WITH CHECK ADD CONSTRAINT FK_DecisionesImportacion_Lote
        FOREIGN KEY (IdLoteImportacion) REFERENCES dbo.LotesImportacion (IdLoteImportacion);
END;
GO
