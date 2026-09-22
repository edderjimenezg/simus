/*
  SIMUS · Las categorías dejan de ser texto suelto, y el contenido se relaciona con el ecosistema

  DOS COSAS QUE VAN JUNTAS PORQUE SE PIDEN JUNTAS.

  1) LAS CATEGORIAS. Los tres módulos de contenido —Agenda, Noticias y Catálogo Editorial— guardan
     hoy su categoría como TEXTO LIBRE. Funciona, pero nadie puede corregir un nombre mal escrito
     sin editar cada ficha, y dos personas escriben «Convocatorias» y «convocatoria» sin que nada
     lo impida.

     `dbo.Categorias` YA EXISTE en este esquema desde el corte inicial, con la forma exacta que
     tiene en el desarrollo de septiembre, y estaba VACIA: nadie la usaba. No se crea nada nuevo:
     se pone a trabajar la que había.

     `CodigoModulo` ADMITE EL VALOR `comun`, que es la decisión que se tomó al plantearlo. Así una
     categoría puede declararse compartida por los tres sin obligar a que todas lo sean: «Bandas»
     sirve para una noticia, un evento y una publicación, y «Partitura» solo para el catálogo. La
     decisión se toma categoría por categoría en vez de de una vez para siempre. La CHECK que ya
     tenía la columna —minúsculas y sin espacios— lo admite sin tocarla.

  2) LOS VINCULOS CON EL ECOSISTEMA. Prácticas musicales y territorios sonoros ya están poblados
     —16 y 14 filas— y los Festivales ya se relacionan con ambos. Los tres módulos de contenido no.
     Se añaden las seis tablas puente que faltan, con el MISMO patrón que
     `FestivalesPracticasMusicales`, para que la ficha de una práctica pueda reunir algún día todo
     lo que hay sobre ella y no solo sus festivales.

  LA RELACION ES OPCIONAL Y MULTIPLE, que es como se pidió: una noticia puede no hablar de ninguna
  práctica, o de tres.

  NINGUNA FORANEA LLEVA CASCADA, que es la regla del esquema.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ═══════════════════════ 1. La categoría pasa a ser una fila ═══════════════════════ */

IF COL_LENGTH('dbo.Noticias', 'CategoriaId') IS NULL
    ALTER TABLE dbo.Noticias ADD CategoriaId int NULL;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'CategoriaId') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD CategoriaId int NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'CategoriaId') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD CategoriaId int NULL;
GO

/*
  LO QUE YA SE ESCRIBIO SE CONSERVA: cada valor distinto del texto libre se convierte en una fila
  de su módulo. Migrar tirando el dato habría dejado sin categoría a todo lo publicado, y quien
  escribió «Convocatorias» tendría que volver a hacerlo.

  El slug se calcula quitando lo que no sea letra o dígito, que es la misma regla que usan las
  direcciones públicas del resto del proyecto.
*/
;WITH Distintas AS (
    SELECT DISTINCT N'noticias' AS Modulo, LTRIM(RTRIM(Categoria)) AS Nombre FROM dbo.Noticias WHERE LEN(LTRIM(RTRIM(ISNULL(Categoria, N'')))) > 0
    UNION
    SELECT DISTINCT N'agenda', LTRIM(RTRIM(Categoria)) FROM dbo.EventosAgenda WHERE LEN(LTRIM(RTRIM(ISNULL(Categoria, N'')))) > 0
    UNION
    SELECT DISTINCT N'editorial', LTRIM(RTRIM(Categoria)) FROM dbo.PublicacionesEditoriales WHERE LEN(LTRIM(RTRIM(ISNULL(Categoria, N'')))) > 0
)
INSERT INTO dbo.Categorias (CodigoModulo, NombreCategoria, Slug, OrdenVisualizacion)
SELECT
    d.Modulo,
    d.Nombre,
    LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
        d.Nombre, N' ', N'-'), N'á', N'a'), N'é', N'e'), N'í', N'i'), N'ó', N'o'), N'ú', N'u'),
        N'ñ', N'n'), N'Á', N'a'), N'É', N'e'), N'.', N'')),
    ROW_NUMBER() OVER (PARTITION BY d.Modulo ORDER BY d.Nombre)
FROM Distintas d
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Categorias c WHERE c.CodigoModulo = d.Modulo AND c.NombreCategoria = d.Nombre
);
GO

