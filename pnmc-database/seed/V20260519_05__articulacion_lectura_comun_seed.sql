/*
    PNMC - Datos minimos de prueba para articulacion y lectura comun.
*/

DECLARE @TipoFestival int = (SELECT IdTipoRegistroEcosistema FROM dbo.TiposRegistroEcosistema WHERE CodigoTipoRegistro = N'festival');
DECLARE @TipoEscuela int = (SELECT IdTipoRegistroEcosistema FROM dbo.TiposRegistroEcosistema WHERE CodigoTipoRegistro = N'escuela_musica');
DECLARE @TipoMercado int = (SELECT IdTipoRegistroEcosistema FROM dbo.TiposRegistroEcosistema WHERE CodigoTipoRegistro = N'mercado_musical');
DECLARE @TipoRed int = (SELECT IdTipoRegistroEcosistema FROM dbo.TiposRegistroEcosistema WHERE CodigoTipoRegistro = N'red_documentacion');
DECLARE @TipoLutier int = (SELECT IdTipoRegistroEcosistema FROM dbo.TiposRegistroEcosistema WHERE CodigoTipoRegistro = N'lutier');

MERGE dbo.RegistrosEcosistema AS destino
USING (VALUES
    (@TipoFestival, 1, N'Festival de prueba PNMC', '05', '05001', CAST(6.260564 AS decimal(9,6)), CAST(-75.591711 AS decimal(9,6))),
    (@TipoEscuela, 1, N'Escuela de musica de prueba PNMC', '05', '05001', CAST(6.260564 AS decimal(9,6)), CAST(-75.591711 AS decimal(9,6))),
    (@TipoMercado, 1, N'Mercado musical de prueba PNMC', '05', '05001', CAST(6.260564 AS decimal(9,6)), CAST(-75.591711 AS decimal(9,6))),
    (@TipoRed, 1, N'Red de documentacion de prueba PNMC', '05', '05001', CAST(6.260564 AS decimal(9,6)), CAST(-75.591711 AS decimal(9,6))),
    (@TipoLutier, 1, N'Lutier de prueba PNMC', '05', '05001', CAST(6.260564 AS decimal(9,6)), CAST(-75.591711 AS decimal(9,6)))
) AS origen (IdTipoRegistroEcosistema, IdRegistroOrigen, NombreRegistro, CodigoDepartamento, CodigoMunicipio, Latitud, Longitud)
ON destino.IdTipoRegistroEcosistema = origen.IdTipoRegistroEcosistema
   AND destino.IdRegistroOrigen = origen.IdRegistroOrigen
WHEN MATCHED THEN
    UPDATE SET
        NombreRegistro = origen.NombreRegistro,
        CodigoDepartamento = origen.CodigoDepartamento,
        CodigoMunicipio = origen.CodigoMunicipio,
        Latitud = origen.Latitud,
        Longitud = origen.Longitud
WHEN NOT MATCHED THEN
    INSERT (IdTipoRegistroEcosistema, IdRegistroOrigen, NombreRegistro, CodigoDepartamento, CodigoMunicipio, Latitud, Longitud)
    VALUES (origen.IdTipoRegistroEcosistema, origen.IdRegistroOrigen, origen.NombreRegistro, origen.CodigoDepartamento, origen.CodigoMunicipio, origen.Latitud, origen.Longitud);

DECLARE @IdRegistroFestival int = (
    SELECT re.IdRegistroEcosistema
    FROM dbo.RegistrosEcosistema re
    WHERE re.IdTipoRegistroEcosistema = @TipoFestival AND re.IdRegistroOrigen = 1
);

DECLARE @IdTerritorioSonoro int = (
    SELECT TOP 1 IdTerritorioSonoro FROM dbo.TerritoriosSonoros ORDER BY OrdenVisualizacion
);

DECLARE @IdPracticaMusical int = (
    SELECT TOP 1 IdPracticaMusical FROM dbo.PracticasMusicales ORDER BY OrdenVisualizacion
);

IF NOT EXISTS (
    SELECT 1 FROM dbo.RegistrosEcosistemaTerritoriosSonoros
    WHERE IdRegistroEcosistema = @IdRegistroFestival AND IdTerritorioSonoro = @IdTerritorioSonoro
)
BEGIN
    INSERT INTO dbo.RegistrosEcosistemaTerritoriosSonoros
        (IdRegistroEcosistema, IdTerritorioSonoro)
    VALUES
        (@IdRegistroFestival, @IdTerritorioSonoro);
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.RegistrosEcosistemaPracticasMusicales
    WHERE IdRegistroEcosistema = @IdRegistroFestival AND IdPracticaMusical = @IdPracticaMusical
)
BEGIN
    INSERT INTO dbo.RegistrosEcosistemaPracticasMusicales
        (IdRegistroEcosistema, IdPracticaMusical)
    VALUES
        (@IdRegistroFestival, @IdPracticaMusical);
END;

DECLARE @IdEtiquetaTerritorio int = (SELECT IdEtiqueta FROM dbo.Etiquetas WHERE Slug = N'territorio');
DECLARE @IdEtiquetaPNMC int = (SELECT IdEtiqueta FROM dbo.Etiquetas WHERE Slug = N'pnmc');
DECLARE @IdArchivo int = (SELECT TOP 1 IdArchivo FROM dbo.Archivos ORDER BY IdArchivo);

IF NOT EXISTS (SELECT 1 FROM dbo.AgendaEtiquetas WHERE IdAgenda = 1 AND IdEtiqueta = @IdEtiquetaTerritorio)
    INSERT INTO dbo.AgendaEtiquetas (IdAgenda, IdEtiqueta) VALUES (1, @IdEtiquetaTerritorio);

IF NOT EXISTS (SELECT 1 FROM dbo.NoticiasEtiquetas WHERE IdNoticia = 1 AND IdEtiqueta = @IdEtiquetaPNMC)
    INSERT INTO dbo.NoticiasEtiquetas (IdNoticia, IdEtiqueta) VALUES (1, @IdEtiquetaPNMC);

IF NOT EXISTS (SELECT 1 FROM dbo.AlbumesGaleriaEtiquetas WHERE IdAlbum = 1 AND IdEtiqueta = @IdEtiquetaPNMC)
    INSERT INTO dbo.AlbumesGaleriaEtiquetas (IdAlbum, IdEtiqueta) VALUES (1, @IdEtiquetaPNMC);

IF NOT EXISTS (SELECT 1 FROM dbo.AgendaArchivos WHERE IdAgenda = 1 AND IdArchivo = @IdArchivo AND RolArchivo = N'adjunto')
    INSERT INTO dbo.AgendaArchivos (IdAgenda, IdArchivo, RolArchivo, OrdenVisualizacion) VALUES (1, @IdArchivo, N'adjunto', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.NoticiasArchivos WHERE IdNoticia = 1 AND IdArchivo = @IdArchivo AND RolArchivo = N'portada')
    INSERT INTO dbo.NoticiasArchivos (IdNoticia, IdArchivo, RolArchivo, OrdenVisualizacion) VALUES (1, @IdArchivo, N'portada', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.AlbumesGaleriaArchivos WHERE IdAlbum = 1 AND IdArchivo = @IdArchivo AND RolArchivo = N'foto')
    INSERT INTO dbo.AlbumesGaleriaArchivos (IdAlbum, IdArchivo, RolArchivo, OrdenVisualizacion) VALUES (1, @IdArchivo, N'foto', 1);

EXEC dbo.sp_ActualizarMetricasMapa;
