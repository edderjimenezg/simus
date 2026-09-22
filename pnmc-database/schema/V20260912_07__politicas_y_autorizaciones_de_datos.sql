-- =============================================================================================
-- Un solo registro de autorizaciones de datos (Ley 1581 de 2012)
-- =============================================================================================
--
-- QUE PROBLEMA CIERRA. El sistema pedia autorizacion de tratamiento por TRES caminos distintos y
-- ninguno de los tres podia responder entera la pregunta que la ley obliga a responder: «quien
-- autorizo que, cuando, con que texto delante, y sigue autorizandolo».
--
--   1. EL ALTA DE ORGANIZACION. `dbo.DocumentosLegales` + `dbo.AceptacionesDocumentosLegales`,
--      con 26 aceptaciones reales guardadas. Lo que guarda de cada una es el identificador del
--      documento y la fecha. NO guarda el texto: el documento solo tiene `UrlPublica`, un enlace a
--      un PDF alojado en mincultura.gov.co. Si ese PDF cambia o se mueve —y los PDF de un
--      ministerio cambian y se mueven—, la evidencia de lo que 26 personas aceptaron desaparece
--      sin que en esta base quede rastro. El deber de demostrar (Ley 1581 art. 17 lit. f, y el
--      decreto 1377 de 2013 art. 5) no se cumple con un enlace a un servidor ajeno.
--
--   2. EL BOLETIN. `dbo.BoletinSuscripciones.AutorizacionTexto` SI copia el texto entero, que es
--      exactamente lo correcto. Pero lo hace solo para el boletin, con el texto escrito como
--      constante dentro de `BoletinEndpoints.cs`, y sin guardar la version —la constante
--      `VersionDeLaPolitica` existe en el codigo y no se persiste en ninguna fila—. Cambiar el
--      texto en el codigo deja tres filas antiguas que dicen una cosa y una constante que dice
--      otra.
--
--   3. LA ORGANIZACION. `dbo.EntidadesResponsable.ResponsableAutorizacionDatos`, un booleano
--      suelto. Un si/no sin texto, sin fecha propia, sin finalidad y sin forma de revocarse.
--
-- LO QUE FALTABA EN LOS TRES.
--
--   FINALIDAD. La Ley 1581 art. 9 y el decreto 1377 art. 5 piden autorizacion PARA FINES
--   DETERMINADOS. Una casilla unica en el alta no distingue entre tratar los datos para
--   administrar la cuenta y usar el correo para mandar boletines: son dos cosas y la persona debe
--   poder decir que si a una y que no a la otra.
--
--   VERSION EN LA AUTORIZACION. `AceptacionesDocumentosLegales` apunta a la FILA del documento, no
--   a su version. Actualizar `Version` en su sitio cambiaria, en silencio, lo que dicen que
--   aceptaron las 26 aceptaciones ya guardadas. Ademas su indice unico es (IdUsuario,
--   IdDocumentoLegal): con el puesto, una persona no puede aceptar la v2 despues de haber aceptado
--   la v1, que es justo lo que hay que poder hacer cuando el texto cambia.
--
--   REVOCACION. La Ley 1581 art. 8 num. 5 da al titular el derecho a revocar. Hoy no hay por donde:
--   ninguna de las tres formas tiene fecha de revocacion. El boletin da de baja la SUSCRIPCION
--   (`FechaBaja`), que no es lo mismo que revocar la autorizacion.
--
-- COMO SE CIERRA: DOS TABLAS Y UNA SOLA RESPUESTA.
--
--   `dbo.PoliticasDatos`        - el texto versionado que el servidor sirve. El texto vive aqui y
--                                 no en el frontend, porque si la pantalla lo llevara dentro, lo
--                                 que la persona lee y lo que el servidor guarda como evidencia
--                                 serian dos cosas distintas que nadie compara nunca.
--   `dbo.AutorizacionesDatos`   - cada autorizacion, con el texto COPIADO dentro. Copiado y no
--                                 referenciado: una referencia dice a que fila apunto; la copia
--                                 dice que leyo. Solo la segunda sirve como prueba.
--
-- NO SE PIERDE NI UNA FILA. Las 26 aceptaciones del alta y las 3 del boletin se trasladan al
-- registro nuevo antes de retirar nada, y las que no capturaron su texto quedan marcadas con
-- `TextoReconstruido = 1`: el registro no puede afirmar como prueba un texto que nadie guardo.
--
-- ESTE GUION NO RETIRA NADA. Es enteramente aditivo: crea las dos tablas y copia dentro lo que ya
-- existia, dejando intactas `DocumentosLegales`, `AceptacionesDocumentosLegales` y las dos
-- columnas del boletin. `V20260912_08` solo relaja lo que impide escribir, y el retiro de verdad
-- va en `V20260912_09`, para que entre copiar y borrar quepa comprobar que el codigo lee de la
-- estructura nueva. Lo que se esta moviendo son declaraciones de voluntad de personas sobre sus
-- datos personales: no es informacion que se pueda volver a generar si sale mal.
--
-- LAS CINCO POLITICAS DE SEPTIEMBRE SE QUEDAN EN TRES. Septiembre declaraba `tratamiento`,
-- `terminos`, `boletin`, `participacion` y `directorio`. Las dos ultimas no se crean: no hay
-- pantalla de participacion —no hay ruta en `app.routes.ts`— ni directorio publico de
-- organizaciones —no hay endpoint—. Crear una politica para una finalidad que el sistema no
-- ejerce seria texto que describe un estado inexistente, que es el defecto que esta base lleva
-- cuatro cortes retirando.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------------------------------------------------------------------------------------------
-- 1. Las politicas: el texto versionado que sirve el servidor
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.PoliticasDatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PoliticasDatos (
        IdPolitica int IDENTITY(1,1) NOT NULL,

        /* La finalidad que ampara. Es la clave por la que el frontend la pide. */
        Clave nvarchar(40) NOT NULL,

        /*
          LA VERSION ES UNA FECHA EN TEXTO (AAAA-MM-DD), y es la fecha en que ESTE texto empezo a
          mostrarse. No es la version del documento del Ministerio: eso va en `ReferenciaOficial`.
          Son dos cosas distintas y confundirlas fue parte del problema —las filas viejas guardaban
          «PL-GSI-001 v0», que identifica un PDF del Ministerio y no dice nada sobre cuando se le
          enseño a alguien que redaccion—.
        */
        Version nvarchar(40) NOT NULL,

        Titulo nvarchar(300) NOT NULL,

        /*
          EL TEXTO QUE LA PERSONA ACEPTA, entero. Es lo que se copia en cada autorizacion.

          NO ES LA POLITICA COMPLETA DEL MINISTERIO. Es el texto de consentimiento informado que
          aparece en pantalla: que se recoge, para que, quien responde y que derechos tiene el
          titular (Ley 1581 art. 12). La politica institucional completa es un documento del
          Ministerio y sigue siendo suya: se enlaza en `UrlOficial`.
        */
        Texto nvarchar(max) NOT NULL,

        /* El PDF institucional al que remite el texto. Anulable: no toda finalidad tiene uno. */
        UrlOficial nvarchar(2000) NULL,

        /* El identificador del documento del Ministerio —«PL-GSI-002 v0»—, cuando lo hay. */
        ReferenciaOficial nvarchar(160) NULL,

        Vigente bit NOT NULL CONSTRAINT DF_PoliticasDatos_Vigente DEFAULT (0),
        FechaPublicacion datetime2(3) NOT NULL CONSTRAINT DF_PoliticasDatos_FechaPublicacion DEFAULT (SYSUTCDATETIME()),
        FechaCreacion datetime2(3) NOT NULL CONSTRAINT DF_PoliticasDatos_FechaCreacion DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_PoliticasDatos PRIMARY KEY (IdPolitica),
        CONSTRAINT UQ_PoliticasDatos_ClaveVersion UNIQUE (Clave, Version),
        CONSTRAINT CK_PoliticasDatos_Clave CHECK (Clave IN (N'tratamiento', N'terminos', N'boletin'))
    );

    /*
      UNA SOLA VIGENTE POR CLAVE, y lo garantiza la base y no el codigo.

      El indice es FILTRADO: restringe la unicidad a las filas con Vigente = 1, de modo que puede
      haber tantas versiones retiradas de `tratamiento` como haga falta y exactamente una en uso.
      Sin el, dos filas vigentes de la misma clave harian que la respuesta del servidor dependiera
      del orden de lectura, y dos personas podrian aceptar textos distintos creyendo que aceptan el
      mismo.
    */
    CREATE UNIQUE INDEX UX_PoliticasDatos_ClaveVigente
        ON dbo.PoliticasDatos (Clave) WHERE Vigente = 1;
