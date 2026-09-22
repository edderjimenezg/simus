/*
    PNMC · Ecosistema · Escenarios

    QUE ES ESTO
    -----------
    El sexto proceso del ecosistema, que no existia. Ni tabla, ni tipo de dominio, ni endpoint, ni
    modulo del panel: de los seis —festivales, escuelas de musica, escenarios, mercados musicales,
    redes y documentacion, luteria— era el unico sin absolutamente nada.

    POR QUE NO SE REUTILIZA `Lutieres`
    ----------------------------------
    Porque son dos procesos distintos, decidido el 25 ago 2026. `Lutieres` llevaba ademas el nombre
    tecnico que le corresponde a este —el tipo de dominio se llamaba `SpaceInfrastructureRow`— y esa
    confusion se deshizo. Un taller de luteria y una sala de conciertos no comparten
    ni los datos ni las preguntas: lo que distingue a un escenario es cuanta gente cabe, si es
    accesible y con que esta dotado.

    DOS DIFERENCIAS DELIBERADAS FRENTE A LAS OTRAS TABLAS DE PROCESO
    ----------------------------------------------------------------
    1. `OrganizacionResponsableId` NACE CON EL PROCESO. Un proceso del Ecosistema pertenece a la
       organizacion que lo administra, y esa columna se declara al crear la tabla: anadirla despues
       obliga a rellenarla, a rehacer indices y a corregir las semillas que ya escribieron filas
       sin ella.

       Nace ANULABLE y no `NOT NULL`: la tabla arranca vacia, asi que la obligatoriedad no cuesta
       nada hoy y costaria una migracion en cuanto haya filas de origen incompletas. Se cierra
       cuando el proceso tenga contenido y camino de alta, igual que se hizo con Festivales.

    2. `Aforo`, `TieneAccesibilidad` y `Dotacion` SON SUYOS. No los tiene ningun otro proceso, y son
       exactamente la razon por la que Escenarios y Luteria son dos y no uno.

    ACCESIBILIDAD COMO DATO, NO COMO ADORNO
    ---------------------------------------
    `TieneAccesibilidad` es anulable a proposito, con tres estados y no dos: si (1), no (0) y
    NO SE SABE (NULL). Un `bit NOT NULL DEFAULT 0` habria afirmado que ningun escenario del pais es
    accesible mientras nadie rellene el campo, y esa afirmacion saldria publicada. «No consta» y
    «no» no son lo mismo, y menos en este dato.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.Escenarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Escenarios
    (
        IdEscenario               int IDENTITY(1,1) NOT NULL,
        Nombre                    nvarchar(220) NOT NULL,
        TipoEscenario             nvarchar(80)  NOT NULL,
        Descripcion               nvarchar(max) NULL,

        -- Lo que distingue un escenario de un taller de luteria.
        Aforo                     int           NULL,
        TieneAccesibilidad        bit           NULL,
        Dotacion                  nvarchar(max) NULL,

        NombreContacto            nvarchar(180) NULL,
        CorreoContacto            nvarchar(180) NULL,
        TelefonoContacto          nvarchar(80)  NULL,
        SitioWeb                  nvarchar(500) NULL,
        Facebook                  nvarchar(500) NULL,
        Instagram                 nvarchar(500) NULL,
        OtroEnlace                nvarchar(500) NULL,

        NivelCobertura            nvarchar(40)  NOT NULL
            CONSTRAINT DF_Escenarios_NivelCobertura DEFAULT (N'municipal'),
        CodigoDepartamento        char(2)       NULL,
        CodigoMunicipio           char(5)       NULL,
        Direccion                 nvarchar(300) NULL,
        Zona                      nvarchar(120) NULL,
        Latitud                   decimal(9,6)  NULL,
        Longitud                  decimal(9,6)  NULL,

        OrganizacionResponsableId int           NULL,

        Activo                    bit           NOT NULL
            CONSTRAINT DF_Escenarios_Activo DEFAULT (1),
        EstadoRegistro            nvarchar(80)  NOT NULL
            CONSTRAINT DF_Escenarios_EstadoRegistro DEFAULT (N'borrador'),
        FechaCreacion             datetime2(0)  NOT NULL
            CONSTRAINT DF_Escenarios_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion        datetime2(0)  NULL,

        CONSTRAINT PK_Escenarios PRIMARY KEY (IdEscenario),
        CONSTRAINT FK_Escenarios_Divipola
            FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_Escenarios_EstadosContenido
            FOREIGN KEY (EstadoRegistro) REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT FK_Escenarios_Organizacion
            FOREIGN KEY (OrganizacionResponsableId) REFERENCES dbo.Entidades (IdEntidad),

        -- El vocabulario cerrado desde el principio. Anadir un tipo obliga a tocar este guion, que
        -- es exactamente lo que se quiere: que nadie invente un tipo escribiendolo en un formulario.
        CONSTRAINT CK_Escenarios_TipoEscenario CHECK (TipoEscenario IN
            (N'sala', N'teatro', N'casa_cultural', N'estudio', N'aire_libre', N'otro')),

        -- El mismo CHECK que Entidades: cobertura nacional no lleva territorio, y la municipal lo
        -- lleva completo. Copiado de CK_Entidades_NivelCobertura para que las dos tablas cuenten
        -- la misma historia sobre el territorio.
        CONSTRAINT CK_Escenarios_NivelCobertura CHECK
        (
            NivelCobertura IN (N'municipal', N'departamental', N'nacional')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        ),

        -- Un aforo negativo no es un dato incompleto, es un dato imposible.
        CONSTRAINT CK_Escenarios_Aforo CHECK (Aforo IS NULL OR Aforo >= 0)
    );
END;
GO

-- La bandeja de una organizacion: sus escenarios por estado. Mismo indice y mismo motivo que
-- IX_Festivales_OrganizacionPrincipalId_EstadoRegistro.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Escenarios_OrganizacionResponsableId_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.Escenarios', N'U'))
BEGIN
    CREATE INDEX IX_Escenarios_OrganizacionResponsableId_EstadoRegistro
        ON dbo.Escenarios (OrganizacionResponsableId, EstadoRegistro);
END;
GO

-- El catalogo publico filtra por estado y ordena por nombre.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Escenarios_EstadoRegistro'
                 AND object_id = OBJECT_ID(N'dbo.Escenarios', N'U'))
BEGIN
    CREATE INDEX IX_Escenarios_EstadoRegistro
        ON dbo.Escenarios (EstadoRegistro) INCLUDE (Nombre);
END;
GO
