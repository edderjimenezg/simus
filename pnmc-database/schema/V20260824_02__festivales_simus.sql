/*
    SIMUS · Festivales · Catalogos, columnas y tablas puente del modulo

    QUE HACE ESTE GUION
    -------------------
    Completa el modelo de Festivales con lo que necesita para describir una edicion entera:
    nueve catalogos propios del modulo, seis columnas nuevas en `VersionesFestival`, tres en
    `Festivales` y las seis tablas puente que relacionan una version con esos catalogos.

    Es aditivo. Ninguna columna existente cambia de tipo, de nulabilidad o de nombre, salvo el
    par territorial de §F, que se alinea con `Divipola` para poder declarar su clave foranea.

    LOS CATALOGOS NACEN VACIOS
    --------------------------
    Este fichero no contiene ni un `INSERT`. El contenido de los catalogos es vocabulario
    institucional y se decide en el propio sistema: sembrarlo aqui ataria el modelo a unos
    identificadores fijados fuera de el.

    CONVENCIONES QUE SIGUE
    ----------------------
    - Nombres en espanol y prefijos `PK_`, `UQ_`, `CK_`, `FK_`, `IX_`, `DF_`.
    - Guiones idempotentes: cada objeto se crea solo si no existe.
    - Forma de catalogo: Id / Nombre / Slug / Descripcion / OrdenVisualizacion.
    - Comentarios sin tildes, como el resto de `pnmc-database/schema/`.
    - `GO` entre anadir una columna y restringirla: SQL Server compila el lote entero antes de
      ejecutarlo, y la resolucion diferida de nombres cubre tablas, no columnas.
    - Columnas de puente por version: `VersionFestivalId` + `<Catalogo>Id` + `FechaCreacion`.
*/

-- Un guion no debe depender de las opciones de sesion de quien lo lance.
-- Ver pnmc-database/MIGRADOR.md.
SET QUOTED_IDENTIFIER ON;

/* =====================================================================================
   §A · LOS NUEVE CATALOGOS DEL MODULO

   El modulo de Festivales necesita doce vocabularios. Dos ya existen en el sistema y se
   reutilizan: los territorios sonoros viven en `TerritoriosSonoros` y los estados en
   `EstadosContenido`. Un tercero, la region OCAD, no se crea aqui: es una agrupacion de
   municipios, es decir geografia, y la geografia la resuelve `Divipola`.

   Los nueve restantes se crean en esta seccion y nacen VACIOS. Ninguno lleva columna
   `Activo`: un catalogo que puede desactivar filas obliga a que cada consulta se acuerde de
   filtrarlo, y la que se olvide muestra lo retirado sin que nada avise.
   ===================================================================================== */

