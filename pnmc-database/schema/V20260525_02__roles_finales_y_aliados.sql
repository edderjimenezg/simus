/*
    PNMC - Convergencia de roles a la nomenclatura final.

    NOMBRE HISTORICO. El fichero se sigue llamando "roles_finales_y_aliados" porque
    renombrarlo cambiaria el orden de aplicacion y el registro de migraciones ya aplicadas.
    De su contenido original solo queda la mitad de los roles: todo el modelo de entidades
    aliadas —tablas, restricciones, indices y el bloque de convergencia de esquema— se
    retiro, cuando el concepto desaparecio del sistema.

    Lo que hace hoy: sembrar los TRES roles del modelo definitivo y reencauzar a ellos
    cualquier rol heredado que siga vivo en dbo.Usuarios.
*/

MERGE dbo.Roles AS destino
USING (VALUES
    (N'webmaster', N'Control total de usuarios, modulos, datos, configuracion, revision, publicacion y mantenimiento.'),
    (N'gestor_interno', N'Segundo nivel general de administracion institucional.'),
    (N'externo', N'Persona del ecosistema. Es a la vez la persona registrada y el usuario externo que representa a un actor: los distingue el vinculo en UsuariosEntidades, no el rol.')
) AS origen (NombreRol, DescripcionRol)
ON destino.NombreRol = origen.NombreRol
WHEN MATCHED THEN
    UPDATE SET DescripcionRol = origen.DescripcionRol
WHEN NOT MATCHED THEN
    INSERT (NombreRol, DescripcionRol)
    VALUES (origen.NombreRol, origen.DescripcionRol);

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
EXEC(N'DECLARE @RolWebmaster int = (SELECT TOP 1 IdRol FROM dbo.Roles WHERE NombreRol = N''webmaster'');
DECLARE @RolGestorInterno int = (SELECT TOP 1 IdRol FROM dbo.Roles WHERE NombreRol = N''gestor_interno'');
DECLARE @RolExterno int = (SELECT TOP 1 IdRol FROM dbo.Roles WHERE NombreRol = N''externo'');

UPDATE usuario
SET IdRol = CASE rol.NombreRol
    WHEN N''administrador'' THEN @RolWebmaster
    WHEN N''admin'' THEN @RolWebmaster
    WHEN N''editor'' THEN @RolGestorInterno
    WHEN N''lider'' THEN @RolGestorInterno
    WHEN N''lider_de_componente'' THEN @RolGestorInterno
    WHEN N''lider-componente'' THEN @RolGestorInterno
    /* Los tres roles de aliado desaparecieron. Estas ramas NO se
       borran: se reapuntan a @RolExterno. Borrarlas mandaria a esos usuarios al ELSE, donde
       CONSERVARIAN su rol heredado; y el DELETE de mas abajo tampoco lo borraria, porque
       seguiria referenciado. Quedaria vivo un rol que ninguna politica conoce —exactamente
       la clase de rol que la puerta institucional dejaba pasar cuando era lista negra. */
    WHEN N''aliado'' THEN @RolExterno
    WHEN N''gestor'' THEN @RolExterno
    WHEN N''cargador'' THEN @RolExterno
    WHEN N''contributor'' THEN @RolExterno
    WHEN N''usuario_externo'' THEN @RolExterno
    WHEN N''colaborador_admin'' THEN @RolExterno
    WHEN N''colaborador_editor'' THEN @RolExterno
    WHEN N''colaborador_lector'' THEN @RolExterno
    ELSE usuario.IdRol
END
FROM dbo.Usuarios usuario
INNER JOIN dbo.Roles rol ON rol.IdRol = usuario.IdRol
WHERE rol.NombreRol IN (
    N''administrador'', N''admin'', N''editor'', N''lider'', N''lider_de_componente'',
    N''lider-componente'', N''aliado'', N''gestor'', N''cargador'', N''contributor'',
    N''usuario_externo'', N''colaborador_admin'', N''colaborador_editor'', N''colaborador_lector''
);

DELETE rol
FROM dbo.Roles rol
WHERE rol.NombreRol IN (
    N''administrador'', N''admin'', N''editor'', N''lider'', N''lider_de_componente'',
    N''lider-componente'', N''aliado'', N''gestor'', N''cargador'', N''contributor'',
    N''usuario_externo'', N''colaborador_admin'', N''colaborador_editor'', N''colaborador_lector''
)
AND NOT EXISTS (SELECT 1 FROM dbo.Usuarios usuario WHERE usuario.IdRol = rol.IdRol);');

/*
    AQUI TERMINABA ESTA MIGRACION Y EMPEZABAN 280 LINEAS DE MODELO DE ALIADOS: los dos
    sp_rename de EntidadesColaboradoras/UsuariosEntidadesColaboradoras, los CREATE TABLE de
    EntidadesAliadas, UsuariosEntidadesAliadas y SolicitudesAliado, la columna
    RegistrosRevisionHistorial.IdEntidadAliada, y un bloque de convergencia que reparaba las
    dos rutas por las que podia construirse la tabla puente.

    Todo eso se retiro con el concepto. Las bases que ya lo tienen
    lo sueltan con V20260823_01__retirada_aliados.sql, que busca las claves foraneas por
    sys.foreign_keys y no por nombre: sp_rename renombra la tabla pero no sus restricciones,
    asi que en las bases construidas desde V20260525_01 los nombres siguen siendo los de
    "Colaboradoras".
*/
