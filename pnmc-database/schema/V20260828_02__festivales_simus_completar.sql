/*
    PNMC · Festivales · completar el modelo de SIMUS

    QUE ANADE ESTE GUION
    --------------------
    Doce columnas a `dbo.VersionesFestival`: once campos de datos y el estado por version. Con
    ellas, la version guarda todo lo que describe una edicion; el contenido de los catalogos se
    siembra aparte, en `seed/V20260828_01__catalogos_festival_seed.sql`.

    LAS DOCE COLUMNAS
    ------------------------------------------------------------------------------------------
    Once son campos de datos y una es el estado por version:

      PRACTICAS_MUSICALES_CONGREGA        -> PracticasMusicalesQueCongrega
      OTRA_TIPOLOGIA                      -> OtraTipologia
      OTRA_MODALIDAD_PARTICIPACION        -> OtraModalidadParticipacion
      OTRA_EXPRESION_ARTISTICA            -> OtraExpresionArtistica
      OTRA_FUENTE_FINANCIACION_PRIMARIA   -> OtraFuenteFinanciacionPrimaria
      OTRA_FUENTE_FINANCIACION_SECUNDARIA -> OtraFuenteFinanciacionSecundaria
      PERTENECE_ORG_COLETIVA              -> PerteneceAOrganizacionColectiva
      NOMBRE_ORGANIZACION                 -> NombreOrganizacionColectiva
      OTRO_TIPO_ORGANIZADOR               -> OtroTipoOrganizador
      OBSERVACIONES_CONTACTO              -> ObservacionesContacto
      OBSERVACIONES_RECHAZO               -> ObservacionesRechazo
      ID_ESTADO                           -> EstadoRegistro

    LOS SEIS «OTRA» NO SON RELLENO. Cada catalogo de SIMUS termina en una fila «OTRA», y la fila
    sola no guarda nada: quien la elige escribe cual. Sin la columna de texto, elegir «OTRA» seria
    indistinguible de no elegir. Es la unica forma de que el catalogo pueda ser cerrado -y por
    tanto agregable- sin perder el caso que no cabe en el.

    `PERTENECE_ORG_COLETIVA` SE ESCRIBE ASI EN EL ORIGEN, sin la «C» de «COLECTIVA». Aqui se
    corrige el nombre; la equivalencia queda escrita arriba para que nadie la busque en vano.

    EL ESTADO POR VERSION RESUELVE LA DUDA HISTORICA documentada el 27 de agosto: PNMC tenia DOS
    modelos para lo mismo -`dbo.EdicionesFestival`, 10 columnas y 3
    filas, y `dbo.VersionesFestival`, 26 columnas y 0 filas- y que habia que decidir cual es «una
    version del Festival». SIMUS tiene uno solo: la version ES la edicion, lleva sus propias
    fechas, su propio nombre, sus propios catalogos Y SU PROPIO ESTADO DE REVISION
    (`ART_MUS_FESTIVALES_VERSION.ID_ESTADO`, con las seis filas de `ART_MUS_FESTIVALES_ESTADO`).
    Anadir aqui `EstadoRegistro` a la version es adoptar esa respuesta. La migracion de las tres
    filas de `EdicionesFestival` y su retirada NO van en este guion: mover datos es otra clase de
    cambio y merece su propio fichero y su propia prueba.

    LO QUE ESTE GUION NO HACE, Y HAY QUE SABERLO
    --------------------------------------------
    - No toca `dbo.EdicionesFestival`. Los dos modelos siguen conviviendo hasta que se decida la
      migracion de sus tres filas.
    - No mapea nada en EF Core. `ParidadEsquemaSinArranqueTests` exige que exista en la base cada
      columna que EF mapea, no al reves: una columna en la base sin entidad no rompe la prueba,
      pero tampoco sirve para nada hasta que se mapee.
    - No crea rutas; el contrato HTTP permanece en la capa de API.

    CONVENCIONES (leidas de `schema/V20260824_02__festivales_simus.sql`, no supuestas)
    ---------------------------------------------------------------------------------
    - Nombres en espanol; PK_/UQ_/CK_/FK_/IX_/DF_.
    - Comentarios sin tildes, como el resto de `pnmc-database/schema/`.
    - Idempotente: se puede volver a ejecutar sin error.
    - `GO` entre anadir una columna y restringirla: SQL Server compila el lote entero antes de
      ejecutarlo, y la resolucion diferida de nombres cubre tablas, no columnas.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ===========================================================================================
-- 1. Las once columnas de datos que faltaban en dbo.VersionesFestival
-- ===========================================================================================

IF COL_LENGTH('dbo.VersionesFestival', 'PracticasMusicalesQueCongrega') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD PracticasMusicalesQueCongrega nvarchar(500) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtraTipologia') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtraTipologia nvarchar(200) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtraModalidadParticipacion') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtraModalidadParticipacion nvarchar(200) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtraExpresionArtistica') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtraExpresionArtistica nvarchar(200) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtraFuenteFinanciacionPrimaria') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtraFuenteFinanciacionPrimaria nvarchar(200) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtraFuenteFinanciacionSecundaria') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtraFuenteFinanciacionSecundaria nvarchar(200) NULL;
GO

-- Nulable de verdad: en el origen hay filas con la pregunta sin responder, y «no respondio» no es
-- lo mismo que «no pertenece». Un DEFAULT 0 convertiria un vacio en una afirmacion.
IF COL_LENGTH('dbo.VersionesFestival', 'PerteneceAOrganizacionColectiva') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD PerteneceAOrganizacionColectiva bit NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'NombreOrganizacionColectiva') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD NombreOrganizacionColectiva nvarchar(500) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'OtroTipoOrganizador') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD OtroTipoOrganizador nvarchar(200) NULL;
GO

IF COL_LENGTH('dbo.VersionesFestival', 'ObservacionesContacto') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD ObservacionesContacto nvarchar(600) NULL;
GO

-- 4000 en el origen. Es el texto con el que un funcionario explica por que devuelve una version;
-- recortarlo aqui recortaria la explicacion, que es justo lo que la persona necesita leer entera.
IF COL_LENGTH('dbo.VersionesFestival', 'ObservacionesRechazo') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD ObservacionesRechazo nvarchar(4000) NULL;
GO

-- ===========================================================================================
-- 2. El estado de revision POR VERSION
-- ===========================================================================================
/*
    NO SE CREA UN CATALOGO NUEVO. `ART_MUS_FESTIVALES_ESTADO` tiene seis filas -Borrador,
    Enviado, Solicitud de aclaraciones, Aprobado, Rechazado, Archivado- y PNMC ya tiene su propio
    catalogo de estados en `dbo.EstadosContenido`, que es el que manda sobre cualquier constante
    del front y del API. Duplicarlo daria dos vocabularios para lo mismo, que es exactamente el
    defecto que costo semanas con `en_evaluacion`.

    Por eso la columna es `nvarchar(80)` con FK a `dbo.EstadosContenido`, exactamente como
    `dbo.Festivales.EstadoRegistro`, y no un `int` a una tabla nueva. Ya hay DIEZ tablas que
    apuntan a `UQ_EstadosContenido_CodigoEstado`; esta es la once.

    Y LOS SEIS ESTADOS DE SIMUS YA ESTAN, comprobado contra la base el 28 de agosto:
      Borrador                 -> borrador
      Enviado                  -> en_revision
      Solicitud de aclaraciones-> ajustes_solicitados
      Aprobado                 -> aprobado
      Rechazado                -> rechazado
      Archivado                -> archivado
    No hay que anadir ninguno: la equivalencia es completa y en los dos sentidos.
*/
IF COL_LENGTH('dbo.VersionesFestival', 'EstadoRegistro') IS NULL
    ALTER TABLE dbo.VersionesFestival ADD EstadoRegistro nvarchar(80) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VersionesFestival_EstadosContenido')
    ALTER TABLE dbo.VersionesFestival WITH NOCHECK
        ADD CONSTRAINT FK_VersionesFestival_EstadosContenido
        FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado);
