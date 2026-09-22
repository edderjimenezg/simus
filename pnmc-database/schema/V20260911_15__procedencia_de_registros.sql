/* PNMC · De dónde viene cada registro: contexto, institución de procedencia y usuario ejecutor */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

/*
  QUE PROBLEMA RESUELVE.

  Hoy el sistema sabe QUIEN RESPONDE por un registro -`OrganizacionResponsableId`, que la propia
  base rellena con la entidad institucional mientras nadie lo reclame- y sabe QUE PASO con el
  -`dbo.BitacoraAuditoria`-. Lo que NO sabe es DE DONDE VINO: si lo incorporo una organizacion
  desde el espacio externo o un funcionario desde el Panel de Gestion Administrativa.

  Esas son TRES COSAS DISTINTAS y confundirlas tiene consecuencias reales:

    1. PROCEDENCIA   -> que entidad lo incorporo al sistema.
    2. RESPONSABLE   -> quien gestiona o desarrolla realmente el proceso o contenido.
    3. USUARIO       -> que cuenta concreta ejecuto la accion.

  El caso que lo obliga: un funcionario del PNMC registra el festival de la Fundacion X. La
  PROCEDENCIA es el PNMC -lo incorporo el Programa-, pero la ORGANIZACION RESPONSABLE sigue siendo
  la Fundacion X. Con un solo campo, o se pierde quien lo metio o se le atribuye al Programa un
  festival que no organiza; lo segundo acabaria contandose en algun informe de participacion.

  POR QUE UNA TABLA Y NO COLUMNAS EN CADA TABLA.

  La regla es transversal: aplica a organizaciones, festivales, ediciones, noticias, agenda,
  catalogo editorial, galeria, contenidos del CMS y a los procesos que se habiliten despues. En
  columnas serian tres campos repetidos en diez tablas y una migracion por cada modulo nuevo. En
  una tabla es una fila por registro y ningun cambio de esquema cuando aparezca el modulo once.

  EL PAR (Dominio, RegistroId) ES LA MISMA CONVENCION QUE YA USA EL PROYECTO en
  `dbo.BitacoraAuditoria` (NombreTabla, IdRegistro) y en `dbo.ReclamacionesAdministracion`
  (Dominio, RegistroCanonicoId). No se inventa una forma nueva de apuntar a un registro.

  `RegistroId` ES TEXTO porque las claves del sistema no son homogeneas: int en Festivales y
  Entidades, bigint en Noticias, EventosAgenda y PublicacionesEditoriales. Un bigint serviria hoy
  y dejaria fuera el dia que un dominio use codigo en vez de identidad.

  NO SUSTITUYE A LA AUDITORIA, LA COMPLEMENTA. La bitacora cuenta la historia completa -cada
  actuacion, con su antes y su despues- y puede depurarse con el tiempo; esto es UN HECHO FIJO por
  registro, que se escribe una vez al nacer y se consulta en cada listado. Resolver la procedencia
  buscando en la bitacora obligaria a una consulta por fila en cada tabla que se pinte.

  LAS FORANEAS NO CASCADEAN, por la regla del proyecto.
*/

