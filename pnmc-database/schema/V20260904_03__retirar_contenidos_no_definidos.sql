/*
  Retira persistencia de módulos aún no definidos funcionalmente.
  La migración es deliberadamente conservadora: no descarta información.
  Si una instalación contiene registros, se detiene para decidir una migración
  o una exportación antes de eliminar estructuras.
*/
SET NOCOUNT ON;
GO

DECLARE @tabla sysname;
DECLARE @tablas TABLE (Nombre sysname NOT NULL PRIMARY KEY);

INSERT INTO @tablas (Nombre)
VALUES
    (N'AgendaEtiquetas'), (N'AgendaArchivos'),
    (N'NoticiasEtiquetas'), (N'NoticiasArchivos'),
    (N'AlbumesGaleriaEtiquetas'), (N'AlbumesGaleriaArchivos'),
    (N'Agenda'), (N'Noticias'), (N'AlbumesGaleria'), (N'CatalogoEditorial');

DECLARE cursor_tablas CURSOR LOCAL FAST_FORWARD FOR
    SELECT Nombre FROM @tablas;

OPEN cursor_tablas;
FETCH NEXT FROM cursor_tablas INTO @tabla;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @tabla, N'U') IS NOT NULL
       AND EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.' + @tabla))
    BEGIN
        DECLARE @sql nvarchar(max) = N'IF EXISTS (SELECT 1 FROM dbo.' + QUOTENAME(@tabla) + N')
            THROW 51003, ''No se puede retirar dbo.' + @tabla + N': contiene registros. Exporte o migre sus datos antes de continuar.'', 1;';
        EXEC sys.sp_executesql @sql;
    END;

    FETCH NEXT FROM cursor_tablas INTO @tabla;
END;

CLOSE cursor_tablas;
DEALLOCATE cursor_tablas;
GO

IF OBJECT_ID(N'dbo.AgendaEtiquetas', N'U') IS NOT NULL DROP TABLE dbo.AgendaEtiquetas;
IF OBJECT_ID(N'dbo.AgendaArchivos', N'U') IS NOT NULL DROP TABLE dbo.AgendaArchivos;
IF OBJECT_ID(N'dbo.NoticiasEtiquetas', N'U') IS NOT NULL DROP TABLE dbo.NoticiasEtiquetas;
IF OBJECT_ID(N'dbo.NoticiasArchivos', N'U') IS NOT NULL DROP TABLE dbo.NoticiasArchivos;
IF OBJECT_ID(N'dbo.AlbumesGaleriaEtiquetas', N'U') IS NOT NULL DROP TABLE dbo.AlbumesGaleriaEtiquetas;
IF OBJECT_ID(N'dbo.AlbumesGaleriaArchivos', N'U') IS NOT NULL DROP TABLE dbo.AlbumesGaleriaArchivos;
IF OBJECT_ID(N'dbo.Agenda', N'U') IS NOT NULL DROP TABLE dbo.Agenda;
IF OBJECT_ID(N'dbo.Noticias', N'U') IS NOT NULL DROP TABLE dbo.Noticias;
IF OBJECT_ID(N'dbo.AlbumesGaleria', N'U') IS NOT NULL DROP TABLE dbo.AlbumesGaleria;
IF OBJECT_ID(N'dbo.CatalogoEditorial', N'U') IS NOT NULL DROP TABLE dbo.CatalogoEditorial;
GO
