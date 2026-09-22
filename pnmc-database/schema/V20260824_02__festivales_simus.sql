/*
    PNMC · Migracion SIMUS · Festivales
    Estado:  DISENO. NO EJECUTADO NUNCA, contra ninguna base.

    QUE ES ESTO
    -----------
    El DDL de lo que le falta a PNMC del modelo de Festivales de SIMUS, y solo eso.
    PNMC ya tiene Festivales, VersionesFestival y PropuestasCambioFestival: aqui no se
    crea un modelo nuevo, se anade lo minimo para que la informacion de SIMUS que hoy no
    tiene donde vivir tenga sitio. La justificacion columna a columna esta en el fichero
    hermano 02-festivales.md; este guion no repite el razonamiento, solo lo aplica.

    ALCANCE (decisiones.md, registro del 24 ago 2026)
    ------------------------------------------------
    - Solo viaja la ESTRUCTURA. No hay ETL, ni ventana de corte, ni consulta viva a SIMUS.
    - Por eso este fichero NO CONTIENE UN SOLO `INSERT`. Los nueve catalogos nacen VACIOS.
      Copiar sus filas traeria los ids que el codigo de SIMUS lleva cableados —cinco en
      festivales: 1, 2, 3, 4 y 5, no dos como decia D2— y eso es justo lo que D12 prohibe:
      «el contenido de los catalogos se decide en PNMC, informado por SIMUS, no copiado
      de SIMUS».
    - D2, D3 y D8 no aplican: hablan de filas que ya no viajan. No hay tabla de
      equivalencia de ids porque no hay ids de origen que equivaler.

    DONDE VA ESTO DE VERDAD
    -----------------------
    Su sitio definitivo es un guion nuevo bajo `pnmc-database/schema/`, porque
    `ParidadEsquemaSinArranqueTests` construye una base SOLO con los .sql de `schema/` y exige
    que exista cada tabla y cada columna que mapea EF. Si estas tablas se quedan aqui, o
    solo en `DatabaseBootstrapper`, esa prueba las declara ausentes. NO se anaden al
    bootstrapper: V20260823_02 movio ese DDL a `schema/` precisamente para dejar de
    tenerlo en dos sitios.

    CONVENCIONES QUE SE SIGUEN (leidas, no supuestas)
    -------------------------------------------------
    - Nombres en espanol, PK_/UQ_/CK_/FK_/IX_/DF_, guiones idempotentes: como
      `schema/V20260519_01__maestras_estaticas.sql`.
    - Forma de catalogo: Id / Nombre / Slug / Descripcion / OrdenVisualizacion, calcada de
      `TerritoriosSonoros` y `PracticasMusicales`.
    - Comentarios sin tildes, como el resto de `pnmc-database/schema/`.
    - `GO` entre anadir una columna y restringirla: SQL Server compila el lote entero
      antes de ejecutarlo y la resolucion diferida de nombres cubre tablas, no columnas.
    - Columnas de puente por version: `VersionFestivalId` + `<Catalogo>Id` + `FechaCreacion`,
      como `VersionesFestivalPracticasMusicales`. NO se usa el patron `IdNoticia`/`IdArchivo`
      de `NoticiasArchivos`: dentro del modulo de festivales manda la convencion del modulo.

    LO QUE ESTE GUION NO TOCA
    -------------------------
    Ni una sola columna existente cambia de tipo, de nulabilidad o de nombre. Todo lo
    nuevo es aditivo y anulable. En particular NO se arregla que
    `VersionesFestival.CodigoDepartamento` sea `nvarchar(20)` mientras
    `Festivales.CodigoDepartamento` es `char(2)`: es un defecto anterior a SIMUS y
    corregirlo aqui lo esconderia dentro de un cambio que no es suyo.
*/


-- Un guion no debe depender de las opciones de sesion de quien lo lance.
-- Doctrina de la casa: pnmc-database/MIGRADOR.md y schema/V20260823_02.
/*
================================================================================================
    PROCEDENCIA. Generado el 24 ago 2026 desde
    donde esta el POR QUE de cada tabla, cada trampa de nombre de SIMUS y cada decision.
    Este fichero es identico a aquel: no lleva comprobaciones de solo lectura que separar.
================================================================================================
*/

