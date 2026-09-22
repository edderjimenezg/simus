/*
    PNMC - Datos minimos de prueba para contenidos y modulos.
*/

DECLARE @IdUsuarioSistema int = (SELECT IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'sistema@pnmc.local');
DECLARE @IdEstadoBorrador int = (SELECT IdEstadoContenido FROM dbo.EstadosContenido WHERE CodigoEstado = N'borrador');
DECLARE @IdEstadoPublicado int = (SELECT IdEstadoContenido FROM dbo.EstadosContenido WHERE CodigoEstado = N'publicado');
DECLARE @IdCategoriaAgenda int = (SELECT TOP 1 IdCategoria FROM dbo.Categorias WHERE CodigoModulo = N'agenda' AND Slug = N'encuentro');
DECLARE @IdCategoriaNoticias int = (SELECT TOP 1 IdCategoria FROM dbo.Categorias WHERE CodigoModulo = N'noticias' AND Slug = N'institucional');
DECLARE @IdCategoriaGaleria int = (SELECT TOP 1 IdCategoria FROM dbo.Categorias WHERE CodigoModulo = N'galeria' AND Slug = N'archivo-fotografico');

IF NOT EXISTS (SELECT 1 FROM dbo.Festivales WHERE NombreFestival = N'Festival de prueba PNMC')
BEGIN
    INSERT INTO dbo.Festivales
        (NombreFestival, NumeroVersiones, FechaUltimaVersion, Descripcion, Organizador, CorreoOrganizador,
         TelefonoOrganizador, SitioWebOrganizador, CorreoFestival, InstagramFestival, FacebookFestival,
         SitioWebFestival, OtroEnlaceFestival, TelefonoFestival, TieneVersionVigenteAnoActual,
         EstadoVersionAnoActual, FechaInicioVersionActual, FechaFinVersionActual, NivelCobertura,
         CodigoDepartamento, CodigoMunicipio, Activo, EstadoRegistro, OrganizacionPrincipalId)
    VALUES
        (N'Festival de prueba PNMC', 1, CONVERT(date, '2026-05-19'), N'Registro de prueba para validar estructura de festivales.',
         N'Sistema PNMC', N'sistema@pnmc.local', NULL, NULL, N'festival@pnmc.local', NULL, NULL,
         NULL, NULL, NULL, 1, N'programada', CONVERT(date, '2026-06-01'), CONVERT(date, '2026-06-03'),
         N'municipal', '05', '05001', 1, N'borrador',
         -- responde la institucion. La crea seed/V20260519_03, que corre antes que este fichero.
         (SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad));
END;

DECLARE @IdFestivalPrueba int = (SELECT IdFestival FROM dbo.Festivales WHERE NombreFestival = N'Festival de prueba PNMC');

IF NOT EXISTS (SELECT 1 FROM dbo.Agenda WHERE Titulo = N'Encuentro de prueba PNMC')
BEGIN
    INSERT INTO dbo.Agenda
        (Titulo, DescripcionCorta, DescripcionLarga, IdCategoria, FechaInicio, FechaFin, HoraInicio, HoraFin,
         NivelCobertura, CodigoDepartamento, CodigoMunicipio, LugarEspecifico, Organizador, UrlMasInformacion,
         IdFestival, IdEstadoContenido, OrdenVisualizacion, IdUsuarioCreador)
    VALUES
        (N'Encuentro de prueba PNMC', N'Evento de prueba para validar agenda.', N'Descripcion larga de prueba.',
         @IdCategoriaAgenda, CONVERT(date, '2026-06-01'), CONVERT(date, '2026-06-01'), '09:00', '11:00',
         N'municipal', '05', '05001', N'Medellin', N'Sistema PNMC', NULL,
         @IdFestivalPrueba, @IdEstadoBorrador, 1, @IdUsuarioSistema);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Noticias WHERE Slug = N'noticia-prueba-pnmc')
BEGIN
    INSERT INTO dbo.Noticias
        (Titulo, Slug, Entradilla, Cuerpo, CitaDestacada, Autor, IdCategoria, FechaPublicacion,
         UrlExterna, UrlEmbed, IdEstadoContenido, OrdenVisualizacion, IdUsuarioCreador)
    VALUES
        (N'Noticia de prueba PNMC', N'noticia-prueba-pnmc', N'Entradilla de prueba.',
         N'Cuerpo de prueba para validar la estructura de noticias.', NULL, N'Equipo PNMC',
         @IdCategoriaNoticias, SYSUTCDATETIME(), NULL, NULL, @IdEstadoBorrador, 1, @IdUsuarioSistema);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.AlbumesGaleria WHERE TituloAlbum = N'Album de prueba PNMC')
