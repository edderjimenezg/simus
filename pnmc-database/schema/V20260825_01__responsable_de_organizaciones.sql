/*
    PNMC · Ecosistema · Quien responde por una organizacion

    QUE HACE
    --------
    Guarda la PERSONA NATURAL que responde por una organizacion —nombre, tipo y numero de
    documento, correo, telefono, desde cuando y su autorizacion de tratamiento de datos— en un
    satelite 1:1 de `Entidades`. Y le anade a `Entidades` el `TipoIdentificacion` que a
    `NumeroIdentificacion` siempre le falto: un NIT y una cedula de extranjeria no se comparan
    igual.

    ESTE FICHERO CAMBIO DE PROPOSITO EL MISMO DIA QUE SE ESCRIBIO, Y CONVIENE DEJARLO DICHO
    ----------------------------------------------------------------------------------------
    Nacio como `EntidadesAcceso` y traia ademas una credencial propia de la organizacion:
    `CorreoAcceso`, `HashContrasena`, `CorreoVerificado`, `FechaVerificacion` y
    `FechaUltimoAcceso`. La idea era que la organizacion entrara a la plataforma por si misma.

    Al revisar el bloque siguiente se vio que **el modelo que ya existe hace eso, y lo hace
    mejor**: una persona entra con su cuenta, y cada escritura se contrasta contra
    `UsuariosEntidades` —vinculo activo con rol `administrador`— en la base, peticion a peticion.
    Un identificador de organizacion metido en el tiquete de sesion seria una afirmacion que se
    comprueba una vez al entrar; el vinculo se comprueba SIEMPRE. Ademas admite lo que el otro
    modelo no: que una persona administre varias organizaciones, y que una organizacion cambie de
    administrador sin tocar ninguna credencial.

    Asi que la credencial sobraba, y una credencial que sobra no es neutra: un `HashContrasena`
    que nadie escribe nunca, sentado al lado del documento de un ciudadano, es exactamente la
    columna que acaba saliendo en un `SELECT *` el dia que alguien proyecte esta tabla. Se retira.

    Lo que queda es lo unico que ninguna otra tabla guarda hoy: **quien responde**.

    POR QUE SIGUE SIENDO UN SATELITE Y NO COLUMNAS DE `Entidades`
    -------------------------------------------------------------
    Por privacidad, y el motivo no cambia al quitar la credencial — se refuerza. `Entidades` se
    lee en CAMINOS ANONIMOS: la ficha publica de un festival resuelve sus entidades socias por
    `VersionesFestivalEntidadesSocias`. Con estos campos dentro de `Entidades`, cualquier
    `SELECT *`, cualquier `Include` de mas y cualquier DTO que se serialice entero publicaria el
    documento de una persona natural. En un satelite, publicarlo exige escribir un `JOIN` a
    proposito: la fuga deja de ser el resultado de un descuido y pasa a exigir una decision.
    `FichaPublicaSinDatosPersonalesTests` vigila el resto.

    LA TABLA VIEJA SE SUELTA AQUI MISMO
    -----------------------------------
    `EntidadesAcceso` alcanzo a crearse en la base antes de este cambio. No la referencia nada
    —nacio vacia y ningun codigo la escribio nunca—, asi que soltarla es seguro y es lo que hace
    converger cualquier base que ya la tenga. La alternativa —dejarla ahi— seria peor que el
    problema que se esta corrigiendo.

    IDEMPOTENCIA
    ------------
    Todo va guardado: `scripts/seed-local-db.sh` tiene que poder correr dos veces seguidas con
    salida 0.

    COL_LENGTH devuelve NULL tambien cuando la TABLA no existe, no solo cuando falta la columna
    (trampa documentada en V20260823_02): por eso el remiendo de `Entidades` va sobre una tabla
    que V20260521_01 ya garantiza.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------------
-- 0. La tabla de credencial que dejo de tener sentido
-- ---------------------------------------------------------------------------------------

IF OBJECT_ID(N'dbo.EntidadesAcceso', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.EntidadesAcceso;
END;
GO

-- ---------------------------------------------------------------------------------------
-- 1. El tipo de documento que a NumeroIdentificacion siempre le falto
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.Entidades', N'TipoIdentificacion') IS NULL
BEGIN
    ALTER TABLE dbo.Entidades ADD TipoIdentificacion nvarchar(40) NULL;
END;
GO

-- ---------------------------------------------------------------------------------------
-- 2. Quien responde por la organizacion
-- ---------------------------------------------------------------------------------------

IF OBJECT_ID(N'dbo.EntidadesResponsable', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntidadesResponsable
    (
        -- PK y FK a la vez: uno a uno con Entidades, sin identidad propia que mantener.
        IdEntidad                    int NOT NULL,

        -- La persona natural que responde. Dato personal de un tercero: no sale al publico.
        ResponsableNombre            nvarchar(240) NOT NULL,
        ResponsableTipoDocumento     nvarchar(40) NOT NULL,
        ResponsableNumeroDocumento   nvarchar(40) NOT NULL,
        ResponsableCorreo            nvarchar(320) NOT NULL,
        ResponsableTelefono          nvarchar(80) NULL,
        ResponsableDesde             datetime2(0) NOT NULL
            CONSTRAINT DF_EntidadesResponsable_Desde DEFAULT (SYSUTCDATETIME()),

        -- Ley 1581 de 2012: la autorizacion del titular se guarda, no se presume.
        ResponsableAutorizacionDatos bit NOT NULL
            CONSTRAINT DF_EntidadesResponsable_Autorizacion DEFAULT (0),

        FechaCreacion                datetime2(0) NOT NULL
            CONSTRAINT DF_EntidadesResponsable_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion           datetime2(0) NULL,

        CONSTRAINT PK_EntidadesResponsable PRIMARY KEY (IdEntidad),
        CONSTRAINT FK_EntidadesResponsable_Entidades FOREIGN KEY (IdEntidad)
            REFERENCES dbo.Entidades (IdEntidad)
    );
END;
GO

-- Buscar por el correo del responsable no es lo mismo que buscar por el de la entidad: hace
-- falta para atender un derecho del titular (Ley 1581, art. 8) sin recorrer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_EntidadesResponsable_Correo'
                 AND object_id = OBJECT_ID(N'dbo.EntidadesResponsable', N'U'))
BEGIN
    CREATE INDEX IX_EntidadesResponsable_Correo
        ON dbo.EntidadesResponsable (ResponsableCorreo);
END;
GO
