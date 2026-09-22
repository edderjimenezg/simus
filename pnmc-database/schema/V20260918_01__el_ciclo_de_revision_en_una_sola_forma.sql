/*
    PNMC · El ciclo de revisión queda en una sola forma

    QUE ES ESTO
    -----------
    El cierre de un movimiento que empezó el 16 de septiembre con Mercados Musicales. Aquel corte
    sacó el ciclo de revisión de la tabla que nombraba un proceso y lo puso en un par genérico
    —`RevisionesDeRegistro` y sus observaciones—, que identifica el registro por `ModuloId` +
    `RegistroId`, el mismo par con el que lo identifican la bitácora de auditoría y la procedencia.
    Los días 17 y 18 se fueron trayendo los demás circuitos. Lo que queda por hacer es retirar lo
    que ya no lee nadie y traer el único que se había quedado atrás.

    TRES COSAS, Y LAS TRES SON LA MISMA
    ------------------------------------
    1. SE RETIRA LA FORMA ANTIGUA. `RevisionesFestival`, sus observaciones,
       `PropuestasCambioFestival` y sus dos tablas de relaciones. Su contenido se copió el 17 de
       septiembre en `V20260917_02` y `V20260917_03`; desde entonces ningún punto de entrada las
       consulta. La dirección de producto autorizó explícitamente su retiro.

    2. SE ABSORBE EL CICLO DE LAS EDICIONES. `RevisionesEdicionesFestival` y sus observaciones eran
       el tercer par, el único que no había pasado. Están VACIAS, así que absorberlas no mueve un
       solo dato: es una tabla de menos y una manera menos de guardar lo mismo. La tabla genérica de
       observaciones es además un superconjunto estricto de la que se retira —añade `Ambito`,
       `SubregistroId`, `FechaAtencion` e `IdUsuarioAtiende`—, de modo que no se pierde ningún campo.

    3. LA INSTANTANEA DE ENVIO DEJA DE NOMBRAR UN PROCESO. `EnviosRevisionFestival` guarda, en
       JSON, lo que la organización entregó en cada envío, y es lo que permite comparar después «lo
       que mandaste» con «lo que hay ahora». No la sustituye la bitácora, que registra el hecho pero
       no el contenido. Pasa a llamarse `EnviosDeRevision` con el mismo par `ModuloId` +
       `RegistroId`, para que Mercados y los procesos que vengan no tengan que crearse la suya.

    LO QUE SE PIERDE, DICHO EN VOZ ALTA
    ------------------------------------
    La clave ajena de los envíos contra `dbo.Festivales`. Es el mismo precio que ya se pagó en
    `RevisionesDeRegistro` y en `PropuestasDeCambio`, y por la misma razón: una clave ajena no puede
    apuntar a cuatro tablas distintas. A cambio, el registro se nombra igual en los cuatro sitios y
    ningún proceso nuevo necesita tablas propias. Las otras tres claves ajenas —usuario,
    organización y revisión de origen— se conservan intactas.

    SEGURIDAD
    ---------
    Es idempotente: cada paso comprueba antes de actuar. Y NO BORRA NADA A CIEGAS: antes de cada
    DROP verifica que no queda contenido sin copiar en el destino, y si no cuadra, se detiene.
*/

-- ────────────────────────────────────────────────────────────────────────────────
-- 1. La forma antigua se retira, pero solo después de comprobar que está copiada.
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.RevisionesFestival', 'U') IS NOT NULL
BEGIN
    DECLARE @revisionesSinCopiar int =
        (SELECT COUNT(*) FROM dbo.RevisionesFestival v
          WHERE NOT EXISTS (SELECT 1 FROM dbo.RevisionesDeRegistro n
                             WHERE n.ModuloId = N'festivales'
                               AND n.RegistroId = CONVERT(nvarchar(64), v.IdFestival)
                               AND n.FechaCreacion = v.FechaCreacion));

    IF @revisionesSinCopiar > 0
    BEGIN
        DECLARE @avisoRevisiones nvarchar(200) =
            CONCAT('Quedan ', @revisionesSinCopiar, ' revisiones de Festival sin copiar. No se retira nada.');
        THROW 50010, @avisoRevisiones, 1;
    END

    PRINT '[V20260918_01] Revisiones de Festival: todas copiadas. Se retira la forma antigua.';
END;
GO

