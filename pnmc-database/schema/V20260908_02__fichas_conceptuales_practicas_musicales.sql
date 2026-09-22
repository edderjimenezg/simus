IF OBJECT_ID(N'dbo.FichasConceptualesPracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FichasConceptualesPracticasMusicales (
        PracticaMusicalId int NOT NULL, DefinicionBreve nvarchar(800) NULL, DefinicionAmpliada nvarchar(max) NULL,
        DescripcionConceptual nvarchar(max) NULL, Caracteristicas nvarchar(max) NULL, Contextos nvarchar(max) NULL,
        Ejemplos nvarchar(max) NULL, Fuentes nvarchar(max) NULL, RecursoVisualUrl nvarchar(1000) NULL,
        TextoAlternativoRecurso nvarchar(500) NULL, FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_FichasConceptualesPracticas_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_FichasConceptualesPracticasMusicales PRIMARY KEY (PracticaMusicalId),
        CONSTRAINT FK_FichasConceptualesPracticas_Practica FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales(IdPracticaMusical) ON DELETE CASCADE
    );
END;
INSERT INTO dbo.FichasConceptualesPracticasMusicales (PracticaMusicalId)
SELECT p.IdPracticaMusical FROM dbo.PracticasMusicales p WHERE NOT EXISTS (SELECT 1 FROM dbo.FichasConceptualesPracticasMusicales f WHERE f.PracticaMusicalId = p.IdPracticaMusical);
