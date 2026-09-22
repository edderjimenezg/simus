/* =================================================================================================
   UN CORREO, UNA ORGANIZACION
   =================================================================================================

   LA REGLA: «una organizacion puede tener
   muchos festivales, pero un correo de una organizacion no puede tener muchas organizaciones, es
   decir un correo debe estar atado a una sola organizacion».

   QUE HABIA. Nada la imponia. `dbo.Entidades.CorreoContacto` no tenia indice unico, y
   `UQ_UsuariosEntidades (IdUsuario, IdEntidad, RolEntidad)` permite explicitamente que un mismo
   usuario quede vinculado a N entidades. La ruta `POST /api/v1/external/organizations/` existia
   justamente para crear la SEGUNDA organizacion de una cuenta.

   QUE HACE ESTE GUION, y que NO hace.

   1. SI: un indice unico filtrado sobre el correo de contacto de la organizacion. Se pudo crear
      porque en la base local hay CERO correos repetidos entre las diecinueve organizaciones,
      

   2. NO: ningun indice que impida que una cuenta responda por varias organizaciones. No se puede
      crear hoy: la base local tiene CINCO cuentas sembradas que ya lo hacen —externo@pnmc.local
      responde por cinco organizaciones, participante.dos por cuatro, participante.tres por tres,
      admin y gestor por dos—. Un indice unico fallaria al aplicarse y dejaria `seed-local-db.sh`
      a medias, que corre con `set -e`. Esa mitad de la regla la impone el servidor, en
      `ExternalOrganizationEndpoints`. Crear la restriccion en base de datos requiere limpiar antes
      las cuentas sembradas que hoy tienen mas de una organizacion.

   EL INDICE ES FILTRADO Y NO UNA RESTRICCION UNIQUE. `CorreoContacto` es anulable, y una
   restriccion UNIQUE en SQL Server trata todos los NULL como iguales entre si: con dos
   organizaciones sin correo, la segunda seria rechazada. `WHERE CorreoContacto IS NOT NULL` deja
   fuera esas filas, que es lo que se quiere: la regla habla de correos, no de su ausencia.

   ES IDEMPOTENTE, como el resto de `pnmc-database/schema/`.
   ================================================================================================= */

/* QUOTED_IDENTIFIER ON ES OBLIGATORIO PARA UN INDICE FILTRADO, y no es un adorno: `sqlcmd` abre la
   sesion con la opcion en OFF, asi que el CREATE INDEX de mas abajo muere con el error 1934 —«CREATE
   INDEX failed because the following SET options have incorrect settings: QUOTED_IDENTIFIER»— y deja
   `seed-local-db.sh`, que corre con `set -e`, a medias.

   NO SE VE DESDE LAS PRUEBAS: la base desechable de la suite se construye por una conexion de
   cliente .NET, que trae la opcion en ON por omision. El guion pasaba en verde y fallaba en la
   maquina. Es la misma leccion de siempre: lo que impone el motor no se deduce leyendo el ORM. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Entidades', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Entidades', N'CorreoContacto') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Entidades')
          AND name = N'UX_Entidades_CorreoContacto')
BEGIN
    /* SI HAY REPETIDOS, NO SE CREA Y SE DICE. Un guion de esquema que revienta deja la base a
       medias; uno que avisa deja el trabajo por hacer a la vista. */
    IF EXISTS (
        SELECT 1 FROM dbo.Entidades
        WHERE CorreoContacto IS NOT NULL
        GROUP BY LOWER(LTRIM(RTRIM(CorreoContacto)))
        HAVING COUNT(*) > 1)
    BEGIN
        PRINT 'V20260828_04: NO se creo UX_Entidades_CorreoContacto: hay correos de contacto repetidos entre organizaciones.';
    END
    ELSE
    BEGIN
        CREATE UNIQUE INDEX UX_Entidades_CorreoContacto
            ON dbo.Entidades (CorreoContacto)
            WHERE CorreoContacto IS NOT NULL;

        PRINT 'V20260828_04: UX_Entidades_CorreoContacto creado.';
    END
END
ELSE
BEGIN
    PRINT 'V20260828_04: UX_Entidades_CorreoContacto ya existia, o la columna no esta.';
END
GO
