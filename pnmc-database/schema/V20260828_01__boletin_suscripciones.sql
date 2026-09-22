/*
    PNMC · Comunicaciones · Suscripciones al boletin

    QUE ES ESTO
    -----------
    Una fila por persona que deja su correo en el boletin de la portada. Lo pidio el usuario el 28
    con este criterio: «este registro deberia llegar a una
    base de datos en la pestaña de comunicaciones en el cms donde salga Mailins, o CRM de lead
    interesados en informacion del plan».

    EL MODELO: NADA. El campo de correo de la portada no estaba conectado a ninguna parte. El
    boton no tenia manejador: se escribia el correo, se pulsaba, y no pasaba absolutamente nada. Ni
    peticion, ni error, ni aviso. Quien lo usara se quedaba creyendo que se habia suscrito.

    POR QUE UNA TABLA PROPIA Y NO `dbo.SolicitudesParticipacion`
    ------------------------------------------------------------
    Porque no son lo mismo y mezclarlas haria trabajo de mas en las dos. Una solicitud de
    participacion trae tipo de actor, nombre, territorio y descripcion, y su destino es la bandeja
    de revision: alguien la aprueba o la rechaza. Un correo de boletin no se aprueba, no tiene
    territorio y no genera registro en el ecosistema. Es una lista de distribucion.

    LA AUTORIZACION SE GUARDA, Y NO ES BUROCRACIA
    ---------------------------------------------
    Un correo es dato personal, y usarlo para enviar comunicaciones exige autorizacion previa,
    expresa e informada (Ley 1581 de 2012, articulo 9). La ley pide ademas poder DEMOSTRAR que se
    obtuvo, asi que no basta un booleano: se guarda tambien el texto exacto que la persona acepto y
    la fecha. Si el texto cambia, las filas viejas siguen diciendo a que se autorizo entonces, que
    es justo lo que hace falta cuando alguien pregunta.

    Es tambien la respuesta a la pregunta que va a llegar del Ministerio, porque su propio paquete
    documental pide el registro RNBD y la politica de tratamiento de datos.

    LO QUE NO SE GUARDA, Y A PROPOSITO
    -----------------------------------
    Ni IP, ni agente de usuario, ni nada que identifique el dispositivo. Para una lista de correo
    no aportan nada y son dato personal adicional que despues hay que declarar, custodiar y borrar.
    El correo y la fecha bastan.

    EL VOCABULARIO DE `Estado`
    --------------------------
    Dos valores y un CHECK: `activa` y `baja`. No se borra la fila al darse de baja porque
    entonces la persona volveria a recibir el boletin en cuanto alguien reimportara una lista
    vieja; la baja tiene que quedar escrita. Anyadir un valor obliga a tocar este guion, que es lo
    que se quiere.

    IDEMPOTENTE: se puede ejecutar sobre una base que ya lo tenga.
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.BoletinSuscripciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BoletinSuscripciones
    (
        IdSuscripcion int IDENTITY(1,1) NOT NULL,

        /*
            Se guarda ya normalizado en minusculas por el API. La restriccion UNIQUE es la que
            impide dos filas para la misma persona, y no una comprobacion previa en el codigo:
            entre el SELECT y el INSERT caben dos peticiones simultaneas.
        */
        CorreoElectronico nvarchar(180) NOT NULL,

        /*
            De donde salio. Hoy solo existe la portada, pero el dia que haya un segundo formulario
            —una pagina de un festival, un evento— hara falta saber de cual vino cada correo para
            poder segmentar los envios. Es una columna barata ahora y cara despues.
        */
        Origen nvarchar(60) NOT NULL CONSTRAINT DF_BoletinSuscripciones_Origen DEFAULT (N'portada'),

        Estado nvarchar(30) NOT NULL CONSTRAINT DF_BoletinSuscripciones_Estado DEFAULT (N'activa'),

        /* La evidencia de la autorizacion: que se acepto, y a que texto exactamente. */
        AutorizacionOtorgada bit NOT NULL,
        AutorizacionTexto nvarchar(400) NOT NULL,

        FechaAlta datetime2(0) NOT NULL CONSTRAINT DF_BoletinSuscripciones_FechaAlta DEFAULT (SYSUTCDATETIME()),
        FechaBaja datetime2(0) NULL,

        CONSTRAINT PK_BoletinSuscripciones PRIMARY KEY (IdSuscripcion),
        CONSTRAINT UQ_BoletinSuscripciones_Correo UNIQUE (CorreoElectronico),
        CONSTRAINT CK_BoletinSuscripciones_Estado CHECK (Estado IN (N'activa', N'baja')),
        CONSTRAINT CK_BoletinSuscripciones_Correo_Formato CHECK (CorreoElectronico LIKE '%_@_%._%'),

        /*
            Una suscripcion sin autorizacion no es una suscripcion, es un correo recogido. La base
            lo impide para que no dependa de que el API se acuerde de comprobarlo.
        */
        CONSTRAINT CK_BoletinSuscripciones_Autorizacion CHECK (AutorizacionOtorgada = 1),

        /* Si esta de baja, hay fecha de baja; si esta activa, no la hay. */
        CONSTRAINT CK_BoletinSuscripciones_FechaBaja CHECK (
            (Estado = N'baja' AND FechaBaja IS NOT NULL)
            OR (Estado = N'activa' AND FechaBaja IS NULL)
        )
    );
END;

/* El listado de la consola ordena por fecha de alta descendente y filtra por estado. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BoletinSuscripciones_Estado_FechaAlta'
               AND object_id = OBJECT_ID(N'dbo.BoletinSuscripciones'))
BEGIN
    CREATE INDEX IX_BoletinSuscripciones_Estado_FechaAlta
        ON dbo.BoletinSuscripciones (Estado, FechaAlta DESC);
END;
