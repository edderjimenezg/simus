-- ==========================================================================================
-- PNMC - Validador de la migracion de SIMUS.  SOLO LECTURA.
--
-- Ni un INSERT, ni un UPDATE, ni un DELETE, ni un DDL.  Se puede ejecutar contra cualquier
-- base, PNMC_LOCAL incluida, sin riesgo.  Hermano de validar_administracion_control.sql y
-- companeros: se ejecuta a mano y se LEE; no lo invoca ningun guion ni ninguna prueba.
--
-- DE DONDE SALE.  De dos sitios, y por la misma razon en los dos: un fichero de `schema/` es
-- lo que se APLICA, y un SELECT que imprime un informe no se aplica -- solo hace ruido en la
-- salida de la siembra y estorba a quien busca un error de verdad entre cien lineas.
--
--   - La PARTE 1 es la seccion 10 de
--   - La PARTE 2 es la PARTE B de
--     que su sitio era este: «cuando la PARTE A se promueva a pnmc-database/schema/, ESTA
--     PARTE NO VIAJA CON ELLA».
--
-- COMO SE EJECUTA (el `-I` no sobra: hay indices filtrados por medio):
--   sqlcmd -S 127.0.0.1,14334 -U sa -P <clave> -d PNMC_LOCAL -C -I -i pnmc-database/scripts/validar_migracion_simus.sql
-- ==========================================================================================


-- ==========================================================================================
-- PARTE 1 - USUARIOS, ROLES Y PERMISOS (de 01)
-- ==========================================================================================

/*
================================================================================================
  SECCION 10.  Comprobaciones de salida.  SOLO LEEN.  Son el informe de lo que quedo.
================================================================================================
*/

SELECT N'usuarios_por_numero_de_roles' AS Comprobacion, NumRoles, COUNT(*) AS Usuarios
FROM (SELECT IdUsuario, COUNT(*) AS NumRoles FROM dbo.UsuariosRoles GROUP BY IdUsuario) AS t
GROUP BY NumRoles
ORDER BY NumRoles;

SELECT N'usuarios_sin_ningun_rol' AS Comprobacion, COUNT(*) AS Filas
FROM dbo.Usuarios AS u
WHERE NOT EXISTS (SELECT 1 FROM dbo.UsuariosRoles AS ur WHERE ur.IdUsuario = u.IdUsuario);

/*
    LA REGLA DE EXCLUSIVIDAD DE 'externo', QUE LA BASE NO PUEDE IMPONER Y HAY QUE VIGILAR.

    Un usuario con {externo, webmaster} pasaria LAS DOS puertas: la institucional
    (AdminAuthEndpoints.cs, que con N:M pasa a ser "alguno de sus roles es interno") y la
    externa (ExternalAuthEndpoints.cs, que pasa a ser "contiene externo") [V].  La misma
    persona tendria a la vez la cookie pnmc.admin y la pnmc.external, con dos ambitos declarados
    distintos, y ninguna politica lo prohibe.  Eso no es "mas roles": es una puerta nueva.  Y
    D12.1 lo dice sin ambiguedad: si un rol nuevo abriera algo, es un defecto, no una funcion.

    NO SE PONE UN CHECK.  La regla cruza filas y un CHECK no ve mas que la suya.  Se podria con
    una funcion escalar dentro de un CHECK -SQL Server lo admite- y NO se hace: esa construccion
    no se reevalua cuando cambia la OTRA fila, de modo que da por buena justamente la combinacion
    que pretende impedir.  Una restriccion que se cree cumplida y no lo esta es peor que ninguna.
    Donde va de verdad: en la ruta que asigna roles (AdminAuthEndpoints.cs), leyendo
    Permisos.EsRolInterno, con su prueba al lado.  Aqui solo se detecta.
*/
SELECT N'combinacion_externo_con_rol_interno' AS Comprobacion, u.IdUsuario, u.CorreoElectronico
FROM dbo.Usuarios AS u
WHERE EXISTS (SELECT 1 FROM dbo.UsuariosRoles ur JOIN dbo.Roles r ON r.IdRol = ur.IdRol
              WHERE ur.IdUsuario = u.IdUsuario AND r.NombreRol = N'externo')
  AND EXISTS (SELECT 1 FROM dbo.UsuariosRoles ur JOIN dbo.Roles r ON r.IdRol = ur.IdRol
              WHERE ur.IdUsuario = u.IdUsuario AND r.NombreRol IN (N'webmaster', N'gestor_interno'));

