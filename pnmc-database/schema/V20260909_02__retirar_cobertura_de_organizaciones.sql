/*
    La organización tiene sede, no "nivel de cobertura".

    El alcance se declara por proceso: un Festival puede ser municipal, departamental o nacional
    sin convertir a la entidad que lo administra en una de esas categorías. Las tres columnas
    heredadas se retiran de dbo.Entidades; CodigoDepartamentoSede y CodigoMunicipioSede permanecen
    como la única ubicación propia de la organización.
*/

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Entidades_Divipola')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT FK_Entidades_Divipola;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_NivelCobertura')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_NivelCobertura;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Entidades_Territorio' AND object_id = OBJECT_ID(N'dbo.Entidades'))
    DROP INDEX IX_Entidades_Territorio ON dbo.Entidades;
GO

DECLARE @restricciones nvarchar(max) = N'';
SELECT @restricciones = @restricciones + N'ALTER TABLE dbo.Entidades DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Entidades')
  AND c.name = N'NivelCobertura';
IF @restricciones <> N'' EXEC sp_executesql @restricciones;
GO

IF COL_LENGTH(N'dbo.Entidades', N'NivelCobertura') IS NOT NULL
    ALTER TABLE dbo.Entidades DROP COLUMN NivelCobertura;
GO

IF COL_LENGTH(N'dbo.Entidades', N'CodigoDepartamento') IS NOT NULL
    ALTER TABLE dbo.Entidades DROP COLUMN CodigoDepartamento;
GO

IF COL_LENGTH(N'dbo.Entidades', N'CodigoMunicipio') IS NOT NULL
    ALTER TABLE dbo.Entidades DROP COLUMN CodigoMunicipio;
GO
