/*
    SIMUS · Identidad de quien responde y procedencia territorial

    Los nombres segmentados se conservan junto al nombre de presentación legado. La fuente del
    catálogo se registra separada de DIVIPOLA para poder cambiar de corte sin inventar procedencia.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.EntidadesResponsable', N'ResponsablePrimerNombre') IS NULL
BEGIN
    ALTER TABLE dbo.EntidadesResponsable ADD
        ResponsablePrimerNombre nvarchar(80) NULL,
        ResponsableSegundoNombre nvarchar(80) NULL,
        ResponsablePrimerApellido nvarchar(80) NULL,
        ResponsableSegundoApellido nvarchar(80) NULL;
END;
GO

IF OBJECT_ID(N'dbo.CatalogosReferenciaFuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CatalogosReferenciaFuente
    (
        CodigoCatalogo nvarchar(80) NOT NULL CONSTRAINT PK_CatalogosReferenciaFuente PRIMARY KEY,
        VersionFuente nvarchar(80) NOT NULL,
        UrlOrigen nvarchar(1000) NOT NULL,
        FechaIncorporacion datetime2(0) NOT NULL,
        CantidadDepartamentos int NULL,
        CantidadMunicipios int NULL
    );
END;
GO
