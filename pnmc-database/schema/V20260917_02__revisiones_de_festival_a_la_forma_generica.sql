/*
    PNMC · Las revisiones de Festival pasan a las tablas genéricas

    QUE ES ESTO
    -----------
    nacieron `RevisionesDeRegistro` y sus observaciones, que identifican
    el registro por MODULO + IDENTIFICADOR y sirven para cualquier proceso del Ecosistema. Aquel
    guion dejó escrito que `RevisionesFestival` seguiría existiendo aparte y que migrarla era «un
    corte propio, con su marcha atrás y su recuento». Este es ese corte.

    POR QUE AHORA Y NO ANTES
    ------------------------
    Porque la deuda no son las filas, es la DUPLICACION: dos juegos de tablas para la misma idea,
    dos entidades, dos contratos y dos piezas de frontend que hay que mantener iguales a mano. Cada
    día que conviven es un día más en que una puede cambiar y la otra no.

    QUE HACE, EN ORDEN
    ------------------
      1. Copia las revisiones de Festival a `RevisionesDeRegistro` con `ModuloId = 'festivales'`.
      2. Copia sus observaciones, traduciendo el ámbito y el subregistro.
      3. Repunta `EnviosRevisionFestival` a los identificadores nuevos.

    QUE **NO** HACE, Y ES DELIBERADO
    --------------------------------
    NO BORRA `RevisionesFestival` NI SUS OBSERVACIONES. Eliminar tablas con datos es irreversible y
    está entre las acciones que este proyecto no ejecuta sin una validación explícita. Quedan ahí,
    sin que nadie las lea, hasta que se decida retirarlas en un guion propio. Mientras tanto son la
    marcha atrás de esta versión: los datos originales están intactos.

    LA TRADUCCION DEL AMBITO
    ------------------------
      · `festival`          -> `principal`    (la ficha del registro)
      · `perfil_versionado` -> `subregistro`  (una de sus realizaciones), con `IdVersionFestival`
                                              copiado a `SubregistroId` como texto.

    Son los mismos dos conceptos con los nombres que no mencionan un proceso concreto, que es justo
    lo que permite que Mercados, Escuelas o Escenarios usen la misma tabla sin traducir nada.

    IDEMPOTENTE POR CONSTRUCCION
    ----------------------------
    Todo el cuerpo está guardado por «¿ya hay revisiones de `festivales` aquí?». DbUp además lo
    ejecuta una sola vez, pero un guion de migración de datos que solo es seguro porque el diario lo
    dice es un guion que no se puede volver a correr en una base restaurada a medias.

    EL RECUENTO, EN LA SALIDA
    -------------------------
    Imprime cuántas filas había y cuántas quedaron. Una migración que no dice cuántas movió obliga a
    creer que las movió todas.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

-- LA CLAVE AJENA DEL ENVIO SE SUELTA PRIMERO. Mientras apunte a `RevisionesFestival`, repuntar la
-- columna a un identificador de la tabla nueva es una violación de integridad: el UPDATE muere con
-- un 547 y el guion entero se deshace. El orden importa, y por eso va antes que la copia.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_RevisionOrigen')
BEGIN
    ALTER TABLE dbo.EnviosRevisionFestival DROP CONSTRAINT FK_EnviosRevisionFestival_RevisionOrigen;
END;
GO

IF EXISTS (SELECT 1 FROM dbo.RevisionesDeRegistro WHERE ModuloId = N'festivales')
BEGIN
    PRINT '[V20260917_02] Ya hay revisiones de festivales en la forma generica: no se copia nada.';
END
ELSE
BEGIN
    DECLARE @revisionesAntes int = (SELECT COUNT(*) FROM dbo.RevisionesFestival);
    DECLARE @notasAntes      int = (SELECT COUNT(*) FROM dbo.RevisionesFestivalObservaciones);

    -- LA CORRESPONDENCIA VIEJA -> NUEVA. Las observaciones y los envíos apuntan a la revisión por
    -- su identificador, y el de la tabla nueva lo asigna IDENTITY: sin guardar el par, no habría
    -- forma de repuntarlos. Se captura con OUTPUT en el mismo MERGE que inserta.
    DECLARE @equivalencias TABLE (IdViejo bigint NOT NULL PRIMARY KEY, IdNuevo bigint NOT NULL);

    MERGE dbo.RevisionesDeRegistro AS destino
    USING (SELECT * FROM dbo.RevisionesFestival) AS origen
       ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (ModuloId, RegistroId, Estado,
                IdUsuarioRevisor, RevisorNombre,
                IdUsuarioDestinatario, DestinatarioNombre,
                IdOrganizacion, OrganizacionNombre,
                ObservacionGeneral,
                FechaCreacion, FechaActualizacion, FechaEnvio, FechaCierre)
        VALUES (N'festivales', CONVERT(nvarchar(120), origen.IdFestival), origen.Estado,
                origen.IdUsuarioRevisor, origen.RevisorNombre,
                origen.IdUsuarioDestinatario, origen.DestinatarioNombre,
                origen.IdOrganizacion, origen.OrganizacionNombre,
                origen.ObservacionGeneral,
                origen.FechaCreacion, origen.FechaActualizacion, origen.FechaEnvio, origen.FechaCierre)
    OUTPUT origen.IdRevisionFestival, inserted.IdRevision INTO @equivalencias (IdViejo, IdNuevo);

    INSERT INTO dbo.RevisionesDeRegistroObservaciones
        (IdRevision, Ambito, SubregistroId, SeccionId, CampoId, CampoEtiqueta,
         ValorObservado, Nota, Estado, FechaCreacion, FechaActualizacion, FechaAtencion, IdUsuarioAtiende)
    SELECT e.IdNuevo,
           CASE WHEN o.Ambito = N'perfil_versionado' THEN N'subregistro' ELSE N'principal' END,
           CASE WHEN o.IdVersionFestival IS NULL THEN NULL
                ELSE CONVERT(nvarchar(120), o.IdVersionFestival) END,
           o.SeccionId, o.CampoId, o.CampoEtiqueta,
           o.ValorObservado, o.Nota, o.Estado,
           o.FechaCreacion, o.FechaActualizacion, o.FechaAtencion, o.IdUsuarioAtiende
      FROM dbo.RevisionesFestivalObservaciones o
      JOIN @equivalencias e ON e.IdViejo = o.IdRevisionFestival;

    -- LOS ENVIOS APUNTAN A LA REVISION QUE LOS ORIGINO, y esa fila ahora vive en otra tabla.
    UPDATE envio
       SET envio.IdRevisionFestivalOrigen = e.IdNuevo
      FROM dbo.EnviosRevisionFestival envio
      JOIN @equivalencias e ON e.IdViejo = envio.IdRevisionFestivalOrigen;

    DECLARE @revisionesCopiadas int = (SELECT COUNT(*) FROM @equivalencias);
    DECLARE @notasCopiadas      int = (SELECT COUNT(*) FROM dbo.RevisionesDeRegistroObservaciones o
                                        JOIN dbo.RevisionesDeRegistro r ON r.IdRevision = o.IdRevision
                                       WHERE r.ModuloId = N'festivales');

    PRINT CONCAT('[V20260917_02] Revisiones: ', @revisionesAntes, ' en origen, ', @revisionesCopiadas, ' copiadas.');
    PRINT CONCAT('[V20260917_02] Observaciones: ', @notasAntes, ' en origen, ', @notasCopiadas, ' copiadas.');

    -- SI NO CUADRA, SE DESHACE. Una migración de datos a medias es peor que una que no corrió:
    -- deja la mitad del historial en un sitio y la mitad en otro, y nadie sabe cuál mirar.
    IF @revisionesCopiadas <> @revisionesAntes OR @notasCopiadas <> @notasAntes
    BEGIN
        THROW 50001, 'La copia de revisiones de Festival no cuadra con el origen. No se aplica.', 1;
    END
END;
GO

-- Y EL NOMBRE DE LA COLUMNA DEJA DE MENTIR: ya no apunta a `RevisionesFestival`.
IF COL_LENGTH('dbo.EnviosRevisionFestival', 'IdRevisionFestivalOrigen') IS NOT NULL
   AND COL_LENGTH('dbo.EnviosRevisionFestival', 'IdRevisionOrigen') IS NULL
BEGIN
    EXEC sp_rename N'dbo.EnviosRevisionFestival.IdRevisionFestivalOrigen', N'IdRevisionOrigen', N'COLUMN';
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_RevisionDeRegistro')
BEGIN
    ALTER TABLE dbo.EnviosRevisionFestival
        ADD CONSTRAINT FK_EnviosRevisionFestival_RevisionDeRegistro
        FOREIGN KEY (IdRevisionOrigen) REFERENCES dbo.RevisionesDeRegistro (IdRevision);
END;
GO
