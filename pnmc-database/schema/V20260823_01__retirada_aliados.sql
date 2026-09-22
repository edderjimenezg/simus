/*
    PNMC - Retirada del concepto de entidad aliada.

    CRITERIO:
    el concepto desaparece. Un aliado hacia exactamente lo mismo que una agrupacion —registrar
    lo suyo, enviarlo a revision, no publicar nunca— pero con rol propio, tablas propias y
    portal propio. Era un quinto tipo de actor con maquinaria duplicada.

    LO QUE ESTE GUION NO TOCA, y conviene tener presente al leerlo porque se parece mucho:
    dbo.Entidades, dbo.UsuariosEntidades y dbo.EntidadesRelaciones. UsuariosEntidades tiene la
    misma forma que UsuariosEntidadesAliadas —tabla puente persona-entidad, columna de rol,
    bandera Activo, unicidad triple— y es el corazon del modelo nuevo, no un resto del viejo.

    las tres tablas tenian cero filas y ningun usuario tenia rol
    aliado_*. La retirada no pierde ni un dato.

    TRES TRAMPAS QUE ESTE GUION SORTEA, y por eso no es un simple DROP TABLE:

    1. HAY UNA CLAVE FORANEA REAL. SolicitudesVinculacionRegistros referencia
       EntidadesAliadas (V20260525_04:25). Un DROP TABLE planeado sin soltarla antes falla.

    2. LOS NOMBRES DE LAS RESTRICCIONES ESTAN FOSILIZADOS. Las tablas nacieron como
       EntidadesColaboradoras / UsuariosEntidadesColaboradoras en V20260525_01 y solo se
       renombraron con sp_rename, que renombra la tabla pero NO sus restricciones. En una
       base construida asi, la FK de la tabla puente todavia se llama
       FK_UsuariosEntidadesColaboradoras_Entidades. Por eso aqui NADA se busca por nombre:
       todo sale de sys.foreign_keys y sys.objects.

    3. EL ORDEN IMPORTA. La hija (UsuariosEntidadesAliadas) lleva la unica FK que apunta a la
       madre (EntidadesAliadas), asi que cae primero.

    IDEMPOTENTE: se puede ejecutar sobre una base que ya no tenga nada de esto.
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @sql nvarchar(max);

/* --- 1. Soltar TODA clave foranea que apunte a las dos tablas, por catalogo --- */

DECLARE @tablasRetiradas TABLE (nombre sysname);
INSERT INTO @tablasRetiradas (nombre)
VALUES (N'EntidadesAliadas'), (N'UsuariosEntidadesAliadas'), (N'SolicitudesAliado'),
       (N'EntidadesColaboradoras'), (N'UsuariosEntidadesColaboradoras');