IF OBJECT_ID(N'dbo.TipologiasFestival', N'U') IS NULL
BEGIN
    /* SIMUS: ART_MUS_FESTIVALES_TIPOLOGIA (ID, TIPOLOGIA nvarchar(50), ACTIVO int). */
    CREATE TABLE dbo.TipologiasFestival
    (
        IdTipologiaFestival int IDENTITY(1,1) NOT NULL,
        NombreTipologiaFestival nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_TipologiasFestival PRIMARY KEY (IdTipologiaFestival),
        CONSTRAINT UQ_TipologiasFestival_Nombre UNIQUE (NombreTipologiaFestival),
        CONSTRAINT UQ_TipologiasFestival_Slug UNIQUE (Slug),
        CONSTRAINT CK_TipologiasFestival_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.TiposOrganizador', N'U') IS NULL
BEGIN
    /*
        NO es `Entidades.TipoEntidad`. Aquel clasifica QUE ES una entidad del ecosistema
        (individuo, colectivo, espacio, festival...); este clasifica a QUIEN ORGANIZA un
        festival. Son dos vocabularios distintos y no deben unificarse.
    */
    CREATE TABLE dbo.TiposOrganizador
    (
        IdTipoOrganizador int IDENTITY(1,1) NOT NULL,
        NombreTipoOrganizador nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_TiposOrganizador PRIMARY KEY (IdTipoOrganizador),
        CONSTRAINT UQ_TiposOrganizador_Nombre UNIQUE (NombreTipoOrganizador),
        CONSTRAINT UQ_TiposOrganizador_Slug UNIQUE (Slug),
        CONSTRAINT CK_TiposOrganizador_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.FuentesFinanciacion', N'U') IS NULL
BEGIN
    /* SIMUS: ART_MUS_FESTIVALES_FUENTE_FINANCIACION (ID, FUENTE_FINANCIACION, ACTIVO bit). */
    CREATE TABLE dbo.FuentesFinanciacion
    (
        IdFuenteFinanciacion int IDENTITY(1,1) NOT NULL,
        NombreFuenteFinanciacion nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_FuentesFinanciacion PRIMARY KEY (IdFuenteFinanciacion),
        CONSTRAINT UQ_FuentesFinanciacion_Nombre UNIQUE (NombreFuenteFinanciacion),
        CONSTRAINT UQ_FuentesFinanciacion_Slug UNIQUE (Slug),
        CONSTRAINT CK_FuentesFinanciacion_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.ExpresionesArtisticas', N'U') IS NULL
BEGIN
    /* SIMUS: ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS (ID, EXPRESION_ARTISTICA, ACTIVO). */
    CREATE TABLE dbo.ExpresionesArtisticas
    (
        IdExpresionArtistica int IDENTITY(1,1) NOT NULL,
        NombreExpresionArtistica nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_ExpresionesArtisticas PRIMARY KEY (IdExpresionArtistica),
        CONSTRAINT UQ_ExpresionesArtisticas_Nombre UNIQUE (NombreExpresionArtistica),
        CONSTRAINT UQ_ExpresionesArtisticas_Slug UNIQUE (Slug),
        CONSTRAINT CK_ExpresionesArtisticas_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.ModalidadesParticipacion', N'U') IS NULL
BEGIN
    /* SIMUS: ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION (ID, MODALIDAD_PARTICIPACION, ACTIVO). */
    CREATE TABLE dbo.ModalidadesParticipacion
    (
        IdModalidadParticipacion int IDENTITY(1,1) NOT NULL,
        NombreModalidadParticipacion nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_ModalidadesParticipacion PRIMARY KEY (IdModalidadParticipacion),
        CONSTRAINT UQ_ModalidadesParticipacion_Nombre UNIQUE (NombreModalidadParticipacion),
        CONSTRAINT UQ_ModalidadesParticipacion_Slug UNIQUE (Slug),
        CONSTRAINT CK_ModalidadesParticipacion_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.NaturalezasEntidad', N'U') IS NULL
BEGIN
    /*
        SIMUS: ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD (ID, NATURALEZA nchar(20)).
        `nchar(20)` es texto de ancho fijo con relleno de espacios: aqui pasa a `nvarchar`.
        El relleno NO se replica, igual que no se replica el `nchar(10)` de las fechas.
        Su unico consumidor en origen es ENTIDADES_ALIADAS, que aqui es
        `VersionesFestivalEntidadesSocias` (§D.5).
    */
    CREATE TABLE dbo.NaturalezasEntidad
    (
        IdNaturalezaEntidad int IDENTITY(1,1) NOT NULL,
        NombreNaturalezaEntidad nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_NaturalezasEntidad PRIMARY KEY (IdNaturalezaEntidad),
        CONSTRAINT UQ_NaturalezasEntidad_Nombre UNIQUE (NombreNaturalezaEntidad),
        CONSTRAINT UQ_NaturalezasEntidad_Slug UNIQUE (Slug),
        CONSTRAINT CK_NaturalezasEntidad_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.ZonasUrbanoRural', N'U') IS NULL
BEGIN
    /*
        SIMUS: ART_MUS_FESTIVALES_ZONA (ID, MUS_FESTIVALES_ZONA nvarchar(50), ES_RURAL bit).
        Dos filas: urbana y rural. Se cambian dos cosas de origen y ambas a proposito:

        1. El nombre de columna `MUS_FESTIVALES_ZONA` mete el nombre de la tabla dentro del
           nombre de la columna. Aqui es `NombreZonaUrbanoRural`.
        2. `ES_RURAL` NO viaja. Con `Slug` NOT NULL y UNIQUE, el bit es una segunda forma
           de decir lo mismo y nada mantiene las dos sincronizadas: se podria grabar
           `Slug='rural'` con `EsRural=0` y la base lo aceptaria.

        Esto NO es `Divipola.TipoTerritorio`: aquella columna clasifica la CLASE de territorio
        -'MUNICIPIO', 'ISLA', 'AREA NO MUNICIPALIZADA'-, no si el ambito es urbano o rural.
    */
    CREATE TABLE dbo.ZonasUrbanoRural
    (
        IdZonaUrbanoRural int IDENTITY(1,1) NOT NULL,
        NombreZonaUrbanoRural nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_ZonasUrbanoRural PRIMARY KEY (IdZonaUrbanoRural),
        CONSTRAINT UQ_ZonasUrbanoRural_Nombre UNIQUE (NombreZonaUrbanoRural),
        CONSTRAINT UQ_ZonasUrbanoRural_Slug UNIQUE (Slug),
        CONSTRAINT CK_ZonasUrbanoRural_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.TitulacionesColectivas', N'U') IS NULL
BEGIN
    /*
        SIMUS: ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA
               (ID, DESCRPCION_ZONA_TITULACION_COLECTIVA nvarchar(250), ACTIVO int).

        El typo fisico `DESCRPCION_` —sin la I— NO SE REPLICA. Y la columna, pese a
        llamarse «descripcion», es el NOMBRE de la fila del catalogo: aqui es
        `NombreTitulacionColectiva`, y `Descripcion` queda libre para lo que de verdad es
        una descripcion.
    */
    CREATE TABLE dbo.TitulacionesColectivas
    (
        IdTitulacionColectiva int IDENTITY(1,1) NOT NULL,
        NombreTitulacionColectiva nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_TitulacionesColectivas PRIMARY KEY (IdTitulacionColectiva),
        CONSTRAINT UQ_TitulacionesColectivas_Nombre UNIQUE (NombreTitulacionColectiva),
        CONSTRAINT UQ_TitulacionesColectivas_Slug UNIQUE (Slug),
        CONSTRAINT CK_TitulacionesColectivas_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.TiposIngreso', N'U') IS NULL
BEGIN
    /*
        SIMUS: ART_MUS_TIPOINGRESO (ID, TIPO_INGRESO nvarchar(50)).
        «Tipo de ingreso» del publico a la edicion (entrada libre, con boleta, ...), no
        ingreso economico. Su puente es §D.3.
    */
    CREATE TABLE dbo.TiposIngreso
    (
        IdTipoIngreso int IDENTITY(1,1) NOT NULL,
        NombreTipoIngreso nvarchar(140) NOT NULL,
        Slug nvarchar(160) NOT NULL,
        Descripcion nvarchar(800) NULL,
        OrdenVisualizacion int NOT NULL,
        CONSTRAINT PK_TiposIngreso PRIMARY KEY (IdTipoIngreso),
        CONSTRAINT UQ_TiposIngreso_Nombre UNIQUE (NombreTipoIngreso),
        CONSTRAINT UQ_TiposIngreso_Slug UNIQUE (Slug),
        CONSTRAINT CK_TiposIngreso_Orden CHECK (OrdenVisualizacion > 0)
    );
END;
GO

/* =====================================================================================
   §B · SEIS COLUMNAS NUEVAS EN `VersionesFestival`

   Todas anulables: el formulario admite guardar una version incompleta y ninguna de estas
   seis es obligatoria para que una edicion exista.
   ===================================================================================== */

/* --- B.1 Las fechas de la edicion. La ausencia mas grande del modelo destino. ---

   En SIMUS `FECHA_INICIO` y `FECHA_FIN` son `nchar(10)`: TEXTO DE ANCHO FIJO CON RELLENO
   DE ESPACIOS, no fecha. Diez caracteres no admiten hora, solo `yyyy-MM-dd` o `dd/MM/yyyy`,
   y el motor no impide grabar `hola      `.

   AQUI SON `date`, Y ESO ES DELIBERADO. `date` es el tipo que PNMC ya usa para las fechas
   de edicion en `Festivales.FechaInicioVersionActual` / `FechaFinVersionActual`, y es el
   que hace imposible por construccion lo que en origen solo evitaba la disciplina del
   formulario. El relleno de `nchar` no se replica: no hay nada que rellenar.

   (Si algun dia entrara texto de SIMUS por otra via, la regla de lectura es
   `TRY_CONVERT(date, LTRIM(RTRIM(col)), 126)` con el estilo 126 explicito. Sin el, el
   motor acepta `17-06-2025` segun el idioma de la sesion e intercambia dia y mes SIN DAR
   ERROR. Se deja escrito aunque hoy no viajen datos.) */

IF COL_LENGTH(N'dbo.VersionesFestival', N'FechaInicio') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD FechaInicio date NULL;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'FechaFin') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD FechaFin date NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'CK_VersionesFestival_FechasEdicion')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT CK_VersionesFestival_FechasEdicion
        CHECK (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio);
GO

/* --- B.2 Tipologia de la edicion --- */

IF COL_LENGTH(N'dbo.VersionesFestival', N'TipologiaFestivalId') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD TipologiaFestivalId int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'FK_VersionesFestival_TipologiaFestival')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT FK_VersionesFestival_TipologiaFestival
        FOREIGN KEY (TipologiaFestivalId) REFERENCES dbo.TipologiasFestival (IdTipologiaFestival);
GO

/* --- B.3 Las dos fuentes de financiacion ---

   SIMUS tiene DOS claves foraneas distintas a la misma tabla:
   `ID_FUENTE_FINANCIACION` y `ID_FUENTE_FINANCIACION_SECUNDARIA`. La asimetria de origen
   —el texto libre de la primera se llama `OTRA_FUENTE_FINANCIACION_PRIMARIA` aunque su FK
   no lleve sufijo— NO se replica: aqui las dos columnas llevan sufijo.

   POR QUE DOS COLUMNAS Y NO UNA PUENTE. Una puente `VersionesFestivalFuentesFinanciacion`
   con una columna `Orden` seria mas parecida a como PNMC trata practicas y territorios, y
   ademas ADMITIRIA MAS DE DOS FUENTES. Eso ultimo es lo que la descarta: seria un cambio
   FUNCIONAL —el formulario pasaria de «principal y secundaria» a «las que haga falta»— y
   el encargo es estructura. Dos columnas dicen exactamente lo que dice SIMUS. La vuelta
   atras es barata mientras la tabla tenga 0 filas. */

IF COL_LENGTH(N'dbo.VersionesFestival', N'FuenteFinanciacionPrimariaId') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD FuenteFinanciacionPrimariaId int NULL;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'FuenteFinanciacionSecundariaId') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD FuenteFinanciacionSecundariaId int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'FK_VersionesFestival_FuenteFinanciacionPrimaria')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT FK_VersionesFestival_FuenteFinanciacionPrimaria
        FOREIGN KEY (FuenteFinanciacionPrimariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'FK_VersionesFestival_FuenteFinanciacionSecundaria')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT FK_VersionesFestival_FuenteFinanciacionSecundaria
        FOREIGN KEY (FuenteFinanciacionSecundariaId) REFERENCES dbo.FuentesFinanciacion (IdFuenteFinanciacion);
GO

/*
    Primaria y secundaria no pueden ser la misma fuente: «primaria y secundaria iguales» no
    describe nada.
*/
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'CK_VersionesFestival_FuentesDistintas')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT CK_VersionesFestival_FuentesDistintas
        CHECK (FuenteFinanciacionPrimariaId IS NULL
               OR FuenteFinanciacionSecundariaId IS NULL
               OR FuenteFinanciacionPrimariaId <> FuenteFinanciacionSecundariaId);
GO

/* --- B.4 Estampilla Procultura ---

   SIMUS: `USO_ESTAMPILLA_PROCULTURA bit`. Politica publica colombiana: si la edicion uso
   recursos de la estampilla Procultura. PNMC no tiene el concepto en ninguna tabla.

   `FuentesFinanciacion` NO debe contener una fila «Estampilla Procultura»: habria dos
   maneras de afirmar lo mismo y nada que las mantuviera de acuerdo. */

IF COL_LENGTH(N'dbo.VersionesFestival', N'UsaEstampillaProcultura') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD UsaEstampillaProcultura bit NULL;
GO

/* =====================================================================================
   §C · TRES COLUMNAS NUEVAS EN `Festivales`

   Las tres son anulables: una columna NOT NULL con DEFAULT le pondria a las filas ya
   existentes un valor que nadie decidio.
   ===================================================================================== */

/* --- C.1 Quien creo el festival (SIMUS: `creado_por`) ---

   TODO MODULO DE CONTENIDO GUARDA QUIEN LO CREO: `Agenda`, `Noticias`, `AlbumesGaleria` y
   `Entidades` tienen su `IdUsuarioCreador`, y `Festivales` pasa a tenerlo aqui.

   Anulable, y `int?` en la clase: a un registro sin creador conocido no se le pone uno,
   porque eso no seria un dato sino una invencion. */

IF COL_LENGTH(N'dbo.Festivales', N'IdUsuarioCreador') IS NULL
    ALTER TABLE dbo.Festivales ADD IdUsuarioCreador int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.Festivales', N'U')
                 AND name = N'FK_Festivales_UsuarioCreador')
    ALTER TABLE dbo.Festivales
        ADD CONSTRAINT FK_Festivales_UsuarioCreador
        FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.Usuarios (IdUsuario);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Festivales_IdUsuarioCreador'
                 AND object_id = OBJECT_ID(N'dbo.Festivales', N'U'))
    CREATE INDEX IX_Festivales_IdUsuarioCreador ON dbo.Festivales (IdUsuarioCreador);
