/*
    PNMC · Revisión institucional campo por campo, para cualquier proceso del Ecosistema

    QUE ES ESTO
    -----------
    Las dos tablas que sostienen la devolución con cambios pedidos UNO A UNO, escritas sin nombrar
    ningún proceso concreto: identifican el registro por MODULO + IDENTIFICADOR. Nacen para
    Mercados Musicales, que hasta hoy solo sabía devolver un párrafo de texto libre, y sirven tal
    cual para Escuelas, Escenarios, Redes y Lutería cuando existan.

    POR QUE GENERICAS Y NO `RevisionesMercado`
    ------------------------------------------
    Porque la alternativa es un juego de tablas por proceso, y con seis procesos son seis copias de
    la misma lógica que divergen en cuanto una cambie. quedó fijado
    pidiendo que Mercados funcione «como festivales […] todas las opciones
    posibles de festivales, replicado»: replicar el COMPORTAMIENTO, no el esquema.

    QUE PASA CON `RevisionesFestival`, QUE YA EXISTE
    ------------------------------------------------
    NO SE TOCA EN ESTE GUION. Migrar los expedientes de Festival —abiertos y cerrados— a la forma
    nueva es un cambio sobre datos que ya existen y merece su propio modelo, con su marcha atrás y su
    recuento antes y después. Hasta entonces conviven las dos, y eso es una deuda NOMBRADA: está
    escrita aquí, en el plan de paridad y en la bitácora, no escondida. Lo que este guion impide es
    que la deuda crezca con cada proceso nuevo.

    POR QUE DOS TABLAS Y NO UN JSON EN UNA COLUMNA
    ----------------------------------------------
    El mismo motivo que en `V20260829_02`: de las notas se pregunta de a una —cuántas quedan
    pendientes, cuáles son de tal edición, cuál se atendió y cuándo—, y con un JSON en una columna
    esas preguntas se contestan trayendo el documento entero a memoria, sin poder indexar ninguna.

    LA GOBERNANZA DEL DATO
    ----------------------
    `IdUsuarioRevisor` es quien envía y `IdUsuarioDestinatario` quien recibe, y los dos nombres se
    COPIAN en el momento. Resolverlos por JOIN contra el presente hace que renombrar a una persona
    reescriba lo que dicen todos los expedientes cerrados. Si el dato no está, se deja nulo: nunca
    se rellena con un valor de reemplazo.

    UNA REVISION VIVA POR REGISTRO
    ------------------------------
    Índice único filtrado sobre las que no están cerradas. Sin él, dos funcionarios con la misma
    bandeja abierta dejan dos borradores sobre el mismo mercado y nada dice cuál se envía.

    EL VOCABULARIO, CERRADO CON UN CHECK
    ------------------------------------
      · borrador  — la escribe el funcionario y solo él la ve.
      · enviada   — la organización ya la tiene y está corrigiendo.
      · cerrada   — la organización volvió a enviar el registro a revisión.

    Y el de la nota, dos: `pendiente` y `atendida`.

    EL AMBITO, SIN NOMBRAR TABLAS
    -----------------------------
    `principal` es la ficha del registro y `subregistro` una de sus realizaciones —una edición—. Son
    dos sitios con campos que se llaman igual: «Lugar específico» existe en el mercado y en cada una
    de sus ediciones, y sin el ámbito una nota sobre el lugar no dice cuál hay que corregir.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RevisionesDeRegistro', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesDeRegistro
    (
        IdRevision             bigint         IDENTITY(1,1) NOT NULL,

        -- QUE SE ESTA REVISANDO. `ModuloId` es el mismo vocabulario que usan la bitácora de
        -- auditoría y la procedencia —`mercados`, `festivales`, `escuelas`…—, y `RegistroId` es
        -- texto por lo mismo que en aquellas: no todos los procesos tienen clave entera, y una
        -- columna que a veces es número y a veces no es peor que una que siempre es texto.
        ModuloId               nvarchar(80)   NOT NULL,
        RegistroId             nvarchar(120)  NOT NULL,

        Estado                 nvarchar(40)   NOT NULL
            CONSTRAINT DF_RevisionesDeRegistro_Estado DEFAULT (N'borrador'),

        -- QUIEN ENVIA. El funcionario que abrió la ficha y escribió las notas.
        IdUsuarioRevisor       int            NOT NULL,
        RevisorNombre          nvarchar(480)  NULL,

        -- QUIEN RECIBE. La persona de la organización que mandó el registro a revisión. Nulable
        -- porque puede no constar, e inventar un destinatario sería peor que decir que no se sabe.
        IdUsuarioDestinatario  int            NULL,
        DestinatarioNombre     nvarchar(480)  NULL,

        IdOrganizacion         int            NULL,
        OrganizacionNombre     nvarchar(480)  NULL,

        -- Lo que no cabe en ningún campo concreto.
        ObservacionGeneral     nvarchar(2400) NULL,

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_RevisionesDeRegistro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,
        FechaEnvio             datetime2(0)   NULL,
        FechaCierre            datetime2(0)   NULL,

        CONSTRAINT PK_RevisionesDeRegistro PRIMARY KEY (IdRevision),

        -- SIN CLAVE AJENA AL REGISTRO, y es el precio de ser genérica: no hay una sola tabla a la
        -- que apuntar. Lo sostiene el servidor, que comprueba que el registro existe antes de abrir
        -- la revisión, igual que hacen la auditoría y la procedencia con el mismo par de columnas.
        CONSTRAINT FK_RevisionesDeRegistro_Revisor
            FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT FK_RevisionesDeRegistro_Destinatario
            FOREIGN KEY (IdUsuarioDestinatario) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT FK_RevisionesDeRegistro_Organizacion
            FOREIGN KEY (IdOrganizacion) REFERENCES dbo.Entidades (IdEntidad),

        CONSTRAINT CK_RevisionesDeRegistro_Estado CHECK (Estado IN
            (N'borrador', N'enviada', N'cerrada')),

        -- UNA FECHA DE ENVIO SIN ESTADO ENVIADO ES UN EXPEDIENTE QUE MIENTE, y al revés igual.
        CONSTRAINT CK_RevisionesDeRegistro_FechaEnvio CHECK
            ((Estado = N'borrador' AND FechaEnvio IS NULL)
             OR (Estado <> N'borrador' AND FechaEnvio IS NOT NULL)),

        CONSTRAINT CK_RevisionesDeRegistro_FechaCierre CHECK
            ((Estado = N'cerrada' AND FechaCierre IS NOT NULL)
             OR (Estado <> N'cerrada' AND FechaCierre IS NULL)),

        CONSTRAINT CK_RevisionesDeRegistro_Registro CHECK
            (LEN(LTRIM(RTRIM(ModuloId))) > 0 AND LEN(LTRIM(RTRIM(RegistroId))) > 0)
    );
END;
GO

-- UNA REVISION VIVA POR REGISTRO. Filtrado: las cerradas se acumulan sin estorbar, que es lo que
-- las hace historial.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_RevisionesDeRegistro_Viva'
                 AND object_id = OBJECT_ID(N'dbo.RevisionesDeRegistro', N'U'))
BEGIN
    CREATE UNIQUE INDEX UQ_RevisionesDeRegistro_Viva
        ON dbo.RevisionesDeRegistro (ModuloId, RegistroId)
        WHERE Estado <> N'cerrada';
END;
GO

-- «Qué le ha pasado a este registro»: todas sus revisiones, de la más reciente a la más antigua.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RevisionesDeRegistro_Registro'
                 AND object_id = OBJECT_ID(N'dbo.RevisionesDeRegistro', N'U'))
BEGIN
    CREATE INDEX IX_RevisionesDeRegistro_Registro
        ON dbo.RevisionesDeRegistro (ModuloId, RegistroId, IdRevision DESC);
END;
GO

IF OBJECT_ID(N'dbo.RevisionesDeRegistroObservaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesDeRegistroObservaciones
    (
        IdObservacion          bigint         IDENTITY(1,1) NOT NULL,
        IdRevision             bigint         NOT NULL,

        -- SOBRE QUE SE PIDE EL CAMBIO. `principal` es la ficha del registro; `subregistro`, una de
        -- sus realizaciones. Sin el ámbito, una nota sobre «Lugar específico» no dice si hay que
        -- corregir el del mercado o el de su edición de 2026.
        Ambito                 nvarchar(40)   NOT NULL,
        SubregistroId          nvarchar(120)  NULL,

        -- DONDE ESTA EN LA PANTALLA, para agrupar las notas como se agrupan los campos.
        SeccionId              nvarchar(80)   NOT NULL,

        CampoId                nvarchar(120)  NOT NULL,

        -- EL ROTULO Y EL VALOR, COPIADOS EN EL MOMENTO. Es la evidencia de sobre qué se pidió el
        -- cambio: el campo ya no dice eso, justo porque se pidió cambiarlo.
        CampoEtiqueta          nvarchar(240)  NOT NULL,
        ValorObservado         nvarchar(max)  NULL,

        Nota                   nvarchar(2400) NOT NULL,

        Estado                 nvarchar(40)   NOT NULL
            CONSTRAINT DF_RevisionesDeRegistroObservaciones_Estado DEFAULT (N'pendiente'),

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_RevisionesDeRegistroObservaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,
        FechaAtencion          datetime2(0)   NULL,
        IdUsuarioAtiende       int            NULL,

        CONSTRAINT PK_RevisionesDeRegistroObservaciones PRIMARY KEY (IdObservacion),

        -- SIN CASCADA, COMO TODA LA BASE: PNMC tiene cero, y lo grave no es la cascada sino la
        -- incoherencia. Y no hace falta: las revisiones se cierran, no se borran.
        CONSTRAINT FK_RevisionesDeRegistroObservaciones_Revision
            FOREIGN KEY (IdRevision) REFERENCES dbo.RevisionesDeRegistro (IdRevision),

        CONSTRAINT FK_RevisionesDeRegistroObservaciones_Atiende
            FOREIGN KEY (IdUsuarioAtiende) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT CK_RevisionesDeRegistroObservaciones_Ambito CHECK (Ambito IN
            (N'principal', N'subregistro')),

        CONSTRAINT CK_RevisionesDeRegistroObservaciones_Estado CHECK (Estado IN
            (N'pendiente', N'atendida')),

        -- UNA NOTA DE SUBREGISTRO SIN SUBREGISTRO NO SE PUEDE PINTAR EN NINGUN SITIO, y una de la
        -- ficha con subregistro apuntaría a dos cosas a la vez.
        CONSTRAINT CK_RevisionesDeRegistroObservaciones_Subregistro CHECK
            ((Ambito = N'subregistro' AND SubregistroId IS NOT NULL)
             OR (Ambito = N'principal' AND SubregistroId IS NULL)),

        -- LA NOTA VACIA NO EXISTE: es la diferencia entre pedir un cambio y no pedirlo.
        CONSTRAINT CK_RevisionesDeRegistroObservaciones_Nota CHECK (LEN(LTRIM(RTRIM(Nota))) > 0),

        -- UN CAMPO, UNA NOTA. Dos notas sobre el mismo campo en la misma revisión son dos
        -- instrucciones que pueden contradecirse, y la pantalla solo tiene sitio para una.
        CONSTRAINT UQ_RevisionesDeRegistroObservaciones_Campo
            UNIQUE (IdRevision, Ambito, SubregistroId, CampoId)
    );
END;
GO

-- La consulta de siempre: las notas de una revisión, agrupadas por sección y en el orden en que se
-- escribieron.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RevisionesDeRegistroObservaciones_Revision'
                 AND object_id = OBJECT_ID(N'dbo.RevisionesDeRegistroObservaciones', N'U'))
BEGIN
    CREATE INDEX IX_RevisionesDeRegistroObservaciones_Revision
        ON dbo.RevisionesDeRegistroObservaciones (IdRevision, SeccionId, IdObservacion);
END;
GO