END
GO

-- ---------------------------------------------------------------------------------------------
-- 2. Las autorizaciones: quien autorizo que, con que texto delante, y si lo revoco
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.AutorizacionesDatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AutorizacionesDatos (
        IdAutorizacion bigint IDENTITY(1,1) NOT NULL,

        /* Para que. Una autorizacion por finalidad, que es como la pide la ley. */
        Finalidad nvarchar(40) NOT NULL,

        /* Que politica la ampara, y en que version. La version se COPIA: ver la nota del texto. */
        IdPolitica int NOT NULL,
        Version nvarchar(40) NOT NULL,

        /*
          EL TEXTO QUE TENIA DELANTE, COPIADO.

          Es la columna que justifica toda la tabla. Guardar `IdPolitica` responde «a que fila
          apunto»; solo la copia responde «que leyo». Cuando el texto de la politica cambie —y va a
          cambiar—, esta columna sigue diciendo lo que esta persona acepto aquel dia.
        */
        TextoAceptado nvarchar(max) NOT NULL,

        /*
          EL TEXTO NO SE CAPTURO EN SU MOMENTO Y SE RECONSTRUYO.

          Solo lo llevan las filas trasladadas desde `AceptacionesDocumentosLegales`, que no
          guardaban texto. Se les copia la frase que la plantilla de alta mostraba entonces, y se
          marcan: una evidencia reconstruida no vale lo mismo que una capturada, y el registro no
          debe presentar las dos con la misma cara. Toda autorizacion nueva nace con 0.
        */
        TextoReconstruido bit NOT NULL CONSTRAINT DF_AutorizacionesDatos_TextoReconstruido DEFAULT (0),

        /*
          QUIEN AUTORIZO. Una de las dos, o las dos.

          `IdUsuario` cuando hay cuenta. `CorreoTitular` cuando no la hay —quien se suscribe al
          boletin desde la portada no tiene cuenta y aun asi es titular de un dato personal y tiene
          derecho a revocar—. Este es el motivo por el que el boletin habia tenido que montarse su
          propio mecanismo aparte: la tabla anterior exigia un usuario.
        */
        IdUsuario int NULL,
        CorreoTitular nvarchar(180) NULL,

        /* Por que puerta entro: alta de organizacion, portada, noticias, consola. */
        Origen nvarchar(40) NOT NULL,

        /* El registro con el que vino, cuando lo hay: la suscripcion, la organizacion. */
        ReferenciaId nvarchar(120) NULL,

        FechaOtorgada datetime2(3) NOT NULL CONSTRAINT DF_AutorizacionesDatos_FechaOtorgada DEFAULT (SYSUTCDATETIME()),

        /*
          REVOCAR NO BORRA LA FILA.

          Borrarla dejaria sin respuesta la pregunta «¿esta persona autorizo alguna vez, y hasta
          cuando?», que es precisamente la que hay que poder contestar. La fila se queda y se le
          pone fecha de fin.
        */
        FechaRevocacion datetime2(3) NULL,
        MotivoRevocacion nvarchar(400) NULL,

        CONSTRAINT PK_AutorizacionesDatos PRIMARY KEY (IdAutorizacion),
        CONSTRAINT FK_AutorizacionesDatos_Politica FOREIGN KEY (IdPolitica) REFERENCES dbo.PoliticasDatos (IdPolitica),
        CONSTRAINT FK_AutorizacionesDatos_Usuario FOREIGN KEY (IdUsuario) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_AutorizacionesDatos_Finalidad CHECK (Finalidad IN (N'tratamiento', N'terminos', N'boletin')),
        CONSTRAINT CK_AutorizacionesDatos_Origen CHECK (Origen IN (N'registro', N'portada', N'noticias', N'consola')),

        /* SIN TITULAR NO HAY AUTORIZACION. Una fila sin cuenta y sin correo no es de nadie. */
        CONSTRAINT CK_AutorizacionesDatos_Titular CHECK (IdUsuario IS NOT NULL OR CorreoTitular IS NOT NULL),

        /* No se puede revocar antes de otorgar. */
        CONSTRAINT CK_AutorizacionesDatos_Fechas CHECK (FechaRevocacion IS NULL OR FechaRevocacion >= FechaOtorgada)
    );

    /* Las dos preguntas que se hacen de verdad: «que autorizo esta cuenta» y «que autorizo este correo». */
    CREATE INDEX IX_AutorizacionesDatos_Usuario
        ON dbo.AutorizacionesDatos (IdUsuario, Finalidad, FechaOtorgada DESC);
    CREATE INDEX IX_AutorizacionesDatos_Correo
        ON dbo.AutorizacionesDatos (CorreoTitular, Finalidad, FechaOtorgada DESC);