IF OBJECT_ID('dbo.PropuestasCambioFestival', 'U') IS NOT NULL
BEGIN
    DECLARE @propuestasSinCopiar int =
        (SELECT COUNT(*) FROM dbo.PropuestasCambioFestival v
          WHERE NOT EXISTS (SELECT 1 FROM dbo.PropuestasDeCambio n
                             WHERE n.ModuloId = N'festivales'
                               AND n.RegistroId = CONVERT(nvarchar(64), v.FestivalOrigenId)
                               AND n.FechaCreacion = v.FechaPropuesta));

    IF @propuestasSinCopiar > 0
    BEGIN
        DECLARE @avisoPropuestas nvarchar(200) =
            CONCAT('Quedan ', @propuestasSinCopiar, ' propuestas de Festival sin copiar. No se retira nada.');
        THROW 50011, @avisoPropuestas, 1;
    END

    PRINT '[V20260918_01] Propuestas de Festival: todas copiadas. Se retira la forma antigua.';
END;
GO

-- Las hijas primero, que es lo que deja caer las claves ajenas sin pelearse con ellas.
DROP TABLE IF EXISTS dbo.PropuestasCambioFestivalPracticasMusicales;
DROP TABLE IF EXISTS dbo.PropuestasCambioFestivalTerritoriosSonoros;
DROP TABLE IF EXISTS dbo.PropuestasCambioFestival;
DROP TABLE IF EXISTS dbo.RevisionesFestivalObservaciones;
DROP TABLE IF EXISTS dbo.RevisionesFestival;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 2. El ciclo de revisión de las ediciones se absorbe en el par genérico.
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.RevisionesEdicionesFestival', 'U') IS NOT NULL
BEGIN
    -- ESTABAN VACIAS CUANDO SE ESCRIBIO ESTO, pero la migración no lo da por hecho: si alguien
    -- llegó a usarlas entre medias, sus expedientes se copian antes de que la tabla desaparezca.
    INSERT INTO dbo.RevisionesDeRegistro
        (ModuloId, RegistroId, Estado, IdUsuarioRevisor, RevisorNombre,
         ObservacionGeneral, FechaCreacion, FechaActualizacion, FechaEnvio, FechaCierre)
    SELECT N'ediciones_festival',
           CONVERT(nvarchar(64), v.IdEdicionFestival),
           v.Estado, v.IdUsuarioRevisor, v.RevisorNombre,
           v.ObservacionGeneral, v.FechaCreacion, v.FechaActualizacion, v.FechaEnvio, v.FechaCierre
      FROM dbo.RevisionesEdicionesFestival v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.RevisionesDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.IdEdicionFestival)
                          AND n.FechaCreacion = v.FechaCreacion);

    INSERT INTO dbo.RevisionesDeRegistroObservaciones
        (IdRevision, Ambito, SeccionId, CampoId, CampoEtiqueta, ValorObservado, Nota, Estado,
         FechaCreacion, FechaActualizacion, FechaAtencion, IdUsuarioAtiende)
    SELECT n.IdRevision, N'principal',
           o.SeccionId, o.CampoId, o.CampoEtiqueta, o.ValorObservado, o.Nota, o.Estado,
           o.FechaCreacion, o.FechaActualizacion, o.FechaAtencion, o.IdUsuarioAtiende
      FROM dbo.RevisionesEdicionesFestivalObservaciones o
      JOIN dbo.RevisionesEdicionesFestival v ON v.IdRevisionEdicionFestival = o.IdRevisionEdicionFestival
      JOIN dbo.RevisionesDeRegistro n
        ON n.ModuloId = N'ediciones_festival'
       AND n.RegistroId = CONVERT(nvarchar(64), v.IdEdicionFestival)
       AND n.FechaCreacion = v.FechaCreacion
     WHERE NOT EXISTS (SELECT 1 FROM dbo.RevisionesDeRegistroObservaciones d
                        WHERE d.IdRevision = n.IdRevision
                          AND d.CampoId = o.CampoId
                          AND d.FechaCreacion = o.FechaCreacion);

    DECLARE @edicionesOrigen int = (SELECT COUNT(*) FROM dbo.RevisionesEdicionesFestival);
    DECLARE @edicionesDestino int = (SELECT COUNT(*) FROM dbo.RevisionesDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_01] Revisiones de edición: ', @edicionesOrigen, ' en origen, ', @edicionesDestino, ' en destino.');

    IF @edicionesDestino < @edicionesOrigen
    BEGIN
        THROW 50012, 'La copia de revisiones de edición no cuadra con el origen. No se aplica.', 1;
    END
END;
GO

DROP TABLE IF EXISTS dbo.RevisionesEdicionesFestivalObservaciones;
DROP TABLE IF EXISTS dbo.RevisionesEdicionesFestival;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- 3. La instantánea de envío deja de nombrar un proceso.
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.EnviosRevisionFestival', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.EnviosDeRevision', 'U') IS NULL
BEGIN
    IF COL_LENGTH('dbo.EnviosRevisionFestival', 'ModuloId') IS NULL
        ALTER TABLE dbo.EnviosRevisionFestival ADD ModuloId nvarchar(40) NULL;
    IF COL_LENGTH('dbo.EnviosRevisionFestival', 'RegistroId') IS NULL
        ALTER TABLE dbo.EnviosRevisionFestival ADD RegistroId nvarchar(64) NULL;