/* Roles fuera de Permisos.RolesDePlataforma (Permisos.cs). Deberia dar cero filas. */
SELECT N'roles_fuera_del_catalogo' AS Comprobacion, r.IdRol, r.NombreRol
FROM dbo.Roles AS r
WHERE r.NombreRol NOT IN (N'webmaster', N'gestor_interno', N'externo');

/*
    ESTAS DOS COMPROBACIONES TAMBIEN VAN EN EXEC(N'...'), Y POR LA MISMA RAZON QUE LAS
    RESTRICCIONES DE LA SECCION 5 -- que este guion ya explica en :381-386 y que aqui se le
    habia olvidado aplicar.

    `r.VersionPermisos` se anade a dbo.Roles en la seccion 8 y `Identificacion` a dbo.Usuarios en
    la seccion 5, las dos en ESTE MISMO LOTE. SQL Server enlaza las columnas de una tabla que YA
    EXISTE al COMPILAR el lote, no al ejecutarlo: nombrarlas estaticamente aborta con Msg 207
    (Invalid column name) ANTES de que el ALTER haya corrido, y entonces no se crea NADA, ni
    siquiera la seccion 1. Verificado ejecutando el guion contra una base de ensayo.
*/
EXEC(N'
SELECT N''reparto_de_permisos'' AS Comprobacion, r.NombreRol,
       COUNT(rp.IdPermiso) AS Permisos, r.VersionPermisos
FROM dbo.Roles AS r
LEFT JOIN dbo.RolesPermisos AS rp ON rp.IdRol = r.IdRol
GROUP BY r.NombreRol, r.VersionPermisos
ORDER BY r.NombreRol;');

/* Permisos catalogados que no concede nadie. No es un error, pero conviene mirarlos. */
SELECT N'permisos_sin_conceder' AS Comprobacion, p.CodigoPermiso
FROM dbo.Permisos AS p
WHERE NOT EXISTS (SELECT 1 FROM dbo.RolesPermisos AS rp WHERE rp.IdPermiso = p.IdPermiso);

/* Documentos de identidad guardados. Tras aplicar este guion sobre PNMC_LOCAL debe ser 0. */
EXEC(N'
SELECT N''usuarios_con_documento'' AS Comprobacion, COUNT(*) AS Filas
FROM dbo.Usuarios
WHERE Identificacion IS NOT NULL;');




-- ==========================================================================================
-- PARTE B - COMPROBACIONES - SOLO LECTURA
--
-- Ni un INSERT, ni un UPDATE, ni un DELETE, ni un DDL. Se puede ejecutar contra PNMC_LOCAL sin
-- riesgo. Cuando la PARTE A se promueva a pnmc-database/schema/, ESTA PARTE NO VIAJA CON ELLA:
-- su sitio es pnmc-database/scripts/validar_*.sql, junto a los cuatro validadores que ya hay.
-- ==========================================================================================


-- B1 - La geografia de PNMC, que es lo que sostiene toda la SECCION 1.
--      Esperado el 24 ago 2026: 1122 filas, 33 departamentos, minimo '05', maximo '99',
--      MunicipioVacio = 0 y FilasDeNivelDepartamento = 0.
--      LA COLUMNA SE LLAMABA MunicipioNuloOVacio Y MEDIA UNA TAUTOLOGIA (C7): CodigoMunicipio
--      es la SEGUNDA columna de PK_Divipola, luego es NOT NULL por construccion y la mitad
--      "IS NULL" del SUM no podia dar otra cosa que cero. Citar ese cero como un hecho sobre el
--      CONTENIDO de la tabla era enganoso: es un hecho sobre su clave primaria. Queda solo la
--      mitad que si mide algo, la cadena vacia, que la PK no impide.
--      FilasDeNivelDepartamento es una heuristica: busca el patron habitual de "fila cabecera"
--      (codigo de municipio terminado en 000). Si algun dia devuelve algo distinto de cero,
--      alguien anadio departamentos a Divipola y la SECCION 1 hay que releerla entera.
SELECT  Comprobacion             = N'B1 - dbo.Divipola',
        Filas                    = COUNT(*),
        DepartamentosDistintos   = COUNT(DISTINCT CodigoDepartamento),
        CodigoDeptoMinimo        = MIN(CodigoDepartamento),
        CodigoDeptoMaximo        = MAX(CodigoDepartamento),
        MunicipioVacio           = SUM(CASE WHEN LTRIM(RTRIM(CodigoMunicipio)) = N''
                                            THEN 1 ELSE 0 END),
        FilasDeNivelDepartamento = SUM(CASE WHEN RIGHT(CodigoMunicipio, 3) = '000'
                                            THEN 1 ELSE 0 END)
FROM dbo.Divipola;


-- B2 - Las foraneas a dbo.Divipola QUE YA EXISTEN.
--      Esperado ANTES de aplicar 02: 9 filas. DESPUES de aplicar 02: 12 -las nueve mas
--      FK_VersionesFestivalLocalizaciones_Divipola, FK_VersionesFestival_Divipola y
--      FK_PropuestasCambioFestival_Divipola-. En los dos casos, todas con NoFiable = 0 y
--      Deshabilitada = 0, que es lo unico que hay que mirar de verdad.
--      LA CIFRA VA EN DOS TIEMPOS A PROPOSITO (C2): esta comprobacion se escribio para
--      promoverse a pnmc-database/scripts/validar_*.sql, y un validador cuyo numero esperado
--      caduca en cuanto corre el guion hermano acaba borrado por ruidoso. Lo que no caduca son
--      las dos ultimas columnas.
SELECT  Comprobacion  = N'B2 - foraneas entrantes a dbo.Divipola',
        Foranea       = fk.name,
        Tabla         = OBJECT_NAME(fk.parent_object_id),
        -- STRING_AGG y no FOR XML: sqlcmd ejecuta con QUOTED_IDENTIFIER OFF, y bajo esa
        -- opcion los metodos del tipo XML fallan con el error 1934. Comprobado hoy contra
        -- PNMC_LOCAL a traves del mismo camino que usa scripts/seed-local-db.sh.
        Columnas      = (SELECT STRING_AGG(c.name, N', ')
                                    WITHIN GROUP (ORDER BY fkc.constraint_column_id)
                         FROM sys.foreign_key_columns fkc
                         JOIN sys.columns c
                           ON c.object_id = fkc.parent_object_id
                          AND c.column_id = fkc.parent_column_id
                         WHERE fkc.constraint_object_id = fk.object_id),
        NoFiable      = fk.is_not_trusted,
        Deshabilitada = fk.is_disabled
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.Divipola')
ORDER BY OBJECT_NAME(fk.parent_object_id);


-- B3 - Censo de las tablas que llevan el par departamento/municipio, con su tipo y si tienen
--      foranea.
--      Esperado ANTES de 02: 10 tablas -8 con char(2)/char(5) y foranea, y 2
--      (VersionesFestival, PropuestasCambioFestival) con nvarchar(20) y SIN foranea-.
--      Esperado DESPUES de 02: 11 tablas y ForaneaADivipola = 'si' en TODAS. Las dos rezagadas
--      pasan a char(2)/char(5) con foranea compuesta en 02 §F, y aparece la undecima,
--      VersionesFestivalLocalizaciones, que nace ya con las dos cosas.
--      ESTA ES LA COMPROBACION QUE IMPORTA, y su forma buena no es un numero: es que la columna
--      ForaneaADivipola no diga 'NO' en ninguna fila.
SELECT  Comprobacion = N'B3 - tablas con el par territorial',
        Tabla        = t.name,
        TipoDepartamento = td.name + N'(' + CAST(CASE WHEN td.name LIKE N'n%char'
                                                      THEN cd.max_length / 2
                                                      ELSE cd.max_length END AS nvarchar(10)) + N')',
        TipoMunicipio    = tm.name + N'(' + CAST(CASE WHEN tm.name LIKE N'n%char'
                                                      THEN cm.max_length / 2
                                                      ELSE cm.max_length END AS nvarchar(10)) + N')',
        ForaneaADivipola = CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys fk
                                             WHERE fk.parent_object_id = t.object_id
                                               AND fk.referenced_object_id = OBJECT_ID(N'dbo.Divipola'))
                                THEN N'si' ELSE N'NO' END
FROM sys.tables t
JOIN sys.columns cd ON cd.object_id = t.object_id AND cd.name = N'CodigoDepartamento'
JOIN sys.columns cm ON cm.object_id = t.object_id AND cm.name = N'CodigoMunicipio'
JOIN sys.types   td ON td.user_type_id = cd.user_type_id
JOIN sys.types   tm ON tm.user_type_id = cm.user_type_id
WHERE t.name <> N'Divipola'
ORDER BY ForaneaADivipola DESC, t.name;


-- B4 - Por que todo CHECK de minusculas necesita COLLATE, incluido el que crea 01.
--      Esperado: EsNoOperativoBajoCI = 'SI' y ConCollateSiFunciona = 'SI'. Es el hallazgo al
--      que llegaron por separado 01 y 03: bajo colacion CI, "Codigo = lower(Codigo)" es cierto
--      siempre, de modo que el CHECK solo prohibe espacios. Si el primero sale 'no', la base
--      cambio de colacion y hay que releer el asunto entero.
SELECT  Comprobacion          = N'B4 - el CHECK "= lower()" bajo colacion CI',
        ColacionBaseDatos     = CAST(DATABASEPROPERTYEX(DB_NAME(), N'Collation') AS nvarchar(100)),
        EsNoOperativoBajoCI   = CASE WHEN N'CC' = LOWER(N'CC') THEN N'SI' ELSE N'no' END,
        ConCollateSiFunciona  = CASE WHEN N'CC' COLLATE SQL_Latin1_General_CP1_CS_AS
                                          = LOWER(N'CC') COLLATE SQL_Latin1_General_CP1_CS_AS
                                     THEN N'no' ELSE N'SI' END;

-- B4 bis - Los CHECK de PNMC afectados por lo anterior, que NINGUN guion de esta tanda
--          corrige, a proposito: son tres tablas ajenas, y arreglarlas dentro del diff de la
--          migracion las esconderia (misma leccion que D11.4).
--          Esperado hoy: tres filas, las tres con LlevaCollate = 'NO' (EstadosContenido,
--          Categorias, TiposRegistroEcosistema). Cuando 01 este aplicado seran cuatro, y la
--          cuarta -CK_TiposDocumento_CodigoEnMinuscula- sera la unica con 'si'.
SELECT  Comprobacion = N'B4 bis - CHECK "_Formato" existentes',
        Tabla        = OBJECT_NAME(cc.parent_object_id),
        Restriccion  = cc.name,
        LlevaCollate = CASE WHEN cc.definition LIKE N'%COLLATE%' THEN N'si' ELSE N'NO' END,
        Definicion   = cc.definition
FROM sys.check_constraints cc
WHERE cc.definition LIKE N'%lower(%'
ORDER BY OBJECT_NAME(cc.parent_object_id);


-- B5 - Que el catalogo de tipos de documento lo entrega 01, y lo entrega UNA sola vez.
--      Esperado ANTES de aplicar 01: las seis primeras a 'no'. DESPUES de aplicar 01: las seis
--      primeras a 'si'. La ultima -el catalogo duplicado que este guion decidio no crear- debe
--      salir 'no' SIEMPRE; si sale 'SI', hay dos catalogos de tipo de documento en la misma
--      base y hay que borrar uno antes de que alguien escriba en los dos.
SELECT Comprobacion = N'B5 - lo que entrega 01', Objeto = N'tabla dbo.TiposDocumento',
       Existe = CASE WHEN OBJECT_ID(N'dbo.TiposDocumento', N'U') IS NOT NULL THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - lo que entrega 01', N'Usuarios.Identificacion',
       CASE WHEN COL_LENGTH(N'dbo.Usuarios', N'Identificacion') IS NOT NULL THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - lo que entrega 01', N'Usuarios.CodigoTipoDocumento',
       CASE WHEN COL_LENGTH(N'dbo.Usuarios', N'CodigoTipoDocumento') IS NOT NULL THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - lo que entrega 01', N'FK_Usuarios_TiposDocumento',
       CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                         WHERE name = N'FK_Usuarios_TiposDocumento') THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - lo que entrega 01', N'CK_Usuarios_Documento_Completo',
       CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints
                         WHERE name = N'CK_Usuarios_Documento_Completo') THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - lo que entrega 01', N'UQ_Usuarios_Documento (indice filtrado)',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Usuarios_Documento'
                           AND object_id = OBJECT_ID(N'dbo.Usuarios')) THEN N'si' ELSE N'no' END
