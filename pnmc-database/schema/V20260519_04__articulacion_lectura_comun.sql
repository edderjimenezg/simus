/*
    PNMC - Tablas de articulacion y lectura comun
    Fase: reconstruccion local desde cero
*/

IF OBJECT_ID(N'dbo.RegistrosEcosistema', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosEcosistema
    (
        IdRegistroEcosistema int IDENTITY(1,1) NOT NULL,
        IdTipoRegistroEcosistema int NOT NULL,
        IdRegistroOrigen int NOT NULL,
        NombreRegistro nvarchar(240) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Latitud decimal(9,6) NULL,
        Longitud decimal(9,6) NULL,
        CONSTRAINT PK_RegistrosEcosistema PRIMARY KEY (IdRegistroEcosistema),
        CONSTRAINT FK_RegistrosEcosistema_TiposRegistroEcosistema FOREIGN KEY (IdTipoRegistroEcosistema)
            REFERENCES dbo.TiposRegistroEcosistema (IdTipoRegistroEcosistema),
        CONSTRAINT FK_RegistrosEcosistema_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT UQ_RegistrosEcosistema_Tipo_Origen UNIQUE (IdTipoRegistroEcosistema, IdRegistroOrigen)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrosEcosistema_Territorio' AND object_id = OBJECT_ID(N'dbo.RegistrosEcosistema'))
BEGIN
    CREATE INDEX IX_RegistrosEcosistema_Territorio
    ON dbo.RegistrosEcosistema (CodigoDepartamento, CodigoMunicipio);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrosEcosistema_Tipo' AND object_id = OBJECT_ID(N'dbo.RegistrosEcosistema'))
BEGIN
    CREATE INDEX IX_RegistrosEcosistema_Tipo
    ON dbo.RegistrosEcosistema (IdTipoRegistroEcosistema);
END;

IF OBJECT_ID(N'dbo.TR_RegistrosEcosistema_ValidarOrigen', N'TR') IS NOT NULL
BEGIN
    DROP TRIGGER dbo.TR_RegistrosEcosistema_ValidarOrigen;
END;
GO

CREATE TRIGGER dbo.TR_RegistrosEcosistema_ValidarOrigen
ON dbo.RegistrosEcosistema
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.TiposRegistroEcosistema t ON t.IdTipoRegistroEcosistema = i.IdTipoRegistroEcosistema
        WHERE
            (t.CodigoTipoRegistro = N'festival' AND NOT EXISTS (
                SELECT 1 FROM dbo.Festivales f WHERE f.IdFestival = i.IdRegistroOrigen
            ))
            OR (t.CodigoTipoRegistro = N'escuela_musica' AND NOT EXISTS (
                SELECT 1 FROM dbo.EscuelasMusica e WHERE e.IdEscuelaMusica = i.IdRegistroOrigen
            ))
            OR (t.CodigoTipoRegistro = N'mercado_musical' AND NOT EXISTS (
                SELECT 1 FROM dbo.MercadosMusicales m WHERE m.IdMercadoMusical = i.IdRegistroOrigen
            ))
            OR (t.CodigoTipoRegistro = N'red_documentacion' AND NOT EXISTS (
                SELECT 1 FROM dbo.RedesDocumentacion r WHERE r.IdRedDocumentacion = i.IdRegistroOrigen
            ))
            OR (t.CodigoTipoRegistro = N'lutier' AND NOT EXISTS (
                SELECT 1 FROM dbo.Lutieres l WHERE l.IdLutier = i.IdRegistroOrigen
            ))
    )
    BEGIN
        THROW 51000, 'IdRegistroOrigen no existe en la tabla fuente correspondiente al tipo de registro ecosistemico.', 1;
    END;
END;
GO

IF OBJECT_ID(N'dbo.Participaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Participaciones
    (
        Referencia nvarchar(64) NOT NULL,
        FechaEnvio datetimeoffset NOT NULL,
        TipoActor nvarchar(80) NOT NULL,
        NombreActor nvarchar(240) NOT NULL,
        CorreoElectronico nvarchar(240) NOT NULL,
        Departamento nvarchar(120) NOT NULL,
        Municipio nvarchar(120) NOT NULL,
        DatosFormularioJson nvarchar(max) NOT NULL,
        CONSTRAINT PK_Participaciones PRIMARY KEY (Referencia)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Participaciones_FechaEnvio' AND object_id = OBJECT_ID(N'dbo.Participaciones'))
BEGIN
    CREATE INDEX IX_Participaciones_FechaEnvio ON dbo.Participaciones (FechaEnvio);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Participaciones_TipoActor' AND object_id = OBJECT_ID(N'dbo.Participaciones'))
BEGIN
    CREATE INDEX IX_Participaciones_TipoActor ON dbo.Participaciones (TipoActor);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Participaciones_Departamento' AND object_id = OBJECT_ID(N'dbo.Participaciones'))
BEGIN
    CREATE INDEX IX_Participaciones_Departamento ON dbo.Participaciones (Departamento);
END;

IF OBJECT_ID(N'dbo.RegistrosEcosistemaTerritoriosSonoros', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosEcosistemaTerritoriosSonoros
    (
        IdRelacionTerritorioSonoro int IDENTITY(1,1) NOT NULL,
        IdRegistroEcosistema int NOT NULL,
        IdTerritorioSonoro int NOT NULL,
        CONSTRAINT PK_RegistrosEcosistemaTerritoriosSonoros PRIMARY KEY (IdRelacionTerritorioSonoro),
        CONSTRAINT FK_RegistrosEcosistemaTerritoriosSonoros_Registros FOREIGN KEY (IdRegistroEcosistema)
            REFERENCES dbo.RegistrosEcosistema (IdRegistroEcosistema),
        CONSTRAINT FK_RegistrosEcosistemaTerritoriosSonoros_Territorios FOREIGN KEY (IdTerritorioSonoro)
            REFERENCES dbo.TerritoriosSonoros (IdTerritorioSonoro),
        CONSTRAINT UQ_RegistrosEcosistemaTerritoriosSonoros UNIQUE (IdRegistroEcosistema, IdTerritorioSonoro)
    );
END;

IF OBJECT_ID(N'dbo.RegistrosEcosistemaPracticasMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosEcosistemaPracticasMusicales
    (
        IdRelacionPracticaMusical int IDENTITY(1,1) NOT NULL,
        IdRegistroEcosistema int NOT NULL,
        IdPracticaMusical int NOT NULL,
        CONSTRAINT PK_RegistrosEcosistemaPracticasMusicales PRIMARY KEY (IdRelacionPracticaMusical),
        CONSTRAINT FK_RegistrosEcosistemaPracticasMusicales_Registros FOREIGN KEY (IdRegistroEcosistema)
            REFERENCES dbo.RegistrosEcosistema (IdRegistroEcosistema),
        CONSTRAINT FK_RegistrosEcosistemaPracticasMusicales_Practicas FOREIGN KEY (IdPracticaMusical)
            REFERENCES dbo.PracticasMusicales (IdPracticaMusical),
        CONSTRAINT UQ_RegistrosEcosistemaPracticasMusicales UNIQUE (IdRegistroEcosistema, IdPracticaMusical)
    );
END;

IF OBJECT_ID(N'dbo.MetricasDepartamentoMapa', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetricasDepartamentoMapa
    (
        IdMetricaDepartamento int IDENTITY(1,1) NOT NULL,
        CodigoDepartamento char(2) NOT NULL,
        EscuelasActivas int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Escuelas DEFAULT (0),
        TotalEstudiantesEscuelas int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Estudiantes DEFAULT (0),
        FestivalesRegistrados int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Festivales DEFAULT (0),
        MercadosRegistrados int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Mercados DEFAULT (0),
        RedesDocumentacionActivas int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Redes DEFAULT (0),
        LutieresRegistrados int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Lutieres DEFAULT (0),
        MunicipiosConFestivales int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_MpiosFest DEFAULT (0),
        MunicipiosConMercados int NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_MpiosMerc DEFAULT (0),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_MetricasDepartamentoMapa_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MetricasDepartamentoMapa PRIMARY KEY (IdMetricaDepartamento),
        CONSTRAINT UQ_MetricasDepartamentoMapa_CodigoDepartamento UNIQUE (CodigoDepartamento),
        CONSTRAINT CK_MetricasDepartamentoMapa_NoNegativas CHECK (
            EscuelasActivas >= 0 AND TotalEstudiantesEscuelas >= 0 AND FestivalesRegistrados >= 0
            AND MercadosRegistrados >= 0 AND RedesDocumentacionActivas >= 0 AND LutieresRegistrados >= 0
            AND MunicipiosConFestivales >= 0 AND MunicipiosConMercados >= 0
        )
    );
END;

IF OBJECT_ID(N'dbo.MetricasMunicipioMapa', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MetricasMunicipioMapa
    (
        IdMetricaMunicipio int IDENTITY(1,1) NOT NULL,
        CodigoMunicipio char(5) NOT NULL,
        EscuelasActivas int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Escuelas DEFAULT (0),
        TotalEstudiantesEscuelas int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Estudiantes DEFAULT (0),
        FestivalesRegistrados int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Festivales DEFAULT (0),
        MercadosRegistrados int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Mercados DEFAULT (0),
        RedesDocumentacionActivas int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Redes DEFAULT (0),
        LutieresRegistrados int NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Lutieres DEFAULT (0),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_MetricasMunicipioMapa_Fecha DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MetricasMunicipioMapa PRIMARY KEY (IdMetricaMunicipio),
        CONSTRAINT FK_MetricasMunicipioMapa_Divipola FOREIGN KEY (CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoMunicipio),
        CONSTRAINT UQ_MetricasMunicipioMapa_CodigoMunicipio UNIQUE (CodigoMunicipio),
        CONSTRAINT CK_MetricasMunicipioMapa_NoNegativas CHECK (
            EscuelasActivas >= 0 AND TotalEstudiantesEscuelas >= 0 AND FestivalesRegistrados >= 0
            AND MercadosRegistrados >= 0 AND RedesDocumentacionActivas >= 0 AND LutieresRegistrados >= 0
        )
    );
END;

IF OBJECT_ID(N'dbo.AgendaEtiquetas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgendaEtiquetas
    (
        IdAgendaEtiqueta int IDENTITY(1,1) NOT NULL,
        IdAgenda int NOT NULL,
        IdEtiqueta int NOT NULL,
        CONSTRAINT PK_AgendaEtiquetas PRIMARY KEY (IdAgendaEtiqueta),
        CONSTRAINT FK_AgendaEtiquetas_Agenda FOREIGN KEY (IdAgenda) REFERENCES dbo.Agenda (IdAgenda),
        CONSTRAINT FK_AgendaEtiquetas_Etiquetas FOREIGN KEY (IdEtiqueta) REFERENCES dbo.Etiquetas (IdEtiqueta),
        CONSTRAINT UQ_AgendaEtiquetas UNIQUE (IdAgenda, IdEtiqueta)
    );
END;

IF OBJECT_ID(N'dbo.AgendaArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AgendaArchivos
    (
        IdAgendaArchivo int IDENTITY(1,1) NOT NULL,
        IdAgenda int NOT NULL,
        IdArchivo int NOT NULL,
        RolArchivo nvarchar(80) NOT NULL,
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_AgendaArchivos_Orden DEFAULT (1),
        CONSTRAINT PK_AgendaArchivos PRIMARY KEY (IdAgendaArchivo),
        CONSTRAINT FK_AgendaArchivos_Agenda FOREIGN KEY (IdAgenda) REFERENCES dbo.Agenda (IdAgenda),
        CONSTRAINT FK_AgendaArchivos_Archivos FOREIGN KEY (IdArchivo) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT UQ_AgendaArchivos UNIQUE (IdAgenda, IdArchivo, RolArchivo),
        CONSTRAINT CK_AgendaArchivos_Orden CHECK (OrdenVisualizacion > 0)
    );
END;

IF OBJECT_ID(N'dbo.NoticiasEtiquetas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasEtiquetas
    (
        IdNoticiaEtiqueta int IDENTITY(1,1) NOT NULL,
        IdNoticia int NOT NULL,
        IdEtiqueta int NOT NULL,
        CONSTRAINT PK_NoticiasEtiquetas PRIMARY KEY (IdNoticiaEtiqueta),
        CONSTRAINT FK_NoticiasEtiquetas_Noticias FOREIGN KEY (IdNoticia) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasEtiquetas_Etiquetas FOREIGN KEY (IdEtiqueta) REFERENCES dbo.Etiquetas (IdEtiqueta),
        CONSTRAINT UQ_NoticiasEtiquetas UNIQUE (IdNoticia, IdEtiqueta)
    );
END;

IF OBJECT_ID(N'dbo.NoticiasArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NoticiasArchivos
    (
        IdNoticiaArchivo int IDENTITY(1,1) NOT NULL,
        IdNoticia int NOT NULL,
        IdArchivo int NOT NULL,
        RolArchivo nvarchar(80) NOT NULL,
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_NoticiasArchivos_Orden DEFAULT (1),
        CONSTRAINT PK_NoticiasArchivos PRIMARY KEY (IdNoticiaArchivo),
        CONSTRAINT FK_NoticiasArchivos_Noticias FOREIGN KEY (IdNoticia) REFERENCES dbo.Noticias (IdNoticia),
        CONSTRAINT FK_NoticiasArchivos_Archivos FOREIGN KEY (IdArchivo) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT UQ_NoticiasArchivos UNIQUE (IdNoticia, IdArchivo, RolArchivo),
        CONSTRAINT CK_NoticiasArchivos_Orden CHECK (OrdenVisualizacion > 0)
    );
END;

IF OBJECT_ID(N'dbo.AlbumesGaleriaEtiquetas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlbumesGaleriaEtiquetas
    (
        IdAlbumGaleriaEtiqueta int IDENTITY(1,1) NOT NULL,
        IdAlbum int NOT NULL,
        IdEtiqueta int NOT NULL,
        CONSTRAINT PK_AlbumesGaleriaEtiquetas PRIMARY KEY (IdAlbumGaleriaEtiqueta),
        CONSTRAINT FK_AlbumesGaleriaEtiquetas_Albumes FOREIGN KEY (IdAlbum) REFERENCES dbo.AlbumesGaleria (IdAlbum),
        CONSTRAINT FK_AlbumesGaleriaEtiquetas_Etiquetas FOREIGN KEY (IdEtiqueta) REFERENCES dbo.Etiquetas (IdEtiqueta),
        CONSTRAINT UQ_AlbumesGaleriaEtiquetas UNIQUE (IdAlbum, IdEtiqueta)
    );
END;

IF OBJECT_ID(N'dbo.AlbumesGaleriaArchivos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlbumesGaleriaArchivos
    (
        IdAlbumGaleriaArchivo int IDENTITY(1,1) NOT NULL,
        IdAlbum int NOT NULL,
        IdArchivo int NOT NULL,
        RolArchivo nvarchar(80) NOT NULL,
        OrdenVisualizacion int NOT NULL CONSTRAINT DF_AlbumesGaleriaArchivos_Orden DEFAULT (1),
        CONSTRAINT PK_AlbumesGaleriaArchivos PRIMARY KEY (IdAlbumGaleriaArchivo),
        CONSTRAINT FK_AlbumesGaleriaArchivos_Albumes FOREIGN KEY (IdAlbum) REFERENCES dbo.AlbumesGaleria (IdAlbum),
        CONSTRAINT FK_AlbumesGaleriaArchivos_Archivos FOREIGN KEY (IdArchivo) REFERENCES dbo.Archivos (IdArchivo),
        CONSTRAINT UQ_AlbumesGaleriaArchivos UNIQUE (IdAlbum, IdArchivo, RolArchivo),
        CONSTRAINT CK_AlbumesGaleriaArchivos_Orden CHECK (OrdenVisualizacion > 0)
    );
END;

IF OBJECT_ID(N'dbo.sp_ActualizarMetricasMapa', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_ActualizarMetricasMapa;
END;
GO

CREATE PROCEDURE dbo.sp_ActualizarMetricasMapa
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.MetricasDepartamentoMapa;
    DELETE FROM dbo.MetricasMunicipioMapa;

    INSERT INTO dbo.MetricasDepartamentoMapa
        (CodigoDepartamento, EscuelasActivas, TotalEstudiantesEscuelas, FestivalesRegistrados,
         MercadosRegistrados, RedesDocumentacionActivas, LutieresRegistrados, MunicipiosConFestivales,
         MunicipiosConMercados, FechaActualizacion)
    SELECT
        departamentos.CodigoDepartamento,
        ISNULL(escuelas.EscuelasActivas, 0),
        ISNULL(escuelas.TotalEstudiantesEscuelas, 0),
        ISNULL(festivales.FestivalesRegistrados, 0),
        ISNULL(mercados.MercadosRegistrados, 0),
        ISNULL(redes.RedesDocumentacionActivas, 0),
        ISNULL(lutieres.LutieresRegistrados, 0),
        ISNULL(festivales.MunicipiosConFestivales, 0),
        ISNULL(mercados.MunicipiosConMercados, 0),
        SYSUTCDATETIME()
    FROM (SELECT DISTINCT CodigoDepartamento FROM dbo.Divipola) departamentos
    LEFT JOIN (
        SELECT CodigoDepartamento, COUNT(*) AS EscuelasActivas, SUM(ISNULL(CantidadEstudiantes, 0)) AS TotalEstudiantesEscuelas
        FROM dbo.EscuelasMusica
        WHERE Activo = 1 AND EscuelaActiva = 1 AND CodigoDepartamento IS NOT NULL
        GROUP BY CodigoDepartamento
    ) escuelas ON escuelas.CodigoDepartamento = departamentos.CodigoDepartamento
    LEFT JOIN (
        SELECT CodigoDepartamento, COUNT(*) AS FestivalesRegistrados, COUNT(DISTINCT CodigoMunicipio) AS MunicipiosConFestivales
        FROM dbo.Festivales
        WHERE Activo = 1 AND CodigoDepartamento IS NOT NULL
        GROUP BY CodigoDepartamento
    ) festivales ON festivales.CodigoDepartamento = departamentos.CodigoDepartamento
    LEFT JOIN (
        SELECT CodigoDepartamento, COUNT(*) AS MercadosRegistrados, COUNT(DISTINCT CodigoMunicipio) AS MunicipiosConMercados
        FROM dbo.MercadosMusicales
        WHERE Activo = 1 AND CodigoDepartamento IS NOT NULL
        GROUP BY CodigoDepartamento
    ) mercados ON mercados.CodigoDepartamento = departamentos.CodigoDepartamento
    LEFT JOIN (
        SELECT CodigoDepartamento, COUNT(*) AS RedesDocumentacionActivas
        FROM dbo.RedesDocumentacion
        WHERE Activo = 1 AND CodigoDepartamento IS NOT NULL
        GROUP BY CodigoDepartamento
    ) redes ON redes.CodigoDepartamento = departamentos.CodigoDepartamento
    LEFT JOIN (
        SELECT CodigoDepartamento, COUNT(*) AS LutieresRegistrados
        FROM dbo.Lutieres
        WHERE Activo = 1 AND CodigoDepartamento IS NOT NULL
        GROUP BY CodigoDepartamento
    ) lutieres ON lutieres.CodigoDepartamento = departamentos.CodigoDepartamento;

    INSERT INTO dbo.MetricasMunicipioMapa
        (CodigoMunicipio, EscuelasActivas, TotalEstudiantesEscuelas, FestivalesRegistrados,
         MercadosRegistrados, RedesDocumentacionActivas, LutieresRegistrados, FechaActualizacion)
    SELECT
        d.CodigoMunicipio,
        ISNULL(escuelas.EscuelasActivas, 0),
        ISNULL(escuelas.TotalEstudiantesEscuelas, 0),
        ISNULL(festivales.FestivalesRegistrados, 0),
        ISNULL(mercados.MercadosRegistrados, 0),
        ISNULL(redes.RedesDocumentacionActivas, 0),
        ISNULL(lutieres.LutieresRegistrados, 0),
        SYSUTCDATETIME()
    FROM dbo.Divipola d
    LEFT JOIN (
        SELECT CodigoMunicipio, COUNT(*) AS EscuelasActivas, SUM(ISNULL(CantidadEstudiantes, 0)) AS TotalEstudiantesEscuelas
        FROM dbo.EscuelasMusica
        WHERE Activo = 1 AND EscuelaActiva = 1 AND CodigoMunicipio IS NOT NULL
        GROUP BY CodigoMunicipio
    ) escuelas ON escuelas.CodigoMunicipio = d.CodigoMunicipio
    LEFT JOIN (
        SELECT CodigoMunicipio, COUNT(*) AS FestivalesRegistrados
        FROM dbo.Festivales
        WHERE Activo = 1 AND CodigoMunicipio IS NOT NULL
        GROUP BY CodigoMunicipio
    ) festivales ON festivales.CodigoMunicipio = d.CodigoMunicipio
    LEFT JOIN (
        SELECT CodigoMunicipio, COUNT(*) AS MercadosRegistrados
        FROM dbo.MercadosMusicales
        WHERE Activo = 1 AND CodigoMunicipio IS NOT NULL
        GROUP BY CodigoMunicipio
    ) mercados ON mercados.CodigoMunicipio = d.CodigoMunicipio
    LEFT JOIN (
        SELECT CodigoMunicipio, COUNT(*) AS RedesDocumentacionActivas
        FROM dbo.RedesDocumentacion
        WHERE Activo = 1 AND CodigoMunicipio IS NOT NULL
        GROUP BY CodigoMunicipio
    ) redes ON redes.CodigoMunicipio = d.CodigoMunicipio
    LEFT JOIN (
        SELECT CodigoMunicipio, COUNT(*) AS LutieresRegistrados
        FROM dbo.Lutieres
        WHERE Activo = 1 AND CodigoMunicipio IS NOT NULL
        GROUP BY CodigoMunicipio
    ) lutieres ON lutieres.CodigoMunicipio = d.CodigoMunicipio;
END;
GO
