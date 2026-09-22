/*
    PNMC · Dónde ocurre, con quién se hace y qué material tiene: tres tablas, no seis

    QUE ES ESTO
    -----------
    La segunda mitad de la consolidación del circuito de Festivales. La primera —`V20260918_02`—
    dejó en cinco las catorce tablas de vocabulario controlado. Esta se ocupa de las otras seis,
    que no son de catálogo pero guardaban lo mismo dos veces con dos juegos de nombres:

      · `VersionesFestivalLocalizaciones` y `EdicionesFestivalLocalizaciones` eran IDENTICAS.
        Campo por campo, sin una sola diferencia salvo de quién colgaban.

      · `VersionesFestivalEntidadesSocias` y `EdicionesFestivalEntidadesAliadas` guardaban la misma
        entidad con los mismos siete campos, llamándola «socia» en una pantalla y «aliada» en la de
        al lado, y escribiendo el nombre y el correo con dos nombres de columna distintos.

      · `VersionesFestivalArchivos` y `EdicionesFestivalMateriales` guardaban el mismo archivo o
        enlace, con descripción y orden, llamándolo «archivo» aquí y «material» allí.

    LO QUE SE CONSERVA DE CADA UNA
    ------------------------------
    Cuando las dos formas no coincidían, se toma la más completa, porque tener la comprobación en
    una sola de las dos era el descuido y no la decisión:

      · La comprobación de que el municipio pertenece al departamento —`left(CodigoMunicipio,2) =
        CodigoDepartamento`— la tenía solo la de versiones. Ahora vale para las dos.

      · La comprobación de que un archivo es un archivo del banco O un enlace, pero no ninguna de
        las dos cosas, la tenía solo la de versiones. Ahora vale para las dos, y el punto de
        entrada de ediciones descarta antes de escribir los materiales que llegan vacíos, para que
        la base no conteste por el formulario.

      · `RolArchivo` —afiche, programa, logo— existía solo en las versiones. Se conserva, con
        `material` por omisión, que es lo que escriben las ediciones, que no lo preguntan.

      · Los largos de columna se toman del mayor de los dos: el nombre de la entidad admite 300
        caracteres y la URL y la descripción del archivo, mil.

    LAS CLAVES AJENAS QUE SE CONSERVAN
    ----------------------------------
    Todas menos la del dueño, que no puede apuntar a dos tablas: territorio contra `Divipola`, zona
    urbano-rural, titulación colectiva, entidad, naturaleza de entidad y archivo del banco. Es
    importante que la de `Divipola` siga existiendo: hay una prueba de estructura que comprueba que
    NINGUNA tabla con el par territorial se queda sin ella.

    SEGURIDAD
    ---------
    Es idempotente. Copia primero, cuenta después y solo entonces retira; si el recuento no cuadra,
    se detiene y no borra nada.
*/