UNION ALL SELECT N'B5 - DUPLICADO QUE NO DEBE EXISTIR', N'dbo.TiposDocumentoIdentidad (el que 03 NO crea)',
       CASE WHEN OBJECT_ID(N'dbo.TiposDocumentoIdentidad', N'U') IS NOT NULL THEN N'SI' ELSE N'no' END;


-- B6 - Comprobacion NEGATIVA: lo que este guion decidio NO crear sigue sin existir.
--      Esperado: las tres con Existe = 'no'. Si alguna sale 'SI', alguien la creo por otra via
--      y hay que averiguar por que antes de seguir. Los nombres son los que proponen las
--      secciones 2, 4 y 5; una tabla con otro nombre y el mismo proposito no la detecta esto.
SELECT Comprobacion = N'B6 - lo que NO se crea', Objeto = N'EquivalenciasMigracionSimus (SECCION 4)',
       Existe = CASE WHEN OBJECT_ID(N'dbo.EquivalenciasMigracionSimus', N'U') IS NOT NULL THEN N'SI' ELSE N'no' END
UNION ALL SELECT N'B6 - lo que NO se crea', N'catalogo propio de estados de solicitud (SECCION 2)',
       CASE WHEN OBJECT_ID(N'dbo.EstadosSolicitud', N'U') IS NOT NULL THEN N'SI' ELSE N'no' END
