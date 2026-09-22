/*
    PNMC - Tablas fuente de contenido y modulos
    Fase: reconstruccion local desde cero
*/

IF OBJECT_ID(N'dbo.Festivales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Festivales
    (
        IdFestival int IDENTITY(1,1) NOT NULL,
        NombreFestival nvarchar(220) NOT NULL,
        NumeroVersiones int NULL,
        FechaUltimaVersion date NULL,
        Descripcion nvarchar(max) NULL,
        Organizador nvarchar(220) NULL,
        CorreoOrganizador nvarchar(180) NULL,
        TelefonoOrganizador nvarchar(80) NULL,
        SitioWebOrganizador nvarchar(500) NULL,
        CorreoFestival nvarchar(180) NULL,
        InstagramFestival nvarchar(500) NULL,
        FacebookFestival nvarchar(500) NULL,
        SitioWebFestival nvarchar(500) NULL,
        OtroEnlaceFestival nvarchar(500) NULL,
        TelefonoFestival nvarchar(80) NULL,
        TieneVersionVigenteAnoActual bit NOT NULL CONSTRAINT DF_Festivales_TieneVersionVigente DEFAULT (0),
        EstadoVersionAnoActual nvarchar(80) NULL,
        FechaInicioVersionActual date NULL,
        FechaFinVersionActual date NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Activo bit NOT NULL CONSTRAINT DF_Festivales_Activo DEFAULT (1),
        EstadoRegistro nvarchar(80) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Festivales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_Festivales PRIMARY KEY (IdFestival),
        CONSTRAINT FK_Festivales_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_Festivales_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT CK_Festivales_NumeroVersiones CHECK (NumeroVersiones IS NULL OR NumeroVersiones >= 0),
        CONSTRAINT CK_Festivales_FechasVersionActual CHECK (FechaFinVersionActual IS NULL OR FechaInicioVersionActual IS NULL OR FechaFinVersionActual >= FechaInicioVersionActual),
        CONSTRAINT CK_Festivales_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Festivales_Territorio' AND object_id = OBJECT_ID(N'dbo.Festivales'))
BEGIN
    CREATE INDEX IX_Festivales_Territorio ON dbo.Festivales (CodigoDepartamento, CodigoMunicipio);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Festivales_Estado_Activo' AND object_id = OBJECT_ID(N'dbo.Festivales'))
BEGIN
    CREATE INDEX IX_Festivales_Estado_Activo ON dbo.Festivales (EstadoRegistro, Activo);
END;

IF OBJECT_ID(N'dbo.Agenda', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Agenda
    (
        IdAgenda int IDENTITY(1,1) NOT NULL,
        Titulo nvarchar(240) NOT NULL,
        DescripcionCorta nvarchar(600) NULL,
        DescripcionLarga nvarchar(max) NULL,
        IdCategoria int NULL,
        FechaInicio date NOT NULL,
        FechaFin date NULL,
        HoraInicio time(0) NULL,
        HoraFin time(0) NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        LugarEspecifico nvarchar(300) NULL,
        Organizador nvarchar(220) NULL,
        UrlMasInformacion nvarchar(500) NULL,
        IdFestival int NULL,
        IdEstadoContenido int NOT NULL,
        OrdenVisualizacion int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Agenda_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,
        FechaArchivo datetime2(0) NULL,
        IdUsuarioCreador int NOT NULL,
        IdUsuarioRevisor int NULL,
        IdUsuarioAprobador int NULL,
        CONSTRAINT PK_Agenda PRIMARY KEY (IdAgenda),
        CONSTRAINT FK_Agenda_Categorias FOREIGN KEY (IdCategoria) REFERENCES dbo.Categorias (IdCategoria),
        CONSTRAINT FK_Agenda_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_Agenda_Festivales FOREIGN KEY (IdFestival) REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT FK_Agenda_EstadosContenido FOREIGN KEY (IdEstadoContenido) REFERENCES dbo.EstadosContenido (IdEstadoContenido),
        CONSTRAINT FK_Agenda_UsuarioCreador FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_Agenda_UsuarioRevisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_Agenda_UsuarioAprobador FOREIGN KEY (IdUsuarioAprobador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_Agenda_Fechas CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio),
        CONSTRAINT CK_Agenda_Horas CHECK (HoraFin IS NULL OR HoraInicio IS NULL OR HoraFin >= HoraInicio),
        CONSTRAINT CK_Agenda_Orden CHECK (OrdenVisualizacion IS NULL OR OrdenVisualizacion > 0),
        CONSTRAINT CK_Agenda_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Agenda_Fechas' AND object_id = OBJECT_ID(N'dbo.Agenda'))
BEGIN
    CREATE INDEX IX_Agenda_Fechas ON dbo.Agenda (FechaInicio, FechaFin);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Agenda_Territorio' AND object_id = OBJECT_ID(N'dbo.Agenda'))
BEGIN
    CREATE INDEX IX_Agenda_Territorio ON dbo.Agenda (CodigoDepartamento, CodigoMunicipio);
END;

IF OBJECT_ID(N'dbo.Noticias', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Noticias
    (
        IdNoticia int IDENTITY(1,1) NOT NULL,
        Titulo nvarchar(260) NOT NULL,
        Slug nvarchar(280) NOT NULL,
        Entradilla nvarchar(800) NULL,
        Cuerpo nvarchar(max) NULL,
        CitaDestacada nvarchar(600) NULL,
        Autor nvarchar(180) NULL,
        IdCategoria int NULL,
        FechaPublicacion datetime2(0) NULL,
        UrlExterna nvarchar(500) NULL,
        UrlEmbed nvarchar(500) NULL,
        IdEstadoContenido int NOT NULL,
        OrdenVisualizacion int NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Noticias_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaArchivo datetime2(0) NULL,
        IdUsuarioCreador int NOT NULL,
        IdUsuarioRevisor int NULL,
        IdUsuarioAprobador int NULL,
        CONSTRAINT PK_Noticias PRIMARY KEY (IdNoticia),
        CONSTRAINT UQ_Noticias_Slug UNIQUE (Slug),
        CONSTRAINT FK_Noticias_Categorias FOREIGN KEY (IdCategoria) REFERENCES dbo.Categorias (IdCategoria),
        CONSTRAINT FK_Noticias_EstadosContenido FOREIGN KEY (IdEstadoContenido) REFERENCES dbo.EstadosContenido (IdEstadoContenido),
        CONSTRAINT FK_Noticias_UsuarioCreador FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_Noticias_UsuarioRevisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_Noticias_UsuarioAprobador FOREIGN KEY (IdUsuarioAprobador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_Noticias_Orden CHECK (OrdenVisualizacion IS NULL OR OrdenVisualizacion > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Noticias_Estado_FechaPublicacion' AND object_id = OBJECT_ID(N'dbo.Noticias'))
BEGIN
    CREATE INDEX IX_Noticias_Estado_FechaPublicacion ON dbo.Noticias (IdEstadoContenido, FechaPublicacion DESC);
END;

IF OBJECT_ID(N'dbo.AlbumesGaleria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlbumesGaleria
    (
        IdAlbum int IDENTITY(1,1) NOT NULL,
        TituloAlbum nvarchar(240) NOT NULL,
        DescripcionAlbum nvarchar(max) NULL,
        IdCategoria int NULL,
        IdEstadoContenido int NOT NULL,
        OrdenVisualizacion int NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_AlbumesGaleria_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        FechaPublicacion datetime2(0) NULL,
        FechaArchivo datetime2(0) NULL,
        IdUsuarioCreador int NOT NULL,
        IdUsuarioRevisor int NULL,
        IdUsuarioAprobador int NULL,
        CONSTRAINT PK_AlbumesGaleria PRIMARY KEY (IdAlbum),
        CONSTRAINT FK_AlbumesGaleria_Categorias FOREIGN KEY (IdCategoria) REFERENCES dbo.Categorias (IdCategoria),
        CONSTRAINT FK_AlbumesGaleria_EstadosContenido FOREIGN KEY (IdEstadoContenido) REFERENCES dbo.EstadosContenido (IdEstadoContenido),
        CONSTRAINT FK_AlbumesGaleria_UsuarioCreador FOREIGN KEY (IdUsuarioCreador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_AlbumesGaleria_UsuarioRevisor FOREIGN KEY (IdUsuarioRevisor) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT FK_AlbumesGaleria_UsuarioAprobador FOREIGN KEY (IdUsuarioAprobador) REFERENCES dbo.Usuarios (IdUsuario),
        CONSTRAINT CK_AlbumesGaleria_Orden CHECK (OrdenVisualizacion > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AlbumesGaleria_Estado_Orden' AND object_id = OBJECT_ID(N'dbo.AlbumesGaleria'))
BEGIN
    CREATE INDEX IX_AlbumesGaleria_Estado_Orden ON dbo.AlbumesGaleria (IdEstadoContenido, OrdenVisualizacion);
END;

IF OBJECT_ID(N'dbo.EscuelasMusica', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EscuelasMusica
    (
        IdEscuelaMusica int IDENTITY(1,1) NOT NULL,
        NombreEscuela nvarchar(240) NOT NULL,
        CategoriaEscuela nvarchar(120) NULL,
        TipoEscuela nvarchar(120) NULL,
        EntidadResponsable nvarchar(240) NULL,
        NombreDirector nvarchar(180) NULL,
        CorreoContacto nvarchar(180) NULL,
        TelefonoContacto nvarchar(80) NULL,
        SitioWeb nvarchar(500) NULL,
        Instagram nvarchar(500) NULL,
        Facebook nvarchar(500) NULL,
        OtroEnlace nvarchar(500) NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        LugarEspecifico nvarchar(300) NULL,
        Direccion nvarchar(300) NULL,
        Latitud decimal(9,6) NULL,
        Longitud decimal(9,6) NULL,
        CapacidadFormativa int NULL,
        CantidadEstudiantes int NULL,
        CantidadGruposActivos int NULL,
        ProcesosFormativos nvarchar(max) NULL,
        PracticasMusicales nvarchar(max) NULL,
        EscuelaActiva bit NOT NULL CONSTRAINT DF_EscuelasMusica_EscuelaActiva DEFAULT (1),
        Observaciones nvarchar(max) NULL,
        Activo bit NOT NULL CONSTRAINT DF_EscuelasMusica_Activo DEFAULT (1),
        EstadoRegistro nvarchar(80) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_EscuelasMusica_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_EscuelasMusica PRIMARY KEY (IdEscuelaMusica),
        CONSTRAINT FK_EscuelasMusica_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_EscuelasMusica_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT CK_EscuelasMusica_Capacidad CHECK (CapacidadFormativa IS NULL OR CapacidadFormativa >= 0),
        CONSTRAINT CK_EscuelasMusica_Estudiantes CHECK (CantidadEstudiantes IS NULL OR CantidadEstudiantes >= 0),
        CONSTRAINT CK_EscuelasMusica_Grupos CHECK (CantidadGruposActivos IS NULL OR CantidadGruposActivos >= 0),
        CONSTRAINT CK_EscuelasMusica_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EscuelasMusica_Territorio' AND object_id = OBJECT_ID(N'dbo.EscuelasMusica'))
BEGIN
    CREATE INDEX IX_EscuelasMusica_Territorio ON dbo.EscuelasMusica (CodigoDepartamento, CodigoMunicipio);
END;

IF OBJECT_ID(N'dbo.MercadosMusicales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MercadosMusicales
    (
        IdMercadoMusical int IDENTITY(1,1) NOT NULL,
        NombreMercado nvarchar(240) NOT NULL,
        NumeroEdiciones int NULL,
        Periodicidad nvarchar(120) NULL,
        Descripcion nvarchar(max) NULL,
        TieneEdicionVigenteAnoActual bit NOT NULL CONSTRAINT DF_MercadosMusicales_TieneEdicionVigente DEFAULT (0),
        EstadoEdicionAnoActual nvarchar(80) NULL,
        FechaInicioEdicionActual date NULL,
        FechaFinEdicionActual date NULL,
        EntidadResponsable nvarchar(240) NULL,
        CorreoEntidadResponsable nvarchar(180) NULL,
        TelefonoEntidadResponsable nvarchar(80) NULL,
        SitioWebEntidadResponsable nvarchar(500) NULL,
        IdFestivalAsociado int NULL,
        NombreFestivalAsociado nvarchar(240) NULL,
        Alcance nvarchar(120) NULL,
        Modalidad nvarchar(120) NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        LugarEspecifico nvarchar(300) NULL,
        Activo bit NOT NULL CONSTRAINT DF_MercadosMusicales_Activo DEFAULT (1),
        EstadoRegistro nvarchar(80) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_MercadosMusicales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_MercadosMusicales PRIMARY KEY (IdMercadoMusical),
        CONSTRAINT FK_MercadosMusicales_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_MercadosMusicales_Festivales FOREIGN KEY (IdFestivalAsociado)
            REFERENCES dbo.Festivales (IdFestival),
        CONSTRAINT FK_MercadosMusicales_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT CK_MercadosMusicales_NumeroEdiciones CHECK (NumeroEdiciones IS NULL OR NumeroEdiciones >= 0),
        CONSTRAINT CK_MercadosMusicales_FechasEdicion CHECK (FechaFinEdicionActual IS NULL OR FechaInicioEdicionActual IS NULL OR FechaFinEdicionActual >= FechaInicioEdicionActual),
        CONSTRAINT CK_MercadosMusicales_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MercadosMusicales_Territorio' AND object_id = OBJECT_ID(N'dbo.MercadosMusicales'))
BEGIN
    CREATE INDEX IX_MercadosMusicales_Territorio ON dbo.MercadosMusicales (CodigoDepartamento, CodigoMunicipio);
END;

IF OBJECT_ID(N'dbo.RedesDocumentacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RedesDocumentacion
    (
        IdRedDocumentacion int IDENTITY(1,1) NOT NULL,
        Nombre nvarchar(240) NOT NULL,
        TipoCentro nvarchar(140) NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Zona nvarchar(120) NULL,
        Latitud decimal(9,6) NULL,
        Longitud decimal(9,6) NULL,
        Descripcion nvarchar(max) NULL,
        CorreoContacto nvarchar(180) NULL,
        SitioWeb nvarchar(500) NULL,
        Facebook nvarchar(500) NULL,
        Instagram nvarchar(500) NULL,
        OtroEnlace nvarchar(500) NULL,
        Activo bit NOT NULL CONSTRAINT DF_RedesDocumentacion_Activo DEFAULT (1),
        EstadoRegistro nvarchar(80) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_RedesDocumentacion_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_RedesDocumentacion PRIMARY KEY (IdRedDocumentacion),
        CONSTRAINT FK_RedesDocumentacion_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_RedesDocumentacion_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT CK_RedesDocumentacion_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;

IF OBJECT_ID(N'dbo.Lutieres', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lutieres
    (
        IdLutier int IDENTITY(1,1) NOT NULL,
        Nombre nvarchar(220) NOT NULL,
        TipoLutier nvarchar(80) NOT NULL,
        NombreTaller nvarchar(220) NULL,
        Especialidad nvarchar(300) NULL,
        Instrumentos nvarchar(max) NULL,
        Descripcion nvarchar(max) NULL,
        NombreContacto nvarchar(180) NULL,
        CorreoContacto nvarchar(180) NULL,
        TelefonoContacto nvarchar(80) NULL,
        SitioWeb nvarchar(500) NULL,
        Facebook nvarchar(500) NULL,
        Instagram nvarchar(500) NULL,
        OtroEnlace nvarchar(500) NULL,
        NivelCobertura nvarchar(40) NOT NULL,
        CodigoDepartamento char(2) NULL,
        CodigoMunicipio char(5) NULL,
        Direccion nvarchar(300) NULL,
        Zona nvarchar(120) NULL,
        Latitud decimal(9,6) NULL,
        Longitud decimal(9,6) NULL,
        Activo bit NOT NULL CONSTRAINT DF_Lutieres_Activo DEFAULT (1),
        EstadoRegistro nvarchar(80) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Lutieres_FechaCreacion DEFAULT (SYSUTCDATETIME()),
        FechaActualizacion datetime2(0) NULL,
        CONSTRAINT PK_Lutieres PRIMARY KEY (IdLutier),
        CONSTRAINT FK_Lutieres_Divipola FOREIGN KEY (CodigoDepartamento, CodigoMunicipio)
            REFERENCES dbo.Divipola (CodigoDepartamento, CodigoMunicipio),
        CONSTRAINT FK_Lutieres_EstadosContenido FOREIGN KEY (EstadoRegistro)
            REFERENCES dbo.EstadosContenido (CodigoEstado),
        CONSTRAINT CK_Lutieres_Tipo CHECK (TipoLutier IN (N'individual', N'taller', N'colectivo')),
        CONSTRAINT CK_Lutieres_NivelCobertura CHECK (
            NivelCobertura IN (N'nacional', N'departamental', N'municipal')
            AND (
                (NivelCobertura = N'nacional' AND CodigoDepartamento IS NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'departamental' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NULL)
                OR (NivelCobertura = N'municipal' AND CodigoDepartamento IS NOT NULL AND CodigoMunicipio IS NOT NULL)
            )
        )
    );
END;
