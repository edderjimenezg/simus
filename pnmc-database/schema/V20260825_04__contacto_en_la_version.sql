/*
    PNMC · Ecosistema · La ficha publica lleva su contacto

    QUE HACE
    --------
    Lleva el bloque de contacto y de organizador a `VersionesFestival` y a
    `PropuestasCambioFestival`: telefono, Instagram, Facebook, sitio web, otro enlace, nombre del
    director y tipo de organizador.

    POR QUE NO BASTA CON QUE ESTEN EN LA CABECERA
    ---------------------------------------------
    `Festivales` ya tiene telefono, Instagram, Facebook y sitio web, y la ficha publica los sacaba
    de ahi. El problema no es que falte el dato: es que **la cabecera es el presente y la version es
    lo publicado**, y son dos cosas distintas.

    Todo lo demas de la ficha —nombre, descripcion, cobertura, territorio, periodicidad, correo—
    ya viaja en la version, precisamente para que editar un festival no cambie lo que el publico
    ve hasta que la edicion se apruebe. El contacto se habia quedado fuera de esa regla, asi que
    cambiar un telefono en el borrador lo cambiaba en la ficha publicada de inmediato, saltandose
    el circuito de revision entero. No era un agujero de seguridad; era una excepcion silenciosa a
    la regla que sostiene todo el modelo de versiones.

    `LecturaFestivalesPublicados.cs` lo dejaba escrito con un `??`:

        version?.CorreoContacto ?? festival.ContactEmail

    Ese respaldo tiene sentido para un festival historico que todavia no tiene ninguna version —y
    se conserva por eso—; no lo tiene para uno versionado, donde manda la version.

    POR QUE TAMBIEN EN `PropuestasCambioFestival`
    ---------------------------------------------
    Una propuesta es el borrador de la proxima version. Si el contacto no viaja en ella, el
    circuito puede publicar una version nueva que pierde el contacto de la anterior, o que no
    puede cambiarlo: proponer un cambio de telefono seria imposible por el camino normal.

    `TipoOrganizadorId` ES ANULABLE Y SU CATALOGO NACE VACIO
    --------------------------------------------------------
    `TiposOrganizador` llego con la estructura de SIMUS y sin filas, por decision expresa: el
    contenido de los catalogos se decide en PNMC, informado por SIMUS, no copiado de SIMUS
    (V20260824_02). Una foranea anulable contra un catalogo vacio es correcta —nadie puede elegir
    todavia— y deja de serlo el dia en que el catalogo se llene.

    ESTE FICHERO TIENE QUE ESTAR EN LA LISTA `SCHEMAS` DE scripts/seed-local-db.sh.
*/
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------------------
-- 1. VersionesFestival: lo que el publico ve
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.VersionesFestival', N'TelefonoContacto') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD TelefonoContacto nvarchar(80) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'Instagram') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD Instagram nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'Facebook') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD Facebook nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'SitioWeb') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD SitioWeb nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'OtroEnlace') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD OtroEnlace nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'Director') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD Director nvarchar(240) NULL;
END;
GO

IF COL_LENGTH(N'dbo.VersionesFestival', N'TipoOrganizadorId') IS NULL
BEGIN
    ALTER TABLE dbo.VersionesFestival ADD TipoOrganizadorId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.VersionesFestival', N'U')
                 AND name = N'FK_VersionesFestival_TipoOrganizador')
BEGIN
    ALTER TABLE dbo.VersionesFestival
        ADD CONSTRAINT FK_VersionesFestival_TipoOrganizador
            FOREIGN KEY (TipoOrganizadorId) REFERENCES dbo.TiposOrganizador (IdTipoOrganizador);
END;
GO

-- ---------------------------------------------------------------------------------------
-- 2. PropuestasCambioFestival: el borrador de la proxima version
-- ---------------------------------------------------------------------------------------

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'TelefonoContacto') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD TelefonoContacto nvarchar(80) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'Instagram') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD Instagram nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'Facebook') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD Facebook nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'SitioWeb') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD SitioWeb nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'OtroEnlace') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD OtroEnlace nvarchar(500) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'Director') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD Director nvarchar(240) NULL;
END;
GO

IF COL_LENGTH(N'dbo.PropuestasCambioFestival', N'TipoOrganizadorId') IS NULL
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival ADD TipoOrganizadorId int NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys
               WHERE parent_object_id = OBJECT_ID(N'dbo.PropuestasCambioFestival', N'U')
                 AND name = N'FK_PropuestasCambioFestival_TipoOrganizador')
BEGIN
    ALTER TABLE dbo.PropuestasCambioFestival
        ADD CONSTRAINT FK_PropuestasCambioFestival_TipoOrganizador
            FOREIGN KEY (TipoOrganizadorId) REFERENCES dbo.TiposOrganizador (IdTipoOrganizador);
END;
GO
