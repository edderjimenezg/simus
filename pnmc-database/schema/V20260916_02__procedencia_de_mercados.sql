/* PNMC · Ningún registro se queda sin procedencia: los mercados musicales */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE CIERRA ESTE GUION.

  Mercados Musicales se monto y nacio SIN anotar procedencia: era el
  unico proceso del Ecosistema cuyos registros no podian responder de donde vinieron. La regla del
  proyecto es transversal y no admite excepciones por modulo: todo registro guarda que entidad lo
  incorporo, que cuenta lo hizo y quien responde por el, y las tres son cosas distintas.

  El codigo ya anota la procedencia de los mercados y de sus ediciones que se creen desde hoy. Este
  guion cierra la otra mitad: la de los que se crearon antes de que el modulo lo hiciera.

  CON QUE CRITERIO. El MISMO que uso `V20260911_15` para los festivales y las organizaciones, sin
  inventar uno nuevo: el CANAL DE ACCESO de quien lo creo dice el contexto —un usuario `interno`
  trabajaba desde la consola, uno `externo` desde el espacio de su organizacion—, y la organizacion
  de procedencia es la que responde por el registro cuando entro por el canal externo, o la entidad
  institucional cuando lo incorporo el Programa.

  QUE ORGANIZACION LO INCORPORE NO LO CONVIERTE EN SU RESPONSABLE, ni al reves. El responsable ya
  vive en `Mercados.OrganizacionPrincipalId` y este guion no lo toca.

  LO QUE DELIBERADAMENTE NO HACE. No inventa nada donde no hay creador conocido: esas filas no se
  escriben y la ficha dira que la procedencia no consta, que es la verdad. Y no reescribe ninguna
  procedencia existente: se escribe una vez y no cambia. Es aditivo: no altera ninguna columna ni
  ningun dato de los mercados.
*/

IF OBJECT_ID(N'dbo.ProcedenciaDeRegistros', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Mercados', N'U') IS NOT NULL
BEGIN
    DECLARE @Institucional int =
        (SELECT TOP 1 IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad);

    INSERT INTO dbo.ProcedenciaDeRegistros
        (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
    SELECT
        N'mercado',
        CAST(m.IdMercado AS nvarchar(120)),
        CASE WHEN u.CanalAcceso = N'externo' THEN N'externo' ELSE N'administrativo' END,
        CASE WHEN u.CanalAcceso = N'externo' THEN m.OrganizacionPrincipalId ELSE @Institucional END,
        m.IdUsuarioCreador,
        m.FechaCreacion
    FROM dbo.Mercados AS m
    INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = m.IdUsuarioCreador
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
        WHERE p.Dominio = N'mercado' AND p.RegistroId = CAST(m.IdMercado AS nvarchar(120)));

    /*
      LAS EDICIONES TIENEN LA SUYA, Y NO LA HEREDAN DEL MERCADO. Cada edicion pudo entrar por un
      canal distinto del mercado que la contiene: una la registro la organizacion y la siguiente la
      incorporo el Programa. La organizacion de procedencia de una edicion externa es la que
      responde por su mercado, que es de quien es la edicion.
    */
    IF OBJECT_ID(N'dbo.EdicionesMercado', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.ProcedenciaDeRegistros
            (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
        SELECT
            N'edicion_mercado',
            CAST(e.IdEdicionMercado AS nvarchar(120)),
            CASE WHEN u.CanalAcceso = N'externo' THEN N'externo' ELSE N'administrativo' END,
            CASE WHEN u.CanalAcceso = N'externo' THEN m.OrganizacionPrincipalId ELSE @Institucional END,
            e.IdUsuarioCreador,
            e.FechaCreacion
        FROM dbo.EdicionesMercado AS e
        INNER JOIN dbo.Mercados AS m ON m.IdMercado = e.MercadoId
        INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = e.IdUsuarioCreador
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
            WHERE p.Dominio = N'edicion_mercado' AND p.RegistroId = CAST(e.IdEdicionMercado AS nvarchar(120)));
    END
END
GO