GO

-- ===========================================================================================
-- 3. Las dos columnas que faltaban en la cabecera, dbo.Festivales
-- ===========================================================================================
/*
    `ART_MUS_FESTIVALES` tiene 15 columnas y `dbo.Festivales` 31, asi que casi todo estaba. Faltan
    dos, y las dos dicen algo que hoy no se puede saber:

      OBSERVACIONES_CONTACTO -> ObservacionesContacto: notas sobre COMO contactar, distintas de la
                                descripcion del Festival. En el origen hay filas con «ninguna» y
                                con texto util; es un campo que la gente usa.
      FECHA_ENVIO            -> FechaEnvioARevision: cuando se remitio a revision. Hoy PNMC solo
                                guarda `FechaActualizacion`, que cambia con cualquier edicion: no
                                hay forma de saber cuanto lleva esperando una revision, que es la
                                pregunta que hace la bandeja del funcionario.
*/
IF COL_LENGTH('dbo.Festivales', 'ObservacionesContacto') IS NULL
    ALTER TABLE dbo.Festivales ADD ObservacionesContacto nvarchar(600) NULL;
GO

IF COL_LENGTH('dbo.Festivales', 'FechaEnvioARevision') IS NULL
    ALTER TABLE dbo.Festivales ADD FechaEnvioARevision datetime2(3) NULL;
