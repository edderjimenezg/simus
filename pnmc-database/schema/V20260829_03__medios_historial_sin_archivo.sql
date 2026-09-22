/* =================================================================================================
   EL HISTORIAL DE IMAGENES DEJA DE GUARDAR EL ARCHIVO
   =================================================================================================

   CRITERIO: «No guardes el original, siempre
   se remplaza, y si se remplaza la imagen, desaparece, no se guarda».

   QUE CAMBIA. dbo.MediosWebHistorial pierde la columna `Contenido varbinary(max)` y con ella el
   CHECK que la acompanaba. Se conserva TODO lo demas: quien, cuando, que accion, y las senas del
   archivo que estuvo ahi —tipo, bytes, ancho, alto y huella—. El registro de lo que paso sigue
   entero; lo que desaparece es el archivo.

   LA CONSECUENCIA, DICHA SIN ADORNAR: no hay «restaurar». Reemplazar una imagen borra la anterior
   para siempre. Si alguien quiere volver atras, tiene que volver a subir el archivo desde su
   computador. La ruta POST /{clave}/restore/{idEntrada} se retira en el mismo cambio, porque una
   ruta que no puede cumplir lo que promete es peor que no tenerla.

   LO QUE SE GANA, CON EL NUMERO. El guion anterior estimaba para el historial 16 claves x 6
   entradas x 2 MiB = 192 MiB de techo y unos 24 MB realistas. Con esta columna fuera, el historial
   pasa a costar unos 200 bytes por entrada: 45 claves x 6 entradas son ~54 KB. Es tres ordenes de
   magnitud menos, y desaparece la razon principal por la que el tope de la poda importaba.

   POR QUE UN GUION APARTE Y NO EDITAR EL ANTERIOR. V20260829_01 ya se aplico a la base y el
   migrador (DbUp) lleva la cuenta de los guiones ejecutados: editar uno aplicado no vuelve a
   correr y deja la base y el fichero diciendo cosas distintas. La regla del repositorio es que un
   guion aplicado no se toca.

   ES IDEMPOTENTE.
   ================================================================================================= */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* EL CHECK VA PRIMERO. `CK_MediosWebHistorial_BytesReales` compara DATALENGTH(Contenido) con
   Bytes; con la columna fuera, la restriccion no puede evaluarse y SQL Server rechaza el DROP
   COLUMN mientras exista. El orden importa y no es intercambiable. */
IF OBJECT_ID(N'CK_MediosWebHistorial_BytesReales', N'C') IS NOT NULL
BEGIN
    ALTER TABLE dbo.MediosWebHistorial DROP CONSTRAINT CK_MediosWebHistorial_BytesReales;
    PRINT 'V20260829_03: CK_MediosWebHistorial_BytesReales retirada.';
END
GO

/* COL_LENGTH devuelve NULL tanto si falta la columna como si falta la TABLA. Aqui se comprueban
   las dos por separado: sin el OBJECT_ID, este guion reventaria en una base donde
   V20260829_01 todavia no hubiera corrido. Es la trampa que V20260823_02 dejo documentada. */
IF OBJECT_ID(N'dbo.MediosWebHistorial', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.MediosWebHistorial', N'Contenido') IS NOT NULL
BEGIN
    ALTER TABLE dbo.MediosWebHistorial DROP COLUMN Contenido;
    PRINT 'V20260829_03: dbo.MediosWebHistorial.Contenido retirada. El historial ya no guarda el archivo.';
END
ELSE
BEGIN
    PRINT 'V20260829_03: dbo.MediosWebHistorial.Contenido ya no estaba.';
END
GO