IF OBJECT_ID(N'dbo.ProcedenciaDeRegistros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProcedenciaDeRegistros (
        IdProcedencia bigint IDENTITY(1,1) NOT NULL,

        /* El modulo al que pertenece el registro: festival, organizacion, noticia, agenda, ... */
        Dominio nvarchar(80) NOT NULL,

        /* La clave del registro dentro de su dominio, como texto. Ver la nota de arriba. */
        RegistroId nvarchar(120) NOT NULL,

        /*
          DESDE DONDE SE INCORPORO.

          `administrativo` -> Panel de Gestion Administrativa.
          `externo`        -> espacio de la organizacion, con su propia cuenta.
          `importacion`    -> carga asistida de archivo, que no es ninguno de los dos: la ejecuta
                              un funcionario pero los datos vienen de una fuente de fuera.
          `siembra`        -> lo creo el arranque del sistema, no una persona.
        */
        ContextoOrigen nvarchar(20) NOT NULL,

        /*
          LA ENTIDAD QUE LO INCORPORO. Para el contexto administrativo es la entidad institucional
          -el PNMC-; para el externo, la organizacion que lo registro.

          ES ANULABLE porque la siembra del arranque no tiene una entidad detras, y porque en una
          base a medio sembrar la entidad institucional puede no existir todavia. Inventar un
          identificador ahi apuntaria a cualquier cosa.
        */
        OrganizacionProcedenciaId int NULL,

        /*
          LA CUENTA QUE EJECUTO LA ACCION. Anulable por la misma razon: lo que crea la siembra no
          lo crea nadie.
        */
        UsuarioCreadorId int NULL,

        FechaRegistro datetime2(0) NOT NULL
            CONSTRAINT DF_ProcedenciaDeRegistros_Fecha DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_ProcedenciaDeRegistros PRIMARY KEY CLUSTERED (IdProcedencia),

        /* UN REGISTRO, UNA PROCEDENCIA. Nace una vez y no cambia: si cambiara, dejaria de ser
           de donde vino para ser de donde esta, que ya lo cuenta la organizacion responsable. */
        CONSTRAINT UQ_ProcedenciaDeRegistros UNIQUE (Dominio, RegistroId),

        CONSTRAINT FK_ProcedenciaDeRegistros_Organizacion
            FOREIGN KEY (OrganizacionProcedenciaId) REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_ProcedenciaDeRegistros_Usuario
            FOREIGN KEY (UsuarioCreadorId) REFERENCES dbo.Usuarios (IdUsuario),

        CONSTRAINT CK_ProcedenciaDeRegistros_Contexto
            CHECK (ContextoOrigen IN (N'administrativo', N'externo', N'importacion', N'siembra'))
    );

    /* Para resolver la procedencia de una pagina entera de un modulo en una sola consulta. */
    CREATE INDEX IX_ProcedenciaDeRegistros_Dominio
        ON dbo.ProcedenciaDeRegistros (Dominio, RegistroId);

    /* Para responder «que incorporo esta entidad» y «que registro esta persona». */
    CREATE INDEX IX_ProcedenciaDeRegistros_Organizacion
        ON dbo.ProcedenciaDeRegistros (OrganizacionProcedenciaId, Dominio);
    CREATE INDEX IX_ProcedenciaDeRegistros_Usuario
        ON dbo.ProcedenciaDeRegistros (UsuarioCreadorId, FechaRegistro DESC);
END
GO

/*
  LO QUE YA ESTABA EN LA BASE TAMBIEN TIENE PROCEDENCIA, y se deduce de lo que el propio sistema
  ya sabe en vez de dejarlo en blanco:

    - El CANAL DE ACCESO de quien lo creo dice el contexto. Un usuario `interno` trabajaba desde la
      consola; uno `externo`, desde el espacio de su organizacion. Es el dato mas fiable que hay
      para los registros anteriores a esta tabla.
    - La ORGANIZACION DE PROCEDENCIA para lo externo es la que ya responde por el registro, porque
      hasta ahora eran la misma cosa; para lo administrativo es la entidad institucional.

  NO SE INVENTA NADA: donde no hay creador conocido, la fila no se escribe y la ficha dira que la
  procedencia no consta, que es la verdad.
*/

IF OBJECT_ID(N'dbo.ProcedenciaDeRegistros', N'U') IS NOT NULL
BEGIN
    DECLARE @Institucional int =
        (SELECT TOP 1 IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad);

    INSERT INTO dbo.ProcedenciaDeRegistros
        (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
    SELECT
        N'festival',
        CAST(f.IdFestival AS nvarchar(120)),
        CASE WHEN u.CanalAcceso = N'externo' THEN N'externo' ELSE N'administrativo' END,
        CASE WHEN u.CanalAcceso = N'externo' THEN f.OrganizacionPrincipalId ELSE @Institucional END,
        f.IdUsuarioCreador,
        f.FechaCreacion
    FROM dbo.Festivales AS f
    INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = f.IdUsuarioCreador
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
        WHERE p.Dominio = N'festival' AND p.RegistroId = CAST(f.IdFestival AS nvarchar(120)));

    INSERT INTO dbo.ProcedenciaDeRegistros
        (Dominio, RegistroId, ContextoOrigen, OrganizacionProcedenciaId, UsuarioCreadorId, FechaRegistro)
    SELECT
        N'organizacion',
        CAST(e.IdEntidad AS nvarchar(120)),
        CASE WHEN u.CanalAcceso = N'externo' THEN N'externo' ELSE N'administrativo' END,
        CASE WHEN u.CanalAcceso = N'externo' THEN e.IdEntidad ELSE @Institucional END,
        e.IdUsuarioCreador,
        e.FechaCreacion
    FROM dbo.Entidades AS e
    INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = e.IdUsuarioCreador
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedenciaDeRegistros AS p
        WHERE p.Dominio = N'organizacion' AND p.RegistroId = CAST(e.IdEntidad AS nvarchar(120)));
END
GO
