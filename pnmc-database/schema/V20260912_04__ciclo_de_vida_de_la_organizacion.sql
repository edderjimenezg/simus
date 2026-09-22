/*
    SIMUS · El ciclo de vida de una organización, en un solo eje

    QUE PROBLEMA CIERRA. El criterio es este: «no entiendo los estados de
    organizaciones». Y tenía razón por dos motivos a la vez.

    1. EL VOCABULARIO NO ERA EL SUYO. Los estados se llamaban `registrada`, `verificada`,
       `ajustes_solicitados` y `archivado`: palabras del circuito institucional, no del ciclo de vida
       de una organización. Nadie que abra la consola sabe qué le pasa a una organización
       «verificada» ni qué gana al pasarla a ese estado.

    2. HABIA DOS EJES Y HABIA QUE COMBINARLOS DE CABEZA. Por un lado `EstadoRegistro`; por otro
       `Activo`, que era el que de verdad decidía si la organización podía entrar. La pantalla
       llegaba a tener un recuadro explicando la diferencia entre los dos campos, que es la señal de
       que el modelo está mal: si «inactiva» describe una situación de la organización, es un estado.

    LOS CUATRO ESTADOS, Y QUE HACE CADA UNO. Un solo eje, y cada valor dice a la vez cómo está la
    organización y qué puede hacer:

      pendiente_de_confirmacion  entra y prepara, NO puede enviar a revisión ni publicar
      activa                     opera con normalidad
      inactiva                   no entra; reversible
      eliminada                  no entra; fuera del ecosistema

    `Activo` NO DESAPARECE, PERO DEJA DE PODER CONTRADECIR AL ESTADO. Nueve consultas del API la
    usan y reescribirlas no arreglaría nada; lo que arregla el problema es que las dos columnas no
    puedan discrepar, y eso lo impone `CK_Entidades_VigenciaCoherente` en la propia base. Ningún
    camino de código puede desincronizarlas, ni siquiera uno escrito mañana.

    LA CONFIRMACION DE CORREO SE MONTA ENTERA AUNQUE FALTE EL PROVEEDOR. decisión de producto, mismo
    día: «deja montado la funcionalidad como si ya existiese el proveedor de correo». Se añaden las
    dos columnas de la cuenta y la tabla de los enlaces de confirmación. Lo único que no existe es el
    transporte, que vive aislado en `IEnviadorDeCorreo`: el mensaje saliente se registra en
    `dbo.Notificaciones` con canal `email` y estado `pendiente` —los dos valores que esa tabla ya
    admitía—, que es la verdad: hay algo que mandar y nadie lo ha mandado. No es una entrega fingida,
    y el día que se conecte el proveedor esas filas son exactamente la cola que hay que drenar.

    LAS CUENTAS QUE YA EXISTEN SE DAN POR CONFIRMADAS, Y ES UNA DECISION. Nacieron cuando esta regla
    no existía. Dejarlas sin confirmar cambiaría las reglas a mitad de partida: cada organización que
    ya estaba trabajando encontraría su envío a revisión bloqueado sin haber hecho nada y sin poder
    arreglarlo hasta recibir un correo que nadie le anunció. Se marcan con su fecha de creación, que
    es la única que se puede afirmar. NO significa que esos correos estén comprobados: significa que
    la regla se aplica de aquí en adelante.

    Y SE RETIRA `agrupacion`. El usuario zanjó el mismo día la pregunta que quedó abierta en
    `V20260912_03`: «no va a existir agrupacion». `dbo.Entidades` es la tabla de un solo actor.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- 1. La cuenta sabe si su correo está confirmado --------------------------------- */

IF COL_LENGTH(N'dbo.Usuarios', N'CorreoConfirmado') IS NULL
    ALTER TABLE dbo.Usuarios ADD CorreoConfirmado bit NOT NULL
        CONSTRAINT DF_Usuarios_CorreoConfirmado DEFAULT (0) WITH VALUES;
GO

IF COL_LENGTH(N'dbo.Usuarios', N'FechaConfirmacionCorreo') IS NULL
    ALTER TABLE dbo.Usuarios ADD FechaConfirmacionCorreo datetime2(0) NULL;
GO

/* Las heredadas, confirmadas con su fecha de creación. Ver la cabecera. */
UPDATE dbo.Usuarios
   SET CorreoConfirmado = 1,
       FechaConfirmacionCorreo = FechaCreacion
 WHERE CorreoConfirmado = 0
   AND FechaConfirmacionCorreo IS NULL;
GO

