/* =================================================================================================
   LA DESCRIPCION DEL MATERIAL MULTIMEDIA, Y EL ROL QUE DEJA DE SER OBLIGATORIO
   =================================================================================================

   QUE CORRIGE. `ART_MUS_MATERIALMULTIMEDIA` del volcado de SIMUS tiene exactamente cuatro columnas:
   ID, ID_VERSION, URL_ARCHIVO y DESCRIPCION_ARCHIVO. Nuestra `dbo.VersionesFestivalArchivos` copio
   la URL y NO copio la descripcion, y a cambio anadio una columna que el volcado no tiene:
   `RolArchivo NOT NULL`, con tres valores inventados —afiche, programa, logo— que el formulario
   ofrecia en un desplegable rotulado «Que es».

   POR QUE IMPORTA. El criterio es que los nombres de los
   campos no se cambien: «a los nombres de los campos no los cambies como vi que hiciste». Un
   desplegable de tres opciones inventadas en lugar de un campo de texto libre no es un cambio de
   rotulo: es un campo distinto, con menos capacidad. «Afiche del dia 3, version en alta» no cabe en
   ninguna de las tres opciones.

   QUE HACE ESTE GUION, y por que no borra nada.

   1. Anade `DescripcionArchivo nvarchar(250) NULL`, con el mismo tamano que `DESCRIPCION_ARCHIVO`
      del volcado.

   2. Le pone a `RolArchivo` un valor por omision. NO SE BORRA LA COLUMNA: hay filas escritas con
      ella y `ArchivoId`/`Url` cuelgan del mismo CHECK; quitarla obligaria a reconstruir la tabla
      entera para ganar nada. Con el DEFAULT, el canal externo deja de estar obligado a mandar un
      valor de una lista que no existe en el modelo de origen, y las filas que ya tienen rol lo
      conservan.

   ES IDEMPOTENTE. Los dos bloques comprueban antes de escribir, como el resto de los guiones de
   `pnmc-database/schema/`: `seed-local-db.sh` los aplica en cada arranque con `set -e`, asi que un
   guion que falle al repetirse deja la base a medias.
   ================================================================================================= */

SET NOCOUNT ON;
GO

/* ------------------------------------------------------------------------------------------------
   1. La descripcion del archivo
   ------------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.VersionesFestivalArchivos', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VersionesFestivalArchivos', N'DescripcionArchivo') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestivalArchivos
        ADD DescripcionArchivo nvarchar(250) NULL;

    PRINT 'V20260828_03: dbo.VersionesFestivalArchivos.DescripcionArchivo anadida.';
END
ELSE
BEGIN
    PRINT 'V20260828_03: dbo.VersionesFestivalArchivos.DescripcionArchivo ya existia.';
END
GO

/* ------------------------------------------------------------------------------------------------
   2. El rol deja de ser obligatorio de facto

   SE BUSCA LA RESTRICCION POR CATALOGO y no por nombre a mano: una restriccion DEFAULT creada sin
   nombre explicito recibe uno generado por SQL Server, distinto en cada base, asi que
   `IF OBJECT_ID('DF_...')` daria falso negativo en unas y positivo en otras.
   ------------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.VersionesFestivalArchivos', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.VersionesFestivalArchivos', N'RolArchivo') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints AS dc
        JOIN sys.columns AS c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.VersionesFestivalArchivos')
          AND c.name = N'RolArchivo')
BEGIN
    ALTER TABLE dbo.VersionesFestivalArchivos
        ADD CONSTRAINT DF_VersionesFestivalArchivos_RolArchivo
        DEFAULT (N'material') FOR RolArchivo;

    PRINT 'V20260828_03: DF_VersionesFestivalArchivos_RolArchivo anadida.';
END
ELSE
BEGIN
    PRINT 'V20260828_03: RolArchivo ya tenia valor por omision, o la columna no existe.';
END
GO
