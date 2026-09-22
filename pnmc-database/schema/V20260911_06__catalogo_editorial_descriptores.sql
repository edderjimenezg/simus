/*
  SIMUS · Catálogo Editorial — descriptores de presentación

  QUE AÑADE Y POR QUE AHORA. El diseño aprobado del catálogo presenta cada publicación por su
  tipo, su categoría, su ámbito y sus palabras clave, y la ilustra con una miniatura. El contrato
  De una revisión anterior modeló la identidad, la autoría, los identificadores, los accesos y los derechos
  —lo que sostiene una ficha bibliográfica— pero no estos descriptores, porque son los que el
  propio modelo dejó por confirmar.

  SE GUARDAN COMO TEXTO, Y ESO ES DELIBERADO. Una revisión anterior decidió que «tipos de publicación,
  prácticas, funciones, ámbitos, formatos, líneas y roles editoriales se administrarán mediante
  catálogos versionados» y que «sus listas definitivas estan por confirmar». Así que aquí NO hay una
  lista cerrada en CHECK ni una clave foránea a una tabla de vocabulario: eso convertiría en regla
  institucional unos valores que nadie ha aprobado todavía.

  Lo que hay es el dato tal como viene de la fuente, conservado para poder presentarlo y, cuando
  existan los vocabularios aprobados, conciliarlo contra ellos. Fijar hoy una lista cerrada
  obligaría a migrar dos veces: una para inventarla y otra para corregirla.

  LA MINIATURA ES UNA RUTA, NO UN ARCHIVO EN LA BASE. Las 170 imágenes del acervo son ficheros
  estáticos del portal, no medios editables del CMS: nadie las recorta ni las versiona desde la
  consola. Guardar la ruta evita meter 170 binarios en SQL Server para no obtener nada a cambio.
  Si algún día se administran desde la consola, pasarán por `dbo.Archivos` como el resto.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'TipoPublicacion') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD TipoPublicacion nvarchar(120) NULL;
GO

IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Categoria') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Categoria nvarchar(160) NULL;
GO

IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Ambito') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Ambito nvarchar(160) NULL;
GO

/* Una fila por término: buscar «cantos de comunidades negras» sobre una cadena separada por
   comas obliga a un LIKE que no usa índice y que confunde «banda» con «bandaje». */
IF OBJECT_ID(N'dbo.PalabrasClaveEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PalabrasClaveEditoriales (
        IdPalabraClaveEditorial bigint IDENTITY(1,1) NOT NULL,
        PublicacionEditorialId bigint NOT NULL,
        Termino nvarchar(120) NOT NULL,
        CONSTRAINT PK_PalabrasClaveEditoriales PRIMARY KEY (IdPalabraClaveEditorial),
        CONSTRAINT FK_PalabrasClaveEditoriales_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT UQ_PalabrasClaveEditoriales UNIQUE (PublicacionEditorialId, Termino)
    );
    CREATE INDEX IX_PalabrasClaveEditoriales_Termino ON dbo.PalabrasClaveEditoriales (Termino);
END;
GO

IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'MiniaturaRuta') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD MiniaturaRuta nvarchar(500) NULL;
GO