END
GO

-- ---------------------------------------------------------------------------------------------
-- 3. Las versiones HISTORICAS, para que lo ya aceptado siga apuntando a algo cierto
-- ---------------------------------------------------------------------------------------------
--
-- POR QUE SE CREAN FILAS DE POLITICA QUE NO ESTAN VIGENTES. Las 26 aceptaciones del alta y las 3
-- del boletin aceptaron redacciones anteriores. Colgarlas de la politica vigente diria que
-- aceptaron un texto que en ese momento no existia. Cada redaccion anterior entra como su propia
-- fila con `Vigente = 0`, y las autorizaciones trasladadas apuntan ahi.
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DocumentosLegales')
BEGIN
    INSERT INTO dbo.PoliticasDatos (Clave, Version, Titulo, Texto, UrlOficial, ReferenciaOficial, Vigente, FechaPublicacion)
    SELECT
        CASE doc.Codigo WHEN N'terminos_uso' THEN N'terminos' ELSE N'tratamiento' END,
        doc.Version,
        doc.Titulo,
        /*
          EL TEXTO RECONSTRUIDO, y dicho con precision de donde sale: es la frase que la plantilla
          del alta —`external-access-page.component.html`, las casillas de consentimiento— componia
          con el titulo y la version del documento. No es una invencion ni una aproximacion: es la
          cadena que esas 26 personas tuvieron delante. Lo que no se puede afirmar es que se
          capturara entonces, y por eso las filas que la usan van marcadas.
        */
        CASE doc.Codigo
            WHEN N'terminos_uso' THEN N'Acepto los ' + doc.Titulo + N' ' + doc.Version + N'.'
            ELSE N'Autorizo el tratamiento conforme a la ' + doc.Titulo + N' ' + doc.Version + N'.'
        END,
        doc.UrlPublica,
        doc.Version,
        0,
        doc.FechaCreacion
    FROM dbo.DocumentosLegales AS doc
    WHERE doc.Codigo IN (N'terminos_uso', N'tratamiento_datos')
      AND NOT EXISTS (
          SELECT 1 FROM dbo.PoliticasDatos AS pol
          WHERE pol.Clave = CASE doc.Codigo WHEN N'terminos_uso' THEN N'terminos' ELSE N'tratamiento' END
            AND pol.Version = doc.Version);
