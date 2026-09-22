/*
================================================================================================
    PNMC - Cada usuario sembrado estrena su fila en dbo.UsuariosRoles.

    ESTA ES LA SECCION 2 DEL DISENO 01-usuarios-y-permisos.sql, Y VIVE AQUI Y NO EN schema/
    POR UNA RAZON DE ORDEN (defecto U3). `scripts/seed-local-db.sh` aplica todo `schema/` antes
    que nada de `seed/`. Puesta alla, esta copia corria contra una `dbo.Usuarios` vacia: cero
    filas copiadas, y los dos THROW pasando en verde sobre el conjunto vacio. Una comprobacion
    que no puede fallar no es una comprobacion.

    ORDEN EXIGIDO: despues de V20260519_03 (que crea `sistema@pnmc.local`) y despues de
    V20260519_07 (que crea los cinco usuarios de la consola de moderacion). El nombre
    V20260824_01 lo garantiza por orden alfabetico, que es como lee la lista `SEEDS`.

    NO ES UNA SIEMBRA DE DATOS DE PRUEBA: es la migracion de un modelo. Por eso trae sus dos
    THROW. Si algun dia falla, la base quedo a medias y hay que mirarla, no reintentar.
================================================================================================
*/

/*
================================================================================================
  SECCION 2.  Migracion de las filas que ya existen.  NADIE PIERDE EL ROL QUE TIENE HOY.
================================================================================================

  Esto es lo que convierte un cambio de modelo en algo aplicable sobre una base viva.

  EN UNA BASE YA SEMBRADA: 6 usuarios, con IdRol 4 (externo, 3
  usuarios), 5 (gestor_interno, 1) y 6 (webmaster, 2).  Los ids de dbo.Roles NO son 1-2-3: son
  4-5-6, porque los tres primeros se gastaron en los roles aliado_* retirados el 22 ago 2026.
  De ahi que TODO en este fichero se resuelva por NombreRol y jamas por id literal.  Un guion que
  escriba ids de rol a mano funciona en una base y estropea otra.
*/
/*
    DOS COSAS QUE ESTA COPIA HACE Y QUE LA PRIMERA VERSION NO HACIA:

    (U5) SOLO COPIA A QUIEN NO TIENE NINGUNA FILA TODAVIA, no «a quien no tenga ESTA fila».  La
    diferencia aparece en la segunda ejecucion: mientras el codigo no este desplegado,
    la consola sigue moviendo `Usuarios.IdRol`.  Un usuario degradado de webmaster a gestor_interno
    entre dos pasadas acababa con LAS DOS filas -la vieja no la borra nadie- y por tanto con mas
    puertas abiertas que antes de degradarlo.  Volver a ejecutar un guion de migracion no puede
    RESUCITAR un rol retirado.

    (U6) VA DENTRO DE EXEC(N'...'), y no por gusto.  La seccion 11 puede soltar `Usuarios.IdRol`.
    En cuanto lo haga, una ejecucion posterior de este mismo fichero moriria al COMPILAR el lote
    -Msg 207 sobre `u.IdRol`- y ni siquiera llegaria a evaluar la guarda `COL_LENGTH` que debia
    protegerla.  Es el mismo mecanismo que U1, al reves: alli la columna aun no existia; aqui ya no
    existe.  El texto diferido se compila al ejecutarse, que es cuando la guarda ya ha decidido.
*/
IF COL_LENGTH(N'dbo.Usuarios', N'IdRol') IS NOT NULL
BEGIN
    EXEC(N'
    INSERT INTO dbo.UsuariosRoles (IdUsuario, IdRol)
    SELECT u.IdUsuario, u.IdRol
    FROM dbo.Usuarios AS u
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.UsuariosRoles AS ur WHERE ur.IdUsuario = u.IdUsuario);');
END;

/*
    LA COMPROBACION, QUE ES LA MITAD DEL TRABAJO.  "No se pierde el rol de nadie" dicho en una
    frase es una intencion.  Dicho como un THROW que aborta es una garantia.  Si alguna fila de
    Usuarios se quedara sin su equivalente -por un IdRol huerfano que FK_Usuarios_Roles deberia
    impedir pero que conviene no dar por supuesto-, el guion para aqui y no sigue construyendo
    encima de una base a medio migrar.
*/
IF COL_LENGTH(N'dbo.Usuarios', N'IdRol') IS NOT NULL
BEGIN
    -- EXEC por U6: esta comprobacion nombra `u.IdRol`, que la seccion 11 puede haber soltado.
    EXEC(N'
    IF EXISTS (
        SELECT 1 FROM dbo.Usuarios AS u
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.UsuariosRoles AS ur
            WHERE ur.IdUsuario = u.IdUsuario AND ur.IdRol = u.IdRol))
    BEGIN
        THROW 51001, N''Migracion abortada: hay usuarios cuyo IdRol no llego a dbo.UsuariosRoles.'', 1;
    END;');
END;

/*
    Y AL REVES: NADIE SE QUEDA CON CERO ROLES.  Con IdRol NOT NULL, "usuario sin rol" no era
    representable en PNMC —que es justo lo que el modelo anterior impedia—.  Con la tabla puente vuelve a serlo, y
    eso es una GARANTIA QUE SE PIERDE, no un detalle: un usuario con cero roles obtiene cookie, no
    cruza ninguna politica y recibe 403 en todas partes sin que nada explique por que.  Se nombra
    aqui para no descubrirlo en produccion.

    SQL NO PUEDE EXPRESAR "al menos una fila en la tabla hija" con un CHECK.  Podria con un
    disparador sobre UsuariosRoles, y NO SE PONE: tendria que distinguir "te quedaste sin roles" de
    "te estan reasignando los roles dentro de la misma transaccion", y un disparador al que hay que
    ensenarle esa diferencia es un disparador que se equivocara.  (Este parrafo decia antes que el
    disparador chocaria con el ON DELETE CASCADE de la seccion 1.  Esa cascada se retiro -U4- y con
    ella aquel argumento; la conclusion no cambia, pero la razon si.)

    LA GUARDA VIVE DONDE YA VIVE SU HERMANA.  AdminAuthEndpoints ya sostiene un candado de este
    tipo -"no se puede desactivar al ultimo webmaster que pueda entrar", con
    CandadoDelUltimoWebmasterTests midiendolo-.  "No se puede dejar a nadie sin roles" es la
    misma clase de invariante y va al mismo sitio.  Aqui solo se comprueba.
*/
IF EXISTS (
    SELECT 1 FROM dbo.Usuarios AS u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.UsuariosRoles AS ur WHERE ur.IdUsuario = u.IdUsuario))
BEGIN
    THROW 51002, N'Migracion abortada: hay usuarios con cero roles en dbo.UsuariosRoles.', 1;
END;

