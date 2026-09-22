/* PNMC · La entidad institucional del Programa existe en toda base, no solo en las sembradas */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE PROBLEMA RESUELVE.

  La entidad institucional —la que responde por todo registro que ninguna organizacion ha
  reclamado— se creaba SOLO en `seed/V20260519_03`. Las semillas son opcionales: una base
  correctamente migrada podia quedarse sin ella, y quedarse sin ella NO es cosmetico.
  sobre la base local:

    - Devolver un proceso a custodia institucional se niega: no hay a quien devolverselo.
    - Los registros creados desde la consola guardan contexto `administrativo` pero sin la
      entidad de procedencia, porque no hay ninguna que apuntar.
    - `PnmcDbContext.AsignarOrganizacionResponsable` no asigna nada, asi que los registros del
      ecosistema nacen sin quien responda por ellos.

  Tres funcionalidades del producto dependen de un dato que vivia en un guion de demostracion.

  POR QUE NO SE CORRIO LA SIEMBRA ENTERA. `seed/V20260519_07` hace
  `DELETE FROM dbo.Entidades WHERE IdEntidad >= 100` en cada pasada, para limpiar sus propios
  datos de moderacion. Sobre una base con organizaciones reales creadas despues —las que registra
  la consola o el canal externo— eso se las lleva por delante. La siembra sirve para levantar una
  base de demostracion, no para reparar una que ya se esta usando.

  NO PELEA CON LA SIEMBRA. Las dos estan guardadas por la misma condicion —que no exista ya una
  entidad marcada— asi que la que corra primero gana y la otra no hace nada. Y las dos escriben lo
  mismo, con el mismo identificador cuando esta libre.

  EL IDENTIFICADOR SE MANTIENE BAJO A PROPOSITO, por la razon que la propia siembra documenta: el
  rango >= 100 pertenece a `seed/V20260519_07` y lo borra en cada pasada. Una entidad institucional
  ahi seria borrada y se llevaria por delante, con Msg 547, a todos los festivales que la apuntan.
  Si no quedara ningun identificador libre por debajo de 100, este guion NO la crea y avisa: es
  preferible una base sin entidad institucional —que el API sabe explicar— que una que se va a
  borrar sola en la siguiente siembra.
*/

IF NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE EsInstitucional = 1)
BEGIN
    /*
      NECESITA UN USUARIO CREADOR: `Entidades.IdUsuarioCreador` es NOT NULL. Se toma el usuario
      interno mas antiguo, que en cualquier base operativa es una cuenta institucional real. Si no
      hay ninguno, la base todavia no esta lista y no se inventa nada.
    */
    DECLARE @Creador int = (
        SELECT TOP 1 IdUsuario
        FROM dbo.Usuarios
        WHERE CanalAcceso = N'interno'
        ORDER BY IdUsuario);

    DECLARE @Id int = (
        SELECT MIN(candidato.Id)
        FROM (SELECT TOP 99 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Id
              FROM sys.all_objects) AS candidato
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Entidades WHERE IdEntidad = candidato.Id));

    IF @Creador IS NULL
        PRINT '[pnmc] No hay usuarios internos todavia: la entidad institucional no se crea en esta pasada.';
    ELSE IF @Id IS NULL
        PRINT '[pnmc] No queda identificador libre por debajo de 100: la entidad institucional no se crea. Revisar a mano.';
    ELSE
    BEGIN
        SET IDENTITY_INSERT dbo.Entidades ON;

        /*
          SIN COLUMNAS DE COBERTURA NI TERRITORIO: `V20260909_02` las retiro de `dbo.Entidades`
          —una organizacion ya no declara alcance, solo su sede— y la siembra original, escrita
          antes, todavia las nombra. Copiarla tal cual fallaba con «Invalid column name».
          La institucional tampoco tiene sede: no es una organizacion del territorio.
        */
        INSERT INTO dbo.Entidades
            (IdEntidad, TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, SitioWeb,
             EstadoRegistro, Activo, EsInstitucional, IdUsuarioCreador, IdUsuarioResponsable, FechaCreacion)
        VALUES
            (@Id,
             N'organizacion',
             N'Plan Nacional de Música para la Convivencia',
             N'Plan Nacional de Música para la Convivencia',
             N'Organización responsable por defecto de los registros que ninguna organización del ecosistema ha reclamado todavía.',
             N'contacto@pnmc.local',
             N'https://pnmc.local',
             N'publicado', 1,
             1, @Creador, NULL, SYSUTCDATETIME());

        SET IDENTITY_INSERT dbo.Entidades OFF;

        PRINT '[pnmc] Entidad institucional del Programa creada.';
    END
END
GO

/*
  LOS REGISTROS QUE NACIERON SIN QUIEN RESPONDA POR ELLOS.

  `PnmcDbContext.AsignarOrganizacionResponsable` asigna la institucional a todo registro del
  ecosistema que nazca sin una, pero NO PUEDE hacerlo si la entidad no existia. Los registros
  creados en ese hueco se quedaron con la columna vacia. Se les asigna ahora, que es exactamente lo
  que habria pasado si la entidad hubiera estado.

  SOLO A LOS QUE ESTAN VACIOS: un Festival que YA tiene organizacion responsable no se toca nunca,
  ni siquiera si es la institucional.
*/
IF EXISTS (SELECT 1 FROM dbo.Entidades WHERE EsInstitucional = 1)
BEGIN
    DECLARE @Institucional int = (SELECT TOP 1 IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad);

    UPDATE dbo.Festivales
    SET OrganizacionPrincipalId = @Institucional
    WHERE OrganizacionPrincipalId IS NULL;
END
GO