END
GO

-- La redaccion del boletin que estuvo en uso, con la version que el codigo declaraba y nunca
-- persistio (`BoletinEndpoints.VersionDeLaPolitica`).
IF NOT EXISTS (SELECT 1 FROM dbo.PoliticasDatos WHERE Clave = N'boletin' AND Version = N'2026-08-28')
BEGIN
    INSERT INTO dbo.PoliticasDatos (Clave, Version, Titulo, Texto, Vigente, FechaPublicacion)
    VALUES (
        N'boletin',
        N'2026-08-28',
        N'Autorización para recibir el boletín del PNMC',
        N'Autorizo al Ministerio de las Culturas, las Artes y los Saberes a tratar mi correo electrónico con la única finalidad de enviarme información del Plan Nacional de Música para la Convivencia. Puedo revocar esta autorización en cualquier momento.',
        0,
        '2026-08-28T00:00:00');
END
GO

-- ---------------------------------------------------------------------------------------------
-- 4. Las tres politicas vigentes
-- ---------------------------------------------------------------------------------------------
--
-- QUE DICE CADA TEXTO Y POR QUE. La Ley 1581 art. 12 obliga a informar al titular, ANTES de que
-- autorice, de cuatro cosas: que tratamiento se le va a dar a sus datos y con que finalidad, si el
-- suministro es facultativo, cuales son sus derechos, y quien es el responsable. Los tres textos
-- las dicen las cuatro, en ese orden, y en la lengua en que se le pregunta.
--
-- LA POLITICA INSTITUCIONAL COMPLETA NO SE COPIA AQUI. Es un documento del Ministerio, vive en su
-- sitio y se enlaza. Lo que se guarda es el consentimiento informado que aparece en pantalla, que
-- es lo que la persona lee y lo unico que este sistema puede demostrar que le mostro.
IF NOT EXISTS (SELECT 1 FROM dbo.PoliticasDatos WHERE Clave = N'tratamiento' AND Version = N'2026-09-12')
BEGIN
    INSERT INTO dbo.PoliticasDatos (Clave, Version, Titulo, Texto, UrlOficial, ReferenciaOficial, Vigente, FechaPublicacion)
    VALUES (
        N'tratamiento',
        N'2026-09-12',
        N'Autorización de tratamiento de datos personales',
        N'Autorizo al Ministerio de las Culturas, las Artes y los Saberes —responsable del tratamiento— a recolectar, almacenar, usar y actualizar los datos personales que entrego en este formulario: mi nombre, mi tipo y número de documento, mi teléfono y mi correo electrónico.

La finalidad es administrar mi cuenta en el Sistema de Información de la Música, identificarme como persona responsable de la organización que registro, y comunicarme lo relativo a los procesos que esa organización inscriba en el Plan Nacional de Música para la Convivencia. Mis datos no se usarán para ninguna otra finalidad ni se entregarán a terceros sin una nueva autorización.

Entregar estos datos es voluntario. No estoy obligado a responder preguntas sobre datos sensibles ni sobre menores de edad, y este formulario no las hace.

Conozco que, conforme al artículo 8 de la Ley 1581 de 2012, puedo conocer, actualizar y rectificar mis datos, solicitar prueba de esta autorización, ser informado sobre el uso que se les ha dado, presentar quejas ante la Superintendencia de Industria y Comercio, y revocar esta autorización. Puedo ejercer estos derechos desde la sección «Mis autorizaciones» de mi cuenta o por los canales que indica la política institucional de tratamiento de datos personales del Ministerio.',
        N'https://mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/PL-GSI-002_PoliticaTratamientodeDatosPersonales_V0_LF_2024_03-12-2024.pdf',
        N'PL-GSI-002 v0',
        1,
        SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PoliticasDatos WHERE Clave = N'terminos' AND Version = N'2026-09-12')
