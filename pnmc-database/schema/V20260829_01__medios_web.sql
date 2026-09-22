/* =================================================================================================
   LAS IMAGENES ADMINISTRABLES DEL SITIO
   =================================================================================================

   EL MODELO: NADA. Cada imagen del sitio publico esta escrita a mano, y no solo en las
   plantillas: nueve de las dieciseis viven en ficheros de configuracion de TypeScript:

     core/services/media-library.config.ts,30,31,32     4 portadas rotatorias del Home
     features/pnmc/.../sobre-el-pnmc-page.component.html:7          hero, atributo bgImage literal
     features/content/.../ejes-page.component.html:7                hero, atributo bgImage literal
     features/ecosistema/.../ecosistema-home-page.component.html:2  hero, atributo bgImage literal
     core/services/ejes-data.config.ts, :63, :132       3 videoImg, uno por eje
     core/cms/resolve-strategy.ts, :72                  2 imagenes de estrategia
     shared/.../navigation.component.html:14               pnmc-blanco.png
     shared/.../footer.component.html:7, :10               logo-gov-co.png, gov-co-footer.png
     features/external-access/external-access-page.component.html:22,141,143,145  las cuatro marcas

   Son DIECISEIS RANURAS sobre DIEZ ARCHIVOS distintos: seis URLs de images.unsplash.com y cuatro
   ficheros de public/assets/branding. Cambiar la portada de una pagina exigia tocar el codigo y
   recompilar.

   El numero «quince» que circulo antes no cerraba: sale quince solo si se deja fuera una de las
   cuatro marcas, y cual dejar fuera es una decision, no un hecho.

   Existia la interfaz WebMediaRecord (textos-web.service.ts) y una senal privada `media` (:100)
   que lee localStorage y que NINGUN componente consume: solo alimenta el respaldo de
   exportContent. No es el mecanismo que esta tabla reemplaza; es un resto.

   POR QUE EL BYTE VA EN LA BASE Y NO EN DISCO
   -------------------------------------------
   Porque el respaldo de la base tiene que seguir bastando para restaurar el sitio. Con los
   bytes en un directorio, un .bak restaurado sobre un disco vacio deja dieciseis filas apuntando a
   archivos que no existen y el sitio pierde las imagenes sin decir por que. El precedente propio
   del repositorio va en la misma direccion: las fotografias del equipo viven en base64 dentro de
   dbo.EquipoWeb (WebTeamContract.MaxPhotoChars = 300_000, WebTeamContract.cs).

   POR QUE varbinary Y NO base64 EN nvarchar(max)
   ----------------------------------------------
   nvarchar es UTF-16: dos bytes por caracter. base64 anade un 33% y el UTF-16 lo duplica, asi que
   un WebP de 300 KB ocupa unos 800 KB de columna. Con varbinary ocupa 300 KB. Ademas servirlo no
   exige un Convert.FromBase64String por peticion publica. El coste es estrenar varbinary en este
   API —hoy no hay ni uno, ni un IFormFile, ni una ruta multipart— y se asume.

   LO QUE CUESTA, CON EL NUMERO
   ----------------------------
   Tope por archivo 2 MiB. El fichero mas grande que hay hoy es pnmc-blanco.png con 227.579 bytes.
   Un hero de 1600 px en WebP q80 ronda 250-300 KB.

     techo absoluto  16 claves x 2 mitades x 2 MiB               =  64 MiB
                     16 claves x 6 entradas de historial x 2 MiB = 192 MiB
     realista a 250 KB por imagen y tres versiones por clave     =  8 MB + 24 MB

   Referencia: dbo.EquipoWeb ya admite 9 personas x 300.000 caracteres x 2 mitades = 10,8 MB de
   nvarchar(max), y su GET publico manda la celda entera a cada visitante de «Sobre el PNMC»
   (WebTeamEndpoints.cs). Esto no estrena un orden de magnitud: lo iguala.

   POR QUE HAY COLUMNA Editable
   ----------------------------
   Tres de las cuatro marcas son institucionales ajenas: GOV.CO, el sello de Colombia y el
   Ministerio de las Culturas. Que el panel deje reemplazar el escudo nacional no es una funcion.
   CK_MediosWeb_NoEditableSinBytes hace que una clave no editable NUNCA pueda guardar bytes, y eso
   no depende de que el API se acuerde de comprobarlo.

   ADVERTENCIA SOBRE EL CARRIL DE PRUEBAS
   --------------------------------------
   La suite por omision corre sobre SQLite fabricada desde el modelo de EF
   (TestWebApplicationFactory.cs, UseSqlite + EnsureCreated). NINGUNO de los CHECK de abajo existe
   alli. Todo lo que protegen se prueba ademas en el carril PNMC_PRUEBAS_SQLSERVER=1, o la suite
   queda verde sobre restricciones que nunca se ejercitaron.

   ESTE GUION NO SE DUPLICA EN DatabaseBootstrapper.cs, a proposito. ParidadEsquemaSinArranque es
   lo que hace segura esa decision: si el modelo de EF mapea algo que este guion no crea, la prueba
   lo lista.

   ES IDEMPOTENTE, como el resto de pnmc-database/schema/.
   ================================================================================================= */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ------------------------------------------------------------------------------------------------
   1. La ranura: una fila por sitio del sitio donde va una imagen editable
   ------------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.MediosWeb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MediosWeb
    (
        IdMedioWeb int IDENTITY(1,1) NOT NULL,

        /* Mismos anchos que dbo.ContenidoWeb: el panel los agrupa y los pinta igual. */
        Clave nvarchar(160) NOT NULL,
        GrupoId nvarchar(120) NOT NULL,
        GrupoEtiqueta nvarchar(160) NOT NULL,
        Seccion nvarchar(120) NOT NULL,
        Etiqueta nvarchar(240) NOT NULL,

        /* Como se usa. Un fondo se recorta con object-cover y se le baja la opacidad
           (page-hero.component.html:16); un logotipo se encaja con object-contain. */
        Uso nvarchar(40) NOT NULL CONSTRAINT DF_MediosWeb_Uso DEFAULT (N'fondo'),
        AnchoSugerido int NULL,
        AltoSugerido int NULL,

        /* Marca institucional ajena: se declara para que nadie la busque, no se deja subir. */
        Editable bit NOT NULL CONSTRAINT DF_MediosWeb_Editable DEFAULT (1),

        /* Accesibilidad. Pertenece a la imagen, no a dbo.ContenidoWeb. */
        TextoAlternativo nvarchar(300) NOT NULL
            CONSTRAINT DF_MediosWeb_TextoAlternativo DEFAULT (N''),

        /* --- Borrador: lo que ve el editor --- */
        BorradorContenido  varbinary(max) NULL,
        BorradorTipo       nvarchar(40) NULL,
        BorradorBytes      int NULL,
        BorradorAncho      int NULL,
        BorradorAlto       int NULL,
        /* SHA-256 en hexadecimal. SHA1 y MD5 no son opcion: CA5350 y CA5351 estan en
           WarningsAsErrors (Directory.Build.props:126), asi que usarlos no compila. */
        BorradorHuella     char(64) NULL,
        /* WebP de 320 px para la cuadricula del panel. En columna aparte a proposito: abrir el
           panel cuesta 16 x 24 KiB y no 16 x 250 KB. */
        BorradorMiniatura  varbinary(max) NULL,

        /* --- Publicado: lo que ve el visitante. Mitad simetrica: publicar es copiar 7 columnas. */
        PublicadoContenido  varbinary(max) NULL,
        PublicadoTipo       nvarchar(40) NULL,
        PublicadoBytes      int NULL,
        PublicadoAncho      int NULL,
        PublicadoAlto       int NULL,
        PublicadoHuella     char(64) NULL,
        PublicadoMiniatura  varbinary(max) NULL,

        /* --- Ciclo, identico al de dbo.ContenidoWeb --- */
        Version int NOT NULL CONSTRAINT DF_MediosWeb_Version DEFAULT (1),
        ActualizadoPor nvarchar(160) NOT NULL
            CONSTRAINT DF_MediosWeb_ActualizadoPor DEFAULT (N'Sistema'),
        FechaActualizacion datetime2(0) NOT NULL
            CONSTRAINT DF_MediosWeb_FechaActualizacion DEFAULT (SYSUTCDATETIME()),
        /* Nulo en dos casos que NO son el mismo: «nunca se publico» y «se retiro». El estado se
           deriva de las dos columnas y no se guarda, igual que en dbo.ContenidoWeb. */
        FechaRetiro datetime2(0) NULL,

        CONSTRAINT PK_MediosWeb PRIMARY KEY (IdMedioWeb),
        CONSTRAINT UQ_MediosWeb_Clave UNIQUE (Clave),

        CONSTRAINT CK_MediosWeb_Uso CHECK (Uso IN (N'fondo', N'logotipo')),

        /* Tres tipos y ni uno mas. SVG queda fuera y NO es una omision: un SVG es un documento XML
           que puede llevar <script>, y esta ruta lo serviria desde el mismo origen que el sitio.
           Un SVG publicado seria XSS almacenado con permiso de webmaster. */
        CONSTRAINT CK_MediosWeb_BorradorTipo CHECK (
            BorradorTipo IS NULL OR BorradorTipo IN (N'image/webp', N'image/png', N'image/jpeg')),
        CONSTRAINT CK_MediosWeb_PublicadoTipo CHECK (
            PublicadoTipo IS NULL OR PublicadoTipo IN (N'image/webp', N'image/png', N'image/jpeg')),

        /* Cada mitad esta entera o no esta. Sin esto cabe una fila con bytes y sin tipo, y el
           endpoint publico serviria un Content-Type nulo. */
        CONSTRAINT CK_MediosWeb_BorradorCompleto CHECK (
            (BorradorContenido IS NULL AND BorradorTipo IS NULL AND BorradorBytes IS NULL
                AND BorradorAncho IS NULL AND BorradorAlto IS NULL AND BorradorHuella IS NULL)
            OR (BorradorContenido IS NOT NULL AND BorradorTipo IS NOT NULL AND BorradorBytes IS NOT NULL
                AND BorradorAncho IS NOT NULL AND BorradorAlto IS NOT NULL AND BorradorHuella IS NOT NULL)),
        CONSTRAINT CK_MediosWeb_PublicadoCompleto CHECK (
            (PublicadoContenido IS NULL AND PublicadoTipo IS NULL AND PublicadoBytes IS NULL
                AND PublicadoAncho IS NULL AND PublicadoAlto IS NULL AND PublicadoHuella IS NULL)
            OR (PublicadoContenido IS NOT NULL AND PublicadoTipo IS NOT NULL AND PublicadoBytes IS NOT NULL
                AND PublicadoAncho IS NOT NULL AND PublicadoAlto IS NOT NULL AND PublicadoHuella IS NOT NULL)),

        /* Publicado y retirado a la vez es imposible. dbo.ContenidoWeb lo deriva en codigo; aqui
           lo impide ademas la base. */
        CONSTRAINT CK_MediosWeb_NoPublicadoYRetirado CHECK (
            PublicadoContenido IS NULL OR FechaRetiro IS NULL),

        /* El tope tambien en la base, no solo en el API: 2 MiB. */
        CONSTRAINT CK_MediosWeb_BorradorTope CHECK (
            BorradorBytes IS NULL OR (BorradorBytes > 0 AND BorradorBytes <= 2097152)),
        CONSTRAINT CK_MediosWeb_PublicadoTope CHECK (
            PublicadoBytes IS NULL OR (PublicadoBytes > 0 AND PublicadoBytes <= 2097152)),
        CONSTRAINT CK_MediosWeb_MiniaturaTope CHECK (
            (BorradorMiniatura IS NULL OR DATALENGTH(BorradorMiniatura) <= 24576)
            AND (PublicadoMiniatura IS NULL OR DATALENGTH(PublicadoMiniatura) <= 24576)),

        /* Los bytes declarados son los bytes guardados. Sin esto, BorradorBytes es un numero que
           el panel muestra y que nadie comprueba. */
        CONSTRAINT CK_MediosWeb_BytesReales CHECK (
            (BorradorContenido IS NULL OR DATALENGTH(BorradorContenido) = BorradorBytes)
            AND (PublicadoContenido IS NULL OR DATALENGTH(PublicadoContenido) = PublicadoBytes)),

        /* Dimensiones plausibles: cierra la bomba de descompresion sin decodificar un pixel. Un
           PNG de 4 KB que dice medir 50.000 x 50.000 se rechaza antes de reservar nada. */
        CONSTRAINT CK_MediosWeb_Dimensiones CHECK (
            (BorradorAncho IS NULL OR (BorradorAncho BETWEEN 1 AND 12000 AND BorradorAlto BETWEEN 1 AND 12000))
            AND (PublicadoAncho IS NULL OR (PublicadoAncho BETWEEN 1 AND 12000 AND PublicadoAlto BETWEEN 1 AND 12000))),

        /* La huella es hexadecimal en minusculas. COLLATE BIN2 porque con la intercalacion por
           omision, que no distingue mayusculas, el NOT LIKE '%[^0-9a-f]%' NO rechaza 'A'. */
        CONSTRAINT CK_MediosWeb_HuellaHex CHECK (
            (BorradorHuella IS NULL
                OR BorradorHuella COLLATE Latin1_General_BIN2 NOT LIKE '%[^0-9a-f]%')
            AND (PublicadoHuella IS NULL
                OR PublicadoHuella COLLATE Latin1_General_BIN2 NOT LIKE '%[^0-9a-f]%')),

        /* Una clave no editable no guarda bytes. Lo impide la base, no la costumbre del API. */
        CONSTRAINT CK_MediosWeb_NoEditableSinBytes CHECK (
            Editable = 1 OR (BorradorContenido IS NULL AND PublicadoContenido IS NULL))
    );

    /* El panel abre un grupo entero, igual que en dbo.ContenidoWeb. */
    CREATE INDEX IX_MediosWeb_GrupoId ON dbo.MediosWeb (GrupoId);

    PRINT 'V20260829_01: dbo.MediosWeb creada.';
