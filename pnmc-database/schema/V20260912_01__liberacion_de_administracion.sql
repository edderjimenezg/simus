/* PNMC · Un proceso bloqueado se puede devolver a custodia institucional */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE PROBLEMA RESUELVE.

  Cuando nadie de una organizacion puede entrar —se fue quien tenia la cuenta, se disolvio la
  entidad—, su Festival se queda congelado: nadie lo edita, nadie registra su edicion, y ninguna
  otra organizacion puede tomarlo. El circuito de reclamacion existe y funciona, pero solo ofrece
  los Festivales que ya estan en custodia institucional (`EsReclamable`), asi que un Festival con
  dueño inalcanzable no aparece en ninguna lista y no hay forma de desbloquearlo.

  QUE SE HABILITA. Que un funcionario DEVUELVA el proceso a custodia institucional. A partir de
  ahi el circuito que ya existe hace el resto: aparece entre los reclamables y otra organizacion
  puede pedirlo, con sus aclaraciones, su conciliacion de datos y su transferencia.

  Es la alternativa a tocar credenciales: no se recupera el acceso de nadie, se desbloquea el
  proceso.

  POR QUE `ReclamacionId` PASA A SER ANULABLE.

  `dbo.TransferenciasAdministracion` es el historial de quien ha administrado cada registro, y
  hasta hoy toda fila nacia de una reclamacion. Una liberacion TAMBIEN es un cambio de
  administracion —de la organizacion a la institucion— pero no tiene reclamacion detras: no la
  pidio nadie, la decidio el Programa.

  Dejarla fuera del historial daria un registro con huecos: se veria a quien lo reclamo despues y
  no como llego a estar libre. Y meterla inventando una reclamacion vacia seria peor, porque
  ensuciaria la bandeja con solicitudes que nadie hizo.

  EL INDICE UNICO SOBRE `ReclamacionId` SE REHACE COMO FILTRADO. Sin el filtro, dos liberaciones
  —las dos con NULL— chocarian entre si, porque SQL Server trata los NULL como iguales en un
  indice unico. Lo que la regla quiere decir es «una reclamacion no se ejecuta dos veces», y eso
  solo aplica a las filas que tienen reclamacion.
*/

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.TransferenciasAdministracion')
      AND name = N'ReclamacionId'
      AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Transferencias_Reclamacion')
        ALTER TABLE dbo.TransferenciasAdministracion DROP CONSTRAINT FK_Transferencias_Reclamacion;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Transferencias_Reclamacion' AND object_id = OBJECT_ID(N'dbo.TransferenciasAdministracion'))
        DROP INDEX UX_Transferencias_Reclamacion ON dbo.TransferenciasAdministracion;

    ALTER TABLE dbo.TransferenciasAdministracion ALTER COLUMN ReclamacionId bigint NULL;

    ALTER TABLE dbo.TransferenciasAdministracion
        ADD CONSTRAINT FK_Transferencias_Reclamacion
        FOREIGN KEY (ReclamacionId) REFERENCES dbo.ReclamacionesAdministracion (IdReclamacion);

    /* Filtrado: «una reclamacion no se ejecuta dos veces» solo aplica a las que tienen una. */
    CREATE UNIQUE INDEX UX_Transferencias_Reclamacion
        ON dbo.TransferenciasAdministracion (ReclamacionId)
        WHERE ReclamacionId IS NOT NULL;

    /* Para responder «que le ha pasado a la administracion de este registro», liberaciones
       incluidas, sin barrer la tabla entera. */
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Transferencias_Registro' AND object_id = OBJECT_ID(N'dbo.TransferenciasAdministracion'))
        CREATE INDEX IX_Transferencias_Registro
            ON dbo.TransferenciasAdministracion (Dominio, RegistroCanonicoId, FechaEjecucion DESC);
END
GO
