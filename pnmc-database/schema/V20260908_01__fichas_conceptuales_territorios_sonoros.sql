/*
  Fuente única del contenido conceptual de los Territorios sonoros.
  Los nombres, slugs y orden pertenecen al catálogo maestro TerritoriosSonoros;
  aquí viven únicamente los textos y recursos que serán entregados y validados.
*/
IF OBJECT_ID(N'dbo.FichasConceptualesTerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FichasConceptualesTerritoriosSonoros (
        TerritorioSonoroId int NOT NULL,
        DefinicionBreve nvarchar(800) NULL,
        DefinicionAmpliada nvarchar(max) NULL,
        DescripcionConceptual nvarchar(max) NULL,
        Caracteristicas nvarchar(max) NULL,
        RelacionTerritorial nvarchar(max) NULL,
        Contextos nvarchar(max) NULL,
        Ejemplos nvarchar(max) NULL,
        Fuentes nvarchar(max) NULL,
        RecursoVisualUrl nvarchar(1000) NULL,
        TextoAlternativoRecurso nvarchar(500) NULL,
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_FichasConceptualesTerritorios_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FichasConceptualesTerritoriosSonoros PRIMARY KEY (TerritorioSonoroId),
        CONSTRAINT FK_FichasConceptualesTerritoriosSonoros_Territorio FOREIGN KEY (TerritorioSonoroId)
            REFERENCES dbo.TerritoriosSonoros(IdTerritorioSonoro) ON DELETE CASCADE
    );
END;

INSERT INTO dbo.FichasConceptualesTerritoriosSonoros (TerritorioSonoroId)
SELECT t.IdTerritorioSonoro FROM dbo.TerritoriosSonoros t
WHERE NOT EXISTS (SELECT 1 FROM dbo.FichasConceptualesTerritoriosSonoros f WHERE f.TerritorioSonoroId = t.IdTerritorioSonoro);
