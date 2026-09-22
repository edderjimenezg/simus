/*
  SIMUS · Catálogo Editorial — la tipología RDA/DCMI y lo que el acervo real no cabía a guardar

  QUE AÑADE. Los cuatro ejes con los que «Estructura del Catálogo — Estándares» describe de qué
  está hecha una publicación, y que hasta ahora no existían en el modelo:

    · Tipo de recurso   — Dublin Core `dc:type`, vocabulario DCMI Type
    · Tipo de contenido — RDA 336 (Content type)
    · Tipo de medio     — RDA 337 (Media type)
    · Tipo de soporte   — RDA 338 (Carrier type)

  POR QUE CUATRO EJES Y NO UN «TIPO DE PUBLICACION». Es la recomendación número 2 del documento de
  estándares, y el motivo es que un campo libre no distingue lo que hay que distinguir: RDA sustituyó
  la vieja designación general de material por estos tres elementos INDEPENDIENTES precisamente
  porque «CD» y «DVD» y «libro de partituras» no se ordenan en un solo eje. Un CD es música
  interpretada / audio / disco de audio; un DVD es imagen en movimiento / vídeo / videodisco; un
  libro de partituras es música notada / sin mediación / volumen. El campo `TipoPublicacion` que ya
  existe se conserva: es el valor tal como lo escribió la fuente, y sirve para conciliar.

  POR QUE SON MULTIVALUADOS, QUE ES LO QUE OBLIGA A UNA TABLA DE ENLACE. De las 171 fichas
  del acervo, 23 declaran más de un valor en al menos un eje. Hay libros que vienen con su
  CD —«disco de audio ; volumen», catorce casos—, hay cajas con DVD, CD y cartilla —«videodisco ;
  disco de audio ; volumen», cuatro— y hay obras que son a la vez sonoras y textuales. Guardar eso
  en una columna de texto con puntos y comas dentro obliga a partir la cadena para poder filtrar, y
  filtrar por soporte es justo lo que el catálogo tiene que saber hacer.

  UN SOLO VOCABULARIO PARA LOS CUATRO EJES. `Eje` distingue a cuál pertenece cada término. Cuatro
  tablas gemelas —y cuatro tablas de enlace— serían el mismo modelo escrito cuatro veces; cada
  consulta nueva habría que escribirla cuatro veces también. Es además la forma que describe el
  propio documento: una clasificación POR FACETAS, donde cada faceta es un eje independiente.

  EL VOCABULARIO SE SIEMBRA CON LOS VALORES DE LA NORMA Y LOS DE LA FUENTE, que no coinciden del
  todo. El documento lista cuatro soportes —volumen, disco de audio, videodisco, recurso en línea—
  y la fuente usa además «disco de computador», que SI está en la lista oficial de RDA 338 (computer
  disc) y que describe seis fichas del acervo. Se siembran los dos conjuntos: recortar el
  vocabulario a la tabla resumida del documento perdería seis fichas o las obligaría a mentir.

  `Formato` NO SE DEDUCE DEL SOPORTE, Y SE COMPROBO ANTES DE DECIDIRLO. El documento lo presenta
  como «derivado del soporte», y el proyecto tiene por norma no guardar lo que se puede calcular.
  Contrastado contra las 171 fichas, la deducción no se sostiene: `volumen` aparece como Físico en
  55, como Mixto en 8 y como Digital en 3. El formato dice si la publicación se distribuye además en
  digital, que es información propia y no una consecuencia del soporte. Por eso es una columna, y
  por eso lleva su lista cerrada: esta sí la fija el documento.

  IDIOMA SE ENSANCHA. Siete fichas declaran más de una lengua —«es ; en», «es ; (lengua nativa)»— y
  la columna eran veinte caracteres, que es exactamente el largo del segundo caso: cabía por un
  carácter. Si el multilingüismo deja de ser la excepción, esto debería ser un quinto eje; hoy no lo
  es y convertirlo en tabla sería construir para siete filas.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

/* ─────────────────────────── El vocabulario de las cuatro facetas ─────────────────────────── */
IF OBJECT_ID(N'dbo.TipologiasEditoriales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TipologiasEditoriales (
        IdTipologiaEditorial int IDENTITY(1,1) NOT NULL,
        /* Cuál de las cuatro facetas. Los nombres son los del documento de estándares. */
        Eje nvarchar(20) NOT NULL,
        /* El término tal como lo nombra la norma, en minúsculas y sin acentos para poder buscarlo. */
        Codigo nvarchar(60) NOT NULL,
        /* Cómo se lee en pantalla. DCMI no se traduce —«MovingImage» es el término normalizado—;
           RDA sí, y el documento ya fijó su forma en español. */
        Etiqueta nvarchar(120) NOT NULL,
        /* La norma de la que sale, para poder citarla en la ficha sin codificarla en la pantalla. */
        Norma nvarchar(60) NOT NULL,
        Orden int NOT NULL CONSTRAINT DF_TipologiasEditoriales_Orden DEFAULT (0),
        CONSTRAINT PK_TipologiasEditoriales PRIMARY KEY (IdTipologiaEditorial),
        CONSTRAINT UQ_TipologiasEditoriales_EjeCodigo UNIQUE (Eje, Codigo),
        CONSTRAINT CK_TipologiasEditoriales_Eje CHECK (Eje IN
            (N'recurso', N'contenido', N'medio', N'soporte'))
    );
