/*
  SIMUS · Los tipos de documento se leen con su ortografía

  EL DEFECTO. `dbo.TiposDocumento` guarda
  «Cedula de ciudadania», «Cedula de extranjeria» y «Numero unico de identificacion personal», sin
  una sola tilde. Como nadie leía la tabla, el alta externa llevaba las ocho parejas escritas a mano
  CON tildes, anotadas en el código como copia y con la instrucción de borrarlas «el día que la
  tabla se mapee». Ese día llegó: el perfil de una cuenta administrativa necesita el mismo
  vocabulario, y una tercera copia era inaceptable.

  SE CORRIGE EL DATO Y NO SE TRADUCE EN PANTALLA. Lo cómodo era dejar la tabla como está y seguir
  arreglando la ortografía en cada sitio que la muestre; eso es exactamente lo que produjo la copia.
  El nombre es lo que una persona lee en un desplegable, y «Cedula» escrito así es una falta de
  ortografía en pantalla.

  LOS CODIGOS NO SE TOCAN. Son lo que se guarda en `Usuarios.CodigoTipoDocumento` y en
  `EntidadesResponsable`, y cambiarlos rompería las filas que ya existen.

  ES ADITIVA EN EL SENTIDO QUE IMPORTA: no crea ni borra filas, no cambia claves y no altera ninguna
  estructura. Solo corrige el texto que se lee.
*/

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Cédula de ciudadanía'
WHERE CodigoTipoDocumento = N'cc' AND NombreTipoDocumento <> N'Cédula de ciudadanía';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Cédula de extranjería'
WHERE CodigoTipoDocumento = N'ce' AND NombreTipoDocumento <> N'Cédula de extranjería';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Tarjeta de identidad'
WHERE CodigoTipoDocumento = N'ti' AND NombreTipoDocumento <> N'Tarjeta de identidad';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Registro civil de nacimiento'
WHERE CodigoTipoDocumento = N'rc' AND NombreTipoDocumento <> N'Registro civil de nacimiento';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Número único de identificación personal'
WHERE CodigoTipoDocumento = N'nuip' AND NombreTipoDocumento <> N'Número único de identificación personal';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Pasaporte'
WHERE CodigoTipoDocumento = N'pa' AND NombreTipoDocumento <> N'Pasaporte';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Permiso especial de permanencia'
WHERE CodigoTipoDocumento = N'pep' AND NombreTipoDocumento <> N'Permiso especial de permanencia';

UPDATE dbo.TiposDocumento SET NombreTipoDocumento = N'Permiso por protección temporal'
WHERE CodigoTipoDocumento = N'ppt' AND NombreTipoDocumento <> N'Permiso por protección temporal';
GO
