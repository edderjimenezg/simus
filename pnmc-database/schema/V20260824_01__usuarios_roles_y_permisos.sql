/*
================================================================================================
    PNMC - Usuarios, roles y permisos.  Modelo de

    QUE ES ESTE FICHERO.  Un DISENO ejecutable, no una migracion ejecutada.  No se ha corrido
    contra ninguna base: lo unico comprobado sobre el es un analisis sintactico (SET PARSEONLY),
    que no resuelve nombres ni valida semantica.
    por SELECT; todo lo que dice del resultado de aplicarlo es prevision, no medida.

    QUE NO HACE, Y ES DELIBERADO
    ----------------------------
    1. NO ABRE NINGUNA PUERTA.  Las politicas de Program.cs siguen siendo la unica puerta:
       FallbackPolicy (Program.cs-163), InstitutionalPolicy / ExternalPolicy
       (Program.cs-172), PoliticaCualquierSesion (Program.cs-183) y PoliticaFuncionario
       (Program.cs-206).  Un permiso concedido aqui es un filtro ADICIONAL dentro de una
       ruta que la politica ya autorizo; nunca un sustituto.  La regla es que "mas roles no pueden significar mas puertas".
    2. NO TRAE DATOS DE SIMUS.  Ni una fila. Alcance: solo estructura.
       Las 272 concesiones de ART_MUSICA_ROL_RECURSO no viajan: son el reparto entre 14
       roles de origen, y aqui hay 3.  Los 85 codigos de ART_MUSICA_RECURSO se miran como
       inventario de "que se puede pedir permiso para hacer", no como reparto a copiar.
    3. NO CAMBIA EL COMPORTAMIENTO DE HOY.  La siembra de la seccion 9 es un ESPEJO de las
       guardas que el codigo ya aplica, una por una y con la cita al lado.  Cuando el API empiece
       a leer la tabla, nadie debe ganar ni perder nada.  La granularidad nueva se anade despues,
       una decision cada vez.
    4. NO SE EJECUTA ENTERO DE UNA VEZ.  La seccion 11 -retirada de dbo.Usuarios.IdRol- esta
       apagada por omision: el DROP tiene que ir DESPUES del despliegue del codigo que deja de
       leer la columna, y son dos eventos distintos.

    DONDE VIVE ESTE GUION CUANDO SE APRUEBE.  Aqui es un documento de diseno.  Para ejecutarse
    tiene que copiarse a pnmc-database/schema/ con el nombre versionado del proyecto
    (V2026MMDD_NN__usuarios_roles_y_permisos.sql).  Mientras no este ahi,
    ParidadEsquemaSinArranqueTests pondra en rojo cada tabla nueva que se mapee en
    PnmcDbContext: esa prueba compara el modelo de EF contra una base hecha SOLO con schema/.
    No es un estorbo, es la guarda: si el guion no esta en la via gobernada, la prueba lo dice
    sin que nadie tenga que acordarse.

    ORDEN DE APLICACION
    -------------------
      A. Este guion con @RetirarIdRol = 0   -> secciones 1 a 10.
      B. Despliegue del codigo: 11 ficheros .cs de src y 8 .ts, listados en el .md hermano.
      C. Este mismo guion con @RetirarIdRol = 1 -> seccion 11.
    Entre A y B la base sostiene las dos formas a la vez: IdRol sigue existiendo y UsuariosRoles
    ya tiene las mismas filas.  Es lo que permite desplegar B -y revertirlo- sin tocar la base.

    IDEMPOTENTE.  Guardas OBJECT_ID / COL_LENGTH / sys.indexes / sys.foreign_keys /
    sys.check_constraints / sys.triggers en cada objeto, y MERGE en cada siembra.  Correrlo dos
    veces no hace nada la segunda.

    CONVENCIONES SEGUIDAS contra pnmc-database/schema/ y contra la base
    --------------------------------------------------------------------------
      - Nombres fisicos en espanol.  Cero nombres de SIMUS, cero nombres en ingles.
      - IdX int IDENTITY(1,1), datetime2(0), prefijos PK_ UQ_ FK_ CK_ DF_ IX_ TR_.
      - "Codigo", nunca "Cod".  Comprobado: en la base no existe NI UNA columna abreviada 'Cod*'
        (consulta sobre sys.columns, 0 filas).  Divipola.CodigoDepartamento,
        EstadosContenido.CodigoEstado, TiposRegistroEcosistema.CodigoTipoRegistro.
        POR ESO LA COLUMNA SE LLAMA CodigoTipoDocumento Y NO CodTipoDocumento, que es como se
        llama en SIMUS y como la nombra el modelo de usuarios.  Desviacion consciente para cumplir su
        intencion; queda como pregunta abierta al propietario en el .md hermano.
      - Catalogo con clave natural por codigo, como dbo.EstadosContenido: las FK del proyecto
        apuntan al codigo (V20260519_03:40, V20260521_01:41-42), no a un id.
      - Sin GO.  ESTO ES UNA ELECCION DE ESTE FICHERO, NO UNA CONVENCION DE schema/, y la primera
        version lo presentaba como lo segundo (U12).  Comprobado: de los doce guiones de schema/, diez
        no llevan ninguno, V20260519_04 lleva cuatro y V20260823_01 lleva ocho.  Lo que necesita
        lote propio va en EXEC(N'...'), como V20260823_02:244-249.
        Y CONVIENE SABER LO QUE CUESTA: ser un solo lote es justo lo que hizo posibles U1 y U6 -las
        columnas de una tabla que YA existe se enlazan al COMPILAR el lote entero-.  Se mantiene
        porque un fichero sin GO se puede aplicar por cualquier via, incluida una conexion de .NET
        que no entiende de GO; el precio se paga con EXEC(N'...') donde toca.
================================================================================================
*/

/*
    QUOTED_IDENTIFIER NO ES ADORNO.  Este guion crea un indice UNIQUE FILTRADO (con WHERE) y SQL
    Server lo rechaza -Msg 1934- si la sesion no lo tiene en ON.  Las conexiones de .NET lo traen
    en ON, asi que el arranque del API y el arnes de pruebas no lo notarian; scripts/seed-local-db.sh
    aplica los guiones con sqlcmd SIN -I, donde esta OFF, y ahi la siembra local abortaria.  El
    mismo tropiezo esta documentado en la cabecera de V20260823_02 y en V20260823_01.  Se declara
    aqui, y no en quien invoca, porque un guion de esquema no debe depender de las opciones de
    sesion de quien lo lance.
*/
SET QUOTED_IDENTIFIER ON;

