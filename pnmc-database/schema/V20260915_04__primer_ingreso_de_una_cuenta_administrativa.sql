/*
  SIMUS · El nombre partido y el recorrido de primer ingreso de una cuenta administrativa

  COMO NACE UNA CUENTA ADMINISTRATIVA, decidido por decisión de producto de
  2026: «ellos no tienen una interfaz como tal para registrarse; a ellos se les registra simplemente
  un correo y una contraseña por defecto. En su primer inicio de sesión les debe pedir cambiar la
  contraseña; una vez cambien la contraseña se les pide completar esos datos básicos —nombre, cédula,
  etcétera— y una vez eso pase ya se le activa el usuario. Los módulos se deciden antes de entregar
  el usuario a la persona: es una creación bastante controlada».

  POR QUE HACEN FALTA LAS DOS MARCAS Y NO UNA. Cambiar la contraseña y completar el perfil son dos
  pasos distintos y se hacen en ese orden: quien cambió la clave pero cerró la ventana antes de
  llenar sus datos NO tiene que volver a cambiarla. Con una sola marca habría que repetir el primer
  paso o dar por bueno el segundo.

  EL NOMBRE, PARTIDO EN CUATRO, IGUAL QUE EN EL ALTA EXTERNA. Quedó definido
  explícitamente: «es importante dejar todos los formularios de nombre con la misma estructura».
  `NombreCompleto` NO se retira: se conserva y se compone a partir de las cuatro partes, porque lo
  leen la bitácora, las fichas y media consola, y porque las cuentas que ya existen solo tienen eso.

  ES ADITIVA. Seis columnas nuevas, ninguna existente se toca. Las cuentas que ya existen quedan con
  las marcas en el valor que describe su situación real: ya usan su contraseña y ya tienen nombre,
  así que no se les pide nada.
*/

IF COL_LENGTH('dbo.Usuarios', 'PrimerNombre') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD
        PrimerNombre nvarchar(80) NULL,
        SegundoNombre nvarchar(80) NULL,
        PrimerApellido nvarchar(80) NULL,
        SegundoApellido nvarchar(80) NULL;
END;
GO

IF COL_LENGTH('dbo.Usuarios', 'DebeCambiarContrasena') IS NULL
BEGIN
    /* Quien recibe la cuenta entra con una contraseña que otra persona eligió: mientras no la
       cambie, esa credencial la conocen dos. La marca arranca en 0 para las cuentas que ya existen
       —llevan meses usando la suya— y el alta la pone en 1 para las nuevas. */
    ALTER TABLE dbo.Usuarios ADD
        DebeCambiarContrasena bit NOT NULL CONSTRAINT DF_Usuarios_DebeCambiarContrasena DEFAULT (0);
END;
GO

IF COL_LENGTH('dbo.Usuarios', 'PerfilCompletado') IS NULL
BEGIN
    /* Una cuenta sin nombre real no permite responder quién hizo qué.

       EL VALOR POR OMISION ES 1, y es deliberado: el recorrido de bienvenida existe porque una
       cuenta se ENTREGA, y eso solo pasa por una vía —el alta de la consola, que la pone en 0
       expresamente—. Todo lo demás que inserta un usuario crea cuentas técnicas que ya tienen
       nombre. Con el valor al revés, cualquier cuenta creada fuera del alta quedaba encerrada sin
       poder ni arreglarlo desde la interfaz. */
    ALTER TABLE dbo.Usuarios ADD
        PerfilCompletado bit NOT NULL CONSTRAINT DF_Usuarios_PerfilCompletado DEFAULT (1);
END;
GO

/* LAS CUENTAS QUE YA EXISTEN NO PASAN POR EL RECORRIDO. Ya usan su contraseña y ya tienen nombre;
   mandarlas a cambiarla y a llenar un formulario sería una interrupción que nadie pidió. Con el
   valor por omisión en 1 esto es redundante para las filas nuevas, y sigue haciendo falta para una
   base donde la columna se hubiera creado antes con otro valor. */
UPDATE dbo.Usuarios
SET PerfilCompletado = 1
WHERE PerfilCompletado = 0 AND LTRIM(RTRIM(ISNULL(NombreCompleto, N''))) <> N'';
GO