GO

/* --- C.2 y C.3 El organizador: tipo y director ---

   POR QUE EN `Festivales` Y NO EN `VersionesFestival`, QUE ES DONDE LOS TIENE SIMUS.
   PNMC ya decidio que el organizador es del festival, no de la edicion: `Organizador`,
   `CorreoOrganizador`, `TelefonoOrganizador`, `SitioWebOrganizador` y
   `OrganizacionPrincipalId` estan en `Festivales`, y `VersionesFestival` NO TIENE NI UNA
   columna de organizador. Poner el TIPO en la version y el NOMBRE en la cabecera partiria
   un solo hecho en dos tablas, sin nada que garantice que describen al mismo organizador.

   El coste queda dicho: si el tipo de organizador o el director cambian de una edicion a
   otra, se guarda solo el ultimo. Es la misma eleccion que rige para el nombre del
   organizador. */

IF COL_LENGTH(N'dbo.Festivales', N'TipoOrganizadorId') IS NULL
    ALTER TABLE dbo.Festivales ADD TipoOrganizadorId int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.Festivales', N'U')
                 AND name = N'FK_Festivales_TipoOrganizador')
    ALTER TABLE dbo.Festivales
        ADD CONSTRAINT FK_Festivales_TipoOrganizador
        FOREIGN KEY (TipoOrganizadorId) REFERENCES dbo.TiposOrganizador (IdTipoOrganizador);