END;
GO

/* ─────────────────────────── Qué tipología tiene cada publicación ─────────────────────────── */
IF OBJECT_ID(N'dbo.PublicacionesEditorialesTipologias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicacionesEditorialesTipologias (
        PublicacionEditorialId bigint NOT NULL,
        TipologiaEditorialId int NOT NULL,
        CONSTRAINT PK_PublicacionesEditorialesTipologias
            PRIMARY KEY (PublicacionEditorialId, TipologiaEditorialId),
        /* SIN CASCADA, COMO EL RESTO DE LA BASE. La primera versión puso `ON DELETE CASCADE` aquí
           razonando que la tipología pertenece a la ficha, y lo tumbó
           `Ninguna_Foranea_Tiene_Cascada_Y_Las_Territorializadas_Apuntan_A_Divipola`: SIMUS tiene
           CERO foráneas con cascada y la regla es del proyecto entero, no de esta tabla. Lo grave
           no sería la cascada, sería la incoherencia —el mismo DELETE haría dos cosas distintas
           según la tabla—. Retirar una publicación desata sus hijos en el código, a la vista, como
           ya hace `RetirarRelacionesAsync` con las ediciones de un Festival. */
        CONSTRAINT FK_PublicacionesEditorialesTipologias_Publicacion FOREIGN KEY (PublicacionEditorialId)
            REFERENCES dbo.PublicacionesEditoriales(IdPublicacionEditorial),
        CONSTRAINT FK_PublicacionesEditorialesTipologias_Tipologia FOREIGN KEY (TipologiaEditorialId)
            REFERENCES dbo.TipologiasEditoriales(IdTipologiaEditorial)
    );
    CREATE INDEX IX_PublicacionesEditorialesTipologias_Tipologia
        ON dbo.PublicacionesEditorialesTipologias (TipologiaEditorialId);
END;
GO

/* ─────────────────────────── Formato, que es dato propio ─────────────────────────── */
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Formato') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Formato nvarchar(20) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PublicacionesEditoriales_Formato')
    ALTER TABLE dbo.PublicacionesEditoriales ADD CONSTRAINT CK_PublicacionesEditoriales_Formato
        CHECK (Formato IS NULL OR Formato IN (N'Físico', N'Digital', N'Mixto'));
GO

