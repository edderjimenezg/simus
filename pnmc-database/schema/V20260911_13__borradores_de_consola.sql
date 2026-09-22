/* PNMC · Borradores automáticos también para la consola institucional */
/*
  LAS OPCIONES DE SESION VAN DECLARADAS y no heredadas del cliente: un indice FILTRADO exige
  QUOTED_IDENTIFIER ON en el momento de crearse, y heredarlo del cliente hace que el guion
  funcione desde una herramienta y falle desde otra. Lo vigila una prueba del proyecto.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  BORRADORES AUTOMATICOS TAMBIEN PARA LA CONSOLA INSTITUCIONAL.

  QUE PROBLEMA RESUELVE. Los formularios de Agenda, Noticias y Catalogo Editorial se llenan de una
  sentada dentro de un dialogo. Un corte de conexion, una sesion caducada o un navegador cerrado
  sin querer se llevaban por delante todo lo escrito, sin rastro. El asistente externo de Festival
  ya resolvio esto guardando un borrador cada pocos segundos en dbo.BorradoresProceso; esto le abre
  esa misma tabla al trabajo institucional.

  POR QUE NO UNA TABLA NUEVA. Lo que se guarda es identico -un dominio, quien lo escribe, un JSON y
  una version-, y la unica razon por la que la tabla no servia es que exigia una organizacion. Una
  tabla gemela daria dos sitios donde limpiar borradores viejos y dos formas de resolver el mismo
  conflicto de version.

  POR QUE LA ORGANIZACION PASA A SER OPCIONAL. Quien administra desde el Ministerio no actua en
  nombre de ninguna entidad del ecosistema: responde el Programa. Rellenar ahi la organizacion
  institucional seria inventar un dato para satisfacer una columna, y ese dato inventado acabaria
  contandose en algun informe de participacion.

  EL INDICE UNICO SIGUE SIRVIENDO. Es (Dominio, OrganizacionId, PersonaId) filtrado a los abiertos;
  SQL Server trata dos NULL como iguales en un indice unico, asi que sigue habiendo como maximo un
  borrador abierto por dominio y persona, que es exactamente la regla que se quiere.
*/

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.BorradoresProceso')
      AND name = N'OrganizacionId'
      AND is_nullable = 0)
BEGIN
    /* La foranea se cae y se vuelve a poner: no se puede alterar una columna que participa en una. */
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BorradoresProceso_Organizacion')
        ALTER TABLE dbo.BorradoresProceso DROP CONSTRAINT FK_BorradoresProceso_Organizacion;

    /* El indice unico filtrado tambien: incluye la columna. */
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_BorradoresProceso_Abierto' AND object_id = OBJECT_ID(N'dbo.BorradoresProceso'))
        DROP INDEX UX_BorradoresProceso_Abierto ON dbo.BorradoresProceso;

    ALTER TABLE dbo.BorradoresProceso ALTER COLUMN OrganizacionId int NULL;

    ALTER TABLE dbo.BorradoresProceso
        ADD CONSTRAINT FK_BorradoresProceso_Organizacion
        FOREIGN KEY (OrganizacionId) REFERENCES dbo.Entidades(IdEntidad);

    CREATE UNIQUE INDEX UX_BorradoresProceso_Abierto
        ON dbo.BorradoresProceso(Dominio, OrganizacionId, PersonaId)
        WHERE Estado = N'borrador';
END;
GO