/*
    Los enlaces de confirmación.

    SE GUARDA EL HASH Y NO EL TESTIGO. Quien pueda leer esta tabla no debe poder confirmar el correo
    de nadie: es el mismo criterio con el que se guardan las contraseñas.

    UNA FILA POR EMISION, no una columna en la cuenta. Reenviar el enlace tiene que invalidar el
    anterior y quedar registrado; con una sola columna, «lo pedí tres veces y no me llegó» no se
    puede responder.
*/
IF OBJECT_ID(N'dbo.ConfirmacionesDeCorreo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConfirmacionesDeCorreo
    (
        IdConfirmacionDeCorreo int IDENTITY(1,1) NOT NULL,
        IdUsuario int NOT NULL,
        CorreoDestino nvarchar(240) NOT NULL,
        HashTestigo char(64) NOT NULL,
        FechaEmision datetime2(0) NOT NULL CONSTRAINT DF_ConfirmacionesDeCorreo_Emision DEFAULT (SYSUTCDATETIME()),
        FechaExpiracion datetime2(0) NOT NULL,
        FechaUso datetime2(0) NULL,
        CONSTRAINT PK_ConfirmacionesDeCorreo PRIMARY KEY (IdConfirmacionDeCorreo),
        CONSTRAINT FK_ConfirmacionesDeCorreo_Usuarios FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT UQ_ConfirmacionesDeCorreo_HashTestigo UNIQUE (HashTestigo),
        CONSTRAINT CK_ConfirmacionesDeCorreo_Vigencia CHECK (FechaExpiracion > FechaEmision)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ConfirmacionesDeCorreo_Usuario' AND object_id = OBJECT_ID(N'dbo.ConfirmacionesDeCorreo'))
    CREATE INDEX IX_ConfirmacionesDeCorreo_Usuario ON dbo.ConfirmacionesDeCorreo (IdUsuario, FechaEmision DESC);
GO

/* ---------- 2. Los cuatro estados del catálogo ---------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'pendiente_de_confirmacion')
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'pendiente_de_confirmacion', N'Pendiente de confirmación',
            N'Organización cuya cuenta responsable todavía no ha confirmado su correo.');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'activa')
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'activa', N'Activa', N'Organización que opera con normalidad en la plataforma.');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'inactiva')
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'inactiva', N'Inactiva', N'Organización suspendida: conserva su ficha y no puede entrar.');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EstadosContenido WHERE CodigoEstado = N'eliminada')
    INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
    VALUES (N'eliminada', N'Eliminada', N'Organización retirada del ecosistema; sus procesos vuelven al Programa.');
GO

/* ---------- 3. El movimiento de las filas existentes ----------------------------------------- */

/*
    LAS RESTRICCIONES CAEN ANTES DE MOVER LAS FILAS, y el orden no es un detalle: la de
    `V20260912_03` solo admite los cuatro estados de aquel corte, así que el primer UPDATE que
    escriba `activa` la haría saltar y DbUp abortaría el guion entero.
*/
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_EstadoRegistro')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_EstadoRegistro;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_VigenciaCoherente')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_VigenciaCoherente;
GO

/*
    La regla, escrita:

      archivado                                        -> eliminada
      cualquier otro con Activo = 0                    -> inactiva
      registrada / verificada / ajustes_solicitados    -> activa

    `ajustes_solicitados` desaparece como estado y no se pierde nada: pedirle a una organización que
    corrija su registro es un MENSAJE, y el canal de mensajes de la consola ya existe desde el corte
    18. Un estado que solo servía para pintar un sello ámbar no describía ninguna situación distinta
    de «activa».
*/
UPDATE dbo.Entidades SET EstadoRegistro = N'eliminada' WHERE EstadoRegistro = N'archivado';
GO

UPDATE dbo.Entidades SET EstadoRegistro = N'inactiva'
 WHERE Activo = 0 AND EstadoRegistro <> N'eliminada';
GO

UPDATE dbo.Entidades SET EstadoRegistro = N'activa'
 WHERE EstadoRegistro NOT IN (N'eliminada', N'inactiva', N'pendiente_de_confirmacion');
GO

/* Coherencia antes de imponerla: una eliminada o inactiva no puede quedar marcada vigente. */
UPDATE dbo.Entidades SET Activo = 0 WHERE EstadoRegistro IN (N'inactiva', N'eliminada') AND Activo = 1;
GO

UPDATE dbo.Entidades SET Activo = 1 WHERE EstadoRegistro IN (N'activa', N'pendiente_de_confirmacion') AND Activo = 0;
GO

/* ---------- 4. Un solo eje, impuesto por la base --------------------------------------------- */

ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_EstadoRegistro
    CHECK (EstadoRegistro IN (N'pendiente_de_confirmacion', N'activa', N'inactiva', N'eliminada'));
GO

/*
    LA MITAD QUE IMPIDE QUE VUELVAN A SER DOS EJES. Mientras `Activo` y `EstadoRegistro` puedan
    discrepar, existe una organización «eliminada» que sigue entrando, y nadie la encuentra porque
    cada pantalla mira una de las dos columnas.
*/
ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_VigenciaCoherente
    CHECK (
        (EstadoRegistro IN (N'pendiente_de_confirmacion', N'activa') AND Activo = 1)
        OR (EstadoRegistro IN (N'inactiva', N'eliminada') AND Activo = 0)
    );
GO

/* ---------- 5. Un solo actor ------------------------------------------------------------------ */

IF EXISTS (SELECT 1 FROM dbo.Entidades WHERE TipoEntidad <> N'organizacion')
    THROW 51007, N'dbo.Entidades contiene filas que no son organizaciones. Concilie esos registros antes de continuar.', 1;
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Entidades_Tipo')
    ALTER TABLE dbo.Entidades DROP CONSTRAINT CK_Entidades_Tipo;
GO

ALTER TABLE dbo.Entidades ADD CONSTRAINT CK_Entidades_Tipo CHECK (TipoEntidad = N'organizacion');
GO

/* ---------- 6. Los estados que ya no usa nadie ------------------------------------------------ */

/*
    `verificada` se añadió para organizaciones y ninguna otra tabla la
    referencia: sale con su ciclo. Los siete del circuito editorial —borrador, en_revision,
    ajustes_solicitados, aprobado, publicado, rechazado, archivado— SE QUEDAN: los usan Festivales,
    Ediciones, Noticias, Agenda y Catálogo editorial, que sí son contenido.
*/
IF NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE EstadoRegistro = N'verificada')
    DELETE FROM dbo.EstadosContenido WHERE CodigoEstado = N'verificada';
GO
