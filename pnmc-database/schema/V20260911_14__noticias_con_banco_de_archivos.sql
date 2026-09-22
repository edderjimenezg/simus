/* PNMC · La imagen de una noticia también sale del banco de archivos */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE PROBLEMA RESUELVE. Hasta ahora la imagen de una noticia era una RUTA ESCRITA A MANO en
  Noticias.ImagenRuta: quien redactaba tecleaba una direccion y confiaba en que el fichero
  estuviera puesto, con el formato correcto y con texto alternativo. Nada lo comprobaba, y una
  ruta mal escrita solo se descubria mirando el portal.

  QUE CAMBIA. Se sube al banco -dbo.Archivos-, que valida los bytes por su firma, limita el tamano
  y exige el texto alternativo antes de aceptar el fichero. Es exactamente lo que ya hace la
  Agenda desde V20260911_11, y esta tabla es su gemela: misma forma, mismo rol por omision, mismas
  reglas.

  POR QUE UNA TABLA PUENTE Y NO UNA COLUMNA. Una noticia puede necesitar mañana una galeria o un
  documento adjunto, y el rol ya esta previsto en la fila. Con una columna habria que migrar otra
  vez el dia que aparezca el segundo archivo.

  LAS FORANEAS NO CASCADEAN, por la regla del proyecto: borrar un archivo del banco no puede
  llevarse por delante en silencio la noticia que lo usaba.

  NO SE TOCA ImagenRuta. Las noticias cargadas antes siguen enseñando su ruta mientras nadie las
  edite; el API prefiere el archivo del banco cuando existe y cae en la ruta cuando no.
*/

IF OBJECT_ID(N'dbo.NoticiasArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasArchivos (
        IdNoticiaArchivo bigint IDENTITY(1,1) NOT NULL,
        NoticiaId bigint NOT NULL,
        ArchivoId int NOT NULL,
        RolArchivo nvarchar(80) NOT NULL CONSTRAINT DF_NoticiasArchivos_Rol DEFAULT (N'imagen_principal'),
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_NoticiasArchivos_Orden DEFAULT (1),
        CONSTRAINT PK_NoticiasArchivos PRIMARY KEY CLUSTERED (IdNoticiaArchivo),
        CONSTRAINT FK_NoticiasArchivos_Noticia FOREIGN KEY (NoticiaId) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasArchivos_Archivo FOREIGN KEY (ArchivoId) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT UQ_NoticiasArchivos UNIQUE (NoticiaId, ArchivoId, RolArchivo),
        CONSTRAINT CK_NoticiasArchivos_Orden CHECK (OrdenVisualizacion > 0)
    );

    CREATE INDEX IX_NoticiasArchivos_Noticia ON dbo.NoticiasArchivos (NoticiaId);
    CREATE INDEX IX_NoticiasArchivos_Archivo ON dbo.NoticiasArchivos (ArchivoId);
END
GO