-- ────────────────────────────────────────────────────────────────────────────────
-- LocalizacionesDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.LocalizacionesDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LocalizacionesDeRegistro (
        IdLocalizacionDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                 nvarchar(40) NOT NULL,
        RegistroId               nvarchar(64) NOT NULL,
        CodigoDepartamento       char(2)      NOT NULL,
        CodigoMunicipio          char(5)      NOT NULL,
        ZonaUrbanoRuralId        int          NULL,
        TitulacionColectivaId    int          NULL,
        FechaCreacion            datetime2(7) NOT NULL
            CONSTRAINT DF_LocalizacionesDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_LocalizacionesDeRegistro PRIMARY KEY CLUSTERED (IdLocalizacionDeRegistro),
        CONSTRAINT UQ_LocalizacionesDeRegistro UNIQUE (ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_LocalizacionesDeRegistro_Divipola
            FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_LocalizacionesDeRegistro_Zona
            FOREIGN KEY (ZonaUrbanoRuralId) REFERENCES dbo.ZonasUrbanoRural (IdZonaUrbanoRural),
        CONSTRAINT FK_LocalizacionesDeRegistro_Titulacion
            FOREIGN KEY (TitulacionColectivaId) REFERENCES dbo.TitulacionesColectivas (IdTitulacionColectiva),
        CONSTRAINT CK_LocalizacionesDeRegistro_Municipio_Departamento
            CHECK (LEFT(CodigoMunicipio, 2) = CodigoDepartamento),
        CONSTRAINT CK_LocalizacionesDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- EntidadesAliadasDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.EntidadesAliadasDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EntidadesAliadasDeRegistro (
        IdEntidadAliadaDeRegistro bigint        IDENTITY(1,1) NOT NULL,
        ModuloId                  nvarchar(40)  NOT NULL,
        RegistroId                nvarchar(64)  NOT NULL,
        NombreEntidadAliada       nvarchar(300) NULL,
        CorreoEntidadAliada       nvarchar(180) NULL,
        NaturalezaEntidadId       int           NULL,
        EntidadId                 int           NULL,
        FechaCreacion             datetime2(7)  NOT NULL
            CONSTRAINT DF_EntidadesAliadasDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_EntidadesAliadasDeRegistro PRIMARY KEY CLUSTERED (IdEntidadAliadaDeRegistro),
        CONSTRAINT FK_EntidadesAliadasDeRegistro_Entidad
            FOREIGN KEY (EntidadId) REFERENCES dbo.Entidades (IdEntidad),
        CONSTRAINT FK_EntidadesAliadasDeRegistro_Naturaleza
            FOREIGN KEY (NaturalezaEntidadId) REFERENCES dbo.NaturalezasEntidad (IdNaturalezaEntidad),
        CONSTRAINT CK_EntidadesAliadasDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- ArchivosDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.ArchivosDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArchivosDeRegistro (
        IdArchivoDeRegistro bigint         IDENTITY(1,1) NOT NULL,
        ModuloId            nvarchar(40)   NOT NULL,
        RegistroId          nvarchar(64)   NOT NULL,
        ArchivoId           int            NULL,
        Url                 nvarchar(1000) NULL,
        RolArchivo          nvarchar(80)   NOT NULL
            CONSTRAINT DF_ArchivosDeRegistro_Rol DEFAULT (N'material'),
        DescripcionArchivo  nvarchar(1000) NULL,
        OrdenVisualizacion  int            NOT NULL,
        FechaCreacion       datetime2(7)   NOT NULL
            CONSTRAINT DF_ArchivosDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ArchivosDeRegistro PRIMARY KEY CLUSTERED (IdArchivoDeRegistro),
        CONSTRAINT UQ_ArchivosDeRegistro UNIQUE (ModuloId, RegistroId, RolArchivo, OrdenVisualizacion),
        CONSTRAINT FK_ArchivosDeRegistro_Archivo
            FOREIGN KEY (ArchivoId) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT CK_ArchivosDeRegistro_Orden CHECK (OrdenVisualizacion > 0),
        CONSTRAINT CK_ArchivosDeRegistro_Destino CHECK (ArchivoId IS NOT NULL OR Url IS NOT NULL),
        CONSTRAINT CK_ArchivosDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ════════════════════════════════════════════════════════════════════════════════
-- La copia.
-- ════════════════════════════════════════════════════════════════════════════════

IF OBJECT_ID('dbo.VersionesFestivalLocalizaciones', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.LocalizacionesDeRegistro
        (ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio, ZonaUrbanoRuralId, TitulacionColectivaId, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId),
           v.CodigoDepartamento, v.CodigoMunicipio, v.ZonaUrbanoRuralId, v.TitulacionColectivaId, v.FechaCreacion
      FROM dbo.VersionesFestivalLocalizaciones v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.LocalizacionesDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.CodigoDepartamento = v.CodigoDepartamento
                          AND n.CodigoMunicipio = v.CodigoMunicipio);

    DECLARE @origenVL int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, CodigoDepartamento, CodigoMunicipio FROM dbo.VersionesFestivalLocalizaciones) x);
    DECLARE @destinoVL int = (SELECT COUNT(*) FROM dbo.LocalizacionesDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_03] VersionesFestivalLocalizaciones: ', @origenVL, ' en origen, ', @destinoVL, ' en destino.');
    IF @destinoVL < @origenVL THROW 50030, 'La copia de VersionesFestivalLocalizaciones no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalLocalizaciones', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.LocalizacionesDeRegistro
        (ModuloId, RegistroId, CodigoDepartamento, CodigoMunicipio, ZonaUrbanoRuralId, TitulacionColectivaId, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId),
           v.CodigoDepartamento, v.CodigoMunicipio, v.ZonaUrbanoRuralId, v.TitulacionColectivaId, v.FechaCreacion
      FROM dbo.EdicionesFestivalLocalizaciones v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.LocalizacionesDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.CodigoDepartamento = v.CodigoDepartamento
                          AND n.CodigoMunicipio = v.CodigoMunicipio);

    DECLARE @origenEL int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, CodigoDepartamento, CodigoMunicipio FROM dbo.EdicionesFestivalLocalizaciones) x);
    DECLARE @destinoEL int = (SELECT COUNT(*) FROM dbo.LocalizacionesDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_03] EdicionesFestivalLocalizaciones: ', @origenEL, ' en origen, ', @destinoEL, ' en destino.');
    IF @destinoEL < @origenEL THROW 50030, 'La copia de EdicionesFestivalLocalizaciones no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalEntidadesSocias', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.EntidadesAliadasDeRegistro
        (ModuloId, RegistroId, NombreEntidadAliada, CorreoEntidadAliada, NaturalezaEntidadId, EntidadId, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId),
           v.NombreEntidadSocia, v.CorreoEntidadSocia, v.NaturalezaEntidadId, v.EntidadId, v.FechaCreacion
      FROM dbo.VersionesFestivalEntidadesSocias v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.EntidadesAliadasDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND ISNULL(n.NombreEntidadAliada, N'') = ISNULL(v.NombreEntidadSocia, N'')
                          AND n.FechaCreacion = v.FechaCreacion);

    DECLARE @origenVS int = (SELECT COUNT(*) FROM dbo.VersionesFestivalEntidadesSocias);
    DECLARE @destinoVS int = (SELECT COUNT(*) FROM dbo.EntidadesAliadasDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_03] VersionesFestivalEntidadesSocias: ', @origenVS, ' en origen, ', @destinoVS, ' en destino.');
    IF @destinoVS < @origenVS THROW 50030, 'La copia de VersionesFestivalEntidadesSocias no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalEntidadesAliadas', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.EntidadesAliadasDeRegistro
        (ModuloId, RegistroId, NombreEntidadAliada, CorreoEntidadAliada, NaturalezaEntidadId, EntidadId, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId),
           v.NombreEntidadAliada, v.CorreoEntidadAliada, v.NaturalezaEntidadId, v.EntidadId, v.FechaCreacion
      FROM dbo.EdicionesFestivalEntidadesAliadas v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.EntidadesAliadasDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND ISNULL(n.NombreEntidadAliada, N'') = ISNULL(v.NombreEntidadAliada, N'')
                          AND n.FechaCreacion = v.FechaCreacion);

    DECLARE @origenEA int = (SELECT COUNT(*) FROM dbo.EdicionesFestivalEntidadesAliadas);
    DECLARE @destinoEA int = (SELECT COUNT(*) FROM dbo.EntidadesAliadasDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_03] EdicionesFestivalEntidadesAliadas: ', @origenEA, ' en origen, ', @destinoEA, ' en destino.');
    IF @destinoEA < @origenEA THROW 50030, 'La copia de EdicionesFestivalEntidadesAliadas no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalArchivos', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ArchivosDeRegistro
        (ModuloId, RegistroId, ArchivoId, Url, RolArchivo, DescripcionArchivo, OrdenVisualizacion, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId),
           v.ArchivoId, v.Url, v.RolArchivo, v.DescripcionArchivo, v.OrdenVisualizacion, v.FechaCreacion
      FROM dbo.VersionesFestivalArchivos v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.ArchivosDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.RolArchivo = v.RolArchivo
                          AND n.OrdenVisualizacion = v.OrdenVisualizacion);

    DECLARE @origenVA int = (SELECT COUNT(*) FROM dbo.VersionesFestivalArchivos);
    DECLARE @destinoVA int = (SELECT COUNT(*) FROM dbo.ArchivosDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_03] VersionesFestivalArchivos: ', @origenVA, ' en origen, ', @destinoVA, ' en destino.');
    IF @destinoVA < @origenVA THROW 50030, 'La copia de VersionesFestivalArchivos no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalMateriales', 'U') IS NOT NULL
