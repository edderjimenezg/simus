/*
    PNMC · Propuesta de cambio sobre lo ya publicado, para cualquier proceso del Ecosistema

    QUE ES ESTO
    -----------
    Un registro publicado no se edita en caliente: lo que ve el público no puede cambiar porque
    alguien de la organización abrió un formulario. Lo que se hace es PROPONER el cambio, que el
    Programa lo compara con lo publicado campo por campo y decide. Festivales lo tiene desde el
    principio; Mercados Musicales no, y es lo último que le faltaba del plan de paridad.

    POR QUE GENERICAS, COMO LAS REVISIONES
    --------------------------------------
    Mismo motivo que `RevisionesDeRegistro` (V20260916_03): el registro se identifica por MODULO +
    IDENTIFICADOR y no por una clave ajena a una tabla concreta, así que Escuelas, Escenarios, Redes
    y Lutería heredan el circuito sin un juego de tablas por proceso.

    POR QUE NO SE COPIA LA FORMA DE `PropuestasCambioFestival`
    ----------------------------------------------------------
    Aquella es una SOMBRA COMPLETA del festival: una columna por cada columna del registro, más sus
    tablas de prácticas y territorios. Funciona, pero obliga a que cada campo nuevo del proceso se
    añada en dos sitios, y a repetir el juego entero por proceso. Aquí se guarda solo LO QUE CAMBIA:
    una fila por campo propuesto, con el valor de antes y el de después. Un campo nuevo del mercado
    no obliga a tocar este esquema.

    Y encima el `CampoId` es el MISMO vocabulario que usan las observaciones de revisión, así que
    una propuesta sobre «Correo de contacto» y una nota pidiendo ajustes sobre «Correo de contacto»
    hablan del mismo campo sin traducción en medio.

    POR QUE SE COPIA EL VALOR ANTERIOR
    ----------------------------------
    Porque es la evidencia de contra qué se propuso. Resolverlo por JOIN contra el presente haría
    que, tras aprobar la propuesta, la comparación dijera que no cambió nada: el «antes» ya sería el
    «después». La copia se toma al ENVIAR, que es el momento en que el Programa se compromete a
    mirar eso.

    FESTIVALES NO SE TOCA EN ESTE GUION
    -----------------------------------
    `PropuestasCambioFestival` sigue como está, con sus datos. Migrarla a esta forma es un corte
    propio, con marcha atrás y recuento, igual que la deuda ya nombrada de `RevisionesFestival`. Lo
    que este guion impide es que la deuda crezca con cada proceso nuevo.

    EL VOCABULARIO, CERRADO CON UN CHECK
    ------------------------------------
      · borrador            — la escribe la organización y solo ella la ve.
      · en_revision         — el Programa la tiene delante y debe decidir.
      · ajustes_solicitados — devuelta con cambios pedidos; la organización la retoma.
      · aplicada            — aprobada, y sus campos ya están en el registro publicado.
      · rechazada           — no se aplica, y el motivo queda escrito.

    UNA PROPUESTA VIVA POR REGISTRO
    -------------------------------
    Índice único filtrado sobre las que no están cerradas. Dos propuestas abiertas sobre el mismo
    mercado son dos futuros distintos de la misma ficha, y nada diría cuál gana al aprobar la
    segunda.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.PropuestasDeCambio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PropuestasDeCambio
    (
        IdPropuesta            bigint         IDENTITY(1,1) NOT NULL,

        -- SOBRE QUE SE PROPONE. Mismo par que la auditoría, la procedencia y las revisiones.
        ModuloId               nvarchar(80)   NOT NULL,
        RegistroId             nvarchar(120)  NOT NULL,

        Estado                 nvarchar(40)   NOT NULL
            CONSTRAINT DF_PropuestasDeCambio_Estado DEFAULT (N'borrador'),

        -- QUIEN PROPONE. La organización responde por el registro; la persona es quien lo escribió.
        IdOrganizacion         int            NOT NULL,
        OrganizacionNombre     nvarchar(480)  NULL,
        IdUsuarioProponente    int            NULL,
        ProponenteNombre       nvarchar(480)  NULL,

        -- POR QUE SE PROPONE. Lo lee quien decide antes de mirar campo por campo.
        Motivo                 nvarchar(2400) NULL,

        -- QUIEN DECIDE Y QUE DIJO. El nombre se COPIA en el momento, por lo mismo que en las
        -- revisiones: renombrar a una persona no puede reescribir lo que dicen los expedientes
        -- ya cerrados.
        IdUsuarioDecide        int            NULL,
        DecideNombre           nvarchar(480)  NULL,
        MotivoDeLaDecision     nvarchar(2400) NULL,

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_PropuestasDeCambio_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,
        FechaEnvio             datetime2(0)   NULL,
        FechaDecision          datetime2(0)   NULL,

        CONSTRAINT PK_PropuestasDeCambio PRIMARY KEY (IdPropuesta),

        -- SIN CLAVE AJENA AL REGISTRO: es el precio de ser genérica, y lo sostiene el servidor,
        -- que comprueba que el registro existe y está publicado antes de abrir la propuesta.
        CONSTRAINT FK_PropuestasDeCambio_Organizacion
            FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),

        CONSTRAINT FK_PropuestasDeCambio_Proponente
            FOREIGN KEY (IdUsuarioProponente) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT FK_PropuestasDeCambio_Decide
            FOREIGN KEY (IdUsuarioDecide) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT CK_PropuestasDeCambio_Estado CHECK (Estado IN
            (N'borrador', N'en_revision', N'ajustes_solicitados', N'aplicada', N'rechazada')),

        -- UN BORRADOR NO SE HA ENVIADO, Y TODO LO DEMAS SI.
        CONSTRAINT CK_PropuestasDeCambio_FechaEnvio CHECK
            ((Estado = N'borrador' AND FechaEnvio IS NULL)
             OR (Estado <> N'borrador' AND FechaEnvio IS NOT NULL)),

        -- SOLO LAS DECIDIDAS TIENEN FECHA DE DECISION, y las dos que cierran la tienen siempre.
        CONSTRAINT CK_PropuestasDeCambio_FechaDecision CHECK
            ((Estado IN (N'aplicada', N'rechazada') AND FechaDecision IS NOT NULL)
             OR (Estado NOT IN (N'aplicada', N'rechazada') AND FechaDecision IS NULL)),

        -- RECHAZAR SIN DECIR POR QUE NO ES UNA DECISION, ES UN PORTAZO. Aplicar, en cambio, no
        -- necesita justificarse: el cambio aprobado habla por sí mismo.
        CONSTRAINT CK_PropuestasDeCambio_MotivoDelRechazo CHECK
            (Estado <> N'rechazada' OR LEN(LTRIM(RTRIM(ISNULL(MotivoDeLaDecision, N'')))) > 0),

        CONSTRAINT CK_PropuestasDeCambio_Registro CHECK
            (LEN(LTRIM(RTRIM(ModuloId))) > 0 AND LEN(LTRIM(RTRIM(RegistroId))) > 0)
    );
END;
GO

-- UNA PROPUESTA VIVA POR REGISTRO. Las aplicadas y las rechazadas se acumulan: son el historial de
-- lo que se pidió cambiar y de lo que se decidió.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_PropuestasDeCambio_Viva'
                 AND object_id = OBJECT_ID(N'dbo.PropuestasDeCambio', N'U'))
BEGIN
    CREATE UNIQUE INDEX UQ_PropuestasDeCambio_Viva
        ON dbo.PropuestasDeCambio (ModuloId, RegistroId)
        -- SIN `NOT IN`: el predicado de un índice filtrado de SQL Server solo admite
        -- comparaciones simples unidas por AND, y con `NOT IN` el guion muere con «Incorrect
        -- syntax near 'NOT'». Las dos desigualdades dicen lo mismo y sí se admiten.
        WHERE Estado <> N'aplicada' AND Estado <> N'rechazada';
END;
GO

-- «Qué se ha propuesto sobre este registro», de lo más reciente a lo más antiguo.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_PropuestasDeCambio_Registro'
                 AND object_id = OBJECT_ID(N'dbo.PropuestasDeCambio', N'U'))
BEGIN
    CREATE INDEX IX_PropuestasDeCambio_Registro
        ON dbo.PropuestasDeCambio (ModuloId, RegistroId, IdPropuesta DESC);
END;
GO

-- LA BANDEJA PREGUNTA POR ESTADO, NO POR REGISTRO. «Qué espera una decisión» recorre todos los
-- módulos a la vez, y sin este índice esa consulta barre la tabla entera.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_PropuestasDeCambio_Estado'
                 AND object_id = OBJECT_ID(N'dbo.PropuestasDeCambio', N'U'))
BEGIN
    CREATE INDEX IX_PropuestasDeCambio_Estado
        ON dbo.PropuestasDeCambio (Estado, FechaEnvio);
END;
GO

IF OBJECT_ID(N'dbo.PropuestasDeCambioCampos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PropuestasDeCambioCampos
    (
        IdCampoPropuesto       bigint         IDENTITY(1,1) NOT NULL,
        IdPropuesta            bigint         NOT NULL,

        -- DONDE ESTA EN LA PANTALLA, para agrupar la comparación como se agrupa la ficha.
        SeccionId              nvarchar(80)   NOT NULL,
        CampoId                nvarchar(120)  NOT NULL,
        CampoEtiqueta          nvarchar(240)  NOT NULL,

        -- LO QUE DECIA Y LO QUE SE PROPONE. `ValorAnterior` se copia al enviar; los dos son texto
        -- porque lo que se compara en pantalla es texto, y guardar aquí el tipo de cada campo
        -- obligaría a esta tabla a conocer el modelo de cada proceso.
        ValorAnterior          nvarchar(max)  NULL,
        ValorPropuesto         nvarchar(max)  NULL,

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_PropuestasDeCambioCampos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,

        CONSTRAINT PK_PropuestasDeCambioCampos PRIMARY KEY (IdCampoPropuesto),

        -- SIN CASCADA, COMO EL RESTO DE LA BASE. PNMC tiene CERO foráneas con `ON DELETE
        -- CASCADE`, y hay una prueba de estructura que lo comprueba sobre la base entera. Lo grave
        -- de una cascada suelta no es la cascada: es que el mismo DELETE haga dos cosas distintas
        -- según la tabla. Quien borra una propuesta borra antes sus campos, y eso se ve en el
        -- código en vez de ocurrir solo.
        CONSTRAINT FK_PropuestasDeCambioCampos_Propuesta
            FOREIGN KEY (IdPropuesta) REFERENCES dbo.PropuestasDeCambio (IdPropuesta),

        -- UN CAMPO SE PROPONE UNA VEZ. Dos filas del mismo campo son dos valores propuestos a la
        -- vez, y al aplicar no habría forma de saber cuál gana.
        CONSTRAINT UQ_PropuestasDeCambioCampos_Campo UNIQUE (IdPropuesta, CampoId),

        CONSTRAINT CK_PropuestasDeCambioCampos_Campo CHECK
            (LEN(LTRIM(RTRIM(CampoId))) > 0 AND LEN(LTRIM(RTRIM(CampoEtiqueta))) > 0)
    );
END;
GO