UNION ALL SELECT N'B6 - lo que NO se crea', N'usuario de web service (SECCION 5)',
       CASE WHEN OBJECT_ID(N'dbo.UsuariosServicioWeb', N'U') IS NOT NULL THEN N'SI' ELSE N'no' END;


-- B7 - El riesgo de resiembra, que NO lo introduce SIMUS: ya estaba ahi.
--      pnmc-database/seed/V20260519_02__divipola_seed.sql:7 hace DELETE FROM dbo.Divipola, y
--      scripts/seed-local-db.sh no borra nada antes de aplicar las semillas. Si el total de
--      esta consulta es mayor que cero, volver a ejecutar la siembra sobre esta base FALLA por
--      las foraneas de B2. No es una prediccion: es lo que dicen esas tres piezas juntas.
SELECT      Comprobacion = N'B7 - filas que referencian dbo.Divipola hoy',
            Tabla = N'Agenda',               Filas = COUNT(*) FROM dbo.Agenda               WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'Entidades',              COUNT(*) FROM dbo.Entidades            WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'EscuelasMusica',         COUNT(*) FROM dbo.EscuelasMusica       WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'Festivales',             COUNT(*) FROM dbo.Festivales           WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'Lutieres',               COUNT(*) FROM dbo.Lutieres             WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'MercadosMusicales',      COUNT(*) FROM dbo.MercadosMusicales    WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'RedesDocumentacion',     COUNT(*) FROM dbo.RedesDocumentacion   WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'RegistrosEcosistema',    COUNT(*) FROM dbo.RegistrosEcosistema  WHERE CodigoMunicipio IS NOT NULL
UNION ALL SELECT N'B7 - filas que referencian dbo.Divipola hoy', N'MetricasMunicipioMapa',  COUNT(*) FROM dbo.MetricasMunicipioMapa;


-- B8 - La medicion que decidio la SECCION 3: a que apuntan las foraneas de PNMC cuando el
--      destino es un catalogo con codigo. Esperado: 6 filas hacia CodigoEstado (Festivales,
--      Entidades, EscuelasMusica, MercadosMusicales, RedesDocumentacion, Lutieres) y 3 hacia
--      IdEstadoContenido (Noticias, AlbumesGaleria, Agenda). El patron dominante en los
--      modulos es EL CODIGO, y por eso la clave natural de 01 es la buena.
SELECT  Comprobacion  = N'B8 - foraneas a dbo.EstadosContenido',
        Foranea       = fk.name,
        ColumnaOrigen = cp.name,
        ApuntaA       = cr.name,
        Clase         = CASE WHEN cr.name = N'CodigoEstado' THEN N'clave natural (codigo)'
                             ELSE N'clave sustituta (id)' END
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.EstadosContenido')
ORDER BY Clase, OBJECT_NAME(fk.parent_object_id);
