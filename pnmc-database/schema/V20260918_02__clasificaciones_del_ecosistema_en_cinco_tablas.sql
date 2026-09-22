/*
    PNMC · Las clasificaciones del Ecosistema dejan de tener una tabla por proceso

    QUE ES ESTO
    -----------
    Catorce tablas pasan a cinco. Un Festival tiene prácticas musicales y territorios sonoros; una
    versión de su perfil público tiene esas dos más expresiones artísticas, modalidades de
    participación y tipos de ingreso; una edición anual tiene las mismas cinco; una edición de
    mercado, dos. Cada una de esas relaciones se había modelado como una tabla propia, y las
    catorce tenían EXACTAMENTE la misma forma: identidad, la clave del dueño, la clave del
    catálogo y la fecha. Ni un campo distinto en ninguna.

    QUE CAMBIA, EXACTAMENTE
    -----------------------
    El dueño deja de ser una columna con clave ajena y pasa a ser el par `ModuloId` + `RegistroId`,
    el mismo con el que el registro se nombra en la revisión, en la propuesta de cambio, en la
    bitácora de auditoría y en la procedencia. Los cuatro valores de `ModuloId` que nacen hoy son
    `festivales`, `versiones_festival`, `ediciones_festival` y `ediciones_mercado`.

    POR QUE CINCO Y NO UNA
    ----------------------
    Una sola tabla con una columna «vocabulario» habría dejado las catorce en una, pero habría
    perdido la clave ajena contra el catálogo, que es la única defensa efectiva contra guardar un
    identificador que no existe. Se prefirió conservar la integridad declarada: cinco tablas, cinco
    claves ajenas vivas.

    LO QUE SE PIERDE, DICHO EN VOZ ALTA
    -----------------------------------
    La clave ajena contra el dueño. Una clave ajena no puede apuntar a cuatro tablas distintas. Es
    el mismo precio que ya se pagó en `RevisionesDeRegistro`, en `PropuestasDeCambio` y en
    `EnviosDeRevision`, y se compensa donde se puede: el índice único `(ModuloId, RegistroId,
    Valor)` impide repetir, una restricción `CHECK` acota los módulos admitidos, y todas las
    lecturas y escrituras pasan por un único ayudante —`Clasificaciones`— precisamente para que el
    módulo no se pueda olvidar en una consulta.

    SEGURIDAD
    ---------
    Es idempotente. Copia primero, cuenta después y solo entonces retira; si el recuento no cuadra,
    se detiene y no borra nada.
*/