END;
GO

IF OBJECT_ID('dbo.EnviosRevisionFestival', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.EnviosRevisionFestival', 'IdFestival') IS NOT NULL
BEGIN
    -- LAS FILAS QUE HABIA SON TODAS DE FESTIVAL, porque hasta hoy la tabla no admitía otra cosa.
    UPDATE dbo.EnviosRevisionFestival
       SET ModuloId = N'festivales',
           RegistroId = CONVERT(nvarchar(64), IdFestival)
     WHERE ModuloId IS NULL OR RegistroId IS NULL;

    ALTER TABLE dbo.EnviosRevisionFestival ALTER COLUMN ModuloId nvarchar(40) NOT NULL;
    ALTER TABLE dbo.EnviosRevisionFestival ALTER COLUMN RegistroId nvarchar(64) NOT NULL;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_Festival')
        ALTER TABLE dbo.EnviosRevisionFestival DROP CONSTRAINT FK_EnviosRevisionFestival_Festival;
    -- SE CREO COMO RESTRICCION UNIQUE, NO COMO INDICE SUELTO, y SQL Server no deja retirar por
    -- `DROP INDEX` el índice que respalda una restricción. Se contemplan las dos formas porque una
    -- base creada desde cero por el bootstrap del API puede tenerlo de la otra.
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_EnviosRevisionFestival_Numero'
                 AND parent_object_id = OBJECT_ID('dbo.EnviosRevisionFestival'))
        ALTER TABLE dbo.EnviosRevisionFestival DROP CONSTRAINT UQ_EnviosRevisionFestival_Numero;
    ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_EnviosRevisionFestival_Numero'
                 AND object_id = OBJECT_ID('dbo.EnviosRevisionFestival'))
        DROP INDEX UQ_EnviosRevisionFestival_Numero ON dbo.EnviosRevisionFestival;

    -- EL INDICE DE FECHA TAMBIEN COLGABA DE `IdFestival`. Se retira aquí y más abajo se recrea
    -- sobre el par nuevo, que es lo que ahora identifica al registro.
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EnviosRevisionFestival_Fecha'
                 AND object_id = OBJECT_ID('dbo.EnviosRevisionFestival'))
        DROP INDEX IX_EnviosRevisionFestival_Fecha ON dbo.EnviosRevisionFestival;

    ALTER TABLE dbo.EnviosRevisionFestival DROP COLUMN IdFestival;
END;
GO

IF OBJECT_ID('dbo.EnviosRevisionFestival', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.EnviosDeRevision', 'U') IS NULL
BEGIN
    IF COL_LENGTH('dbo.EnviosRevisionFestival', 'IdEnvioRevisionFestival') IS NOT NULL
        EXEC sp_rename N'dbo.EnviosRevisionFestival.IdEnvioRevisionFestival', N'IdEnvioDeRevision', N'COLUMN';

    EXEC sp_rename N'dbo.EnviosRevisionFestival', N'EnviosDeRevision';
END;
GO

-- Los nombres de las restricciones se quedarían diciendo «Festival» si no se renombran también.
IF OBJECT_ID('dbo.EnviosDeRevision', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'PK_EnviosRevisionFestival' AND object_id = OBJECT_ID('dbo.EnviosDeRevision'))
        EXEC sp_rename N'dbo.EnviosDeRevision.PK_EnviosRevisionFestival', N'PK_EnviosDeRevision', N'INDEX';
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_Usuario')
        EXEC sp_rename N'FK_EnviosRevisionFestival_Usuario', N'FK_EnviosDeRevision_Usuario', N'OBJECT';
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_Organizacion')
        EXEC sp_rename N'FK_EnviosRevisionFestival_Organizacion', N'FK_EnviosDeRevision_Organizacion', N'OBJECT';
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_EnviosRevisionFestival_RevisionDeRegistro')
        EXEC sp_rename N'FK_EnviosRevisionFestival_RevisionDeRegistro', N'FK_EnviosDeRevision_RevisionDeRegistro', N'OBJECT';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_EnviosDeRevision_Numero' AND object_id = OBJECT_ID('dbo.EnviosDeRevision'))
        CREATE UNIQUE INDEX UQ_EnviosDeRevision_Numero ON dbo.EnviosDeRevision (ModuloId, RegistroId, NumeroEnvio);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EnviosDeRevision_Fecha' AND object_id = OBJECT_ID('dbo.EnviosDeRevision'))
        CREATE INDEX IX_EnviosDeRevision_Fecha ON dbo.EnviosDeRevision (ModuloId, RegistroId, FechaEnvio DESC);
END;
GO

PRINT '[V20260918_01] El ciclo de revisión queda en una sola forma.';
GO