SELECT @sql = STRING_AGG(
    CAST(N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(padre.schema_id)) + N'.' + QUOTENAME(padre.name)
         + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' AS nvarchar(max)), CHAR(10))
FROM sys.foreign_keys fk
INNER JOIN sys.objects padre ON padre.object_id = fk.parent_object_id
INNER JOIN sys.objects referida ON referida.object_id = fk.referenced_object_id
/*
    EL COLLATE NO ES DECORATIVO, Y EL MOTIVO NO SE VE EN LOCAL.

    Azure SQL guarda los METADATOS del catalogo en SQL_Latin1_General_CP1_CI_AS sin
    importar la intercalacion de la base. Comprobado contra la base de pruebas:

        base de datos               Modern_Spanish_CI_AI   (la fija infra/main.bicep)
        sys.objects.name            SQL_Latin1_General_CP1_CI_AS
        variable tabla (sysname)    Modern_Spanish_CI_AI

    Comparar las dos sin coaccionar da el error 468, «Cannot resolve the collation
    conflict», y detiene la migracion entera dejando la base a medio aplicar: aqui paro
    con 10 de 22 guiones puestos. En el contenedor local no pasa porque la base se crea
    sin intercalacion explicita y hereda la del servidor, asi que las dos coinciden.

    Se coacciona el lado del CATALOGO, no el de la variable: DATABASE_DEFAULT lleva ambos
    a la intercalacion de la base sea cual sea, y por eso funciona igual en las dos partes.
    Poner COLLATE en la variable no arregla nada: ya estaba en la intercalacion de la base.
*/
WHERE referida.name COLLATE DATABASE_DEFAULT IN (SELECT nombre FROM @tablasRetiradas);

IF @sql IS NOT NULL
BEGIN
    PRINT N'Soltando claves foraneas que apuntan al modelo de aliados:';
    PRINT @sql;
    EXEC sp_executesql @sql;
END;

/* --- 2. La columna de ambito de la solicitud de vinculacion pasa a la entidad --- */

/*
    No se borra el dato: se traslada. EntidadAliadaId apuntaba a EntidadesAliadas y la
    columna nueva apunta a dbo.Entidades, que es otra tabla; como EntidadesAliadas tenia cero
    filas, no hay ningun valor que trasladar y la columna nueva nace toda NULL. Si algun dia
    este guion corriera sobre una base con datos, el traslado tendria que decidirse a mano y
    por eso NO se intenta aqui un UPDATE ciego.
*/
IF COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadId') IS NULL
    ALTER TABLE dbo.SolicitudesVinculacionRegistros ADD EntidadId int NULL;
GO

/*
    TODO ESTE BLOQUE VA EN SQL DINAMICO, y no por gusto. SQL Server compila el lote entero
    antes de ejecutar nada, y la resolucion diferida de nombres cubre las TABLAS que no
    existen, no las COLUMNAS de una tabla que si existe. Sobre una base nueva —donde
    V20260525_04 ya crea la tabla con EntidadId— un simple
    `SELECT COUNT(*) ... WHERE EntidadAliadaId IS NOT NULL` dentro de un IF que nunca se
    cumple hace fallar el guion completo con "Invalid column name", aunque la rama sea
    inalcanzable. Comprobado: rompia las cinco pruebas de SQL Server de golpe.
*/
IF COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadAliadaId') IS NOT NULL
BEGIN
    DECLARE @huerfanas int;
    EXEC sp_executesql
        N'SELECT @salida = COUNT(*) FROM dbo.SolicitudesVinculacionRegistros WHERE EntidadAliadaId IS NOT NULL;',
        N'@salida int OUTPUT', @salida = @huerfanas OUTPUT;

    IF @huerfanas > 0
        PRINT N'AVISO: ' + CAST(@huerfanas AS nvarchar(10))
            + N' solicitudes tenian EntidadAliadaId. Su ambito se pierde y hay que reasignarlo a mano.';

    /* Las restricciones DEFAULT tambien impiden soltar la columna, y tampoco se buscan por nombre. */
    DECLARE @sqlDef nvarchar(max);
    SELECT @sqlDef = STRING_AGG(
        CAST(N'ALTER TABLE dbo.SolicitudesVinculacionRegistros DROP CONSTRAINT ' + QUOTENAME(d.name) + N';' AS nvarchar(max)), CHAR(10))
    FROM sys.default_constraints d
    INNER JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE d.parent_object_id = OBJECT_ID(N'dbo.SolicitudesVinculacionRegistros', N'U')
      AND c.name = N'EntidadAliadaId';
    IF @sqlDef IS NOT NULL EXEC sp_executesql @sqlDef;

    EXEC sp_executesql N'ALTER TABLE dbo.SolicitudesVinculacionRegistros DROP COLUMN EntidadAliadaId;';
END;
GO

IF OBJECT_ID(N'dbo.Entidades', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadId') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.SolicitudesVinculacionRegistros', N'U')
          AND name = N'FK_SolicitudesVinculacionRegistros_Entidad')
    ALTER TABLE dbo.SolicitudesVinculacionRegistros
        ADD CONSTRAINT FK_SolicitudesVinculacionRegistros_Entidad
        FOREIGN KEY (EntidadId) REFERENCES dbo.Entidades (IdEntidad);
GO

/* --- 3. La columna espejo del historial de revision --- */

/*
    Siempre fue NULL: ninguno de los cinco sitios que escriben historial la rellenaba. El
    modelo de EF ya no la declara, asi que dejarla haria divergir la base del modelo.
*/
IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'IdEntidadAliada') IS NOT NULL
    ALTER TABLE dbo.RegistrosRevisionHistorial DROP COLUMN IdEntidadAliada;
GO

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'IdEntidadColaboradora') IS NOT NULL
    ALTER TABLE dbo.RegistrosRevisionHistorial DROP COLUMN IdEntidadColaboradora;
GO

/* --- 4. Las tablas: la hija antes que la madre --- */

IF OBJECT_ID(N'dbo.UsuariosEntidadesAliadas', N'U') IS NOT NULL
    DROP TABLE dbo.UsuariosEntidadesAliadas;
IF OBJECT_ID(N'dbo.UsuariosEntidadesColaboradoras', N'U') IS NOT NULL
    DROP TABLE dbo.UsuariosEntidadesColaboradoras;

IF OBJECT_ID(N'dbo.SolicitudesAliado', N'U') IS NOT NULL
    DROP TABLE dbo.SolicitudesAliado;

IF OBJECT_ID(N'dbo.EntidadesAliadas', N'U') IS NOT NULL
    DROP TABLE dbo.EntidadesAliadas;
IF OBJECT_ID(N'dbo.EntidadesColaboradoras', N'U') IS NOT NULL
    DROP TABLE dbo.EntidadesColaboradoras;
GO

/* --- 5. Los tres roles --- */