-- ────────────────────────────────────────────────────────────────────────────────
-- PracticasMusicalesDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.PracticasMusicalesDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PracticasMusicalesDeRegistro (
        IdPracticaMusicalDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                    nvarchar(40) NOT NULL,
        RegistroId                  nvarchar(64) NOT NULL,
        IdPracticaMusical           int          NOT NULL,
        FechaCreacion               datetime2(7) NOT NULL
            CONSTRAINT DF_PracticasMusicalesDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PracticasMusicalesDeRegistro PRIMARY KEY CLUSTERED (IdPracticaMusicalDeRegistro),
        CONSTRAINT UQ_PracticasMusicalesDeRegistro UNIQUE (ModuloId, RegistroId, IdPracticaMusical),
        CONSTRAINT FK_PracticasMusicalesDeRegistro_Catalogo
            FOREIGN KEY (IdPracticaMusical) REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
        CONSTRAINT CK_PracticasMusicalesDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- TerritoriosSonorosDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TerritoriosSonorosDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TerritoriosSonorosDeRegistro (
        IdTerritorioSonoroDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                     nvarchar(40) NOT NULL,
        RegistroId                   nvarchar(64) NOT NULL,
        IdTerritorioSonoro           int          NOT NULL,
        FechaCreacion                datetime2(7) NOT NULL
            CONSTRAINT DF_TerritoriosSonorosDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TerritoriosSonorosDeRegistro PRIMARY KEY CLUSTERED (IdTerritorioSonoroDeRegistro),
        CONSTRAINT UQ_TerritoriosSonorosDeRegistro UNIQUE (ModuloId, RegistroId, IdTerritorioSonoro),
        CONSTRAINT FK_TerritoriosSonorosDeRegistro_Catalogo
            FOREIGN KEY (IdTerritorioSonoro) REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
        CONSTRAINT CK_TerritoriosSonorosDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- ExpresionesArtisticasDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.ExpresionesArtisticasDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpresionesArtisticasDeRegistro (
        IdExpresionArtisticaDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                       nvarchar(40) NOT NULL,
        RegistroId                     nvarchar(64) NOT NULL,
        IdExpresionArtistica           int          NOT NULL,
        FechaCreacion                  datetime2(7) NOT NULL
            CONSTRAINT DF_ExpresionesArtisticasDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ExpresionesArtisticasDeRegistro PRIMARY KEY CLUSTERED (IdExpresionArtisticaDeRegistro),
        CONSTRAINT UQ_ExpresionesArtisticasDeRegistro UNIQUE (ModuloId, RegistroId, IdExpresionArtistica),
        CONSTRAINT FK_ExpresionesArtisticasDeRegistro_Catalogo
            FOREIGN KEY (IdExpresionArtistica) REFERENCES dbo.ExpresionesArtisticas (IdExpresionArtistica),
        CONSTRAINT CK_ExpresionesArtisticasDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- ModalidadesParticipacionDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.ModalidadesParticipacionDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ModalidadesParticipacionDeRegistro (
        IdModalidadParticipacionDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                           nvarchar(40) NOT NULL,
        RegistroId                         nvarchar(64) NOT NULL,
        IdModalidadParticipacion           int          NOT NULL,
        FechaCreacion                      datetime2(7) NOT NULL
            CONSTRAINT DF_ModalidadesParticipacionDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ModalidadesParticipacionDeRegistro PRIMARY KEY CLUSTERED (IdModalidadParticipacionDeRegistro),
        CONSTRAINT UQ_ModalidadesParticipacionDeRegistro UNIQUE (ModuloId, RegistroId, IdModalidadParticipacion),
        CONSTRAINT FK_ModalidadesParticipacionDeRegistro_Catalogo
            FOREIGN KEY (IdModalidadParticipacion) REFERENCES dbo.ModalidadesParticipacion (IdModalidadParticipacion),
        CONSTRAINT CK_ModalidadesParticipacionDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ────────────────────────────────────────────────────────────────────────────────
-- TiposIngresoDeRegistro
-- ────────────────────────────────────────────────────────────────────────────────
IF OBJECT_ID('dbo.TiposIngresoDeRegistro', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TiposIngresoDeRegistro (
        IdTipoIngresoDeRegistro bigint       IDENTITY(1,1) NOT NULL,
        ModuloId                nvarchar(40) NOT NULL,
        RegistroId              nvarchar(64) NOT NULL,
        IdTipoIngreso           int          NOT NULL,
        FechaCreacion           datetime2(7) NOT NULL
            CONSTRAINT DF_TiposIngresoDeRegistro_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_TiposIngresoDeRegistro PRIMARY KEY CLUSTERED (IdTipoIngresoDeRegistro),
        CONSTRAINT UQ_TiposIngresoDeRegistro UNIQUE (ModuloId, RegistroId, IdTipoIngreso),
        CONSTRAINT FK_TiposIngresoDeRegistro_Catalogo
            FOREIGN KEY (IdTipoIngreso) REFERENCES dbo.TiposIngreso (IdTipoIngreso),
        CONSTRAINT CK_TiposIngresoDeRegistro_Modulo
            CHECK (ModuloId IN (N'festivales', N'versiones_festival', N'ediciones_festival', N'ediciones_mercado'))
    );
END;
GO

-- ════════════════════════════════════════════════════════════════════════════════
-- La copia. Cada bloque comprueba que el origen existe, copia lo que falte y se
-- detiene si el destino no llega a lo que había.
-- ════════════════════════════════════════════════════════════════════════════════

IF OBJECT_ID('dbo.FestivalesPracticasMusicales', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PracticasMusicalesDeRegistro (ModuloId, RegistroId, IdPracticaMusical, FechaCreacion)
    SELECT N'festivales', CONVERT(nvarchar(64), v.FestivalId), v.PracticaMusicalId, v.FechaCreacion
      FROM dbo.FestivalesPracticasMusicales v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticasMusicalesDeRegistro n
                        WHERE n.ModuloId = N'festivales'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.FestivalId)
                          AND n.IdPracticaMusical = v.PracticaMusicalId);

    DECLARE @origenFP int = (SELECT COUNT(*) FROM (SELECT DISTINCT FestivalId, PracticaMusicalId FROM dbo.FestivalesPracticasMusicales) x);
    DECLARE @destinoFP int = (SELECT COUNT(*) FROM dbo.PracticasMusicalesDeRegistro WHERE ModuloId = N'festivales');
    PRINT CONCAT('[V20260918_02] FestivalesPracticasMusicales: ', @origenFP, ' en origen, ', @destinoFP, ' en destino.');
    IF @destinoFP < @origenFP THROW 50020, 'La copia de FestivalesPracticasMusicales no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalPracticasMusicales', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PracticasMusicalesDeRegistro (ModuloId, RegistroId, IdPracticaMusical, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId), v.PracticaMusicalId, v.FechaCreacion
      FROM dbo.VersionesFestivalPracticasMusicales v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticasMusicalesDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.IdPracticaMusical = v.PracticaMusicalId);

    DECLARE @origenVP int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, PracticaMusicalId FROM dbo.VersionesFestivalPracticasMusicales) x);
    DECLARE @destinoVP int = (SELECT COUNT(*) FROM dbo.PracticasMusicalesDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_02] VersionesFestivalPracticasMusicales: ', @origenVP, ' en origen, ', @destinoVP, ' en destino.');
    IF @destinoVP < @origenVP THROW 50020, 'La copia de VersionesFestivalPracticasMusicales no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalPracticasMusicales', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PracticasMusicalesDeRegistro (ModuloId, RegistroId, IdPracticaMusical, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId), v.PracticaMusicalId, v.FechaCreacion
      FROM dbo.EdicionesFestivalPracticasMusicales v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticasMusicalesDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.IdPracticaMusical = v.PracticaMusicalId);

    DECLARE @origenEP int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, PracticaMusicalId FROM dbo.EdicionesFestivalPracticasMusicales) x);
    DECLARE @destinoEP int = (SELECT COUNT(*) FROM dbo.PracticasMusicalesDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_02] EdicionesFestivalPracticasMusicales: ', @origenEP, ' en origen, ', @destinoEP, ' en destino.');
    IF @destinoEP < @origenEP THROW 50020, 'La copia de EdicionesFestivalPracticasMusicales no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesMercadoPracticasMusicales', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.PracticasMusicalesDeRegistro (ModuloId, RegistroId, IdPracticaMusical, FechaCreacion)
    SELECT N'ediciones_mercado', CONVERT(nvarchar(64), v.EdicionMercadoId), v.PracticaMusicalId, v.FechaCreacion
      FROM dbo.EdicionesMercadoPracticasMusicales v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticasMusicalesDeRegistro n
                        WHERE n.ModuloId = N'ediciones_mercado'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionMercadoId)
                          AND n.IdPracticaMusical = v.PracticaMusicalId);

    DECLARE @origenMP int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionMercadoId, PracticaMusicalId FROM dbo.EdicionesMercadoPracticasMusicales) x);
    DECLARE @destinoMP int = (SELECT COUNT(*) FROM dbo.PracticasMusicalesDeRegistro WHERE ModuloId = N'ediciones_mercado');
    PRINT CONCAT('[V20260918_02] EdicionesMercadoPracticasMusicales: ', @origenMP, ' en origen, ', @destinoMP, ' en destino.');
    IF @destinoMP < @origenMP THROW 50020, 'La copia de EdicionesMercadoPracticasMusicales no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.FestivalesTerritoriosSonoros', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TerritoriosSonorosDeRegistro (ModuloId, RegistroId, IdTerritorioSonoro, FechaCreacion)
    SELECT N'festivales', CONVERT(nvarchar(64), v.FestivalId), v.TerritorioSonoroId, v.FechaCreacion
      FROM dbo.FestivalesTerritoriosSonoros v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TerritoriosSonorosDeRegistro n
                        WHERE n.ModuloId = N'festivales'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.FestivalId)
                          AND n.IdTerritorioSonoro = v.TerritorioSonoroId);

    DECLARE @origenFT int = (SELECT COUNT(*) FROM (SELECT DISTINCT FestivalId, TerritorioSonoroId FROM dbo.FestivalesTerritoriosSonoros) x);
    DECLARE @destinoFT int = (SELECT COUNT(*) FROM dbo.TerritoriosSonorosDeRegistro WHERE ModuloId = N'festivales');
    PRINT CONCAT('[V20260918_02] FestivalesTerritoriosSonoros: ', @origenFT, ' en origen, ', @destinoFT, ' en destino.');
    IF @destinoFT < @origenFT THROW 50020, 'La copia de FestivalesTerritoriosSonoros no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalTerritoriosSonoros', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TerritoriosSonorosDeRegistro (ModuloId, RegistroId, IdTerritorioSonoro, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId), v.TerritorioSonoroId, v.FechaCreacion
      FROM dbo.VersionesFestivalTerritoriosSonoros v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TerritoriosSonorosDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.IdTerritorioSonoro = v.TerritorioSonoroId);

    DECLARE @origenVT int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, TerritorioSonoroId FROM dbo.VersionesFestivalTerritoriosSonoros) x);
    DECLARE @destinoVT int = (SELECT COUNT(*) FROM dbo.TerritoriosSonorosDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_02] VersionesFestivalTerritoriosSonoros: ', @origenVT, ' en origen, ', @destinoVT, ' en destino.');
    IF @destinoVT < @origenVT THROW 50020, 'La copia de VersionesFestivalTerritoriosSonoros no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalTerritoriosSonoros', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TerritoriosSonorosDeRegistro (ModuloId, RegistroId, IdTerritorioSonoro, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId), v.TerritorioSonoroId, v.FechaCreacion
      FROM dbo.EdicionesFestivalTerritoriosSonoros v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TerritoriosSonorosDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.IdTerritorioSonoro = v.TerritorioSonoroId);

    DECLARE @origenET int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, TerritorioSonoroId FROM dbo.EdicionesFestivalTerritoriosSonoros) x);
    DECLARE @destinoET int = (SELECT COUNT(*) FROM dbo.TerritoriosSonorosDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_02] EdicionesFestivalTerritoriosSonoros: ', @origenET, ' en origen, ', @destinoET, ' en destino.');
    IF @destinoET < @origenET THROW 50020, 'La copia de EdicionesFestivalTerritoriosSonoros no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesMercadoTerritoriosSonoros', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TerritoriosSonorosDeRegistro (ModuloId, RegistroId, IdTerritorioSonoro, FechaCreacion)
    SELECT N'ediciones_mercado', CONVERT(nvarchar(64), v.EdicionMercadoId), v.TerritorioSonoroId, v.FechaCreacion
      FROM dbo.EdicionesMercadoTerritoriosSonoros v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TerritoriosSonorosDeRegistro n
                        WHERE n.ModuloId = N'ediciones_mercado'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionMercadoId)
                          AND n.IdTerritorioSonoro = v.TerritorioSonoroId);

    DECLARE @origenMT int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionMercadoId, TerritorioSonoroId FROM dbo.EdicionesMercadoTerritoriosSonoros) x);
    DECLARE @destinoMT int = (SELECT COUNT(*) FROM dbo.TerritoriosSonorosDeRegistro WHERE ModuloId = N'ediciones_mercado');
    PRINT CONCAT('[V20260918_02] EdicionesMercadoTerritoriosSonoros: ', @origenMT, ' en origen, ', @destinoMT, ' en destino.');
    IF @destinoMT < @origenMT THROW 50020, 'La copia de EdicionesMercadoTerritoriosSonoros no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalExpresionesArtisticas', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ExpresionesArtisticasDeRegistro (ModuloId, RegistroId, IdExpresionArtistica, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId), v.ExpresionArtisticaId, v.FechaCreacion
      FROM dbo.VersionesFestivalExpresionesArtisticas v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpresionesArtisticasDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.IdExpresionArtistica = v.ExpresionArtisticaId);

    DECLARE @origenVE int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, ExpresionArtisticaId FROM dbo.VersionesFestivalExpresionesArtisticas) x);
    DECLARE @destinoVE int = (SELECT COUNT(*) FROM dbo.ExpresionesArtisticasDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_02] VersionesFestivalExpresionesArtisticas: ', @origenVE, ' en origen, ', @destinoVE, ' en destino.');
    IF @destinoVE < @origenVE THROW 50020, 'La copia de VersionesFestivalExpresionesArtisticas no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalExpresionesArtisticas', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ExpresionesArtisticasDeRegistro (ModuloId, RegistroId, IdExpresionArtistica, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId), v.ExpresionArtisticaId, v.FechaCreacion
      FROM dbo.EdicionesFestivalExpresionesArtisticas v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpresionesArtisticasDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.IdExpresionArtistica = v.ExpresionArtisticaId);

    DECLARE @origenEE int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, ExpresionArtisticaId FROM dbo.EdicionesFestivalExpresionesArtisticas) x);
    DECLARE @destinoEE int = (SELECT COUNT(*) FROM dbo.ExpresionesArtisticasDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_02] EdicionesFestivalExpresionesArtisticas: ', @origenEE, ' en origen, ', @destinoEE, ' en destino.');
    IF @destinoEE < @origenEE THROW 50020, 'La copia de EdicionesFestivalExpresionesArtisticas no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalModalidadesParticipacion', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ModalidadesParticipacionDeRegistro (ModuloId, RegistroId, IdModalidadParticipacion, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId), v.ModalidadParticipacionId, v.FechaCreacion
      FROM dbo.VersionesFestivalModalidadesParticipacion v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.ModalidadesParticipacionDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.IdModalidadParticipacion = v.ModalidadParticipacionId);

    DECLARE @origenVM int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, ModalidadParticipacionId FROM dbo.VersionesFestivalModalidadesParticipacion) x);
    DECLARE @destinoVM int = (SELECT COUNT(*) FROM dbo.ModalidadesParticipacionDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_02] VersionesFestivalModalidadesParticipacion: ', @origenVM, ' en origen, ', @destinoVM, ' en destino.');
    IF @destinoVM < @origenVM THROW 50020, 'La copia de VersionesFestivalModalidadesParticipacion no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalModalidadesParticipacion', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.ModalidadesParticipacionDeRegistro (ModuloId, RegistroId, IdModalidadParticipacion, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId), v.ModalidadParticipacionId, v.FechaCreacion
      FROM dbo.EdicionesFestivalModalidadesParticipacion v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.ModalidadesParticipacionDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.IdModalidadParticipacion = v.ModalidadParticipacionId);

    DECLARE @origenEM int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, ModalidadParticipacionId FROM dbo.EdicionesFestivalModalidadesParticipacion) x);
    DECLARE @destinoEM int = (SELECT COUNT(*) FROM dbo.ModalidadesParticipacionDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_02] EdicionesFestivalModalidadesParticipacion: ', @origenEM, ' en origen, ', @destinoEM, ' en destino.');
    IF @destinoEM < @origenEM THROW 50020, 'La copia de EdicionesFestivalModalidadesParticipacion no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.VersionesFestivalTiposIngreso', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TiposIngresoDeRegistro (ModuloId, RegistroId, IdTipoIngreso, FechaCreacion)
    SELECT N'versiones_festival', CONVERT(nvarchar(64), v.VersionFestivalId), v.TipoIngresoId, v.FechaCreacion
      FROM dbo.VersionesFestivalTiposIngreso v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TiposIngresoDeRegistro n
                        WHERE n.ModuloId = N'versiones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.VersionFestivalId)
                          AND n.IdTipoIngreso = v.TipoIngresoId);

    DECLARE @origenVI int = (SELECT COUNT(*) FROM (SELECT DISTINCT VersionFestivalId, TipoIngresoId FROM dbo.VersionesFestivalTiposIngreso) x);
    DECLARE @destinoVI int = (SELECT COUNT(*) FROM dbo.TiposIngresoDeRegistro WHERE ModuloId = N'versiones_festival');
    PRINT CONCAT('[V20260918_02] VersionesFestivalTiposIngreso: ', @origenVI, ' en origen, ', @destinoVI, ' en destino.');
    IF @destinoVI < @origenVI THROW 50020, 'La copia de VersionesFestivalTiposIngreso no cuadra. No se aplica.', 1;