SET QUOTED_IDENTIFIER ON;

/* =====================================================================================
   §A · LOS NUEVE CATALOGOS QUE PNMC NO TIENE

   Comprobado en `PnmcDbContext.cs` (los 48 DbSet — eran 41 cuando se escribio esta linea y
   se dejo sin recontar, F8), en el listado de `sys.tables` de PNMC_LOCAL (59 tablas) y en los
   .sql de `pnmc-database/schema/`. De los DOCE catalogos que SIMUS cuelga del modulo de
   festivales -- la cuenta decia once y no cuadraba con su propia aritmetica de 2+1+9 (F9) --,
   PNMC ya tiene dos:

     - `ART_MUS_TERRITORIOS_SONOROS`  -> `TerritoriosSonoros` (existe, 14 filas sembradas)
     - `ART_MUS_FESTIVALES_ESTADO`    -> `EstadosContenido`   (existe, 8 codigos de texto)

   Y uno se descarta: `ART_MUS_FESTIVALES_REGION_OCAD` con su puente
   `ART_MUS_ZONAXREGION_OCAD`. Razon en el .md, §2: ninguna tabla de festivales lo
   referencia; es una agrupacion de municipios, es decir geografia, y la geografia la
   cerro D11.

   Los nueve de aqui nacen VACIOS. Ninguno lleva la columna `ACTIVO` de SIMUS: las
   maestras de PNMC no la tienen, y un catalogo con `Activo` obliga a que cada consulta
   se acuerde de filtrarlo —la que se olvide muestra lo retirado sin que nada avise—.
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
        SIMUS: ART_MUS_FESTIVALES_TIPO_ORGANIZADOR (ID, TIPO_ORGANIZADOR nvarchar(50)).
        NO es `Entidades.TipoEntidad`. Aquel clasifica QUE ES una entidad del ecosistema
        (individuo, colectivo, espacio, festival...); este clasifica a QUIEN ORGANIZA un
        festival. Que las ocho filas de SIMUS solapen o no con los valores de
        `CK_Entidades_Tipo` NO SE PUEDE SABER: la base disponible tiene cero filas y esos
        ocho nombres no se han visto nunca. NO VERIFICADO.
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
        y esta sembrada con TRES valores distintos -'MUNICIPIO', 'ISLA' y
        'AREA NO MUNICIPALIZADA'-, no con si es urbano o rural. (Antes esta linea decia que
        estaba sembrada solo con 'MUNICIPIO', presentandolo como medido: F11. La conclusion se
        sostenia; la evidencia no era la que se habia medido.)
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

   Todas anulables. `VersionesFestival` tiene hoy 0 filas en PNMC_LOCAL (contado), asi
   que anadir NOT NULL seria posible; se dejan anulables igualmente porque el formulario
   de SIMUS admite guardar una version incompleta y ninguna de estas seis es obligatoria
   para que una edicion exista.
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
    Esta restriccion NO viene de SIMUS: alli nada impide poner la misma fuente dos veces.
    Se anade porque «primaria y secundaria iguales» no describe nada y hoy no hay filas
    que pudieran incumplirla. Si el propietario prefiere el comportamiento de origen, se
    borra esta sola sentencia y nada mas cambia.
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

   RIESGO DECLARADO: si el catalogo `FuentesFinanciacion` acabara conteniendo una fila
   «Estampilla Procultura», habria dos maneras de afirmar lo mismo. Las ocho filas de
   `ART_MUS_FESTIVALES_FUENTE_FINANCIACION` NO SE HAN VISTO NUNCA —la base disponible esta
   vacia—, asi que esto es riesgo declarado, no solape verificado. Como el contenido del
   catalogo lo decide PNMC (D12), la salida es no crear esa fila. */

IF COL_LENGTH(N'dbo.VersionesFestival', N'UsaEstampillaProcultura') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD UsaEstampillaProcultura bit NULL;
GO


/* =====================================================================================
   §C · TRES COLUMNAS NUEVAS EN `Festivales`

   `Festivales` tiene 50 filas en PNMC_LOCAL (contado). Las tres son anulables: una
   columna NOT NULL con DEFAULT le pondria a esas 50 filas un valor que nadie decidio, que
   es exactamente el riesgo que §5.H.1 del documento de migracion senala.
   ===================================================================================== */

