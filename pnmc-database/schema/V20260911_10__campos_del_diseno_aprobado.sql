/*
  SIMUS · Los campos que el diseño aprobado necesita para funcionar

  DE DONDE SALE ESTA LISTA. No de una propuesta: de leer qué consume la plantilla aprobada que
  vive en el diseño aprobado del portal. Sus tres tarjetas de detalle —Clasificación Editorial, Ficha Bibliográfica,
  Disponibilidad y Consulta— nombran campo por campo lo que enseñan, y nueve de ellos no existían
  en `PublicacionesEditoriales`. Sin ellos las tarjetas se dibujan vacías, que es la definición
  de una pantalla falsamente conectada.

  TAMPOCO SON INVENCION NUESTRA. Los nueve están en el desarrollo de septiembre, en la tabla
  `CatalogoEditorial` que recibió la importación del acervo, y traen datos reales de las 171
  publicaciones. Lo que hace este guion es dejarles sitio en el modelo relacional de una revisión anterior.

  SIGUEN SIENDO TEXTO LIBRE. Una revisión anterior dejó los vocabularios editoriales sin aprobar y eso no
  ha cambiado: aquí se abre el hueco para el dato que ya existe, no se cierra ninguna lista.

  `Paginas` Y `Duracion` SON TEXTO Y NO NUMEROS, igual que en septiembre, que documenta por qué:
  la fuente trae «48 p.», «2 v.», «ca. 30 min», y convertirlos exige una decisión de normalización
  que nadie ha tomado. Guardar el dato como viene permite tomarla después; convertirlo a la brava
  la toma en silencio y pierde lo que no encaje.

  ORGANIZADOR EN LA AGENDA por el mismo motivo: la tarjeta del diseño aprobado lo enseña y la
  tabla no lo tenía.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ─────────────── Clasificación editorial: lo que pide la primera tarjeta ─────────────── */
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'SeccionPrincipal') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD SeccionPrincipal nvarchar(200) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'RutaSeccion') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD RutaSeccion nvarchar(500) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'PracticaMusical') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD PracticaMusical nvarchar(160) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'Subcategoria') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Subcategoria nvarchar(160) NULL;
GO

/* ─────────────── Descripción física: lo que pide la ficha bibliográfica ─────────────── */
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'TamanoFormato') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD TamanoFormato nvarchar(120) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'Paginas') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Paginas nvarchar(20) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'Duracion') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Duracion nvarchar(40) NULL;
GO

/* ─────────────── Notas y portada: la cuarta tarjeta y el texto de cubierta ─────────────── */
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'CamposAdicionales') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD CamposAdicionales nvarchar(max) NULL;
GO
IF COL_LENGTH('dbo.PublicacionesEditoriales', 'TextoPortada') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD TextoPortada nvarchar(max) NULL;
GO

/* La sección agrupa el acervo en la barra lateral del diseño: se consulta en cada carga. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PublicacionesEditoriales_Seccion' AND object_id = OBJECT_ID(N'dbo.PublicacionesEditoriales'))
    CREATE INDEX IX_PublicacionesEditoriales_Seccion ON dbo.PublicacionesEditoriales (SeccionPrincipal);
GO

/* ─────────────── Quién organiza, en la Agenda ─────────────── */
IF COL_LENGTH('dbo.EventosAgenda', 'Organizador') IS NULL
    ALTER TABLE dbo.EventosAgenda ADD Organizador nvarchar(220) NULL;
GO