END;
GO

IF OBJECT_ID('dbo.EdicionesFestivalTiposIngreso', 'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.TiposIngresoDeRegistro (ModuloId, RegistroId, IdTipoIngreso, FechaCreacion)
    SELECT N'ediciones_festival', CONVERT(nvarchar(64), v.EdicionFestivalId), v.TipoIngresoId, v.FechaCreacion
      FROM dbo.EdicionesFestivalTiposIngreso v
     WHERE NOT EXISTS (SELECT 1 FROM dbo.TiposIngresoDeRegistro n
                        WHERE n.ModuloId = N'ediciones_festival'
                          AND n.RegistroId = CONVERT(nvarchar(64), v.EdicionFestivalId)
                          AND n.IdTipoIngreso = v.TipoIngresoId);

    DECLARE @origenEI int = (SELECT COUNT(*) FROM (SELECT DISTINCT EdicionFestivalId, TipoIngresoId FROM dbo.EdicionesFestivalTiposIngreso) x);
    DECLARE @destinoEI int = (SELECT COUNT(*) FROM dbo.TiposIngresoDeRegistro WHERE ModuloId = N'ediciones_festival');
    PRINT CONCAT('[V20260918_02] EdicionesFestivalTiposIngreso: ', @origenEI, ' en origen, ', @destinoEI, ' en destino.');
    IF @destinoEI < @origenEI THROW 50020, 'La copia de EdicionesFestivalTiposIngreso no cuadra. No se aplica.', 1;
END;
GO

-- ════════════════════════════════════════════════════════════════════════════════
-- Y ahora sí: las catorce se retiran.
-- ════════════════════════════════════════════════════════════════════════════════
DROP TABLE IF EXISTS dbo.FestivalesPracticasMusicales;
DROP TABLE IF EXISTS dbo.VersionesFestivalPracticasMusicales;
DROP TABLE IF EXISTS dbo.EdicionesFestivalPracticasMusicales;
DROP TABLE IF EXISTS dbo.EdicionesMercadoPracticasMusicales;
DROP TABLE IF EXISTS dbo.FestivalesTerritoriosSonoros;
DROP TABLE IF EXISTS dbo.VersionesFestivalTerritoriosSonoros;
DROP TABLE IF EXISTS dbo.EdicionesFestivalTerritoriosSonoros;
DROP TABLE IF EXISTS dbo.EdicionesMercadoTerritoriosSonoros;
DROP TABLE IF EXISTS dbo.VersionesFestivalExpresionesArtisticas;
DROP TABLE IF EXISTS dbo.EdicionesFestivalExpresionesArtisticas;
DROP TABLE IF EXISTS dbo.VersionesFestivalModalidadesParticipacion;
DROP TABLE IF EXISTS dbo.EdicionesFestivalModalidadesParticipacion;
DROP TABLE IF EXISTS dbo.VersionesFestivalTiposIngreso;
DROP TABLE IF EXISTS dbo.EdicionesFestivalTiposIngreso;
GO

PRINT '[V20260918_02] Las clasificaciones del Ecosistema quedan en cinco tablas.';
GO