/* ─────────────────────────── Tres columnas que no daban el ancho ───────────────────────────

   LOS TRES MAXIMOS SON REALES, tomados del propio acervo. Cargar el acervo sin esto falla con
   truncamiento y, como la semilla corre en una sola transacción, no entra ni una obra:

     · `TipoPublicacion`  144 caracteres en E111 — la fuente mete en el tipo una frase con la
       dirección del PDF. La columna medía 120.
     · `Paginas`           98 en E001 — el despiece de los diez módulos de capacitación
       («1- 26 p., 2- 52 p., …»). La columna medía 20.
     · `Duracion`          84 en E140. La columna medía 40.

   SIGUEN SIENDO TEXTO, Y NO ES UN DESCUIDO. `Paginas` y `Duracion` son cadenas en el origen
   —«20 pistas», «1- 26 p., 2- 52 p.»— y convertirlas a número obligaría a interpretar lo que la
   fuente escribió, que es justo lo que un catálogo bibliográfico no debe hacer con su propio dato.
   El documento de estándares recomienda ISO 8601 para la duración; eso se normaliza catalogando,
   no importando. */
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'TipoPublicacion') = 240   /* nvarchar(120) */
    ALTER TABLE dbo.PublicacionesEditoriales ALTER COLUMN TipoPublicacion nvarchar(240) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Paginas') = 40            /* nvarchar(20) */
    ALTER TABLE dbo.PublicacionesEditoriales ALTER COLUMN Paginas nvarchar(160) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Duracion') = 80           /* nvarchar(40) */
    ALTER TABLE dbo.PublicacionesEditoriales ALTER COLUMN Duracion nvarchar(160) NULL;
GO

/* ─────────────────────────── Lo que el acervo trae y no tenía dónde ir ───────────────────────────

   SIETE COLUMNAS, Y LAS SIETE CON DATO EN LAS 171 FICHAS. Se toman del desarrollo de septiembre,
   que ya modeló esta misma carga, con la cuenta de cuántas fichas usan cada una:

     · NotaFecha            9 fichas — lo que la fuente escribió cuando no pudo fijar la fecha.
     · CategoriaSecundaria 58 — una obra puede tocar dos prácticas; la segunda no cabía.
     · AmbitoTexto         16 — el ámbito escrito a mano cuando no entra en el vocabulario.
     · NotasCatalogacion   18 — la nota de quien catalogó.
     · Confianza          171 — cuánta da la ficha de origen: Alta 148, Media 23, Baja 0.
     · RevisarClasificacion 4 y RevisarCreditos 14 — las dos banderas de revisión de la fuente.
     · DiapositivaOrigen  171 — de qué lámina del catálogo original salió cada ficha. Es la
       procedencia del registro, y existía en la tabla plana que se retiró en `V20260904_03`.

   NO SE CREAN `LineaEstrategica` NI UNA COLUMNA DE LICENCIA. Septiembre sí creó la primera «para
   que el diario editorial la pueda llenar sin otra migración», y aquí no: está vacía en las 171 y
   una columna que nadie llena es una columna que nadie mira. La licencia ya existe en SIMUS como
   `DerechosLicenciaONota`, y la fuente no declara ninguna: escribir «Todos los derechos reservados»
   por omisión sería inventar una afirmación jurídica que nadie hizo. */
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'NotaFecha') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD NotaFecha nvarchar(300) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'CategoriaSecundaria') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD CategoriaSecundaria nvarchar(240) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'AmbitoTexto') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD AmbitoTexto nvarchar(240) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'NotasCatalogacion') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD NotasCatalogacion nvarchar(500) NULL;
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Confianza') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD Confianza nvarchar(20) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PublicacionesEditoriales_Confianza')
    ALTER TABLE dbo.PublicacionesEditoriales ADD CONSTRAINT CK_PublicacionesEditoriales_Confianza
        CHECK (Confianza IS NULL OR Confianza IN (N'Alta', N'Media', N'Baja'));
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'RevisarClasificacion') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD RevisarClasificacion bit NOT NULL
        CONSTRAINT DF_PublicacionesEditoriales_RevisarClasificacion DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'RevisarCreditos') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD RevisarCreditos bit NOT NULL
        CONSTRAINT DF_PublicacionesEditoriales_RevisarCreditos DEFAULT (0);