/* --- C.1 Quien creo el festival (SIMUS: `creado_por`) ---

   ESTO CIERRA UN AGUJERO QUE PNMC YA TENIA, y esta medido: `Agenda`, `Noticias`,
   `AlbumesGaleria` y `Entidades` tienen `IdUsuarioCreador`; `Festivales` es el UNICO
   modulo de contenido que no. La clase `FestivalRow` declara `CreatedByUserId` y
   `PnmcDbContext.cs` la marca `entity.Ignore(...)` porque la columna no existe. SIMUS no
   trae un concepto nuevo: ensena uno que falta.

   Anulable, y ademas `int?` en la clase: las 50 filas actuales no tienen creador conocido
   y ponerles uno no seria un dato, seria una invencion. */

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

   El coste de esta decision esta dicho, no escondido: si el tipo de organizador o el
   director cambian de una edicion a otra, PNMC guardara solo el ultimo. Es la misma
   eleccion que PNMC ya hizo con el nombre del organizador. */

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

   PNMC ya tiene dos (`VersionesFestivalPracticasMusicales`,
   `VersionesFestivalTerritoriosSonoros`). SIMUS cuelga SIETE tablas de la version; de ellas
   una ya tiene destino (TERRITORIOS_SONOROSXVERSION) y las otras seis se crean aqui.

   CORREGIDO (F10): la cuenta decia OCHO y restaba `ZONAXREGION_OCAD` como si fuera una de
   ellas. No lo es -- cuelga de la geografia, no de la version, cosa que este mismo fichero
   admite dos secciones mas abajo --. El resultado final, seis puentes, salia bien por
   compensacion de dos errores: quien auditara «cubrimos las ocho» habria buscado una octava
   puente que no existe.

   LAS DOS TRAMPAS DE NOMBRE, RESUELTAS DE UNA VEZ Y PARA SIEMPRE:

   TRAMPA 1 · `ART_MUS_FESTIVALES_ENTIDADES_ALIADAS.ID_FESTIVAL` NO APUNTA AL FESTIVAL.
   Su clave foranea va a `ART_MUS_FESTIVALES_VERSION.ID`. El nombre miente. En PNMC la
   columna se llama `VersionFestivalId` y su FK va a `VersionesFestival`: quien lea el
   modelo destino no puede caer en la trampa porque el nombre ya no la tiende. Ver §D.5.

   TRAMPA 2 · `ART_MUS_TIPOINGRESOXVERSION.IDVERSION` VA SIN GUION BAJO, contra `ID_VERSION`
   en las otras cinco puentes. Cualquier mapeo por convencion se rompe justo ahi. En PNMC
   la columna se llama `VersionFestivalId`, igual que en las otras cinco: no hay excepcion
   que recordar. Ver §D.3.
   ===================================================================================== */

/* --- NINGUNA DE LAS SEIS PUENTES LLEVA `ON DELETE CASCADE` (corregido, F2) ---

   La primera version de este fichero se las puso a las seis, y era una invencion con
   apariencia de convencion. Comprobado: `PNMC_LOCAL` tiene CERO foraneas con cascada, y `grep`
   sobre `pnmc-database/` da CERO ocurrencias. Tampoco viene del origen.

   Lo grave no era la cascada en si, sino la INCOHERENCIA que introducia: `VersionesFestival`
   ya tiene dos puentes -`VersionesFestivalPracticasMusicales` y
   `VersionesFestivalTerritoriosSonoros`- sin cascada. Con las seis nuevas puestas, el MISMO
   `DELETE` sobre una version se habria comportado de dos maneras: bloqueado por las dos
   viejas, y destruyendo en silencio las seis nuevas. Un borrado que hace una cosa u otra
   segun la tabla no es una politica, es una trampa.

   Si algun dia PNMC decide usar cascadas, se decide para todas y se aplica tambien a las dos
   que ya existen. Adoptar la estructura de SIMUS no incluye estrenar aqui una politica de
   borrado que el proyecto no tiene. --- */

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