/*
    Y AHORA LA CONSECUENCIA QUE ESTE GUION LE IMPONE A TERCEROS, QUE ES LO QUE DE VERDAD DUELE.

    Declarar la opcion aqui basta para que ESTE fichero corra. NO basta para el proyecto: el
    indice filtrado UQ_Usuarios_Documento (seccion 5) queda VIVO sobre dbo.Usuarios, y desde ese
    momento **cualquier escritura sobre esa tabla** exige QUOTED_IDENTIFIER ON en la sesion que
    escribe, la haya creado quien la haya creado.

    COMPROBADO:
      - Base con el esquema y SIN este guion  -> los .sql de pnmc-database/seed/ siembran: 0 errores, 1 usuario.
      - La misma base CON este guion aplicado -> V20260519_03__administracion_control_seed.sql
        aborta con Msg 1934 en su MERGE sobre dbo.Usuarios, y por el -b se lleva la siembra entera.
      - El mismo fichero, la misma base, anadiendo -I a sqlcmd -> 0 errores, 1 usuario.

    CAMBIO ACOMPANANTE OBLIGATORIO, sin el cual este guion rompe la siembra local:
      o los guiones de seed que escriben dbo.Usuarios declaran SET QUOTED_IDENTIFIER ON,
      o scripts/seed-local-db.sh pasa -I a sqlcmd.
    La doctrina del proyecto (pnmc-database/MIGRADOR.md) dice que la declare el guion y no quien
    lo invoca, asi que lo coherente es lo primero. No se aplica desde aqui porque toca ficheros
    ajenos al alcance de SIMUS: va en el plan de construccion como cambio acompanante.

    Y EL PUNTO CIEGO QUE ESTO DESTAPA: GuionesIndependientesDeLaSesionTests vigila que todo guion
    que CREA un indice filtrado declare la opcion. No vigila a los guiones que ESCRIBEN en una
    tabla que ya tiene uno, que es por donde entro este defecto. La prueba habria seguido verde.
*/
SET NOCOUNT ON;

/*
    EL UNICO INTERRUPTOR DEL FICHERO.  Ver seccion 11.  Puesto a 1 retira dbo.Usuarios.IdRol.

    ACTIVO, Y ESE ES EL ORDEN CORRECTO: el codigo que deja
    de leer la columna- se desplego el mismo dia, commit 740653d.  Encenderlo ANTES habria tumbado
    el API; encenderlo despues es lo que hace comprobable el invariante "los roles de una persona
    son exactamente las filas de dbo.UsuariosRoles", sin segunda fuente que pueda contradecirlo.

    LO QUE HUBO QUE CAMBIAR CON EL, Y NO ES OBVIO: `schema/` corre ENTERO antes que `seed/`, de
    modo que en cuanto esto se enciende, la columna ya no existe cuando las semillas insertan sus
    usuarios.  `seed/V20260519_03` y `seed/V20260519_07` escribian `IdRol`; ahora escriben en
    `dbo.UsuariosRoles`.  Sin ese cambio, la primera semilla que crea una cuenta muere con Msg 207
    y se lleva por delante toda la siembra que viene detras.
*/
DECLARE @RetirarIdRol bit = 1;

/*
================================================================================================

    EL REPARTO EN TRES FICHEROS NO ES COSMETICO: LO EXIGE EL ORDEN DE LA SIEMBRA (defecto U3).
    `scripts/seed-local-db.sh` aplica TODO `schema/` y solo despues `seed/`. Con el diseno
    entero puesto en `schema/`, la seccion 2 -la que copia `Usuarios.IdRol` a `UsuariosRoles`-
    corria con `dbo.Usuarios` VACIA: copiaba cero filas, y sus dos THROW de comprobacion pasaban
    en verde sobre el conjunto vacio. El resultado habria sido una base sembrada en la que cada
    usuario tiene cero filas en `UsuariosRoles`, que es exactamente el fallo que el diseno nombra
    en su seccion 2: «obtiene cookie, no cruza ninguna politica y recibe 403 en todas partes sin
    que nada lo explique».

      - schema/V20260824_01__usuarios_roles_y_permisos.sql  <- ESTE. Estructura y catalogo.
      - seed/V20260824_01__usuarios_roles_seed.sql          <- la copia de filas, DESPUES de
                                                               que la siembra cree los usuarios.
      - pnmc-database/scripts/validar_migracion_simus.sql                 <- las comprobaciones de solo lectura.
================================================================================================
*/