END
ELSE
BEGIN
    PRINT 'V20260829_01: dbo.MediosWeb ya existia.';
END
GO

/* ------------------------------------------------------------------------------------------------
   2. El historial

   Gemelo de dbo.ContenidoWebHistorial (V20260823_02__objetos_del_arranque.sql:145). Guarda el
   ARCHIVO y no solo el hecho: eso es lo que hace posible restaurar, que es la razon por la que el
   historial existe. Nulo cuando la accion no trae archivo —retirar quita del sitio y no reescribe
   el borrador—, igual que ContenidoWebHistorial.Valor.

   LO QUE CUESTA: la poda vigente conserva 6 entradas por clave (PodaDelHistorialContenidoWeb.Tope,
   PodaDelHistorialContenidoWeb.cs). 16 x 6 x 2 MiB son 192 MiB de techo y unos 24 MB realistas.

   SIN LA ACCION 'importado', y no es un olvido: el respaldo JSON del panel no lleva imagenes, y el
   importador ya lo declara informando `entradasDeMedios` en su bloque de no importado
   (WebContentImportEndpoints.cs). Anadir el valor prometeria un camino que no existe. Los
   otros cuatro son los de WebCmsGuards.cs-53.
   ------------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.MediosWebHistorial', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MediosWebHistorial
    (
        IdMedioWebHistorial bigint IDENTITY(1,1) NOT NULL,
        Clave nvarchar(160) NOT NULL,
        Accion nvarchar(40) NOT NULL,

        Contenido varbinary(max) NULL,
        Tipo nvarchar(40) NULL,
        Bytes int NULL,
        Ancho int NULL,
        Alto int NULL,
        Huella char(64) NULL,

        Usuario nvarchar(160) NOT NULL,
        Fecha datetime2(0) NOT NULL
            CONSTRAINT DF_MediosWebHistorial_Fecha DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_MediosWebHistorial PRIMARY KEY (IdMedioWebHistorial),

        CONSTRAINT CK_MediosWebHistorial_Accion CHECK (
            Accion IN (N'guardado', N'publicado', N'retirado', N'republicado')),
        CONSTRAINT CK_MediosWebHistorial_Tipo CHECK (
            Tipo IS NULL OR Tipo IN (N'image/webp', N'image/png', N'image/jpeg')),
        CONSTRAINT CK_MediosWebHistorial_Tope CHECK (
            Bytes IS NULL OR (Bytes > 0 AND Bytes <= 2097152)),
        CONSTRAINT CK_MediosWebHistorial_BytesReales CHECK (
            Contenido IS NULL OR DATALENGTH(Contenido) = Bytes)
    );

    /* La consulta del panel es «las N ultimas de esta clave», igual que en textos. */
    CREATE INDEX IX_MediosWebHistorial_Clave_Fecha
        ON dbo.MediosWebHistorial (Clave, Fecha DESC);

    PRINT 'V20260829_01: dbo.MediosWebHistorial creada.';
END
ELSE
BEGIN
    PRINT 'V20260829_01: dbo.MediosWebHistorial ya existia.';
END
GO

/* NOTA PARA QUIEN AMPLIE ESTE GUION MANANA, que es la trampa que ya documento V20260823_02:
   COL_LENGTH devuelve NULL tambien cuando la TABLA no existe, no solo cuando falta la columna. Un
   ALTER TABLE ADD guardado solo por COL_LENGTH se ejecuta y revienta en cuanto la tabla deja de
   crearse. Toda columna nueva va DESPUES del CREATE de su tabla. */
