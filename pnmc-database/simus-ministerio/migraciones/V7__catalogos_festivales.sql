/*
  Valores de los catálogos propios de Festivales y su correspondencia con los ART_MUS_*.
  Los identificadores heredados no se reutilizan: el enlace entre ambos queda en
  CorrespondenciasHeredadas.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT INTO dbo.TipologiasFestival (NombreTipologiaFestival, Slug, OrdenVisualizacion)
VALUES
    (N'Fiesta popular', N'fiesta-popular', 1),
    (N'Celebración religiosa', N'celebracion-religiosa', 2),
    (N'Festival de música', N'festival-de-musica', 3),
    (N'Reinado', N'reinado', 4),
    (N'Feria', N'feria', 5),
    (N'Conferencia', N'conferencia', 6),
    (N'Mercado', N'mercado', 7),
    (N'Mercados culturales', N'mercados-culturales', 8),
    (N'Encuentros', N'encuentros', 9),
    (N'Otra', N'otra', 10);

INSERT INTO dbo.ExpresionesArtisticas (NombreExpresionArtistica, Slug, OrdenVisualizacion)
VALUES
    (N'Música', N'musica', 1),
    (N'Danza', N'danza', 2),
    (N'Culinaria', N'culinaria', 3),
    (N'Artesanía', N'artesania', 4),
    (N'Cestería', N'cesteria', 5),
    (N'Narración', N'narracion', 6),
    (N'Cuentería', N'cuenteria', 7),
    (N'Improvisación', N'improvisacion', 8),
    (N'Artes plásticas', N'artes-plasticas', 9),
    (N'Otra', N'otra', 10),
    (N'Ninguna', N'ninguna', 11);

INSERT INTO dbo.FuentesFinanciacion (NombreFuenteFinanciacion, Slug, OrdenVisualizacion)
VALUES
    (N'Aportes privados', N'aportes-privados', 1),
    (N'Boletería', N'boleteria', 2),
    (N'Estímulos', N'estimulos', 3),
    (N'Concertación', N'concertacion', 4),
    (N'Ministerio de las Culturas, las Artes y los Saberes', N'mincultura', 5),
    (N'Gobernación', N'gobernacion', 6),
    (N'Alcaldía', N'alcaldia', 7),
    (N'Recursos propios', N'recursos-propios', 8),
    (N'Otra', N'otra', 9);

INSERT INTO dbo.ModalidadesParticipacion (NombreModalidadParticipacion, Slug, OrdenVisualizacion)
VALUES
    (N'Concurso', N'concurso', 1),
    (N'Por convocatoria', N'por-convocatoria', 2),
    (N'Invitados pagos', N'invitados-pagos', 3),
    (N'Invitados sin paga', N'invitados-sin-paga', 4),
    (N'Otra', N'otra', 5);

INSERT INTO dbo.TiposIngreso (NombreTipoIngreso, Slug, OrdenVisualizacion)
VALUES
    (N'Entrada libre (sin boletería)', N'entrada-libre', 1),
    (N'Entrada gratuita (con boletería)', N'entrada-gratuita', 2),
    (N'Boletería paga', N'boleteria-paga', 3),
    (N'Sin público (transmisión en vivo)', N'sin-publico', 4);

-- «Otro» habilita el campo OtroTipoOrganizador.
INSERT INTO dbo.TiposOrganizador (NombreTipoOrganizador, Slug, OrdenVisualizacion)
VALUES
    (N'Entidad pública / Alcaldía', N'entidad-publica-alcaldia', 1),
    (N'Entidad pública / Gobernación', N'entidad-publica-gobernacion', 2),
    (N'Fundación', N'fundacion', 3),
    (N'Museo', N'museo', 4),
    (N'Casa cultural', N'casa-cultural', 5),
    (N'Comunitario', N'comunitario', 6),
    (N'Mixto', N'mixto', 7),
    (N'Otro', N'otro', 8);
GO

DECLARE @Pares TABLE (TablaHeredada sysname, IdHeredado int, TablaDestino sysname, Slug nvarchar(160));
INSERT INTO @Pares VALUES
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 2, N'TipologiasFestival', N'fiesta-popular'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 4, N'TipologiasFestival', N'celebracion-religiosa'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 5, N'TipologiasFestival', N'festival-de-musica'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 6, N'TipologiasFestival', N'reinado'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 7, N'TipologiasFestival', N'feria'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 8, N'TipologiasFestival', N'conferencia'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 9, N'TipologiasFestival', N'mercado'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 10, N'TipologiasFestival', N'mercados-culturales'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 12, N'TipologiasFestival', N'encuentros'),
    (N'ART_MUS_FESTIVALES_TIPOLOGIA', 14, N'TipologiasFestival', N'otra'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 1, N'ExpresionesArtisticas', N'musica'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 2, N'ExpresionesArtisticas', N'danza'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 3, N'ExpresionesArtisticas', N'culinaria'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 4, N'ExpresionesArtisticas', N'artesania'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 5, N'ExpresionesArtisticas', N'cesteria'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 6, N'ExpresionesArtisticas', N'narracion'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 7, N'ExpresionesArtisticas', N'cuenteria'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 9, N'ExpresionesArtisticas', N'improvisacion'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 11, N'ExpresionesArtisticas', N'otra'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 12, N'ExpresionesArtisticas', N'ninguna'),
    (N'ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS', 13, N'ExpresionesArtisticas', N'artes-plasticas'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 1, N'FuentesFinanciacion', N'aportes-privados'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 2, N'FuentesFinanciacion', N'boleteria'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 3, N'FuentesFinanciacion', N'estimulos'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 4, N'FuentesFinanciacion', N'concertacion'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 5, N'FuentesFinanciacion', N'mincultura'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 6, N'FuentesFinanciacion', N'gobernacion'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 7, N'FuentesFinanciacion', N'alcaldia'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 8, N'FuentesFinanciacion', N'recursos-propios'),
    (N'ART_MUS_FESTIVALES_FUENTE_FINANCIACION', 9, N'FuentesFinanciacion', N'otra'),
    (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 1, N'ModalidadesParticipacion', N'concurso'),
    (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 3, N'ModalidadesParticipacion', N'invitados-pagos'),
    (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 4, N'ModalidadesParticipacion', N'invitados-sin-paga'),
    (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 6, N'ModalidadesParticipacion', N'por-convocatoria'),
    (N'ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION', 7, N'ModalidadesParticipacion', N'otra'),
    (N'ART_MUS_TIPOINGRESO', 1, N'TiposIngreso', N'entrada-libre'),
    (N'ART_MUS_TIPOINGRESO', 2, N'TiposIngreso', N'entrada-gratuita'),
    (N'ART_MUS_TIPOINGRESO', 3, N'TiposIngreso', N'boleteria-paga'),
    (N'ART_MUS_TIPOINGRESO', 4, N'TiposIngreso', N'sin-publico'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 1, N'TiposOrganizador', N'entidad-publica-alcaldia'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 2, N'TiposOrganizador', N'entidad-publica-gobernacion'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 3, N'TiposOrganizador', N'fundacion'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 4, N'TiposOrganizador', N'museo'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 5, N'TiposOrganizador', N'casa-cultural'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 6, N'TiposOrganizador', N'comunitario'),
    (N'ART_MUS_FESTIVALES_TIPO_ORGANIZADOR', 7, N'TiposOrganizador', N'mixto');

DECLARE @Heredadas TABLE (TablaHeredada sysname, IdHeredado int);
DECLARE @Tabla sysname, @Sql nvarchar(max);
DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT TablaHeredada FROM @Pares;
OPEN tablas;
FETCH NEXT FROM tablas INTO @Tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + QUOTENAME(@Tabla), N'U') IS NOT NULL
    BEGIN
        SET @Sql = N'SELECT @t, ID FROM dbo.' + QUOTENAME(@Tabla) + N';';
        INSERT INTO @Heredadas EXEC sys.sp_executesql @Sql, N'@t sysname', @t = @Tabla;
    END
    FETCH NEXT FROM tablas INTO @Tabla;
END
CLOSE tablas;
DEALLOCATE tablas;

INSERT INTO dbo.CorrespondenciasHeredadas (TablaHeredada, IdHeredado, TablaDestino, IdDestino)
SELECT p.TablaHeredada, CAST(p.IdHeredado AS nvarchar(64)), p.TablaDestino,
       CAST(COALESCE(tf.IdTipologiaFestival, ea.IdExpresionArtistica, ff.IdFuenteFinanciacion,
                     mp.IdModalidadParticipacion, ti.IdTipoIngreso, tor.IdTipoOrganizador) AS nvarchar(120))
FROM @Pares AS p
JOIN @Heredadas AS h ON h.TablaHeredada = p.TablaHeredada AND h.IdHeredado = p.IdHeredado
LEFT JOIN dbo.TipologiasFestival AS tf ON p.TablaDestino = N'TipologiasFestival' AND tf.Slug = p.Slug
LEFT JOIN dbo.ExpresionesArtisticas AS ea ON p.TablaDestino = N'ExpresionesArtisticas' AND ea.Slug = p.Slug
LEFT JOIN dbo.FuentesFinanciacion AS ff ON p.TablaDestino = N'FuentesFinanciacion' AND ff.Slug = p.Slug
LEFT JOIN dbo.ModalidadesParticipacion AS mp ON p.TablaDestino = N'ModalidadesParticipacion' AND mp.Slug = p.Slug
LEFT JOIN dbo.TiposIngreso AS ti ON p.TablaDestino = N'TiposIngreso' AND ti.Slug = p.Slug
LEFT JOIN dbo.TiposOrganizador AS tor ON p.TablaDestino = N'TiposOrganizador' AND tor.Slug = p.Slug;
GO