/*
================================================================================================
  SECCION 1.  dbo.UsuariosRoles - la tabla puente.  usuario <-> rol es una relacion N:M.
================================================================================================

  POR QUE UNA TABLA PUENTE Y NO UNA SEGUNDA COLUMNA.  Con 1:1 habia que elegir una regla para
  colapsar los roles -maximo o minimo- y las dos son lesivas: el minimo retira accesos vigentes,
  el maximo concede de mas.  Con N:M no hay nada que colapsar, asi que no hay regla que
  equivocar.  Es la primera razon, y es la buena: la pregunta desaparece en vez de
  responderse.  N:M contiene a 1:1 como caso particular; subir no pierde nada.

  NO HAY COLUMNA "Activo", Y ES UNA DECISION.  Mismo criterio que la seccion 7 aplica a
  RolesPermisos, y viene de un defecto de origen [S]: ART_MUSICA_ROL_RECURSO.esActivo existe y el
  resolutor NO LA MIRA (ServicioRecurso.cs-23 y :59-64 filtran por Tipo y por rol, nunca por
  esActivo), de modo que una concesion "desactivada" sigue concediendo.  Una columna que dice
  "revocado" y no revoca es peor que no tenerla: da una falsa sensacion de control.  Aqui la
  asignacion existe o no existe.

  CLAVE SUSTITUTA + UNIQUE, y no clave compuesta.  Se copia la forma de dbo.UsuariosEntidades
  (PnmcDbContext.cs-870), que es la tabla puente que PNMC ya tiene, para que las dos se
  lean igual.  El UNIQUE hace el trabajo real; el IdUsuarioRol es lo que espera el patron
  entity.HasKey(x => x.Id) del contexto.
*/
IF OBJECT_ID(N'dbo.UsuariosRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UsuariosRoles
    (
        IdUsuarioRol  int IDENTITY(1,1) NOT NULL,
        IdUsuario     int NOT NULL,
        IdRol         int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_UsuariosRoles_FechaCreacion DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_UsuariosRoles PRIMARY KEY (IdUsuarioRol),
        CONSTRAINT UQ_UsuariosRoles UNIQUE (IdUsuario, IdRol),

        /*
            SIN CASCADA HACIA Usuarios (U4). ERA FALSA, y se comprobo abriendo la ruta: no borra nada.
            AdminAuthEndpoints.cs-551 hace `user.IsActive = false; user.UpdatedAt = ...` -baja
            LOGICA- y `Users.Remove` / `Remove(user` no aparece NI UNA VEZ en `src` ni en `tests`.
            En PNMC nadie borra filas de dbo.Usuarios, de modo que el Msg 547 que la cascada venia
            a evitar no puede ocurrir.

            Y la cascada no era neutral: una cascada sobre la tabla de ASIGNACION DE ROLES borra
            concesiones en silencio, que es exactamente lo que este mismo bloque rechaza dos
            parrafos mas abajo para la FK hacia Roles.  Ademas PNMC no tiene ninguna: cero
            foraneas con cascada en la base, cero ocurrencias en pnmc-database/.
        */
        CONSTRAINT FK_UsuariosRoles_Usuarios FOREIGN KEY (IdUsuario)
            REFERENCES dbo.Usuarios (IdUsuario),

        /*
            HACIA Roles, SIN CASCADA, Y TAMBIEN A PROPOSITO.  Borrar un rol que alguien tiene debe
            FALLAR, no vaciar cuentas en silencio.  Permisos.cs dice que cualquier
            nombre fuera de RolesDePlataforma es un residuo; retirarlo es un trabajo con su propio
            guion -como fue V20260823_01__retirada_aliados.sql-, no un efecto colateral.
        */
        CONSTRAINT FK_UsuariosRoles_Roles FOREIGN KEY (IdRol)
            REFERENCES dbo.Roles (IdRol)
    );
END;

/*
    "QUIEN TIENE ESTE ROL" ES UNA PREGUNTA QUE EL API YA HACE.  La guarda del ultimo webmaster
    (WebmastersQuePuedenEntrarAsync, AdminAuthEndpoints.cs) recorre los usuarios por rol.
    El UQ de arriba ordena por (IdUsuario, IdRol) y no sirve para esa direccion.
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_UsuariosRoles_IdRol'
                 AND object_id = OBJECT_ID(N'dbo.UsuariosRoles'))
BEGIN
    CREATE INDEX IX_UsuariosRoles_IdRol
        ON dbo.UsuariosRoles (IdRol) INCLUDE (IdUsuario);
END;

/*
================================================================================================
  SECCION 3.  dbo.Roles gana VersionPermisos.  Es el sello que permite "cero consultas nuevas por
  peticion" SIN que un permiso revocado se quede colgado dentro de una cookie.
================================================================================================

  EL PROBLEMA.  Los permisos se resuelven en el login y viajan en los claims.  Un claim
  escrito en el login se queda congelado hasta que la cookie expire: 8 horas deslizantes
  (Program.cs y :120).  Sin nada mas, revocar un permiso no revoca nada durante 8 horas.

  LA SOLUCION QUE PNMC YA TIENE.  RevalidacionDeSesion comprueba por peticion, contra la base y
  con cache de 30 s acotada a [1,300], que la cuenta siga activa y con el mismo rol
  (RevalidacionDeSesion.cs-107 y Program.cs).  Basta con que compare una tercera cosa.

  POR QUE UNA COLUMNA Y NO CHECKSUM_AGG.  El diseno previo (perfilado/mapeo-usuarios-permisos.md
  §5.3) proponia calcular el sello al vuelo con CONCAT(COUNT(*), ':', CHECKSUM_AGG(IdPermiso)).
  ESO SE ROMPE PRECISAMENTE CON N:M, que es la relacion que este guion establece: CHECKSUM_AGG es un XOR
  agregado, y con dos roles que concedan el mismo permiso el permiso aparece dos veces y su XOR se
  anula.  Habria que escribir COUNT(DISTINCT ...) y CHECKSUM_AGG(DISTINCT ...), y acordarse
  siempre.  Una columna entera que solo sube no tiene ese filo.

  Y ADEMAS SE PROYECTA SOLA.  La consulta de RevalidacionDeSesion.cs-102 ya une Usuarios con
  Roles; anadir r.VersionPermisos a esa proyeccion es una columna mas en el mismo viaje.  Un
  CHECKSUM_AGG sobre otra tabla seria una subconsulta que LINQ no compone contra ese Join.

  int y no bigint: haria falta pasar de dos mil millones de ediciones del reparto.
*/
IF COL_LENGTH(N'dbo.Roles', N'VersionPermisos') IS NULL
BEGIN
    ALTER TABLE dbo.Roles
    ADD VersionPermisos int NOT NULL
        CONSTRAINT DF_Roles_VersionPermisos DEFAULT (1) WITH VALUES;
END;

/*
================================================================================================
  SECCION 4.  dbo.TiposDocumento - el catalogo de tipos de documento.
================================================================================================

  Este guion anade a dbo.Usuarios el documento de identidad y su tipo.  En SIMUS,
  ART_MUSICA_USUARIO.CodTipoDocumento es un varchar(50) suelto, sin catalogo ni FK [S]: cualquier
  cadena entra.  Aqui no.

  CLAVE NATURAL POR CODIGO, como dbo.EstadosContenido: las FK del proyecto apuntan al codigo
  (V20260519_03:40, V20260521_01:41-42).  Asi la columna de Usuarios se lee sin join, que es
  lo que se quiere de un catalogo de ocho filas.

  CODIGOS EN MINUSCULA, como todo codigo de PNMC: 'en_revision', 'gestor_interno', 'organizacion'.  Que la calle escriba "CC" no es razon para que la base lo escriba; el rotulo para la
  persona es NombreTipoDocumento.
*/
IF OBJECT_ID(N'dbo.TiposDocumento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TiposDocumento
    (
        CodigoTipoDocumento nvarchar(20)  NOT NULL,
        NombreTipoDocumento nvarchar(120) NOT NULL,
        OrdenVisualizacion  int NOT NULL,
        Activo              bit NOT NULL
            CONSTRAINT DF_TiposDocumento_Activo DEFAULT (1),

        CONSTRAINT PK_TiposDocumento PRIMARY KEY (CodigoTipoDocumento),
        CONSTRAINT UQ_TiposDocumento_Nombre UNIQUE (NombreTipoDocumento),
        CONSTRAINT CK_TiposDocumento_Orden CHECK (OrdenVisualizacion > 0),

        /*
            SIN ESPACIOS AL BORDE, COMPROBADO CON DATALENGTH Y NO COMPARADO.
            En origen ART_MUSICA_RECURSO.Codigo es NCHAR(30) y trae relleno, y el codigo de SIMUS
            tuvo que defenderse a mano (ManejadorUsuario.cs-46, "TrimEnd defensivo") [S].
            CUIDADO SI ALGUIEN "SIMPLIFICA" ESTO: la version obvia
                CHECK (CodigoTipoDocumento = LTRIM(RTRIM(CodigoTipoDocumento)))
            NO FUNCIONA.  SQL Server rellena con espacios al comparar cadenas, de modo que
            'cc ' = 'cc' es verdadero y el CHECK deja pasar justo lo que pretende impedir.
            DATALENGTH no compara: mide.
        */
        CONSTRAINT CK_TiposDocumento_CodigoSinRelleno
            CHECK (DATALENGTH(CodigoTipoDocumento) = DATALENGTH(LTRIM(RTRIM(CodigoTipoDocumento)))
                   AND DATALENGTH(CodigoTipoDocumento) > 0),

        /*
            MINUSCULA COMPROBADA EN COLACION BINARIA, Y ES EL SEGUNDO FILO DEL MISMO CUCHILLO.
            La base es SQL_Latin1_General_CP1_CI_AS: ignora mayusculas.  Escrito como
                CHECK (CodigoTipoDocumento = LOWER(CodigoTipoDocumento))
            el CHECK es CIERTO para 'CC', porque en colacion CI 'CC' = 'cc'.  Un CHECK que no
            puede fallar es un comentario con sintaxis de restriccion.  COLLATE lo arregla.
        */
        CONSTRAINT CK_TiposDocumento_CodigoEnMinuscula
            CHECK (CodigoTipoDocumento COLLATE Latin1_General_BIN2
                   = LOWER(CodigoTipoDocumento) COLLATE Latin1_General_BIN2)
    );
END;

/*
    EL CONTENIDO LO DECIDE PNMC, NO SIMUS.  El criterio para los catalogos es: "la tabla
    si; el contenido lo define PNMC".  Estos ocho son los tipos de documento de PERSONA NATURAL
    vigentes en Colombia; no salen de ART_MUSICA_USUARIO, cuya columna es texto libre.

    'nit' NO ESTA, y es una decision.  Identifica a una persona juridica, y en PNMC las personas
    juridicas son Entidades -Entidades.NumeroIdentificacion, con su indice unico filtrado
    UQ_Entidades_NumeroIdentificacion (V20260823_02:244-249)-, no Usuarios.  Meterlo aqui
    invitaria a registrar una empresa como si fuera una persona, que es exactamente la confusion
    que UsuariosEntidades existe para no tener (Permisos.cs: el rol dice que clase de
    cosas puede hacer, el alcance dice sobre que actor).
*/
MERGE dbo.TiposDocumento AS destino
USING (VALUES
    (N'cc',   N'Cedula de ciudadania',                     1),
    (N'ce',   N'Cedula de extranjeria',                    2),
    (N'ti',   N'Tarjeta de identidad',                     3),
    (N'rc',   N'Registro civil de nacimiento',             4),
    (N'nuip', N'Numero unico de identificacion personal',  5),
    (N'pa',   N'Pasaporte',                                6),
    (N'pep',  N'Permiso especial de permanencia',          7),
    (N'ppt',  N'Permiso por proteccion temporal',          8)
) AS origen (CodigoTipoDocumento, NombreTipoDocumento, OrdenVisualizacion)
ON destino.CodigoTipoDocumento = origen.CodigoTipoDocumento
WHEN MATCHED THEN
    UPDATE SET NombreTipoDocumento = origen.NombreTipoDocumento,
               OrdenVisualizacion  = origen.OrdenVisualizacion
WHEN NOT MATCHED THEN
    INSERT (CodigoTipoDocumento, NombreTipoDocumento, OrdenVisualizacion)
    VALUES (origen.CodigoTipoDocumento, origen.NombreTipoDocumento, origen.OrdenVisualizacion);

/*
================================================================================================
  SECCION 5.  dbo.Usuarios gana Identificacion y CodigoTipoDocumento.
================================================================================================

  >>>  DATO PERSONAL SENSIBLE.  Ley 1581 de 2012.  <<<
  A partir de aqui dbo.Usuarios guarda documentos de identidad.  Que DTO no puede llevarlo, que
  rutas de PNMC hay que revisar y donde queda el rastro de lectura esta en la seccion 3 del .md
  hermano, y NO es posterior ni opcional: el criterio es "se anota aqui porque el modulo
  de privacidad tiene que enterarse ANTES de que la columna exista".  Esta seccion es ese momento.

  NULABLES LAS DOS.  Los 6 usuarios que ya existen no tienen documento y no hay de donde
  sacarlo: bajo el alcance de solo estructura, de SIMUS no viene ni una fila.  NOT NULL obligaria
  a inventar un valor, que es convertir un dato ausente en un dato falso.

  nvarchar(60) Y NO nvarchar(40), aunque 60 sobre para una cedula.  Es el ancho exacto de
  Entidades.NumeroIdentificacion (nvarchar(60)).  Las dos columnas guardan la misma
  clase de valor, y algun dia alguien las va a comparar o a copiar; si una es mas estrecha, la
  copia trunca en silencio.  La igualdad de ancho no garantiza nada, pero la desigualdad si
  garantiza un fallo posible, y evitarlo es gratis.
*/
IF COL_LENGTH(N'dbo.Usuarios', N'Identificacion') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD Identificacion nvarchar(60) NULL;
END;

IF COL_LENGTH(N'dbo.Usuarios', N'CodigoTipoDocumento') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD CodigoTipoDocumento nvarchar(20) NULL;
END;

/*
    LAS RESTRICCIONES VAN EN EXEC(N'...') POR LA MISMA RAZON QUE V20260823_02:242-249: las
    columnas pueden haberse anadido en este mismo lote, y un ALTER TABLE ADD CONSTRAINT que las
    nombre estaticamente falla al COMPILAR el lote con Msg 207 (Invalid column name), antes de que
    el ALTER anterior haya llegado a ejecutarse.
*/
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_TiposDocumento')
BEGIN
    EXEC(N'ALTER TABLE dbo.Usuarios WITH CHECK
        ADD CONSTRAINT FK_Usuarios_TiposDocumento FOREIGN KEY (CodigoTipoDocumento)
            REFERENCES dbo.TiposDocumento (CodigoTipoDocumento);');
END;

/*
    LOS DOS JUNTOS O NINGUNO.  Un numero de documento sin decir de que documento es no significa
    nada; es literalmente el motivo por el que perfilado/mapeo-usuarios-permisos.md §1.2 descarta
    CodTipoDocumento por separado ("acompana a la anterior; sin ella no significa nada").  Y al
    reves, un tipo sin numero es ruido.
*/
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Usuarios_Documento_Completo')
BEGIN
    EXEC(N'ALTER TABLE dbo.Usuarios WITH CHECK
        ADD CONSTRAINT CK_Usuarios_Documento_Completo CHECK (
            (Identificacion IS NULL     AND CodigoTipoDocumento IS NULL)
         OR (Identificacion IS NOT NULL AND CodigoTipoDocumento IS NOT NULL));');
END;

/*
    FORMA DEL NUMERO, DELIBERADAMENTE LAXA: alfanumerico y guion, sin espacios, minimo tres
    caracteres.  No se exige "solo digitos" porque un pasaporte no lo es, y un CHECK que rechaza
    pasaportes obliga a dejar el campo vacio justo a quien mas necesita identificarse.
*/
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Usuarios_Identificacion_Formato')
BEGIN
    EXEC(N'ALTER TABLE dbo.Usuarios WITH CHECK
        ADD CONSTRAINT CK_Usuarios_Identificacion_Formato CHECK (
            Identificacion IS NULL
            OR (LEN(Identificacion) >= 3
                AND Identificacion NOT LIKE ''%[^0-9A-Za-z-]%''));');
END;

/*
    UN DOCUMENTO, UNA CUENTA.  Dos cuentas con la misma cedula son la misma persona dos veces, y
    eso rompe cualquier cosa que despues se cuelgue de la identidad.

    TIENE QUE SER UN INDICE FILTRADO, NO UN UNIQUE.  En SQL Server un UNIQUE admite UN SOLO NULL;
    con las 6 filas actuales -todas con Identificacion NULL- un UNIQUE fallaria al crearse.
    El indice filtrado ignora los nulos y deja que "todavia no lo sabemos" sea lo normal.  Es la
    misma forma exacta de UQ_Entidades_NumeroIdentificacion, que en la base tiene has_filter=1
    y filtro ([NumeroIdentificacion] IS NOT NULL).  Y por eso el fichero abre con
    SET QUOTED_IDENTIFIER ON.
*/
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UQ_Usuarios_Documento'
                 AND object_id = OBJECT_ID(N'dbo.Usuarios'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UQ_Usuarios_Documento
        ON dbo.Usuarios (CodigoTipoDocumento, Identificacion)
        WHERE Identificacion IS NOT NULL;');
END;

/*
================================================================================================
  SECCION 6.  dbo.Permisos - el catalogo de capacidades.
================================================================================================

  UN PERMISO ES UNA CAPACIDAD, NO UNA PANTALLA.  Es la mitad de la separacion entre capacidad y pantalla.
  En SIMUS, ART_MUSICA_RECURSO es a la vez el menu que se pinta y el permiso que se valida, y el
  dano lo escribio el propio equipo de origen [S]:
  sql/versioned/V6_0_2__menu_y_permisos_roles_dotaciones.sql:12-28 explica que "los 22 recursos
  PAG_DOTA_* son permisos, no paginas: todos apuntan a la misma ruta Dotacion/Index", que al
  colgar de un menu raiz "el menu de SIMUS los muestra como 22 enlaces identicos", y que la salida
  que encontraron fue explotar una asimetria del codigo para esconderlos.  Con las dos tablas
  separadas ese apano deja de hacer falta: aqui un permiso sin entrada de menu es lo normal.

  LAS CONCESIONES DE ORIGEN NO VIAJAN.  Los 85 codigos de origen son inventario de que se puede
  permitir; el reparto lo decide PNMC, en la seccion 9.
*/
IF OBJECT_ID(N'dbo.Permisos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permisos
    (
        IdPermiso          int IDENTITY(1,1) NOT NULL,
        CodigoPermiso      nvarchar(60)  NOT NULL,
        NombrePermiso      nvarchar(120) NOT NULL,
        DescripcionPermiso nvarchar(500) NULL,
        Modulo             nvarchar(60)  NOT NULL,
        FechaCreacion      datetime2(0)  NOT NULL
            CONSTRAINT DF_Permisos_FechaCreacion DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Permisos PRIMARY KEY (IdPermiso),

        /*
            UNICO SOBRE EL CODIGO.  En origen NO lo hay [S] y el rol se busca por codigo: con dos
            filas iguales, el resultado es indefinido.
        */
        CONSTRAINT UQ_Permisos_CodigoPermiso UNIQUE (CodigoPermiso),

        -- Los dos filos ya explicados en la seccion 4: DATALENGTH mide, COLLATE compara de veras.
        CONSTRAINT CK_Permisos_CodigoSinRelleno
            CHECK (DATALENGTH(CodigoPermiso) = DATALENGTH(LTRIM(RTRIM(CodigoPermiso)))
                   AND DATALENGTH(CodigoPermiso) > 0),
        CONSTRAINT CK_Permisos_CodigoEnMinuscula
            CHECK (CodigoPermiso COLLATE Latin1_General_BIN2
                   = LOWER(CodigoPermiso) COLLATE Latin1_General_BIN2),

        /*
            FORMA "modulo.accion": exactamente un punto, y sin espacios.  Un catalogo de permisos
            se lee mucho mas de lo que se escribe, y la primera vez que conviven 'cms.publicar' y
            'PublicarCms' deja de poder ordenarse ni agruparse por modulo.
            OJO AL LEER EL PATRON: en LIKE, '_' es comodin de UN caracter, no un subrayado.  El
            patron '%_.%_' pide "algo, un caracter, punto, algo, un caracter".
        */
        CONSTRAINT CK_Permisos_CodigoFormato
            CHECK (CodigoPermiso LIKE '%_.%_'
                   AND CodigoPermiso NOT LIKE '%.%.%'
                   AND CodigoPermiso NOT LIKE '% %')
    );
END;

/*
================================================================================================
  SECCION 7.  dbo.RolesPermisos - las concesiones.  EL PERMISO CUELGA DEL ROL, NUNCA DEL USUARIO.
================================================================================================

  POR QUE NO HAY UNA TABLA UsuariosPermisos, QUE ES LA PREGUNTA OBVIA.  Porque el permiso por
  usuario destruye lo unico que hace barato este diseno.  Si los permisos son funcion del conjunto
  de roles, y solo hay tres roles, en todo el sistema existen A LO SUMO CUATRO conjuntos de
  permisos distintos -{externo}, {gestor_interno}, {webmaster}, {gestor_interno, webmaster}-, y
  caben en la memoria del proceso.  Con permisos por usuario hay tantos conjuntos como personas, y
  vuelve la consulta por peticion que este modelo existe para evitar.  Si algun dia hace falta un permiso
  nominal, se crea un rol.

  NOTA SOBRE EL PASO A N:M: la variante "solo el sello en la cookie + catalogo en memoria"
  (perfilado §5.5, Opcion B) se justificaba en que un usuario tiene exactamente un rol.  ESA
  PREMISA SE CAE HOY, y la conclusion NO: se pasa de 3 conjuntos posibles a 4.  Sigue cabiendo.
*/
IF OBJECT_ID(N'dbo.RolesPermisos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolesPermisos
    (
        IdRolPermiso  int IDENTITY(1,1) NOT NULL,
        IdRol         int NOT NULL,
        IdPermiso     int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL
            CONSTRAINT DF_RolesPermisos_FechaCreacion DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_RolesPermisos PRIMARY KEY (IdRolPermiso),
        CONSTRAINT UQ_RolesPermisos UNIQUE (IdRol, IdPermiso),
        CONSTRAINT FK_RolesPermisos_Roles FOREIGN KEY (IdRol)
            REFERENCES dbo.Roles (IdRol),

        /*
            SIN CASCADA HACIA Permisos, y por coherencia, no por doctrina.  El argumento original
            -«un permiso que ya no existe no puede seguir concedido»- sigue siendo razonable, pero
            PNMC no usa cascadas EN NINGUN SITIO: cero foraneas con ON DELETE CASCADE en
            la base. Estrenarlas aqui, en la tabla que decide quien puede hacer que, dejaria el
            proyecto con dos politicas de borrado segun la tabla que se toque.

            Consecuencia asumida: retirar un permiso del catalogo falla con Msg 547 mientras algun
            rol lo tenga concedido.  Eso es lo que se quiere: retirar un permiso es un trabajo con
            su propio guion -como fue V20260823_01__retirada_aliados.sql-, no un efecto colateral.
            Es el mismo razonamiento que la FK hacia Roles de la seccion 1.
        */
        CONSTRAINT FK_RolesPermisos_Permisos FOREIGN KEY (IdPermiso)
            REFERENCES dbo.Permisos (IdPermiso)
    );

    -- Sin columna "Activo", por el motivo escrito en la seccion 1.
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_RolesPermisos_IdRol'
                 AND object_id = OBJECT_ID(N'dbo.RolesPermisos'))
BEGIN
    CREATE INDEX IX_RolesPermisos_IdRol
        ON dbo.RolesPermisos (IdRol) INCLUDE (IdPermiso);
END;

/*
    EL DISPARADOR QUE MANTIENE EL SELLO.  Sube dbo.Roles.VersionPermisos cada vez que cambia el
    reparto de ese rol.

    POR QUE UN DISPARADOR, SI EL PROYECTO DESCONFIA DE LA MAGIA.  Porque la alternativa -que la
    ruta del API que edite permisos suba la version- falla exactamente en el caso para el que el
    sello existe.  RevalidacionDeSesion.cs-35 lo dice de su propia ventana de 30 s: "la
    ventana solo cubre los cambios hechos POR FUERA del API -un UPDATE a mano contra la base-".
    Un contador que solo sube cuando el API lo sube no ve el UPDATE a mano, que es justo el cambio
    que nadie va a recordar propagar.
    Y NO ES UN MECANISMO AJENO A LA CASA: La base ya tiene un disparador propio,
    TR_RegistrosEcosistema_ValidarOrigen (verificado en sys.triggers).

    EL COSTE, DICHO: una escritura invisible sobre dbo.Roles -3 filas- en cada edicion del
    reparto.  Trabaja por conjuntos (nada de un UPDATE por fila) y no puede recursar, porque
    escribe en Roles y no en RolesPermisos.
*/
IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = N'TR_RolesPermisos_VersionPermisos')
BEGIN
    EXEC(N'
CREATE TRIGGER dbo.TR_RolesPermisos_VersionPermisos
ON dbo.RolesPermisos
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE r
       SET r.VersionPermisos = r.VersionPermisos + 1
      FROM dbo.Roles AS r
     WHERE r.IdRol IN (SELECT IdRol FROM inserted
                       UNION
                       SELECT IdRol FROM deleted);
END;');
END;

/*
================================================================================================
  SECCION 8.  dbo.MenuElementos - la otra mitad de esa separacion.  SE CREA VACIA, Y ESO ES EL DISENO.
================================================================================================

  LEA ESTO ANTES DE SEMBRARLA.  Hoy el menu de la consola NO sale de la base: esta escrito en el
  frontend, y en dos sitios -pnmc-web/src/app/features/admin/domain/admin-config.ts (los
  modulos, con su allowedRoles) y
  pnmc-web/src/app/features/admin/admin-shell-page/admin-shell-page.component.ts-933
  (las secciones y el
  webmasterOnly)-.  Sembrar filas aqui mientras eso siga asi produce DOS MENUS: uno que se pinta y
  otro que nadie mira, y el segundo se queda atras sin que nada se ponga rojo.  Es la falla que
  Permisos.cs describe para las listas de roles repetidas, aplicada al menu.

  LA TABLA SE CREA IGUAL, porque la ESTRUCTURA viaja y porque tenerla vacia y
  documentada es lo que permite mover el menu aqui despues sin volver a discutir su forma.
  CONDICION PARA SEMBRARLA: que el frontend lea el menu del API en la misma entrega.  Ni antes.
*/
IF OBJECT_ID(N'dbo.MenuElementos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuElementos
    (
        IdMenuElemento     int IDENTITY(1,1) NOT NULL,
        CodigoMenu         nvarchar(60)  NOT NULL,
        IdMenuPadre        int NULL,
        Etiqueta           nvarchar(120) NOT NULL,
        Ruta               nvarchar(200) NULL,
        Icono              nvarchar(60)  NULL,
        Orden              int NOT NULL
            CONSTRAINT DF_MenuElementos_Orden DEFAULT (0),

        /*
            NULABLE A PROPOSITO: ES LO QUE SEPARA EL MENU DEL PERMISO.
              NULL     = entrada siempre visible para quien tenga sesion (una raiz, un separador).
                         No concede nada.
              NOT NULL = se pinta solo si el CONJUNTO de roles del usuario tiene ese permiso.
            Y al reves: un permiso NO necesita fila aqui.  Los 22 PAG_DOTA_* de V6_0_2 [S] son
            exactamente ese caso, y son la razon de que existan dos tablas y no una.
        */
        IdPermisoRequerido int NULL,
        Visible            bit NOT NULL
            CONSTRAINT DF_MenuElementos_Visible DEFAULT (1),

        CONSTRAINT PK_MenuElementos PRIMARY KEY (IdMenuElemento),
        CONSTRAINT UQ_MenuElementos_CodigoMenu UNIQUE (CodigoMenu),
        CONSTRAINT FK_MenuElementos_Padre FOREIGN KEY (IdMenuPadre)
            REFERENCES dbo.MenuElementos (IdMenuElemento),

        /*
            SIN CASCADA HACIA Permisos, al reves que RolesPermisos, y la diferencia importa: si
            borrar un permiso borrara la entrada de menu, retirar un permiso haria DESAPARECER una
            pagina del menu.  Que el borrado falle y obligue a mirar es el comportamiento correcto.
        */
        CONSTRAINT FK_MenuElementos_Permisos FOREIGN KEY (IdPermisoRequerido)
            REFERENCES dbo.Permisos (IdPermiso),

        /*
            Solo atrapa el ciclo de profundidad 1.  Los ciclos largos no se expresan en un CHECK;
            los tiene que impedir quien escriba el arbol.  Se dice para que nadie crea que estan
            cubiertos por tener esta restriccion.
        */
        CONSTRAINT CK_MenuElementos_NoEsSuPropioPadre
            CHECK (IdMenuPadre IS NULL OR IdMenuPadre <> IdMenuElemento)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MenuElementos_IdMenuPadre'
                 AND object_id = OBJECT_ID(N'dbo.MenuElementos'))
BEGIN
    CREATE INDEX IX_MenuElementos_IdMenuPadre
        ON dbo.MenuElementos (IdMenuPadre, Orden);
END;

/*
================================================================================================
  SECCION 9.  El catalogo de permisos de PNMC y su reparto.
  ESPEJO DE LAS GUARDAS QUE YA EXISTEN.  NI UNA CAPACIDAD NUEVA.
================================================================================================

  DE DONDE SALEN ESTOS CODIGOS, Y DE DONDE NO.  No de los 85 recursos de SIMUS: de las decisiones
  de rol que el codigo de PNMC YA toma hoy, una por una y con la cita al lado.  Es lo que
  exige que "el catalogo de permisos se define aqui, informado por los codigos de origen
  como inventario de que se puede pedir permiso para hacer, no como reparto a copiar".

  POR QUE ESPEJO Y NO MEJORA.  Porque asi, el dia que Permisos.cs empiece a leer esta tabla, el
  resultado es identico al de hoy para las tres figuras, y cualquier diferencia observada es un
  defecto y no "el modelo nuevo, que es distinto".  Un cambio de mecanismo y un cambio de politica
  a la vez son indistinguibles cuando algo falla.

  externo SE QUEDA CON CERO CONCESIONES, Y NO ES UN OLVIDO.  Lo que una persona externa puede
  hacer no lo decide un permiso fino sino ExternalPolicy (Program.cs-172) mas su vinculo en
  UsuariosEntidades: "el rol dice que clase de cosas puede hacer; el alcance dice sobre que actor"
  (Permisos.cs).  Darle filas aqui seria empezar a describir su alcance en el sitio
  equivocado.
*/
MERGE dbo.Permisos AS destino
USING (VALUES
    -- ecosistema
    (N'registros.revisar',            N'Revisar registros',            N'ecosistema',     N'Entrar a la bandeja institucional de revision. Hoy: PoliticaFuncionario sobre el circuito (Program.cs-206).'),
    (N'registros.publicar',           N'Publicar registros',           N'ecosistema',     N'Aprobar o publicar un registro sectorial. Hoy: cualquier rol interno (cualquier rol interno).'),
    (N'entidades.administrar',        N'Administrar entidades',        N'ecosistema',     N'Alta, edicion y estado de Entidades. Hoy: AdminEntityEndpoints.cs-538.'),
    (N'gobernanza.resolver_vinculos', N'Resolver vinculaciones',       N'ecosistema',     N'Solicitudes de vinculacion, duplicados y banderas de calidad. Hoy no hay lista de roles: deciden los `.RequireAuthorization()` sin politica de RecordGovernanceEndpoints.cs y :38, que caen en el esquema institucional por omision (Program.cs) y admiten a los dos roles internos.'),
    -- festivales
    (N'festivales.decidir',           N'Decidir sobre Festivales',     N'festivales',     N'Publicar o rechazar un Festival en el circuito institucional. Hoy: PoliticaFuncionario, unica guarda de rol del circuito.'),
    (N'propuestas.decidir',           N'Decidir propuestas de cambio', N'festivales',     N'Aceptar o rechazar una propuesta de cambio de Festival. Hoy: PoliticaFuncionario.'),
    (N'festivales.normalizar',        N'Normalizar versiones',         N'festivales',     N'Lanzar la normalizacion masiva de versiones historicas. Hoy: PoliticaFuncionario.'),
    -- cms
    (N'cms.editar',                   N'Editar textos del sitio',      N'cms',            N'Guardar contenido del CMS de textos. Hoy: WebContentEndpoints.cs-552.'),
    (N'cms.publicar',                 N'Publicar textos del sitio',    N'cms',            N'Llevar una clave a estado publicado. Hoy SOLO webmaster: WebContentEndpoints.cs declara PublisherRoles con webmaster y nadie mas.'),
    (N'cms.importar',                 N'Importar textos',              N'cms',            N'Carga masiva de contenido web. Hoy: WebContentImportEndpoints.cs-351.'),
    (N'equipo_web.editar',            N'Editar el equipo web',         N'cms',            N'Guardar la nomina del equipo web. Hoy: WebTeamEndpoints.cs EditorRoles = webmaster + gestor_interno.'),
    (N'equipo_web.publicar',          N'Publicar el equipo web',       N'cms',            N'Llevar la nomina a publicada. Hoy SOLO webmaster: WebTeamEndpoints.cs PublisherRoles, comprobado en :192.'),
    -- administracion
    (N'usuarios.administrar',         N'Administrar usuarios',         N'administracion', N'Alta, edicion, desactivacion y borrado de cuentas. Hoy SOLO webmaster: AdminAuthEndpoints.cs.'),
    (N'sistema.configurar',           N'Configurar el sistema',        N'administracion', N'Seccion Sistema de la consola. Hoy SOLO webmaster: admin-shell-page.component.ts.'),
    (N'monitor.consultar',            N'Consultar el monitor',         N'administracion', N'Panel de estado de la plataforma. Hoy: AdminDataEndpoints.cs, sesion institucional.')
) AS origen (CodigoPermiso, NombrePermiso, Modulo, DescripcionPermiso)
ON destino.CodigoPermiso = origen.CodigoPermiso
WHEN MATCHED THEN
    UPDATE SET NombrePermiso      = origen.NombrePermiso,
               Modulo             = origen.Modulo,
               DescripcionPermiso = origen.DescripcionPermiso
WHEN NOT MATCHED THEN
    INSERT (CodigoPermiso, NombrePermiso, Modulo, DescripcionPermiso)
    VALUES (origen.CodigoPermiso, origen.NombrePermiso, origen.Modulo, origen.DescripcionPermiso);

/*
    EL REPARTO.  Por NOMBRE de rol y por CODIGO de permiso, nunca por id: en la base los tres
    roles son IdRol 4, 5 y 6, no 1, 2 y 3.

    LA DIFERENCIA ENTRE LOS DOS ROLES INTERNOS SON TRES FILAS, y las tres estan medidas en el
    codigo de hoy: cms.publicar (WebContentEndpoints.cs), usuarios.administrar
    (AdminAuthEndpoints.cs) y sistema.configurar (admin-shell-page.component.ts).  Todo lo
    demas que hace un webmaster lo hace tambien un gestor_interno, porque el modelo
    de roles dice que CUALQUIER funcionario decide, publicacion de registros incluida
    (Permisos.cs).  Si esta tabla dijera otra cosa, estaria cambiando la politica de
    tapadillo, que es lo que la seccion promete no hacer.

    EL MERGE NO BORRA LO QUE NO ESTA EN LA LISTA -no lleva WHEN NOT MATCHED BY SOURCE- y es a
    proposito: si manana alguien concede un permiso a mano desde la consola, volver a correr este
    guion no debe deshacerlo en silencio.  El reverso de esa moneda esta documentado en
    seed/V20260519_03__administracion_control_seed.sql:5-12: un MERGE que reinsertaba deshacia
    los DELETE de una migracion en la misma ejecucion, sin error y sin aviso.
*/
MERGE dbo.RolesPermisos AS destino
USING (
    SELECT r.IdRol, p.IdPermiso
    FROM (VALUES
        (N'webmaster',      N'registros.revisar'),
        (N'webmaster',      N'registros.publicar'),
        (N'webmaster',      N'entidades.administrar'),
        (N'webmaster',      N'gobernanza.resolver_vinculos'),
        (N'webmaster',      N'festivales.decidir'),
        (N'webmaster',      N'propuestas.decidir'),
        (N'webmaster',      N'festivales.normalizar'),
        (N'webmaster',      N'cms.editar'),
        (N'webmaster',      N'cms.publicar'),
        (N'webmaster',      N'cms.importar'),
        (N'webmaster',      N'equipo_web.editar'),
        (N'webmaster',      N'equipo_web.publicar'),
        (N'webmaster',      N'usuarios.administrar'),
        (N'webmaster',      N'sistema.configurar'),
        (N'webmaster',      N'monitor.consultar'),

        (N'gestor_interno', N'registros.revisar'),
        (N'gestor_interno', N'entidades.administrar'),
        (N'gestor_interno', N'gobernanza.resolver_vinculos'),
        (N'gestor_interno', N'festivales.decidir'),
        (N'gestor_interno', N'propuestas.decidir'),
        (N'gestor_interno', N'festivales.normalizar'),
        (N'gestor_interno', N'cms.editar'),
        (N'gestor_interno', N'equipo_web.editar'),
        (N'gestor_interno', N'monitor.consultar')
        -- externo: ninguna. Ver la cabecera de esta seccion.
    ) AS reparto (NombreRol, CodigoPermiso)
    JOIN dbo.Roles    AS r ON r.NombreRol     = reparto.NombreRol
    JOIN dbo.Permisos AS p ON p.CodigoPermiso = reparto.CodigoPermiso
) AS origen
ON destino.IdRol = origen.IdRol AND destino.IdPermiso = origen.IdPermiso
WHEN NOT MATCHED THEN
    INSERT (IdRol, IdPermiso) VALUES (origen.IdRol, origen.IdPermiso);

/*
================================================================================================
  SECCION 11.  Retirada de dbo.Usuarios.IdRol.  ACTIVA.
================================================================================================

  LA RECOMENDACION ES RETIRARLA.  Las otras dos opciones, y por que se descartan:

  (a) CONSERVARLA COMO "ROL PRINCIPAL".  Devuelve la pregunta que la relacion N:M elimina.  Su mejor
      argumento era que con N:M "la pregunta desaparece en vez de responderse"; un rol principal la
      resucita -cual de los tres es el principal, y quien lo decide cuando cambian- y ademas obliga
      a cada lector a elegir entre dos fuentes para "que rol tiene esta persona".  Dos fuentes para
      un dato es como se llega a que una se quede atras sin que nadie lo note, que es literalmente
      el argumento de Permisos.cs contra las listas de roles repetidas.

  (b) DEJARLA DE USAR SIN RETIRARLA.  Es la peor de las tres.  La columna seguiria siendo NOT NULL
      con FK, asi que TODO INSERT en Usuarios tendria que escribir algo -vease
      ExternalAuthEndpoints.cs y DatabaseBootstrapper.cs-, y lo que escriba no lo
      comprueba nadie contra UsuariosRoles.  Es exactamente la patologia de
      ART_MUSICA_ROL_RECURSO.esActivo [S]: una columna que parece decir algo y no lo dice.  Con el
      agravante de que aqui la columna viva y la tabla puente pueden CONTRADECIRSE, y quien lea la
      fila creera lo que ve.

  (c) RETIRARLA -lo recomendado-.  Es lo unico que hace comprobable el invariante "los roles de una
      persona son exactamente las filas de UsuariosRoles", sin excepciones y sin segunda fuente.  Y
      el coste ya esta contado: los ficheros .cs que la leen estan listados en el .md hermano, y
      ninguno la usa para nada que la tabla puente no haga.

  POR QUE ESTO NO CORRE HOY.  El DROP y el despliegue del codigo son dos eventos.  Mientras el API
  en pie siga leyendo IdRol, retirarla lo tumba.  Ejecute este bloque -con @RetirarIdRol = 1-
  DESPUES de desplegar el codigo, y solo entonces.

  Y ANTES DE SOLTAR LA COLUMNA HAY QUE SOLTAR LO QUE CUELGA DE ELLA: FK_Usuarios_Roles
  (V20260519_02:38) e IX_Usuarios_IdRol (V20260519_02:56-59).  Un DROP COLUMN con una FK
  encima falla con Msg 5074 y deja el guion a medias.
*/
IF @RetirarIdRol = 1 AND COL_LENGTH(N'dbo.Usuarios', N'IdRol') IS NOT NULL
BEGIN
    -- Ultima red: no se suelta la columna si alguien fuera a quedarse sin roles al hacerlo.
    IF EXISTS (
        SELECT 1 FROM dbo.Usuarios AS u
        WHERE NOT EXISTS (SELECT 1 FROM dbo.UsuariosRoles AS ur WHERE ur.IdUsuario = u.IdUsuario))
    BEGIN
        THROW 51003, N'Retirada abortada: hay usuarios sin ninguna fila en dbo.UsuariosRoles.', 1;
    END;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_Roles')
    BEGIN
        ALTER TABLE dbo.Usuarios DROP CONSTRAINT FK_Usuarios_Roles;
    END;

    IF EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Usuarios_IdRol' AND object_id = OBJECT_ID(N'dbo.Usuarios'))
    BEGIN
        DROP INDEX IX_Usuarios_IdRol ON dbo.Usuarios;
    END;

    ALTER TABLE dbo.Usuarios DROP COLUMN IdRol;
END;
