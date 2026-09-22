/*
  SIMUS · Noticias — el modelo de contenido que la sección declaraba no tener

  DE DONDE VIENE. Hasta hoy `/noticias` era una pantalla honesta y vacía: «la sección se activará
  cuando cuente con su modelo de contenido, flujo editorial, revisión institucional y API de
  consulta pública». Ninguna de esas cuatro cosas existía. Este guion pone la primera.

  LO QUE SE DECIDE AQUI, Y QUE QUEDA PENDIENTE DE APROBACION. El modelo es deliberadamente
  conservador: una noticia es un título, un resumen, un cuerpo, una fecha y una imagen, con
  autoría y etiquetas. NO se inventan categorías ni secciones editoriales, porque serían
  vocabulario institucional sin aprobar —el mismo criterio que una revisión anterior aplicó al Catálogo
  Editorial—. Las etiquetas son texto libre por la misma razón.

  UNA SOLA COLUMNA DE ESTADO, a diferencia del Catálogo Editorial. Allí hacían falta dos —calidad
  de la ficha y visibilidad— porque catalogar y publicar son decisiones de equipos distintos. Una
  noticia no se cataloga: se escribe, se revisa y se publica. Dos columnas aquí serían una
  ceremonia sin nadie que la ejecute.

  LA FECHA DE PUBLICACION ES UN DATO, NO UN EFECTO. Se guarda aparte del estado porque una noticia
  publicada hoy puede fecharse el lunes, y porque el orden del portal es cronológico por esa fecha
  y no por cuándo alguien pulsó el botón.

  EL RESUMEN ES OBLIGATORIO Y LO IMPONE LA BASE. El portal lo enseña en la lista y en las tarjetas
  del Inicio; una noticia sin resumen obliga a recortar el cuerpo por la mitad de una frase, que es
  como se llega a los «...» en mitad de una palabra.

  NINGUNA FORANEA LLEVA CASCADA, que es la regla del esquema y `EstructuraSimusSqlServerTests` la
  comprueba. Retirar una noticia es cambiarla de estado, no borrarla.

  NO SIEMBRA NADA. Un portal que nace con noticias de demostración acaba enseñándolas en
  producción.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Noticias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Noticias (
        IdNoticia bigint IDENTITY(1,1) NOT NULL,
        /* La dirección pública. Se fija al crear y no cambia: un enlace compartido no puede
           dejar de funcionar porque alguien corrigiera una tilde del título. */
        Slug nvarchar(180) NOT NULL,
        Titulo nvarchar(300) NOT NULL,
        Resumen nvarchar(500) NOT NULL,
        Cuerpo nvarchar(max) NULL,
        FechaPublicacion date NULL,
        ImagenRuta nvarchar(500) NULL,
        /* El texto alternativo viaja CON la imagen y no aparte: una imagen sin alternativa es
           inaccesible, y separarlos hace que se olvide. */
        ImagenAlternativa nvarchar(300) NULL,
        AutoriaNombre nvarchar(200) NULL,
        Estado nvarchar(20) NOT NULL CONSTRAINT DF_Noticias_Estado DEFAULT (N'borrador'),
        Version int NOT NULL CONSTRAINT DF_Noticias_Version DEFAULT (1),
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_Noticias_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(3) NOT NULL CONSTRAINT DF_Noticias_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Noticias PRIMARY KEY CLUSTERED (IdNoticia),
        CONSTRAINT UQ_Noticias_Slug UNIQUE (Slug),
        CONSTRAINT CK_Noticias_Estado CHECK (Estado IN (N'borrador', N'en_revision', N'publicado', N'archivado')),
        /* PUBLICAR EXIGE FECHA. Sin ella el portal no sabría dónde ordenarla, y la regla vive en
           la base para que también la cumpla una importación que escriba directo. */
        CONSTRAINT CK_Noticias_PublicadaConFecha CHECK (Estado <> N'publicado' OR FechaPublicacion IS NOT NULL),
        CONSTRAINT CK_Noticias_Titulo CHECK (LEN(LTRIM(RTRIM(Titulo))) > 0),
        CONSTRAINT CK_Noticias_Resumen CHECK (LEN(LTRIM(RTRIM(Resumen))) > 0),
        CONSTRAINT CK_Noticias_Version CHECK (Version >= 1)
    );

    CREATE INDEX IX_Noticias_Estado_Fecha ON dbo.Noticias (Estado, FechaPublicacion DESC);
END
GO

/* ─────────────────────────── Etiquetas, una fila por término ─────────────────────────── */
/*
  UNA FILA POR ETIQUETA Y NO UNA CADENA SEPARADA POR COMAS. Filtrar «convivencia» sobre una cadena
  obliga a un LIKE que no usa índice y que confunde «banda» con «bandaje». Es la misma decisión que
  el Catálogo Editorial tomó con sus palabras clave.
*/
IF OBJECT_ID(N'dbo.EtiquetasNoticia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EtiquetasNoticia (
        IdEtiquetaNoticia bigint IDENTITY(1,1) NOT NULL,
        NoticiaId bigint NOT NULL,
        Termino nvarchar(120) NOT NULL,
        CONSTRAINT PK_EtiquetasNoticia PRIMARY KEY CLUSTERED (IdEtiquetaNoticia),
        CONSTRAINT FK_EtiquetasNoticia_Noticia FOREIGN KEY (NoticiaId) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT UQ_EtiquetasNoticia_Termino UNIQUE (NoticiaId, Termino),
        CONSTRAINT CK_EtiquetasNoticia_Termino CHECK (LEN(LTRIM(RTRIM(Termino))) > 0)
    );

    CREATE INDEX IX_EtiquetasNoticia_Termino ON dbo.EtiquetasNoticia (Termino);
END
GO
