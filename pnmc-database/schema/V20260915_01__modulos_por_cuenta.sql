/*
  SIMUS · Qué módulos de la consola tiene activados cada cuenta

  pregunta de alcance que llevaba abierta: los permisos del Espacio de Gestión Administrativa se
  definen POR CUENTA y no por rol. «Debería haber algún apartado donde yo pueda seleccionar cuál de
  los módulos que actualmente aparece en la izquierda se le activan.»

  POR QUE NO POR ROL. Un permiso por rol obliga a inventar un rol nuevo cada vez que una persona
  necesita una combinación distinta, y se termina con roles llamados «coordinador-pero-sin-catálogo».
  La pregunta real es qué hace ESTA persona en el Programa, y esa se responde por cuenta.

  UNA FILA POR PAR, Y NO UNA LISTA EN UNA COLUMNA. Con una fila por (cuenta, módulo) se puede
  preguntar «quién tiene Catálogo Editorial» sin abrir cadenas, el índice sirve para la comprobación
  que hace el servidor en cada petición, y conceder o quitar uno no reescribe los dieciséis.

  LOS DOS DE SIEMPRE NO SE GUARDAN AQUI. «monitor» y «solicitudes» los tiene toda cuenta de consola
  por decisión de producto, y guardarlos sería poder borrarlos: bastaría un DELETE para dejar a
  alguien en una consola vacía sin sitio donde ver lo que espera una decisión. Viven en el catálogo
  del servidor, que es donde vive una regla que no admite excepción.

  QUIEN LO CONCEDIO Y CUANDO. Sin eso, la pregunta «por qué esta persona puede publicar» no tiene
  respuesta seis meses después, que es justo cuando se hace. Se permite nulo porque la siembra
  inicial no la concede nadie.

  ES ADITIVA. No toca ninguna tabla existente, no borra nada y no cambia ninguna columna.
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ModulosPorCuenta' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.ModulosPorCuenta (
        IdModuloPorCuenta int IDENTITY(1,1) NOT NULL,
        IdUsuario int NOT NULL,
        /* El identificador del módulo tal como lo nombra la barra izquierda: «catalogo-editorial»,
           «agenda», «boletin». Es el MISMO que usa la navegación, no una taxonomía paralela. */
        CodigoModulo nvarchar(60) NOT NULL,
        FechaOtorgado datetime2(3) NOT NULL CONSTRAINT DF_ModulosPorCuenta_FechaOtorgado DEFAULT (SYSUTCDATETIME()),
        IdUsuarioQueOtorgo int NULL,
        CONSTRAINT PK_ModulosPorCuenta PRIMARY KEY (IdModuloPorCuenta),
        /* Un módulo no se concede dos veces a la misma cuenta. Sin esto, quitarlo tendría que borrar
           un número indeterminado de filas y una podría sobrevivir. */
        CONSTRAINT UQ_ModulosPorCuenta_CuentaModulo UNIQUE (IdUsuario, CodigoModulo),
        /* SIN CASCADA. El proyecto no usa foráneas con cascada y una prueba lo vigila: una cascada
           borra filas en silencio y deja el mismo DELETE comportándose de dos maneras según la
           tabla. Al dar de baja una cuenta hay que retirar sus módulos explícitamente, que además
           es la ocasión de dejarlo en la bitácora. */
        CONSTRAINT FK_ModulosPorCuenta_Usuario FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),
        /* Quien concedió puede borrarse de la plataforma sin que el permiso concedido desaparezca:
           el permiso es de la cuenta que lo recibió, no de quien lo dio. */
        CONSTRAINT FK_ModulosPorCuenta_Otorgante FOREIGN KEY (IdUsuarioQueOtorgo)
            REFERENCES dbo.Usuarios (IdUsuario)
    );

    /* La comprobación que hace el servidor en cada petición es «esta cuenta, este módulo», y ese es
       exactamente el índice único de arriba. Este segundo sirve a la pregunta inversa —«quién tiene
       Catálogo Editorial»— que hace la pantalla de administración. */
    CREATE INDEX IX_ModulosPorCuenta_Modulo ON dbo.ModulosPorCuenta (CodigoModulo) INCLUDE (IdUsuario);

    /*
      LAS CUENTAS QUE YA EXISTEN CONSERVAN TODO LO QUE HOY PUEDEN HACER.

      Hasta ahora cualquier cuenta con rol interno llegaba a cualquier módulo. Si esta migración se
      limitara a crear la tabla vacía, al día siguiente TODAS las cuentas de gestión se quedarían
      con dos módulos y el resto en 403, sin que nadie hubiera decidido eso. Un permiso nuevo no
      puede quitar permisos: se concede lo que ya tenían y, desde ahí, quien administra usuarios
      recorta a quien corresponda.

      EL WEBMASTER NO NECESITA FILAS: los tiene todos por definición. Se siembran igual para que la
      pantalla de permisos enseñe un estado coherente y no casillas puestas sin fila detrás.

      Se excluyen los dos que van siempre activados, que no se guardan nunca.
    */
    INSERT INTO dbo.ModulosPorCuenta (IdUsuario, CodigoModulo, FechaOtorgado, IdUsuarioQueOtorgo)
    SELECT u.IdUsuario, m.CodigoModulo, SYSUTCDATETIME(), NULL
    FROM dbo.Usuarios u
    CROSS JOIN (VALUES
        (N'ecosistema'), (N'organizaciones'), (N'catalogo-editorial'), (N'agenda'),
        (N'noticias'), (N'categorias'), (N'banco-de-archivos'), (N'galeria'),
        (N'gestion-sitio'), (N'boletin'), (N'analisis'), (N'auditoria'),
        (N'usuarios'), (N'sistema')
    ) AS m(CodigoModulo)
    WHERE EXISTS (
        SELECT 1
        FROM dbo.UsuariosRoles ur
        JOIN dbo.Roles r ON r.IdRol = ur.IdRol
        WHERE ur.IdUsuario = u.IdUsuario AND r.NombreRol IN (N'webmaster', N'gestor_interno')
    );
END;
GO
