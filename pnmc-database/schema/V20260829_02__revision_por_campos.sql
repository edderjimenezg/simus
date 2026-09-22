/*
    PNMC · Revisión institucional · Cambios pedidos campo por campo

    QUE ES ESTO
    -----------
    Dos tablas que sostienen el circuito de devolución de un Festival: quien revisa abre la ficha
    completa, anota QUE hay que cambiar EN CADA CAMPO, guarda el borrador tantas veces como quiera,
    y cuando termina envía la solicitud a la organización. Lo pidió el usuario el 29 de agosto de
    2026: «en todos los campos poder pedir cambios puntuales sobre alguno de los campos, ir
    guardando el borrador, y luego poder enviar la solicitud de cambios y que al devolverlo le
    llegue al usuario para hacer esos cambios sobre el festival […] este proceso debe tener
    gobernanza del dato e historial de cambios en la base de datos, quién envía, quién recibe».

    EL MODELO
    -----------------------
    Un solo campo de texto. `RevisionInstitucionalFestivalesEndpoints.cs` recibe
    `DecisionRevisionFestivalSolicitud.Observacion` y la guarda en
    `RegistrosRevisionHistorial.Comentario`, nvarchar(2400), UNA fila por decisión. Todo lo que el
    funcionario tuviera que decir sobre cuarenta y ocho campos cabía en ese párrafo, y llegaba a la
    organización como un aviso suelto en la tarjeta del Festival
    (`seccion-festivales.component.html:95-100`). No había forma de saber a qué campo se refería
    cada frase, ni de marcar un punto como atendido, ni de contar cuántos quedaban.

    Y HABIA UNA COLUMNA ESPERANDO. `RegistrosRevisionHistorial.CamposObservados`, nvarchar(max),
    existe desde `V20260525_01__administracion_extendida.sql`. La escribe una sola ruta
    —`POST /admin/data/{modulo}/{id}/status`, para agenda, noticias y galería— desde
    `AdminDataEndpoints.cs`, y NINGUNA pantalla la llena: `observedFieldsJson` viaja siempre
    vacío desde `admin.service.ts`. Es decir: el modelo ya preveía la revisión por campos y
    nunca llegó a existir. Estas dos tablas la construyen para Festivales, y el envío sigue
    escribiendo esa columna con el JSON de lo pedido, para que el historial que ya se consulta lo
    lleve dentro sin cambiar de forma.

    POR QUE DOS TABLAS Y NO UN JSON EN UNA COLUMNA
    ----------------------------------------------
    Porque de las notas se pregunta de a una: cuántas quedan pendientes en este Festival, cuáles son
    de la edición 2025, cuál atendió la organización y cuándo. Con un JSON en una columna esas cuatro
    preguntas se contestan trayendo el documento entero a memoria y recorriéndolo en C#, y ninguna
    se puede indexar. `RegistrosRevisionHistorial.CamposObservados` sigue guardando el JSON, pero
    como INSTANTANEA de lo que se envió aquel día —el papel del expediente—, no como fuente de
    consulta.

    LA GOBERNANZA DEL DATO, QUE ES LA MITAD DEL PEDIDO
    --------------------------------------------------
    `RevisionesFestival` guarda las dos puntas de la conversación: `IdUsuarioRevisor` es quien
    envía, `IdUsuarioDestinatario` quien recibe —la persona de la organización que había mandado el
    Festival a revisión—, y con cada uno su nombre COPIADO en ese momento. La copia sigue el mismo
    criterio de `InstantaneaDelHistorial.cs`: resolver el nombre por JOIN contra el presente hace
    que renombrar a una persona reescriba lo que dicen todos los expedientes viejos, sin error y sin
    aviso. Si el dato no está, se deja nulo; nunca se rellena con un valor de reemplazo.

    UNA REVISION VIVA POR FESTIVAL
    ------------------------------
    `UQ_RevisionesFestival_Viva` es un índice único filtrado sobre las revisiones que no están
    cerradas. Sin él, dos funcionarios con la misma bandeja abierta dejan dos borradores sobre el
    mismo Festival y nada dice cuál se envía. Con él, el segundo recibe un error de la base en vez
    de escribir un expediente paralelo.

    EL VOCABULARIO DE `Estado`, CERRADO CON UN CHECK
    ------------------------------------------------
    Tres valores y no más, por lo que costó `en_evaluacion`: un estado inventado en un formulario
    vivió semanas porque su etiqueta en pantalla estaba bien escrita.

      · borrador  — la escribe el funcionario y solo él la ve.
      · enviada   — la organización ya la tiene y está corrigiendo.
      · cerrada   — la organización volvió a enviar el Festival a revisión.

    Y el de la nota, dos:

      · pendiente — nadie la ha dado por resuelta.
      · atendida  — la organización la marcó, o el reenvío a revisión la cerró.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.RevisionesFestival', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesFestival
    (
        IdRevisionFestival     bigint         IDENTITY(1,1) NOT NULL,
        IdFestival             int            NOT NULL,

        Estado                 nvarchar(40)   NOT NULL
            CONSTRAINT DF_RevisionesFestival_Estado DEFAULT (N'borrador'),

        -- QUIEN ENVIA. El funcionario que abrió la ficha y escribió las notas.
        IdUsuarioRevisor       int            NOT NULL,
        RevisorNombre          nvarchar(480)  NULL,

        -- QUIEN RECIBE. La persona de la organización que mandó el Festival a revisión. Es nulable
        -- porque hay Festivales historicos cuyo envio no dejo fila en RegistrosRevisionHistorial:
        -- inventar un destinatario seria peor que decir que no se sabe.
        IdUsuarioDestinatario  int            NULL,
        DestinatarioNombre     nvarchar(480)  NULL,

        IdOrganizacion         int            NULL,
        OrganizacionNombre     nvarchar(480)  NULL,

        -- Lo que no cabe en ningun campo concreto. Es lo unico que el circuito sabia decir antes.
        ObservacionGeneral     nvarchar(2400) NULL,

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_RevisionesFestival_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,
        FechaEnvio             datetime2(0)   NULL,
        FechaCierre            datetime2(0)   NULL,

        CONSTRAINT PK_RevisionesFestival PRIMARY KEY (IdRevisionFestival),

        CONSTRAINT FK_RevisionesFestival_Festivales
            FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival),

        CONSTRAINT FK_RevisionesFestival_Revisor
            FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT FK_RevisionesFestival_Destinatario
            FOREIGN KEY (IdUsuarioDestinatario) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT CK_RevisionesFestival_Estado CHECK (Estado IN
            (N'borrador', N'enviada', N'cerrada')),

        -- UNA FECHA DE ENVIO SIN ESTADO ENVIADO ES UN EXPEDIENTE QUE MIENTE, y al reves igual. Las
        -- dos columnas dicen lo mismo con distinto tipo, asi que la base las obliga a coincidir.
        CONSTRAINT CK_RevisionesFestival_FechaEnvio CHECK
            ((Estado = N'borrador' AND FechaEnvio IS NULL)
             OR (Estado <> N'borrador' AND FechaEnvio IS NOT NULL)),

        CONSTRAINT CK_RevisionesFestival_FechaCierre CHECK
            ((Estado = N'cerrada' AND FechaCierre IS NOT NULL)
             OR (Estado <> N'cerrada' AND FechaCierre IS NULL))
    );
END;
GO

-- UNA REVISION VIVA POR FESTIVAL. Filtrado: las cerradas se acumulan sin estorbar, que es lo que
-- las hace historial.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_RevisionesFestival_Viva'
                 AND object_id = OBJECT_ID(N'dbo.RevisionesFestival', N'U'))
BEGIN
    CREATE UNIQUE INDEX UQ_RevisionesFestival_Viva
        ON dbo.RevisionesFestival (IdFestival)
        WHERE Estado <> N'cerrada';
END;
GO

IF OBJECT_ID(N'dbo.RevisionesFestivalObservaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevisionesFestivalObservaciones
    (
        IdObservacion          bigint         IDENTITY(1,1) NOT NULL,
        IdRevisionFestival     bigint         NOT NULL,

        -- SOBRE QUE SE PIDE EL CAMBIO. `festival` es la cabecera —dbo.Festivales—; `edicion` es una
        -- fila concreta de dbo.VersionesFestival. Son dos tablas distintas con campos que se llaman
        -- igual: «Correo de contacto» existe en las dos. Sin el ambito, una nota sobre el correo no
        -- dice cual de los dos hay que corregir.
        Ambito                 nvarchar(40)   NOT NULL,
        IdVersionFestival      int            NULL,

        -- DONDE ESTA EN LA PANTALLA. El identificador de la seccion de la ficha, para agrupar las
        -- notas como se agrupan los campos: «es importante que sea por secciones», del usuario.
        SeccionId              nvarchar(80)   NOT NULL,

        CampoId                nvarchar(120)  NOT NULL,

        -- EL ROTULO Y EL VALOR, COPIADOS EN EL MOMENTO. Es la evidencia de sobre que se pidio el
        -- cambio: sin ellos, leer un expediente de hace un anyo obliga a adivinar que decia el campo
        -- entonces, y el campo ya no dice eso —justo porque se pidio cambiarlo—.
        CampoEtiqueta          nvarchar(240)  NOT NULL,
        ValorObservado         nvarchar(max)  NULL,

        Nota                   nvarchar(2400) NOT NULL,

        Estado                 nvarchar(40)   NOT NULL
            CONSTRAINT DF_RevisionesFestivalObservaciones_Estado DEFAULT (N'pendiente'),

        FechaCreacion          datetime2(0)   NOT NULL
            CONSTRAINT DF_RevisionesFestivalObservaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion     datetime2(0)   NULL,
        FechaAtencion          datetime2(0)   NULL,
        IdUsuarioAtiende       int            NULL,

        CONSTRAINT PK_RevisionesFestivalObservaciones PRIMARY KEY (IdObservacion),

        -- SIN CASCADA, COMO TODA LA BASE. El modelo no usa cascadas en ninguna clave foranea, y
        -- `EstructuraSimusSqlServerTests.Ninguna_Foranea_Tiene_Cascada` lo comprueba. Lo grave de
        -- una cascada suelta no es la cascada sino la incoherencia: el mismo DELETE haciendo dos
        -- cosas distintas segun la tabla.
        --
        -- Y NO HACIA FALTA: ninguna ruta borra una fila de `RevisionesFestival`. Las revisiones se
        -- cierran, no se borran, que es lo que las convierte en historial. Lo que si se borra son
        -- notas sueltas, y eso lo hace `ReemplazarObservacionesAsync` una por una.
        CONSTRAINT FK_RevisionesFestivalObservaciones_Revision
            FOREIGN KEY (IdRevisionFestival) REFERENCES dbo.RevisionesFestival (IdRevisionFestival),

        CONSTRAINT FK_RevisionesFestivalObservaciones_Version
            FOREIGN KEY (IdVersionFestival) REFERENCES dbo.VersionesFestival (IdVersionFestival),

        CONSTRAINT FK_RevisionesFestivalObservaciones_Atiende
            FOREIGN KEY (IdUsuarioAtiende) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT CK_RevisionesFestivalObservaciones_Ambito CHECK (Ambito IN
            (N'festival', N'edicion')),

        CONSTRAINT CK_RevisionesFestivalObservaciones_Estado CHECK (Estado IN
            (N'pendiente', N'atendida')),

        -- UNA NOTA DE EDICION SIN EDICION NO SE PUEDE PINTAR EN NINGUN SITIO, y una de cabecera con
        -- edicion apuntaria a dos cosas a la vez.
        CONSTRAINT CK_RevisionesFestivalObservaciones_Version CHECK
            ((Ambito = N'edicion' AND IdVersionFestival IS NOT NULL)
             OR (Ambito = N'festival' AND IdVersionFestival IS NULL)),

        -- LA NOTA VACIA NO EXISTE: es la diferencia entre pedir un cambio y no pedirlo. El front
        -- borra la fila cuando se vacia el texto; esto lo sostiene tambien contra el API.
        CONSTRAINT CK_RevisionesFestivalObservaciones_Nota CHECK (LEN(LTRIM(RTRIM(Nota))) > 0),

        -- UN CAMPO, UNA NOTA. Dos notas sobre «Nombre del festival» en la misma revision son dos
        -- instrucciones que pueden contradecirse, y la pantalla solo tiene sitio para una.
        -- SQL Server trata los NULL como iguales en UNIQUE, asi que esto tambien cierra el caso de
        -- la cabecera, donde IdVersionFestival es nulo.
        CONSTRAINT UQ_RevisionesFestivalObservaciones_Campo
            UNIQUE (IdRevisionFestival, Ambito, IdVersionFestival, CampoId)
    );
END;
GO

-- La consulta de siempre: las notas de una revision, agrupadas por seccion y en el orden en que se
-- escribieron.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RevisionesFestivalObservaciones_Revision'
                 AND object_id = OBJECT_ID(N'dbo.RevisionesFestivalObservaciones', N'U'))
BEGIN
    CREATE INDEX IX_RevisionesFestivalObservaciones_Revision
        ON dbo.RevisionesFestivalObservaciones (IdRevisionFestival, SeccionId, IdObservacion);
END;
GO
