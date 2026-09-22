/* Garantía para despliegues donde V20260901_01 se aplicó antes de incluir la conciliación. */
IF OBJECT_ID(N'dbo.ReclamacionesConciliaciones', N'U') IS NULL
CREATE TABLE dbo.ReclamacionesConciliaciones (
 IdConciliacion bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
 ReclamacionId bigint NOT NULL, ValoresHistoricosJson nvarchar(max) NOT NULL,
 ValoresBorradorJson nvarchar(max) NOT NULL, SeleccionesJson nvarchar(max) NULL,
 FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_ReclamacionesConciliaciones_Creacion_Garantia DEFAULT (SYSUTCDATETIME()),
 FechaResolucion datetime2(0) NULL,
 CONSTRAINT UQ_ReclamacionesConciliaciones_Reclamacion_Garantia UNIQUE(ReclamacionId),
 CONSTRAINT FK_ReclamacionesConciliaciones_Reclamacion_Garantia FOREIGN KEY(ReclamacionId) REFERENCES dbo.ReclamacionesAdministracion(IdReclamacion)
);