/* --- D.3 Tipos de ingreso (SIMUS: ART_MUS_TIPOINGRESOXVERSION) · TRAMPA 2 ---

   La columna de origen se llama `IDVERSION`, sin guion bajo. Aqui es `VersionFestivalId`,
   igual que en las otras cinco puentes. La excepcion desaparece con el nombre. */

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

/* --- D.4 Localizaciones (SIMUS: ART_MUS_LOCALIZACIONXVERSION) ---

   La puente que mas informacion recupera. SIMUS guarda N MUNICIPIOS POR VERSION, cada uno
   con su zona (urbana/rural) y su titulacion colectiva. `VersionesFestival` guarda UN
   municipio y ninguna de las dos cosas.

   SI LLEVA FK A `Divipola`, y hay que explicar por que no contradice D11.4:

     - D11.4 decidio NO endurecer las ocho tablas EXISTENTES que guardan pares
       departamento/municipio, y una de sus tres razones era «`HasForeignKey` hacia
       `Divipola` aparece cero veces en todo el `PnmcDbContext`». Eso es cierto DEL MODELO
       DE EF y FALSO DE LA BASE: contadas hoy sobre PNMC_LOCAL, NUEVE tablas tienen ya FK
       a `Divipola` —`Agenda`, `Entidades`, `EscuelasMusica`, `Festivales`, `Lutieres`,
       `MercadosMusicales`, `MetricasMunicipioMapa`, `RedesDocumentacion` y
       `RegistrosEcosistema`—, `FK_Festivales_Divipola` entre ellas, declarada en
       `schema/V20260519_03__contenidos_modulos.sql:37`.
     - Su tercera razon —«la siembra hace `DELETE FROM dbo.Divipola` y una FK entrante lo
       impediria»— describe una situacion QUE YA EXISTE desde antes de este trabajo
       (`seed/V20260519_02__divipola_seed.sql:7`). Esta tabla no la crea.
     - D11.4 tampoco queda desobedecida: lo que decidio fue no meter un cambio transversal
       sobre ocho tablas ajenas dentro del trabajo de SIMUS. Aqui no se toca ninguna de las
       ocho; una tabla nueva sigue la convencion que nueve tablas ya siguen.

   `CodigoMunicipio` es NOT NULL a proposito: una fila sin municipio no diria nada que el
   `NivelCobertura` de la version no diga ya, y `Divipola` NO TIENE fila de solo
   departamento (0 filas con municipio nulo o vacio, verificado en D11.4). Una edicion de
   cobertura departamental se expresa con el par escalar de la version, no con una fila
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
        /* `ZonaUrbanoRuralId` DENTRO de la clave (F4). Sin el, la restriccion prohibia registrar
           el mismo municipio con zona urbana Y rural, que es exactamente lo que esta tabla
           existe para recuperar. En origen no hay ninguna restriccion unica: esta la ANADE
           PNMC, y por eso se declara aqui en vez de presentarse como heredada. Con la zona
           sin declarar (NULL), SQL Server admite una sola fila por version y municipio. */
        CONSTRAINT UQ_VersionesFestivalLocalizaciones UNIQUE (VersionFestivalId, CodigoMunicipio, ZonaUrbanoRuralId),
        CONSTRAINT CK_VersionesFestivalLocalizaciones_Municipio_Departamento
            CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento)
    );
END;
GO