GO

/*
    SIMUS: `DIRECTOR nvarchar(250)`. Aqui `nvarchar(220)`, la misma longitud que
    `Organizador`, porque es el mismo tipo de valor en la misma tabla. Director y
    organizador NO son la misma persona: meterlos en la misma columna inventaria un hecho.
    Se llama `Director` sin sufijo, como `Organizador`, no `DirectorFestival`.
*/
IF COL_LENGTH(N'dbo.Festivales', N'Director') IS NULL
    ALTER TABLE dbo.Festivales ADD Director nvarchar(220) NULL;
GO

/* =====================================================================================
   §D · LAS SEIS TABLAS PUENTE POR VERSION

   El modulo ya tiene dos puentes por version —`VersionesFestivalPracticasMusicales` y
   `VersionesFestivalTerritoriosSonoros`—. Aqui se crean las seis restantes: expresiones
   artisticas, modalidades de participacion, tipos de ingreso, localizaciones, entidades
   socias y archivos.

   EN LAS SEIS, LA COLUMNA DEL LADO DE LA VERSION SE LLAMA `VersionFestivalId`, sin
   excepciones, y su clave foranea apunta a `VersionesFestival`. Es deliberado: un mapeo por
   convencion se rompe en cuanto una tabla nombra esa columna de otra manera, y quien lea el
   modelo no deberia tener que recordar cual es la rara.

   ===================================================================================== */

