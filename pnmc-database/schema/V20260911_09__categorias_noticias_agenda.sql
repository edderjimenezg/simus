/*
  SIMUS · Categoría en Noticias y en la Agenda

  POR QUE APARECE AHORA Y NO EN LOS CORTES 09 Y 10. Aquellos dejaron la categoría fuera a
  propósito: sin un vocabulario aprobado, inventar una lista de categorías habría sido fijar como
  regla institucional unos valores que nadie había decidido —el mismo criterio de una revisión anterior—.

  LO QUE CAMBIA. El diseño aprobado del portal, que vive en el diseño aprobado del portal, organiza AMBAS pantallas
  alrededor de una barra de categorías: es el filtro principal de Noticias y una de las columnas
  del aside de la Agenda. La categoría deja de ser una invención nuestra y pasa a ser un requisito
  del diseño ya aprobado.

  SIGUE SIENDO TEXTO LIBRE, y eso no ha cambiado. No hay lista cerrada en CHECK ni foránea a una
  tabla de vocabulario: el diseño exige que exista el campo, no dice cuáles son sus valores. Las
  barras de categorías del portal se construyen con las que de hecho aparecen en lo publicado, que
  es lo que hace el diseño de referencia y lo que evita ofrecer filtros que no llevan a ninguna
  parte.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.Noticias', 'Categoria') IS NULL
BEGIN
    ALTER TABLE dbo.Noticias ADD Categoria nvarchar(160) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Noticias_Categoria' AND object_id = OBJECT_ID(N'dbo.Noticias'))
BEGIN
    CREATE INDEX IX_Noticias_Categoria ON dbo.Noticias (Categoria);
END
GO

IF COL_LENGTH('dbo.EventosAgenda', 'Categoria') IS NULL
BEGIN
    ALTER TABLE dbo.EventosAgenda ADD Categoria nvarchar(160) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EventosAgenda_Categoria' AND object_id = OBJECT_ID(N'dbo.EventosAgenda'))
BEGIN
    CREATE INDEX IX_EventosAgenda_Categoria ON dbo.EventosAgenda (Categoria);
END
GO
