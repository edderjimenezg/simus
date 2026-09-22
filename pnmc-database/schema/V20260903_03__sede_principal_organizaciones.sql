/* La sede es una ubicación; el alcance sigue siendo una propiedad opcional de la organización. */
IF COL_LENGTH(N'dbo.Entidades', N'CodigoDepartamentoSede') IS NULL
    ALTER TABLE dbo.Entidades ADD CodigoDepartamentoSede char(2) NULL;
GO

IF COL_LENGTH(N'dbo.Entidades', N'CodigoMunicipioSede') IS NULL
    ALTER TABLE dbo.Entidades ADD CodigoMunicipioSede char(5) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Entidades_Sede_Divipola')
    ALTER TABLE dbo.Entidades ADD CONSTRAINT FK_Entidades_Sede_Divipola
        FOREIGN KEY (CodigoDepartamentoSede, CodigoMunicipioSede)
        REFERENCES dbo.Divipola(CodigoDepartamento, CodigoMunicipio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_Sede_Completa')
    ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_Sede_Completa CHECK (
        (CodigoDepartamentoSede IS NULL AND CodigoMunicipioSede IS NULL)
        OR (CodigoDepartamentoSede IS NOT NULL AND CodigoMunicipioSede IS NOT NULL)
    );
GO