/* --- NINGUNA DE LAS SEIS PUENTES LLEVA `ON DELETE CASCADE` ---

   El sistema no usa cascadas en ninguna clave foranea, y las dos puentes por version que ya
   existian tampoco. Ponerlas solo en estas seis haria que el MISMO `DELETE` sobre una version
   se comportara de dos maneras: bloqueado por unas tablas y destruyendo en silencio en otras.
   Un borrado que hace una cosa u otra segun la tabla no es una politica.

   Si alguna vez se adopta la cascada, se adopta para todas. --- */

/* --- D.1 Expresiones artisticas (SIMUS: ART_MUS_FESTIVALES_EXPRESIONXVERSION) --- */

IF OBJECT_ID(N'dbo.VersionesFestivalExpresionesArtisticas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalExpresionesArtisticas
    (
        IdVersionFestivalExpresionArtistica int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        ExpresionArtisticaId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalExpresiones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalExpresionesArtisticas PRIMARY KEY (IdVersionFestivalExpresionArtistica),
        CONSTRAINT FK_VersionesFestivalExpresiones_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalExpresiones_Expresion FOREIGN KEY (ExpresionArtisticaId)
            REFERENCES dbo.ExpresionesArtisticas (IdExpresionArtistica),
        CONSTRAINT UQ_VersionesFestivalExpresiones UNIQUE (VersionFestivalId, ExpresionArtisticaId)
    );
END;
GO

/* --- D.2 Modalidades de participacion
       (SIMUS: ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACIONXVERSION) --- */

IF OBJECT_ID(N'dbo.VersionesFestivalModalidadesParticipacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalModalidadesParticipacion
    (
        IdVersionFestivalModalidadParticipacion int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        ModalidadParticipacionId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalModalidades_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalModalidadesParticipacion PRIMARY KEY (IdVersionFestivalModalidadParticipacion),
        CONSTRAINT FK_VersionesFestivalModalidades_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalModalidades_Modalidad FOREIGN KEY (ModalidadParticipacionId)
            REFERENCES dbo.ModalidadesParticipacion (IdModalidadParticipacion),
        CONSTRAINT UQ_VersionesFestivalModalidades UNIQUE (VersionFestivalId, ModalidadParticipacionId)
    );
END;
GO

/* --- D.3 Tipos de ingreso ---

   La columna del lado de la version se llama `VersionFestivalId`, igual que en las otras
   cinco puentes. */

IF OBJECT_ID(N'dbo.VersionesFestivalTiposIngreso', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalTiposIngreso
    (
        IdVersionFestivalTipoIngreso int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        TipoIngresoId int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalTiposIngreso_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalTiposIngreso PRIMARY KEY (IdVersionFestivalTipoIngreso),
        CONSTRAINT FK_VersionesFestivalTiposIngreso_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalTiposIngreso_TipoIngreso FOREIGN KEY (TipoIngresoId)
            REFERENCES dbo.TiposIngreso (IdTipoIngreso),
        CONSTRAINT UQ_VersionesFestivalTiposIngreso UNIQUE (VersionFestivalId, TipoIngresoId)
    );
END;
GO

/* --- D.4 Localizaciones ---

   La puente que mas informacion recupera: guarda N MUNICIPIOS POR VERSION, cada uno con su
   zona —urbana o rural— y su titulacion colectiva. La cabecera de la version guarda un solo
   municipio y ninguna de las dos cosas.

   LLEVA CLAVE FORANEA A `Divipola`, como toda tabla que guarde un par departamento/municipio.
   `Divipola` es la fuente unica de territorio del sistema.

   `CodigoMunicipio` es NOT NULL a proposito: una fila sin municipio no diria nada que el
   `NivelCobertura` de la version no diga ya, y `Divipola` no tiene filas de solo departamento.
   Una edicion de cobertura departamental se expresa con el par de la version, no con una fila
   mutilada aqui. */

IF OBJECT_ID(N'dbo.VersionesFestivalLocalizaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalLocalizaciones
    (
        IdVersionFestivalLocalizacion int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        /* char(2)/char(5) para casar con `Divipola`. NO se copia el `nvarchar(20)` de
           `VersionesFestival`, que es un defecto anterior y sin FK que lo sujete. */
        CodigoDepartamento char(2) NOT NULL,
        CodigoMunicipio char(5) NOT NULL,
        ZonaUrbanoRuralId int NULL,
        TitulacionColectivaId int NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalLocalizaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalLocalizaciones PRIMARY KEY (IdVersionFestivalLocalizacion),
        CONSTRAINT FK_VersionesFestivalLocalizaciones_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalLocalizaciones_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_VersionesFestivalLocalizaciones_Zona FOREIGN KEY (ZonaUrbanoRuralId)
            REFERENCES dbo.ZonasUrbanoRural (IdZonaUrbanoRural),
        CONSTRAINT FK_VersionesFestivalLocalizaciones_Titulacion FOREIGN KEY (TitulacionColectivaId)
            REFERENCES dbo.TitulacionesColectivas (IdTitulacionColectiva),
        /* `ZonaUrbanoRuralId` FORMA PARTE DE LA CLAVE UNICA. Sin el, la restriccion prohibiria
           registrar el mismo municipio con zona urbana Y rural, que es precisamente lo que esta
           tabla existe para recuperar. Con la zona sin declarar (NULL), SQL Server admite una
           sola fila por version y municipio. */
        CONSTRAINT UQ_VersionesFestivalLocalizaciones UNIQUE (VersionFestivalId, CodigoMunicipio, ZonaUrbanoRuralId),
        CONSTRAINT CK_VersionesFestivalLocalizaciones_Municipio_Departamento
            CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento)
    );
END;
GO

/* --- D.5 Entidades socias ---

   LA TABLA NO SE LLAMA «ALIADAS», Y NO ES UN CAPRICHO. El sistema retiro el modelo de
   entidades aliadas —tres tablas y tres roles— y una prueba comprueba que no vuelva bajo
   ninguno de sus dos nombres. Tomar prestado ese vocabulario haria creer, a quien mire
   `sys.tables` dentro de seis meses, que el modelo regreso.

   POR QUE UNA TABLA PROPIA Y NO `Entidades` + `EntidadesRelaciones`. Ese camino solo relaciona
   entidad con entidad, y un festival vive en `Festivales`, no en `Entidades`. Espejar el
   festival como una entidad colgaria las socias DEL FESTIVAL Y NO DE LA VERSION. Ademas una
   entidad socia puede ser un nombre y un correo sueltos, no una entidad registrada del
   ecosistema.

   `EntidadId` queda anulable para el caso en que la socia SI este registrada en `Entidades`.
   Es una columna, no un mecanismo: nada obliga a rellenarla. */

IF OBJECT_ID(N'dbo.VersionesFestivalEntidadesSocias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalEntidadesSocias
    (
        IdVersionFestivalEntidadSocia int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        /* ANULABLE: una socia puede identificarse por su nombre o por su vinculo con
           `Entidades`, y el CHECK de mas abajo exige al menos uno de los dos. */
        NombreEntidadSocia nvarchar(300) NULL,
        CorreoEntidadSocia nvarchar(180) NULL,
        NaturalezaEntidadId int NULL,
        EntidadId int NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalEntidadesSocias_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalEntidadesSocias PRIMARY KEY (IdVersionFestivalEntidadSocia),
        CONSTRAINT FK_VersionesFestivalEntidadesSocias_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalEntidadesSocias_Naturaleza FOREIGN KEY (NaturalezaEntidadId)
            REFERENCES dbo.NaturalezasEntidad (IdNaturalezaEntidad),
        CONSTRAINT FK_VersionesFestivalEntidadesSocias_Entidad FOREIGN KEY (EntidadId)
            REFERENCES dbo.Entidades (IdEntidad),
        /* SIN UNIQUE POR NOMBRE. Dos socias homonimas en la misma edicion —dos «Alcaldia
           Municipal» con correos distintos— son un caso real. Queda un indice NO unico en §E
           para la busqueda. */
        CONSTRAINT CK_VersionesFestivalEntidadesSocias_Identificable
            CHECK (NombreEntidadSocia IS NOT NULL OR EntidadId IS NOT NULL)
    );
END;
GO

/* --- D.6 Material multimedia (SIMUS: ART_MUS_MATERIALMULTIMEDIA) ---

   PNMC tiene `Archivos` y cuatro puentes hacia el —`AgendaArchivos`, `NoticiasArchivos`,
   `AlbumesGaleriaArchivos`, `RecursosEditorialesArchivos`— y NINGUNA para festivales.

   SIMUS guarda `URL_ARCHIVO` y `DESCRIPCION_ARCHIVO` en la propia fila. Aqui no: la
   descripcion ya tiene sitio en `Archivos.Pie` y `Archivos.TextoAlternativo`, y la ruta en
   `Archivos.RutaAlmacenamiento` / `UrlPublica`. Repetirlas en la puente crearia dos
   verdades sobre el mismo fichero. La puente aporta lo unico que falta: a que version
   pertenece, con que papel y en que orden. */

IF OBJECT_ID(N'dbo.VersionesFestivalArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalArchivos
    (
        IdVersionFestivalArchivo int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        /* ANULABLE, Y CON `Url` AL LADO. Un material puede ser un fichero subido, registrado en
           `dbo.Archivos`, o un enlace externo. Exigir `ArchivoId NOT NULL` obligaria a
           inventarle a cada enlace una ruta de almacenamiento, un nombre original, un tipo MIME
           y un usuario de carga. El CHECK de mas abajo impide la fila que no apunta a nada. */
        ArchivoId int NULL,
        Url nvarchar(500) NULL,
        RolArchivo nvarchar(80) NOT NULL,
        OrdenVisualizacion int NOT NULL
            CONSTRAINT DF_VersionesFestivalArchivos_Orden DEFAULT (1),
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_VersionesFestivalArchivos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VersionesFestivalArchivos PRIMARY KEY (IdVersionFestivalArchivo),
        CONSTRAINT FK_VersionesFestivalArchivos_Version FOREIGN KEY (VersionFestivalId)
            REFERENCES dbo.VersionesFestival (IdVersionFestival),
        CONSTRAINT FK_VersionesFestivalArchivos_Archivo FOREIGN KEY (ArchivoId)
            REFERENCES dbo.Archivos (IdArchivo),
        /* `RolArchivo` FORMA PARTE DE LA CLAVE UNICA, como en las tres puentes hacia `Archivos`
           que ya existen. Sin el, un mismo fichero no podria ser portada Y galeria de la misma
           version. */
        CONSTRAINT UQ_VersionesFestivalArchivos UNIQUE (VersionFestivalId, ArchivoId, RolArchivo),
        CONSTRAINT CK_VersionesFestivalArchivos_Orden CHECK (OrdenVisualizacion > 0),
        /* Uno de los dos, no ninguno. `RecursosEditorialesArchivos` deja las dos columnas
           anulables sin CHECK y admite la fila que no apunta a nada; ese hueco no se copia. */
        CONSTRAINT CK_VersionesFestivalArchivos_Destino
            CHECK (ArchivoId IS NOT NULL OR Url IS NOT NULL)
    );
END;
GO

/* =====================================================================================
   §E · INDICES DE APOYO

   Solo los del lado del catalogo y el de la columna nueva de `VersionesFestival`. El lado
   de la version ya lo cubre la primera columna de cada UNIQUE.
   ===================================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestivalExpresiones_Expresion'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestivalExpresionesArtisticas', N'U'))
    CREATE INDEX IX_VersionesFestivalExpresiones_Expresion
        ON dbo.VersionesFestivalExpresionesArtisticas (ExpresionArtisticaId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestivalModalidades_Modalidad'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestivalModalidadesParticipacion', N'U'))
    CREATE INDEX IX_VersionesFestivalModalidades_Modalidad
        ON dbo.VersionesFestivalModalidadesParticipacion (ModalidadParticipacionId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestivalTiposIngreso_TipoIngreso'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestivalTiposIngreso', N'U'))
    CREATE INDEX IX_VersionesFestivalTiposIngreso_TipoIngreso
        ON dbo.VersionesFestivalTiposIngreso (TipoIngresoId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestivalLocalizaciones_Municipio'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestivalLocalizaciones', N'U'))
    CREATE INDEX IX_VersionesFestivalLocalizaciones_Municipio
        ON dbo.VersionesFestivalLocalizaciones (CodigoDepartamento, CodigoMunicipio);
GO

/* Busqueda por nombre de aliada. NO unico: ver F6 en la definicion de la tabla. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestivalEntidadesSocias_Nombre'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestivalEntidadesSocias', N'U'))
    CREATE INDEX IX_VersionesFestivalEntidadesSocias_Nombre
        ON dbo.VersionesFestivalEntidadesSocias (NombreEntidadSocia);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VersionesFestival_TipologiaFestivalId'
               AND object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U'))
    CREATE INDEX IX_VersionesFestival_TipologiaFestivalId
        ON dbo.VersionesFestival (TipologiaFestivalId);
GO

/* =====================================================================================
   §F · LA FORANEA A `Divipola` EN `VersionesFestival` Y `PropuestasCambioFestival`

   Toda tabla que guarde un par departamento/municipio declara su clave foranea a `Divipola`, que
   es la fuente unica de territorio del sistema. Esta seccion alinea con esa regla a las dos que
   faltaban.

   POR QUE HACE FALTA UN `ALTER COLUMN` Y NO BASTA ANADIR LA RESTRICCION. Las dos guardaban el
   par en `nvarchar(20)` mientras `Divipola` lo declara `char(2)` / `char(5)`, y SQL Server exige
   el mismo tipo a los dos lados de una foranea.

   ===================================================================================== */

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
             AND name = N'CodigoDepartamento' AND system_type_id = TYPE_ID(N'nvarchar'))
BEGIN
    ALTER TABLE dbo.VersionesFestival ALTER COLUMN CodigoDepartamento char(2) NULL;
    ALTER TABLE dbo.VersionesFestival ALTER COLUMN CodigoMunicipio char(5) NULL;
END;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.PropuestasCambioFestival', N'U')
             AND name = N'CodigoDepartamento' AND system_type_id = TYPE_ID(N'nvarchar'))
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ALTER COLUMN CodigoDepartamento char(2) NULL;
    ALTER TABLE dbo.PropuestasCambioFestival ALTER COLUMN CodigoMunicipio char(5) NULL;
END;
GO

/* Compuesta, sobre las dos columnas, y sin cascada como el resto del modelo. El par es anulable
   a los dos lados, y SQL Server no exige la foranea cuando alguna de las dos columnas es NULL:
   una edicion de cobertura nacional sigue siendo representable. */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_VersionesFestival_Divipola')
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT FK_VersionesFestival_Divipola
        FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PropuestasCambioFestival_Divipola')
    ALTER TABLE dbo.PropuestasCambioFestival
        ADD CONSTRAINT FK_PropuestasCambioFestival_Divipola
        FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
        REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio);
GO

/* =====================================================================================
   §G · LO QUE ESTE GUION NO HACE

   1. NO inserta filas. Los nueve catalogos nacen vacios.
   2. NO crea `RegionesOcad` ni su puente municipio-region: la geografia la resuelve `Divipola`.
   3. NO da a `PropuestasCambioFestival` las columnas ni las puentes nuevas. Una propuesta de
      cambio no puede proponer fechas, tipologia, financiacion, expresiones, modalidades, tipos
      de ingreso, localizaciones, entidades socias ni archivos. Lo unico que §F le toca es el
      tipo del par territorial y su foranea a `Divipola`.
   4. NO anade columna de estado a `VersionesFestival`: el estado es del festival y la vigencia
      es de la version.
   5. NO cambia `Festivales.FechaInicioVersionActual`, `FechaFinVersionActual`,
      `TieneVersionVigenteAnoActual` ni `EstadoVersionAnoActual`, que a partir de §B.1 pasan a
      ser derivables de la version.

   ===================================================================================== */