BEGIN
    INSERT INTO dbo.PoliticasDatos (Clave, Version, Titulo, Texto, UrlOficial, ReferenciaOficial, Vigente, FechaPublicacion)
    VALUES (
        N'terminos',
        N'2026-09-12',
        N'Términos de uso del Sistema de Información de la Música',
        N'Acepto usar el Sistema de Información de la Música del Plan Nacional de Música para la Convivencia para registrar y administrar procesos musicales reales, y respondo por la veracidad de la información que inscriba en él.

Entiendo que la información que registre sobre una organización o un proceso musical es revisada por el equipo del Plan antes de publicarse, que puede devolverse para ajustes, y que su publicación no implica aval, financiación ni vínculo contractual con el Ministerio de las Culturas, las Artes y los Saberes.

Entiendo que la información publicada queda visible para cualquier persona que consulte el portal, y que soy responsable de no incluir en ella datos personales de terceros sin su autorización.

Este sistema se rige por la política general de seguridad y privacidad de la información del Ministerio, que puedo consultar en el enlace de esta página.',
        N'https://www.mincultura.gov.co/transparencia/Documents/2-normativa/politicas-y-lineamientos/PL-GSI-001_PoliticaGeneraldeSeguridadyPrivacidaddelaInformacion.pdf',
        N'PL-GSI-001 v0',
        1,
        SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.PoliticasDatos WHERE Clave = N'boletin' AND Version = N'2026-09-12')
BEGIN
    INSERT INTO dbo.PoliticasDatos (Clave, Version, Titulo, Texto, Vigente, FechaPublicacion)
    VALUES (
        N'boletin',
        N'2026-09-12',
        N'Autorización para recibir el boletín del PNMC',
        N'Autorizo al Ministerio de las Culturas, las Artes y los Saberes —responsable del tratamiento— a usar mi correo electrónico con la única finalidad de enviarme información del Plan Nacional de Música para la Convivencia: convocatorias, publicaciones y agenda.

Mi correo no se usará para ninguna otra finalidad, no se entregará a terceros y no se cruzará con otra información mía.

Suscribirme es voluntario. Conforme al artículo 8 de la Ley 1581 de 2012 puedo conocer, actualizar y rectificar mi dato, solicitar prueba de esta autorización y revocarla en cualquier momento, desde el enlace que acompaña a cada envío o escribiendo por los canales del Ministerio.',
        1,
        SYSUTCDATETIME());