/*
    REENCAUZAR ANTES DE BORRAR. FK_Usuarios_Roles ata IdRol y la columna es NOT NULL, asi que
    un DELETE sobre un rol con usuarios falla. El dato es cero usuarios aliado_*,
    pero el guion no depende de ese dato: primero manda a "externo" a quien quede —que es el
    rol que le corresponde en el modelo nuevo, porque un aliado hacia lo que hace una
    agrupacion— y solo despues borra.
*/
/*
    ================================================================================================
    GUARDA DE COMPATIBILIDAD — TODO EL BLOQUE HEREDADO, DENTRO DE UNA SOLA CONDICION.
    ================================================================================================

    `dbo.Usuarios.IdRol` ya no existe: la retira la seccion 11 de
    V20260824_01__usuarios_roles_y_permisos.sql.  Este bloque migra nombres de rol antiguos y solo
    tiene sentido mientras la columna exista, es decir, en la PRIMERA construccion de una base o en
    una base antigua sin migrar.  En cualquier pasada posterior no queda nada que migrar.

    TRES DECISIONES, Y NINGUNA ES OBVIA:

    (a) VA DENTRO DE EXEC, no solo detras del IF.  SQL Server enlaza las columnas de una tabla ya
        existente al COMPILAR el lote, no al ejecutarlo: sin el EXEC, el lote entero muere con
        Msg 207 sobre `IdRol` y ni siquiera llega a evaluar el COL_LENGTH que debia protegerlo.

    (b) LOS IDENTIFICADORES DE ROL SE RESUELVEN DENTRO, no fuera.  Una variable del lote de fuera
        no existe dentro de la cadena; la primera version lo intento y SQL Server contesto «Must
        declare the scalar variable "@RolWebmaster"».

    (c) EL DELETE SIGUE PREGUNTANDO POR `Usuarios.IdRol` Y NO POR `dbo.UsuariosRoles`, aunque desde
        esa sea la tabla que manda.  Razon de orden: `UsuariosRoles` la crea
        V20260824_01, que va DESPUES de este fichero, asi que en la primera construccion todavia no
        existe.  Dentro de esta guarda la columna existe por definicion, de modo que preguntar por
        ella es a la vez correcto y suficiente.

    POR QUE HACE FALTA.  El guion debe poder aplicarse tanto sobre una base recien creada
    -donde `IdRol` existe porque este mismo fichero la acaba de crear- como sobre una ya migrada,
    donde la columna ya no esta.  Sin la guarda, el segundo caso muere con Msg 1911 en el indice
    y Msg 207 en los UPDATE.
*/
IF COL_LENGTH(N'dbo.Usuarios', N'IdRol') IS NOT NULL
EXEC(N'DECLARE @RolExterno int = (SELECT TOP 1 IdRol FROM dbo.Roles WHERE NombreRol = N''externo'');

IF @RolExterno IS NOT NULL
BEGIN
    UPDATE usuario
    SET IdRol = @RolExterno,
        CanalAcceso = N''externo''
    FROM dbo.Usuarios usuario
    INNER JOIN dbo.Roles rol ON rol.IdRol = usuario.IdRol
    WHERE rol.NombreRol IN (N''aliado_admin'', N''aliado_editor'', N''aliado_lector'');

    IF @@ROWCOUNT > 0
        PRINT N''AVISO: se reencauzaron '' + CAST(@@ROWCOUNT AS nvarchar(10))
            + N'' usuarios de rol aliado_* al rol externo.'';
END;

DELETE rol
FROM dbo.Roles rol
WHERE rol.NombreRol IN (N''aliado_admin'', N''aliado_editor'', N''aliado_lector'')
  AND NOT EXISTS (SELECT 1 FROM dbo.Usuarios usuario WHERE usuario.IdRol = rol.IdRol);');
GO

/* --- 6. El canal de acceso: dos valores, no tres --- */

/*
    CanalAcceso no tiene CHECK, asi que "aliado" no rompia nada; simplemente dejo de tener
    significado. Se normaliza para que la columna no cuente una historia que ya no existe.
*/
UPDATE dbo.Usuarios SET CanalAcceso = N'externo' WHERE CanalAcceso = N'aliado';
GO

/* --- 7. Comprobacion --- */

/*
    Lo que debe quedar. Si algo de esto no se cumple, el guion no termino su trabajo y hay
    que averiguar por que antes de seguir: fallar aqui es mucho mas barato que descubrirlo
    cuando el modelo de EF no encuentre una columna.
*/
IF EXISTS (SELECT 1 FROM sys.tables WHERE name LIKE N'%Aliad%' OR name LIKE N'%Colaborad%')
    THROW 50001, N'Quedaron tablas del modelo de aliados en sys.tables.', 1;

IF EXISTS (SELECT 1 FROM dbo.Roles WHERE NombreRol LIKE N'aliado[_]%')
    THROW 50002, N'Quedaron roles aliado_* en dbo.Roles.', 1;

IF COL_LENGTH(N'dbo.SolicitudesVinculacionRegistros', N'EntidadAliadaId') IS NOT NULL
    THROW 50003, N'SolicitudesVinculacionRegistros conserva EntidadAliadaId.', 1;

IF COL_LENGTH(N'dbo.RegistrosRevisionHistorial', N'IdEntidadAliada') IS NOT NULL
    THROW 50004, N'RegistrosRevisionHistorial conserva IdEntidadAliada.', 1;

/*
    LO QUE SIGUE AHI Y DEBE SEGUIR: las filas de dbo.Auditoria con
    TablaAfectada = 'SolicitudesAliado'. No tienen clave foranea contra la tabla, sobreviven
    como texto, y son el rastro de que el concepto existio. Borrarlas seria reescribir el
    registro de lo que paso.
*/
PRINT N'Retirada del modelo de aliados completada.';