GO
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'DiapositivaOrigen') IS NULL
    ALTER TABLE dbo.PublicacionesEditoriales ADD DiapositivaOrigen nvarchar(20) NULL;
GO

/* ─────────────────────────── Idioma, que a veces es más de uno ─────────────────────────── */
IF COL_LENGTH(N'dbo.PublicacionesEditoriales', N'Idioma') = 40   /* nvarchar(20) = 40 bytes */
    ALTER TABLE dbo.PublicacionesEditoriales ALTER COLUMN Idioma nvarchar(120) NULL;
GO

/* ─────────────────────────── El vocabulario, sembrado ───────────────────────────
   DCMI Type Vocabulary (dc:type) y las tres listas de RDA. Se insertan solo los que faltan, para
   que el guion se pueda volver a pasar sin duplicar nada. */
MERGE dbo.TipologiasEditoriales AS destino
USING (VALUES
    /* Dublin Core — DCMI Type Vocabulary. NO se traducen: son términos normalizados. */
    (N'recurso',   N'text',                N'Text',                N'DCMI Type Vocabulary',  1),
    (N'recurso',   N'sound',               N'Sound',               N'DCMI Type Vocabulary',  2),
    (N'recurso',   N'movingimage',         N'MovingImage',         N'DCMI Type Vocabulary',  3),
    (N'recurso',   N'image',               N'Image',               N'DCMI Type Vocabulary',  4),
    (N'recurso',   N'interactiveresource', N'InteractiveResource', N'DCMI Type Vocabulary',  5),
    (N'recurso',   N'dataset',             N'Dataset',             N'DCMI Type Vocabulary',  6),
    (N'recurso',   N'collection',          N'Collection',          N'DCMI Type Vocabulary',  7),

    /* RDA 336 — tipo de contenido. */
    (N'contenido', N'texto',               N'texto',                                  N'RDA 336', 1),
    (N'contenido', N'musica-notada',       N'música notada',                          N'RDA 336', 2),
    (N'contenido', N'musica-interpretada', N'música interpretada',                    N'RDA 336', 3),
    (N'contenido', N'imagen-movimiento',   N'imagen en movimiento bidimensional',     N'RDA 336', 4),
    (N'contenido', N'imagen-fija',         N'imagen fija',                            N'RDA 336', 5),
    (N'contenido', N'palabra-hablada',     N'palabra hablada',                        N'RDA 336', 6),

    /* RDA 337 — tipo de medio. */
    (N'medio',     N'sin-mediacion',       N'sin mediación',       N'RDA 337', 1),
    (N'medio',     N'audio',               N'audio',               N'RDA 337', 2),
    (N'medio',     N'video',               N'vídeo',               N'RDA 337', 3),
    (N'medio',     N'informatico',         N'informático',         N'RDA 337', 4),

    /* RDA 338 — tipo de soporte. «disco de computador» no está en la tabla resumida del documento
       y sí en la lista oficial de RDA; describe seis fichas del acervo. */
    (N'soporte',   N'volumen',             N'volumen',             N'RDA 338', 1),
    (N'soporte',   N'disco-de-audio',      N'disco de audio',      N'RDA 338', 2),
    (N'soporte',   N'videodisco',          N'videodisco',          N'RDA 338', 3),
    (N'soporte',   N'disco-de-computador', N'disco de computador', N'RDA 338', 4),
    (N'soporte',   N'recurso-en-linea',    N'recurso en línea',    N'RDA 338', 5)
) AS origen (Eje, Codigo, Etiqueta, Norma, Orden)
    ON destino.Eje = origen.Eje AND destino.Codigo = origen.Codigo
WHEN MATCHED THEN UPDATE SET
    destino.Etiqueta = origen.Etiqueta,
    destino.Norma = origen.Norma,
    destino.Orden = origen.Orden
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Eje, Codigo, Etiqueta, Norma, Orden)
    VALUES (origen.Eje, origen.Codigo, origen.Etiqueta, origen.Norma, origen.Orden);
GO