/* Y cada ficha queda apuntando a la suya. */
UPDATE n SET CategoriaId = c.IdCategoria
FROM dbo.Noticias n
JOIN dbo.Categorias c ON c.CodigoModulo = N'noticias' AND c.NombreCategoria = LTRIM(RTRIM(n.Categoria))
WHERE n.CategoriaId IS NULL;
GO
UPDATE e SET CategoriaId = c.IdCategoria
FROM dbo.EventosAgenda e
JOIN dbo.Categorias c ON c.CodigoModulo = N'agenda' AND c.NombreCategoria = LTRIM(RTRIM(e.Categoria))
WHERE e.CategoriaId IS NULL;
GO
UPDATE p SET CategoriaId = c.IdCategoria
FROM dbo.PublicacionesEditoriales p
JOIN dbo.Categorias c ON c.CodigoModulo = N'editorial' AND c.NombreCategoria = LTRIM(RTRIM(p.Categoria))
WHERE p.CategoriaId IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Noticias_Categorias')
    ALTER TABLE dbo.Noticias ADD CONSTRAINT FK_Noticias_Categorias
        FOREIGN KEY (CategoriaId) REFERENCES dbo.Categorias (IdCategoria);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EventosAgenda_Categorias')
    ALTER TABLE dbo.EventosAgenda ADD CONSTRAINT FK_EventosAgenda_Categorias
        FOREIGN KEY (CategoriaId) REFERENCES dbo.Categorias (IdCategoria);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PublicacionesEditoriales_Categorias')
    ALTER TABLE dbo.PublicacionesEditoriales ADD CONSTRAINT FK_PublicacionesEditoriales_Categorias
        FOREIGN KEY (CategoriaId) REFERENCES dbo.Categorias (IdCategoria);
GO

/*
  LA COLUMNA DE TEXTO SE RETIRA. Dejarla al lado del identificador daría dos verdades sobre la
  misma cosa, y la que envejece es siempre la que nadie mira. Los índices que la usaban se van con
  ella.
*/
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Noticias_Categoria' AND object_id = OBJECT_ID(N'dbo.Noticias'))
    DROP INDEX IX_Noticias_Categoria ON dbo.Noticias;
GO
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EventosAgenda_Categoria' AND object_id = OBJECT_ID(N'dbo.EventosAgenda'))
    DROP INDEX IX_EventosAgenda_Categoria ON dbo.EventosAgenda;
GO
IF COL_LENGTH('dbo.Noticias', 'Categoria') IS NOT NULL
    ALTER TABLE dbo.Noticias DROP COLUMN Categoria;