END
GO

-- ---------------------------------------------------------------------------------------------
-- 5. Se trasladan las autorizaciones que ya existian
-- ---------------------------------------------------------------------------------------------
--
-- PRIMERO SE COPIA Y DESPUES SE RETIRA, y en este orden. Si el retiro fuera antes, un fallo a
-- mitad dejaria la base sin las 29 autorizaciones y sin forma de reponerlas: son declaraciones de
-- voluntad de personas reales, no datos de trabajo.
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AceptacionesDocumentosLegales')
BEGIN
    INSERT INTO dbo.AutorizacionesDatos
        (Finalidad, IdPolitica, Version, TextoAceptado, TextoReconstruido, IdUsuario, CorreoTitular, Origen, FechaOtorgada)
    SELECT
        pol.Clave,
        pol.IdPolitica,
        pol.Version,
        pol.Texto,
        /* Reconstruido: la tabla de origen no guardaba texto. Ver la nota de la columna. */
        1,
        acep.IdUsuario,
        usr.CorreoElectronico,
        N'registro',
        acep.FechaAceptacion
    FROM dbo.AceptacionesDocumentosLegales AS acep
    INNER JOIN dbo.DocumentosLegales AS doc ON doc.IdDocumentoLegal = acep.IdDocumentoLegal
    INNER JOIN dbo.PoliticasDatos AS pol
        ON pol.Version = doc.Version
       AND pol.Clave = CASE doc.Codigo WHEN N'terminos_uso' THEN N'terminos' ELSE N'tratamiento' END
    LEFT JOIN dbo.Usuarios AS usr ON usr.IdUsuario = acep.IdUsuario
    WHERE doc.Codigo IN (N'terminos_uso', N'tratamiento_datos')
      AND NOT EXISTS (
          SELECT 1 FROM dbo.AutorizacionesDatos AS ya
          WHERE ya.IdUsuario = acep.IdUsuario AND ya.IdPolitica = pol.IdPolitica AND ya.Origen = N'registro');
END
GO

-- El boletin SI guardaba el texto: sus filas se trasladan con la evidencia real y sin marca de
-- reconstruccion. Y la baja de la suscripcion se traslada como lo que siempre fue —revocacion de
-- la autorizacion—, que hasta ahora vivia solo como un estado de la lista de envio.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.BoletinSuscripciones') AND name = N'AutorizacionTexto')
BEGIN
    DECLARE @politicaBoletin int =
        (SELECT TOP 1 IdPolitica FROM dbo.PoliticasDatos WHERE Clave = N'boletin' AND Version = N'2026-08-28');

    INSERT INTO dbo.AutorizacionesDatos
        (Finalidad, IdPolitica, Version, TextoAceptado, TextoReconstruido, CorreoTitular, Origen, ReferenciaId, FechaOtorgada, FechaRevocacion, MotivoRevocacion)
    SELECT
        N'boletin',
        @politicaBoletin,
        N'2026-08-28',
        sus.AutorizacionTexto,
        0,
        sus.CorreoElectronico,
        CASE WHEN sus.Origen IN (N'portada', N'noticias') THEN sus.Origen ELSE N'portada' END,
        CAST(sus.IdSuscripcion AS nvarchar(120)),
        sus.FechaAlta,
        sus.FechaBaja,
        CASE WHEN sus.FechaBaja IS NULL THEN NULL ELSE N'Baja de la suscripción al boletín.' END
    FROM dbo.BoletinSuscripciones AS sus
    WHERE sus.AutorizacionOtorgada = 1
      AND LEN(ISNULL(sus.AutorizacionTexto, N'')) > 0
      AND NOT EXISTS (
          SELECT 1 FROM dbo.AutorizacionesDatos AS ya
          WHERE ya.Finalidad = N'boletin' AND ya.ReferenciaId = CAST(sus.IdSuscripcion AS nvarchar(120)));
END
GO