GO

-- ===========================================================================================
-- 4. Region OCAD: la clasificacion que no existia en PNMC
-- ===========================================================================================
/*
    SIMUS agrupa los departamentos en SEIS regiones -`ART_MUS_FESTIVALES_REGION_OCAD`- y guarda la
    correspondencia en `ART_MUS_ZONAXREGION_OCAD`, 32 filas de codigo de departamento a region.
    PNMC no tenia ninguna de las dos: `SELECT` sobre `sys.tables` con `LIKE '%Ocad%'` y
    `LIKE '%Region%'` devolvia cero.

    ES LA UNICA AGRUPACION SUPRADEPARTAMENTAL DEL MODELO, y es la que usa el OCAD -Organo
    Colegiado de Administracion y Decision- para repartir recursos del Sistema General de
    Regalias. Sin ella no se puede responder «cuantos Festivales hay en el Pacifico».

    NO SE DERIVA DEL DEPARTAMENTO. Podria parecer que si -son 32 departamentos y 6 regiones- pero
    el reparto es una decision administrativa, no geografica: Antioquia esta en EJE CAFETERO y no
    en PACIFICO, aunque tenga costa pacifica. Por eso es una tabla y no un CASE.

    LA CORRESPONDENCIA VA CONTRA `dbo.Divipola`. En el origen la columna se llama `ZON_ID` y es
    `varchar(5)`, que mezcla codigos de departamento de dos caracteres con otras cosas; aqui es
    `char(2)` con FK, que es lo que PNMC usa para un departamento en todas partes.
*/
IF OBJECT_ID('dbo.RegionesOcad', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegionesOcad
    (
        IdRegionOcad       int           IDENTITY(1,1) NOT NULL,
        NombreRegionOcad   nvarchar(280) NOT NULL,
        Slug               nvarchar(320) NOT NULL,
        Descripcion        nvarchar(1600) NULL,
        OrdenVisualizacion int           NOT NULL CONSTRAINT DF_RegionesOcad_Orden DEFAULT (0),
        CONSTRAINT PK_RegionesOcad PRIMARY KEY CLUSTERED (IdRegionOcad),
        CONSTRAINT UQ_RegionesOcad_Slug UNIQUE (Slug)
    );
END
GO

/*
    UN DEPARTAMENTO ESTA EN UNA SOLA REGION, y por eso la PK es el codigo de departamento y no un
    identidad propio. En el origen la tabla lleva un `ID IDENTITY` y nada impide meter el mismo
    departamento en dos regiones; aqui la clave lo impide.
*/
IF OBJECT_ID('dbo.DepartamentosRegionOcad', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DepartamentosRegionOcad
    (
        CodigoDepartamento char(2) NOT NULL,
        RegionOcadId       int     NOT NULL,
        CONSTRAINT PK_DepartamentosRegionOcad PRIMARY KEY CLUSTERED (CodigoDepartamento),
        CONSTRAINT FK_DepartamentosRegionOcad_RegionesOcad
            FOREIGN KEY (RegionOcadId) REFERENCES dbo.RegionesOcad (IdRegionOcad)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DepartamentosRegionOcad_Region')
    CREATE INDEX IX_DepartamentosRegionOcad_Region
        ON dbo.DepartamentosRegionOcad (RegionOcadId) INCLUDE (CodigoDepartamento);
GO