GO
IF COL_LENGTH('dbo.EventosAgenda', 'Categoria') IS NOT NULL
    ALTER TABLE dbo.EventosAgenda DROP COLUMN Categoria;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'Categoria') IS NOT NULL
    ALTER TABLE dbo.PublicacionesEditoriales DROP COLUMN Categoria;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Noticias_CategoriaId' AND object_id = OBJECT_ID(N'dbo.Noticias'))
    CREATE INDEX IX_Noticias_CategoriaId ON dbo.Noticias (CategoriaId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EventosAgenda_CategoriaId' AND object_id = OBJECT_ID(N'dbo.EventosAgenda'))
    CREATE INDEX IX_EventosAgenda_CategoriaId ON dbo.EventosAgenda (CategoriaId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PublicacionesEditoriales_CategoriaId' AND object_id = OBJECT_ID(N'dbo.PublicacionesEditoriales'))
    CREATE INDEX IX_PublicacionesEditoriales_CategoriaId ON dbo.PublicacionesEditoriales (CategoriaId);
GO

/* ═══════════════════════ 2. Los vínculos con el ecosistema ═══════════════════════ */

IF OBJECT_ID(N'dbo.NoticiasPracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasPracticasMusicales (
        IdNoticiaPracticaMusical bigint IDENTITY(1,1) NOT NULL,
        NoticiaId bigint NOT NULL,
        PracticaMusicalId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_NoticiasPracticasMusicales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_NoticiasPracticasMusicales PRIMARY KEY CLUSTERED (IdNoticiaPracticaMusical),
        CONSTRAINT FK_NoticiasPracticasMusicales_Padre FOREIGN KEY (NoticiaId) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasPracticasMusicales_Vocabulario FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
        CONSTRAINT UQ_NoticiasPracticasMusicales UNIQUE (NoticiaId, PracticaMusicalId)
    );

    CREATE INDEX IX_NoticiasPracticasMusicales_Padre ON dbo.NoticiasPracticasMusicales (NoticiaId);
END
GO

IF OBJECT_ID(N'dbo.NoticiasTerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasTerritoriosSonoros (
        IdNoticiaTerritorioSonoro bigint IDENTITY(1,1) NOT NULL,
        NoticiaId bigint NOT NULL,
        TerritorioSonoroId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_NoticiasTerritoriosSonoros_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_NoticiasTerritoriosSonoros PRIMARY KEY CLUSTERED (IdNoticiaTerritorioSonoro),
        CONSTRAINT FK_NoticiasTerritoriosSonoros_Padre FOREIGN KEY (NoticiaId) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasTerritoriosSonoros_Vocabulario FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
        CONSTRAINT UQ_NoticiasTerritoriosSonoros UNIQUE (NoticiaId, TerritorioSonoroId)
    );

    CREATE INDEX IX_NoticiasTerritoriosSonoros_Padre ON dbo.NoticiasTerritoriosSonoros (NoticiaId);
END
GO

IF OBJECT_ID(N'dbo.EventosAgendaPracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventosAgendaPracticasMusicales (
        IdEventoAgendaPracticaMusical bigint IDENTITY(1,1) NOT NULL,
        EventoAgendaId bigint NOT NULL,
        PracticaMusicalId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_EventosAgendaPracticasMusicales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EventosAgendaPracticasMusicales PRIMARY KEY CLUSTERED (IdEventoAgendaPracticaMusical),
        CONSTRAINT FK_EventosAgendaPracticasMusicales_Padre FOREIGN KEY (EventoAgendaId) REFERENCES dbo.EventosAgenda (IdEventoAgenda),
        CONSTRAINT FK_EventosAgendaPracticasMusicales_Vocabulario FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
        CONSTRAINT UQ_EventosAgendaPracticasMusicales UNIQUE (EventoAgendaId, PracticaMusicalId)
    );

    CREATE INDEX IX_EventosAgendaPracticasMusicales_Padre ON dbo.EventosAgendaPracticasMusicales (EventoAgendaId);
END
GO

IF OBJECT_ID(N'dbo.EventosAgendaTerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EventosAgendaTerritoriosSonoros (
        IdEventoAgendaTerritorioSonoro bigint IDENTITY(1,1) NOT NULL,
        EventoAgendaId bigint NOT NULL,
        TerritorioSonoroId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_EventosAgendaTerritoriosSonoros_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EventosAgendaTerritoriosSonoros PRIMARY KEY CLUSTERED (IdEventoAgendaTerritorioSonoro),
        CONSTRAINT FK_EventosAgendaTerritoriosSonoros_Padre FOREIGN KEY (EventoAgendaId) REFERENCES dbo.EventosAgenda (IdEventoAgenda),
        CONSTRAINT FK_EventosAgendaTerritoriosSonoros_Vocabulario FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
        CONSTRAINT UQ_EventosAgendaTerritoriosSonoros UNIQUE (EventoAgendaId, TerritorioSonoroId)
    );

    CREATE INDEX IX_EventosAgendaTerritoriosSonoros_Padre ON dbo.EventosAgendaTerritoriosSonoros (EventoAgendaId);
END
GO

IF OBJECT_ID(N'dbo.PublicacionesEditorialesPracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditorialesPracticasMusicales (
        IdPublicacionEditorialPracticaMusical bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        PracticaMusicalId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_PublicacionesEditorialesPracticasMusicales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PublicacionesEditorialesPracticasMusicales PRIMARY KEY CLUSTERED (IdPublicacionEditorialPracticaMusical),
        CONSTRAINT FK_PublicacionesEditorialesPracticasMusicales_Padre FOREIGN KEY (PublicacionEditorialId) REFERENCES dbo.PublicacionesEditoriales (IdPublicacionEditorial),
        CONSTRAINT FK_PublicacionesEditorialesPracticasMusicales_Vocabulario FOREIGN KEY (PracticaMusicalId) REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
        CONSTRAINT UQ_PublicacionesEditorialesPracticasMusicales UNIQUE (PublicacionEditorialId, PracticaMusicalId)
    );

    CREATE INDEX IX_PublicacionesEditorialesPracticasMusicales_Padre ON dbo.PublicacionesEditorialesPracticasMusicales (PublicacionEditorialId);
END
GO

IF OBJECT_ID(N'dbo.PublicacionesEditorialesTerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditorialesTerritoriosSonoros (
        IdPublicacionEditorialTerritorioSonoro bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        TerritorioSonoroId int NOT NULL,
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_PublicacionesEditorialesTerritoriosSonoros_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PublicacionesEditorialesTerritoriosSonoros PRIMARY KEY CLUSTERED (IdPublicacionEditorialTerritorioSonoro),
        CONSTRAINT FK_PublicacionesEditorialesTerritoriosSonoros_Padre FOREIGN KEY (PublicacionEditorialId) REFERENCES dbo.PublicacionesEditoriales (IdPublicacionEditorial),
        CONSTRAINT FK_PublicacionesEditorialesTerritoriosSonoros_Vocabulario FOREIGN KEY (TerritorioSonoroId) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
        CONSTRAINT UQ_PublicacionesEditorialesTerritoriosSonoros UNIQUE (PublicacionEditorialId, TerritorioSonoroId)
    );

    CREATE INDEX IX_PublicacionesEditorialesTerritoriosSonoros_Padre ON dbo.PublicacionesEditorialesTerritoriosSonoros (PublicacionEditorialId);
END
GO