BEGIN
    INSERT INTO dbo.AlbumesGaleria
        (TituloAlbum, DescripcionAlbum, IdCategoria, IdEstadoContenido, OrdenVisualizacion, IdUsuarioCreador)
    VALUES
        (N'Album de prueba PNMC', N'Album inicial para validar galeria.', @IdCategoriaGaleria, @IdEstadoBorrador, 1, @IdUsuarioSistema);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.EscuelasMusica WHERE NombreEscuela = N'Escuela de musica de prueba PNMC')
BEGIN
    INSERT INTO dbo.EscuelasMusica
        (NombreEscuela, CategoriaEscuela, TipoEscuela, EntidadResponsable, NombreDirector, CorreoContacto,
         TelefonoContacto, NivelCobertura, CodigoDepartamento, CodigoMunicipio, LugarEspecifico, Direccion,
         Latitud, Longitud, CapacidadFormativa, CantidadEstudiantes, CantidadGruposActivos,
         ProcesosFormativos, PracticasMusicales, EscuelaActiva, Observaciones, Activo, EstadoRegistro,
         OrganizacionResponsableId)
    VALUES
        (N'Escuela de musica de prueba PNMC', N'Municipal', N'Publica', N'Sistema PNMC', NULL, N'escuela@pnmc.local',
         NULL, N'municipal', '05', '05001', N'Medellin', NULL, 6.260564, -75.591711, 120, 80, 3,
         N'Iniciacion musical; ensamble', N'Musicas comunitarias', 1, N'Registro de prueba.', 1, N'borrador',
         -- nadie lo reclame, responde la institucion; la crea seed/V20260519_03.
         (SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad));
END;

IF NOT EXISTS (SELECT 1 FROM dbo.MercadosMusicales WHERE NombreMercado = N'Mercado musical de prueba PNMC')
BEGIN
    INSERT INTO dbo.MercadosMusicales
        (NombreMercado, NumeroEdiciones, Periodicidad, Descripcion, TieneEdicionVigenteAnoActual,
         EstadoEdicionAnoActual, FechaInicioEdicionActual, FechaFinEdicionActual, EntidadResponsable,
         IdFestivalAsociado, NombreFestivalAsociado, Alcance, Modalidad, NivelCobertura,
         CodigoDepartamento, CodigoMunicipio, LugarEspecifico, Activo, EstadoRegistro,
         OrganizacionResponsableId)
    VALUES
        (N'Mercado musical de prueba PNMC', 1, N'Anual', N'Registro de prueba para mercados musicales.',
         1, N'programada', CONVERT(date, '2026-06-02'), CONVERT(date, '2026-06-03'), N'Sistema PNMC',
         @IdFestivalPrueba, N'Festival de prueba PNMC', N'local', N'presencial', N'municipal',
         '05', '05001', N'Medellin', 1, N'borrador',
         -- nadie lo reclame, responde la institucion; la crea seed/V20260519_03.
         (SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad));
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RedesDocumentacion WHERE Nombre = N'Red de documentacion de prueba PNMC')
BEGIN
    INSERT INTO dbo.RedesDocumentacion
        (Nombre, TipoCentro, NivelCobertura, CodigoDepartamento, CodigoMunicipio, Zona, Latitud, Longitud,
         Descripcion, CorreoContacto, SitioWeb, Activo, EstadoRegistro,
         OrganizacionResponsableId)
    VALUES
        (N'Red de documentacion de prueba PNMC', N'Archivo sonoro', N'municipal', '05', '05001', N'Urbana',
         6.260564, -75.591711, N'Registro de prueba para redes de documentacion.', N'documentacion@pnmc.local',
         NULL, 1, N'borrador',
         -- nadie lo reclame, responde la institucion; la crea seed/V20260519_03.
         (SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad));
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Lutieres WHERE Nombre = N'Lutier de prueba PNMC')
BEGIN
    INSERT INTO dbo.Lutieres
        (Nombre, TipoLutier, NombreTaller, Especialidad, Instrumentos, Descripcion, NombreContacto,
         CorreoContacto, TelefonoContacto, NivelCobertura, CodigoDepartamento, CodigoMunicipio,
         Direccion, Zona, Latitud, Longitud, Activo, EstadoRegistro,
         OrganizacionResponsableId)
    VALUES
        (N'Lutier de prueba PNMC', N'individual', NULL, N'Cuerdas pulsadas', N'Guitarra; tiple',
         N'Registro de prueba para luteria.', N'Lutier de prueba', N'lutier@pnmc.local', NULL,
         N'municipal', '05', '05001', NULL, N'Urbana', 6.260564, -75.591711, 1, N'borrador',
         -- nadie lo reclame, responde la institucion; la crea seed/V20260519_03.
         (SELECT TOP (1) IdEntidad FROM dbo.Entidades WHERE EsInstitucional = 1 ORDER BY IdEntidad));
END;

