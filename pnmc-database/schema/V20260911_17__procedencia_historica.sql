/* PNMC · Ningún registro se queda sin procedencia: el origen histórico */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE CIERRA ESTE GUION.

  `V20260911_15` creo la tabla de procedencia y dedujo la de lo que ya existia a partir del canal
  de acceso de quien lo creo. Donde no habia creador conocido no se invento nada, y esos registros
  quedaron sin procedencia. La direccion cerro la regla: a la larga
  TODO tiene que tener procedencia, y solo hay tres origenes validos —institucional, desde
  registros historicos, o desde una entidad—.

  POR QUE `historico` Y NO `institucional` PARA ESTOS.

  Un Festival del acervo que nadie sabe quien cargo NO lo incorporo el Programa desde su consola:
  venia del volcado historico de SIMUS. Llamarlo `institucional` seria atribuirle al Programa un
  acto que no consta que hiciera, y perderia la unica informacion util que queda: que es anterior
  al sistema. `historico` dice exactamente eso.

  LOS DATOS DE PRUEBA NO SE MEZCLAN CON EL ACERVO. El contexto `prueba` existe para lo que se creo
  verificando el sistema, que no es ni institucional ni historico. Sin el, marcar de golpe todo lo
  huerfano como historico convertiria un festival de prueba en patrimonio documental.

  LO QUE ESTE GUION HACE, Y LO QUE DELIBERADAMENTE NO HACE. Amplia la lista de contextos y anota
  como `historico` los registros del acervo que siguen sin procedencia. NO toca los que ya la
  tienen: la procedencia se escribe una vez y no se reescribe.
*/

/* 1. La lista de contextos crece. Se reescribe la CHECK entera: es la unica forma de ampliarla. */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProcedenciaDeRegistros_Contexto')
    ALTER TABLE dbo.ProcedenciaDeRegistros DROP CONSTRAINT CK_ProcedenciaDeRegistros_Contexto;
GO

ALTER TABLE dbo.ProcedenciaDeRegistros
    ADD CONSTRAINT CK_ProcedenciaDeRegistros_Contexto
    CHECK (ContextoOrigen IN (N'administrativo', N'externo', N'importacion', N'siembra', N'historico', N'prueba'));
GO

/*
  2. El acervo que sigue sin procedencia queda como historico.

  SE ELIGE POR AUSENCIA DE CREADOR, que es exactamente el criterio por el que `V20260911_15` los
  dejo fuera: si hubiera creador conocido, aquel guion ya habria deducido su contexto del canal de
  acceso de esa persona.
*/
INSERT INTO dbo.ProcedenciaDeRegistros
    (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
SELECT
    N'festival',
    CAST(f.IdFestival AS nvarchar(120)),
    N'historico',
    /* Sin entidad de procedencia: el acervo no vino de ninguna organizacion del ecosistema. */
    NULL,
    NULL,
    f.FechaCreacion
FROM dbo.Festivales AS f
WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
        WHERE p.Dominio = N'festival' AND p.RegistroId = CAST(f.IdFestival AS nvarchar(120)));
GO

INSERT INTO dbo.ProcedenciaDeRegistros
    (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
SELECT
    N'organizacion',
    CAST(e.IdEntidad AS nvarchar(120)),
    N'historico',
    NULL,
    NULL,
    e.FechaCreacion
FROM dbo.Entidades AS e
WHERE e.TipoEntidad = N'organizacion'
  AND NOT EXISTS (
        SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
        WHERE p.Dominio = N'organizacion' AND p.RegistroId = CAST(e.IdEntidad AS nvarchar(120)));
GO