/* --- D.5 Entidades socias (SIMUS: ART_MUS_FESTIVALES_ENTIDADES_ALIADAS) · TRAMPA 1 ---

   EL NOMBRE EN PNMC NO DICE «ALIADA», Y NO ES UN CAPRICHO. La primera version llamo a esta
   tabla `VersionesFestivalEntidadesAliadas`, copiando el sustantivo del origen. Lo rechazo
   `MuestraSqlServerTests.Una_base_nueva_no_tiene_nada_del_modelo_de_aliados`, que cuenta las
   tablas `LIKE '%Aliad%' OR LIKE '%Colaborad%'` y exige CERO: PNMC RETIRO el modelo de
   entidades aliadas -tres tablas y tres roles, en
   V20260823_01__retirada_aliados.sql- y esa prueba existe para que el concepto no vuelva por
   ninguna de sus dos puertas, ni con el nombre nuevo ni con el viejo.

   Y HABRIA SIDO FACIL «ARREGLARLO» AL REVES, afinando el patron de la prueba para que dejara
   pasar esta tabla. Seria el arreglo equivocado: el problema no es que la prueba sea ancha, es
   que el nombre pide prestado un vocabulario que este proyecto acaba de enterrar. Quien lea
   `sys.tables` dentro de seis meses y vea «EntidadesAliadas» concluira que el modelo volvio, y
   tendra razon en preocuparse. Se cambia el nombre, no la prueba.

   Es la misma correccion que se le hizo a `ZonasTerritorio` -> `ZonasUrbanoRural` (F12) por la
   misma razon: no tomar prestado un vocabulario ya ocupado.

   LA TRAMPA DEL ORIGEN, que sigue en pie: en SIMUS la columna se llama `ID_FESTIVAL` y apunta a
   `ART_MUS_FESTIVALES_VERSION.ID`. Aqui se llama `VersionFestivalId` y apunta a
   `VersionesFestival`. La trampa no se documenta: se desactiva.

   POR QUE UNA TABLA PROPIA Y NO `Entidades` + `EntidadesRelaciones`. Ese camino solo
   relaciona entidad con entidad, y un festival de PNMC vive en `Festivales`, no en
   `Entidades`. Espejar el festival como `Entidades.TipoEntidad='festival'` colgaria las
   aliadas DEL FESTIVAL Y NO DE LA VERSION: exactamente el error contra el que avisa la
   trampa. Ademas una aliada de SIMUS es un nombre y un correo sueltos, no una entidad
   registrada del ecosistema.

   Se deja `EntidadId` anulable para el dia en que una aliada SI este registrada en
   `Entidades`. Es una columna, no un mecanismo: nada obliga hoy a rellenarla. */

