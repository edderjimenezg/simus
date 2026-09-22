/* SIMUS · evidencia de consentimientos del alta externa */
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.DocumentosLegales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentosLegales
    (
        IdDocumentoLegal int IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentosLegales PRIMARY KEY,
        Codigo nvarchar(80) NOT NULL,
        Titulo nvarchar(300) NOT NULL,
        Version nvarchar(80) NOT NULL,
        UrlPublica nvarchar(1000) NOT NULL,
        Vigente bit NOT NULL CONSTRAINT DF_DocumentosLegales_Vigente DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_DocumentosLegales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_DocumentosLegales_CodigoVersion UNIQUE (Codigo, Version)
    );
END;
GO

IF OBJECT_ID(N'dbo.AceptacionesDocumentosLegales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AceptacionesDocumentosLegales
    (
        IdAceptacionDocumentoLegal bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AceptacionesDocumentosLegales PRIMARY KEY,
        IdUsuario int NOT NULL,
        IdDocumentoLegal int NOT NULL,
        FechaAceptacion datetime2(0) NOT NULL CONSTRAINT DF_AceptacionesDocumentosLegales_FechaAceptacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_AceptacionesDocumentosLegales_UsuarioDocumento UNIQUE (IdUsuario, IdDocumentoLegal),
        CONSTRAINT FK_AceptacionesDocumentosLegales_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios(IdUsuario),
        CONSTRAINT FK_AceptacionesDocumentosLegales_Documento FOREIGN KEY (IdDocumentoLegal) REFERENCES dbo.DocumentosLegales(IdDocumentoLegal)
    );
END;
GO

MERGE dbo.DocumentosLegales AS destino
USING (VALUES
 (N'terminos_uso', N'Términos y condiciones del portal institucional', N'PL-GSI-001 v0', N'https://www.mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/PL-GSI-001_PoliticaGeneraldeSeguridadyPrivacidaddelaInformacion.pdf'),
 (N'tratamiento_datos', N'Política de Tratamiento de Datos Personales', N'PL-GSI-002 v0', N'https://mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/PL-GSI-002_PoliticaTratamientodeDatosPersonales_V0_LF_2024_03-12-2024.pdf')
) AS origen (Codigo, Titulo, Version, UrlPublica)
ON destino.Codigo = origen.Codigo AND destino.Version = origen.Version
WHEN MATCHED THEN UPDATE SET Titulo=origen.Titulo, UrlPublica=origen.UrlPublica, Vigente=1
WHEN NOT MATCHED THEN INSERT (Codigo,Titulo,Version,UrlPublica,Vigente) VALUES (origen.Codigo,origen.Titulo,origen.Version,origen.UrlPublica,1);
GO