BEGIN
    -- LOS QUE NO SON NI ARCHIVO NI ENLACE NO VIAJAN. La tabla de destino exige que un material sea
    -- una cosa o la otra; una fila con las dos en nulo no dice nada y no se puede enseñar.
    INSERT INTO dbo.ArchivosDeRegistro
        (ModuloId, RegistroId, ArchivoId, Url, RolArchivo, DescripcionArchivo, OrdenVisualizacion, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId),
           v.ArchivoId, v.Url, N'material', v.DescripcionArchivo, v.OrdenVisualizacion, v.FechaCreacion
      FROM dbo.EdicionesFestivalMateriales v
     WHERE (v.ArchivoId IS NOT NULL OR v.Url IS NOT NULL)
       AND NOT EXISTS (SELECT 1 FROM dbo.ArchivosDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.RolArchivo = N'material'
                          AND n.OrdenVisualizacion = v.OrdenVisualizacion);

    DECLARE @origenEMa int = (SELECT COUNT(*) FROM dbo.EdicionesFestivalMateriales WHERE ArchivoId IS NOT NULL OR Url IS NOT NULL);
    DECLARE @destinoEMa int = (SELECT COUNT(*) FROM dbo.ArchivosDeRegistro WHERE ModuloId = N'ediciones_festival');
    DECLARE @vaciosEMa  int = (SELECT COUNT(*) FROM dbo.EdicionesFestivalMateriales WHERE ArchivoId IS NULL AND Url IS NULL);
    PRINT CONCAT('[V20260918_03] EdicionesFestivalMateriales: ', @origenEMa, ' en origen, ', @destinoEMa, ' en destino, ', @vaciosEMa, ' descartados por vacios.');
    IF @destinoEMa < @origenEMa THROW 50030, 'La copia de EdicionesFestivalMateriales no cuadra. No se aplica.', 1;
END;
GO

-- ════════════════════════════════════════════════════════════════════════════════
-- Y las seis se retiran.
-- ════════════════════════════════════════════════════════════════════════════════
DROP TABLE IF EXISTS dbo.VersionesFestivalLocalizaciones;
DROP TABLE IF EXISTS dbo.EdicionesFestivalLocalizaciones;
DROP TABLE IF EXISTS dbo.VersionesFestivalEntidadesSocias;
DROP TABLE IF EXISTS dbo.EdicionesFestivalEntidadesAliadas;
DROP TABLE IF EXISTS dbo.VersionesFestivalArchivos;
DROP TABLE IF EXISTS dbo.EdicionesFestivalMateriales;
GO

PRINT '[V20260918_03] Las relaciones del registro quedan en tres tablas.';
GO