IF OBJECT_ID(N'dbo.VersionesFestivalEntidadesSocias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VersionesFestivalEntidadesSocias
    (
        IdVersionFestivalEntidadSocia int IDENTITY(1,1) NOT NULL,
        VersionFestivalId int NOT NULL,
        /* ANULABLE (F6): en origen lo es. La cabecera de este guion promete que «todo lo
           nuevo es aditivo y anulable» y esta columna era la excepcion, sin decirlo. */
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
        /* SIN UNIQUE POR NOMBRE (F6). Dos aliadas homonimas en la misma edicion -dos
           «Alcaldia Municipal» con correos distintos- son un caso real, y en origen no hay
           unicidad ninguna. Queda un indice NO unico en §E para la busqueda. */
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
        /* ANULABLE, Y CON `Url` AL LADO (F5). El unico dato real de
           `ART_MUS_MATERIALMULTIMEDIA` es una URL EXTERNA, y `dbo.Archivos` es el registro de
           ficheros SUBIDOS: exigir `ArchivoId NOT NULL` obligaba a inventarle a cada enlace una
           ruta de almacenamiento, un nombre original, un MIME y un usuario de carga. Es la forma
           que PNMC ya usa para este caso exacto en `RecursosEditorialesArchivos`
           (pnmc-database/schema/V20260525_01__administracion_extendida.sql:162-163), la misma
           tabla que este guion citaba como modelo y de la que no habia copiado lo que la hace
           servir. El CHECK de mas abajo impide la fila que no apunta a nada. */
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
        /* `RolArchivo` DENTRO de la clave (F3). Las tres puentes hacia `Archivos` que PNMC ya
           tiene lo hacen asi. Sin el, un mismo fichero no podria ser portada Y galeria de la
           misma version, que es justo lo que esta tabla dice aportar. */
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
   §F · D11.4 · LAS DOS TABLAS QUE SE QUEDARON SIN FORANEA A `Divipola`

   ESTA SECCION EXISTE PORQUE UNA DECISION SE REVIRTIO. D11.4 decia primero que NO habia que
   endurecerlas, con tres razones. Dos eran falsas y las dos se midieron mal:

     - «Son ocho tablas y ninguna tiene foranea; `HasForeignKey` hacia `Divipola` aparece cero
       veces». Eso mide el MODELO DE EF, no la base. Sobre `sys.foreign_keys` hay NUEVE foraneas
       reales a `dbo.Divipola`: Agenda, Entidades, EscuelasMusica, Festivales, Lutieres,
       MercadosMusicales, MetricasMunicipioMapa, RedesDocumentacion y RegistrosEcosistema. EF no
       las declara porque para LEER no las necesita.
     - «No hay a que apuntar: la clave es compuesta y no hay filas de nivel departamento».
       `FK_Festivales_Divipola` apunta con LAS DOS columnas y funciona desde hace meses. Y ademas
       existe `UQ_Divipola_CodigoMunicipio`, unico sobre una sola columna.

   La tercera razon -«la siembra hace DELETE+INSERT sobre Divipola y una foranea lo impediria»-
   es cierta y es irrelevante: `Festivales` ya tiene la suya y la siembra corre igual.

   LA VERDAD ES LA CONTRARIA DE LA QUE SE ESCRIBIO. `VersionesFestival` y
   `PropuestasCambioFestival` son las DOS UNICAS tablas territorializadas de PNMC SIN foranea a
   `Divipola`. No son la regla: son la anomalia. Alinearlas con sus nueve hermanas no introduce
   ningun patron nuevo.

   POR QUE HACE FALTA UN ALTER COLUMN Y NO BASTA UN ALTER TABLE ADD CONSTRAINT. Las dos guardan
   el par en `nvarchar(20)` mientras `Divipola` lo declara `char(2)` / `char(5)`, y SQL Server
   exige el MISMO tipo a los dos lados de una foranea. Ese desajuste -y no las razones que se
   dieron- es el motivo real por el que la foranea nunca se puso. Merecia decirse.

   ES SEGURO, Y ESTA MEDIDO: las dos tablas tienen CERO filas en PNMC_LOCAL y cero en la base de
   ensayo, ninguna semilla las escribe, no hay indice ni CHECK sobre esas columnas -consultado
   sys.index_columns y sys.check_constraints, 0 filas- y `PnmcDbContext` no declara ni
   `HasMaxLength` ni `HasColumnType` para ellas, de modo que EF no ve el cambio.
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

/* Compuesta, como las nueve hermanas. NOT FOR REPLICATION no aplica; sin cascada, como todo
   en PNMC. El par es anulable a los dos lados, y SQL Server no exige la foranea cuando alguna
   de las dos columnas es NULL: una edicion de cobertura nacional sigue siendo representable. */
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
   §G · LO QUE ESTE GUION NO HACE, DICHO PARA QUE NADIE LO BUSQUE

   1. NO inserta filas. Ni una. Los nueve catalogos nacen vacios (D12).
   2. NO crea tabla de equivalencia de ids con SIMUS: no viajan filas, luego no hay ids de
      origen que equivaler. D2 no aplica en el alcance vigente.
   3. NO da a `PropuestasCambioFestival` ninguna de las columnas ni de las puentes nuevas. La
      propuesta de cambio no podra proponer fechas, tipologia, financiacion, expresiones,
      modalidades, tipos de ingreso, localizaciones, aliadas ni archivos. Es una asimetria REAL y
      es la pregunta 1 del .md. (Lo unico que §F le toca es el tipo del par territorial y su
      foranea a `Divipola`, que es alinearla con las nueve hermanas, no ampliarle el alcance.)
   4. NO replica ningun typo fisico de SIMUS. Lista completa en el .md, §5.
   5. NO crea `RegionesOcad` ni su puente municipio-region. Descartado con razon en el .md, §2.
   6. NO anade columna de estado a `VersionesFestival`. PNMC ya decidio que el estado es del
      festival y la vigencia es de la version; `ART_MUS_FESTIVALES_VERSION.ID_ESTADO` ademas
      sigue SIN VERIFICAR en produccion.
   7. NO cambia `Festivales.FechaInicioVersionActual` / `FechaFinVersionActual` /
      `TieneVersionVigenteAnoActual` / `EstadoVersionAnoActual`, que a partir de B.1 pasan a
      ser derivables de la version. Es la pregunta 2 del .md.
   ===================================================================================== */
