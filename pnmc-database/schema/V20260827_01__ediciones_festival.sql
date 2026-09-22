/*
    PNMC · Ecosistema · Ediciones del Festival

    QUE ES ESTO
    -----------
    Una fila por edicion de un Festival: 2024, 2025, 2026, cada una con su nombre, sus fechas y su
    estado. Lo pidio el usuario sobre el formulario de registro, con esas
    palabras: «cuando se crea se le puede crear de una varias versiones, ejemplo 2024 2025 2026 y
    estas tienen nombres diferentes».

    POR QUE UNA TABLA NUEVA Y NO `dbo.VersionesFestival`
    ----------------------------------------------------
    Porque `VersionesFestival` ya significa otra cosa, y reutilizarla romperia el sitio publico.
    Comprobado:

      · Tiene CERO filas y nueve tablas satelite.
      · La escriben `PropuestasCambioFestivalExternosEndpoints` y
        `RevisionInstitucionalPropuestasFestivalEndpoints`: es la version del REGISTRO PUBLICADO,
        la que nace cuando el equipo del PNMC aprueba una propuesta de cambios.
      · Lleva `EsVigente`, y `LecturaFestivalesPublicados.cs-38` lee justo esa fila para
        SUSTITUIR nombre, descripcion, cobertura, practicas y territorios de la ficha publica.

    Es decir: meter ahi «la edicion de 2025» cambiaria lo que ve cualquier visitante del sitio. Son
    dos conceptos con el mismo nombre coloquial y consecuencias opuestas.

    POR QUE TAMPOCO SIRVEN LAS COLUMNAS DE `dbo.Festivales`
    -------------------------------------------------------
    `Festivales` lleva `NumeroVersiones` (entre 3 y 20 en los treinta festivales sembrados),
    `FechaUltimaVersion`, `TieneVersionVigenteAnoActual`, `EstadoVersionAnoActual`,
    `FechaInicioVersionActual` y `FechaFinVersionActual`. Eso es un CONTADOR mas la edicion del
    anyo en curso: no admite varias, y no guarda un nombre por edicion. Se conservan tal cual —el
    sitio publico y la consola las leen— y esta tabla no las toca.

    EL VOCABULARIO DE `Estado`
    --------------------------
    Cerrado desde el principio, con un CHECK. Los dos primeros valores no son inventados: son los
    unicos que hay hoy en `Festivales.EstadoVersionAnoActual`
    (`en_preparacion` y `programada`). Los otros dos cierran el ciclo. Anyadir uno obliga a tocar
    este guion, que es exactamente lo que se quiere: que nadie invente un estado escribiendolo en
    un formulario, como paso con `en_evaluacion`.

    UNA EDICION POR ANYO Y POR FESTIVAL
    -----------------------------------
    `UQ_EdicionesFestival_FestivalAnio` lo impone en la base y no solo en el formulario. Sin esa
    restriccion, dos envios seguidos del mismo formulario —o dos pestanyas abiertas— dejan dos
    ediciones de 2025 con nombres distintos y nada dice cual vale.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.EdicionesFestival', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EdicionesFestival
    (
        IdEdicionFestival  int           IDENTITY(1,1) NOT NULL,
        FestivalId         int           NOT NULL,

        -- El anyo es el identificador natural de la edicion, y por eso es NOT NULL: una edicion
        -- sin anyo no se puede ordenar ni distinguir de la siguiente.
        Anio               int           NOT NULL,

        -- El nombre SI cambia de una edicion a otra; es la mitad del pedido.
        Nombre             nvarchar(240) NOT NULL,
        Descripcion        nvarchar(max) NULL,

        -- Anulables a proposito: al abrir la edicion de un anyo que aun no tiene fechas cerradas,
        -- una fecha inventada es peor que ninguna.
        FechaInicio        date          NULL,
        FechaFin           date          NULL,

        Estado             nvarchar(40)  NOT NULL
            CONSTRAINT DF_EdicionesFestival_Estado DEFAULT (N'en_preparacion'),

        FechaCreacion      datetime2(0)  NOT NULL
            CONSTRAINT DF_EdicionesFestival_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0)  NULL,

        CONSTRAINT PK_EdicionesFestival PRIMARY KEY (IdEdicionFestival),

        CONSTRAINT FK_EdicionesFestival_Festivales
            FOREIGN KEY (FestivalId) REFERENCES dbo.Festivales (IdFestival),

        -- Una edicion por anyo y por Festival, impuesto por la base y no solo por el formulario.
        CONSTRAINT UQ_EdicionesFestival_FestivalAnio UNIQUE (FestivalId, Anio),

        -- Un anyo de cuatro cifras dentro de un rango con sentido. `Anio = 20255` no es un dato
        -- incompleto, es un dato imposible, y ordena la lista al principio o al final para siempre.
        CONSTRAINT CK_EdicionesFestival_Anio CHECK (Anio BETWEEN 1900 AND 2200),

        CONSTRAINT CK_EdicionesFestival_Fechas CHECK
            (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio),

        CONSTRAINT CK_EdicionesFestival_Estado CHECK (Estado IN
            (N'en_preparacion', N'programada', N'realizada', N'cancelada'))
    );
END;
GO

-- La consulta de siempre: las ediciones de un Festival, de la mas reciente a la mas antigua.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_EdicionesFestival_FestivalId_Anio'
                 AND object_id = OBJECT_ID(N'dbo.EdicionesFestival', N'U'))
BEGIN
    CREATE INDEX IX_EdicionesFestival_FestivalId_Anio
        ON dbo.EdicionesFestival (FestivalId, Anio DESC);
END;
GO
