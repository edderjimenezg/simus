/*
  Datos de referencia sin los cuales el sistema no funciona: estados, tipos de documento, la
  entidad institucional del Programa, los catálogos transversales con sus fichas y la
  correspondencia de los identificadores de los catálogos ART_MUS_* con los nuevos.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

INSERT INTO dbo.EstadosContenido (CodigoEstado, NombreEstado, DescripcionEstado)
VALUES
    (N'borrador',                  N'Borrador',                  N'Contenido en elaboración interna.'),
    (N'en_revision',               N'En revisión',               N'Contenido enviado a revisión editorial o técnica.'),
    (N'ajustes_solicitados',       N'Ajustes solicitados',       N'Contenido devuelto al responsable para corregir campos u observaciones.'),
    (N'aprobado',                  N'Aprobado',                  N'Contenido aprobado, pendiente de publicación o activación.'),
    (N'publicado',                 N'Publicado',                 N'Contenido visible para usuarios finales.'),
    (N'rechazado',                 N'Rechazado',                 N'Contenido revisado y no aprobado para publicación.'),
    (N'archivado',                 N'Archivado',                 N'Contenido retirado de la vista pública sin eliminarlo.'),
    (N'registrada',                N'Registrada',                N'Entidad dada de alta por su responsable, activa y sin aprobación ministerial previa.'),
    (N'pendiente_de_confirmacion', N'Pendiente de confirmación', N'Organización cuya cuenta responsable todavía no ha confirmado su correo.'),
    (N'activa',                    N'Activa',                    N'Organización que opera con normalidad en la plataforma.'),
    (N'inactiva',                  N'Inactiva',                  N'Organización suspendida: conserva su ficha y no puede entrar.'),
    (N'eliminada',                 N'Eliminada',                 N'Organización retirada del ecosistema; sus procesos vuelven al Programa.');

INSERT INTO dbo.TiposDocumento (CodigoTipoDocumento, NombreTipoDocumento, OrdenVisualizacion)
VALUES
    (N'cc', N'Cédula de ciudadanía', 1),
    (N'ce', N'Cédula de extranjería', 2),
    (N'ti', N'Tarjeta de identidad', 3),
    (N'rc', N'Registro civil de nacimiento', 4),
    (N'nuip', N'Número único de identificación personal', 5),
    (N'pa', N'Pasaporte', 6),
    (N'pep', N'Permiso especial de permanencia', 7),
    (N'ppt', N'Permiso por protección temporal', 8),
    (N'die', N'Documento de identificación extranjero', 9),
    (N'cd', N'Carné diplomático', 10);
GO

/*
  La entidad institucional necesita un usuario creador y la base todavía no tiene cuentas. Se crea
  una cuenta de sistema desactivada, sin contraseña utilizable, que solo figura como autora de los
  datos que instala esta migración.
*/
INSERT INTO dbo.Usuarios (NombreCompleto, CorreoElectronico, HashContrasena, CanalAcceso, Activo, CorreoConfirmado, PerfilCompletado)
VALUES (N'Sistema SIMUS', N'sistema@simus.invalid', N'!', N'interno', 0, 0, 1);

INSERT INTO dbo.Entidades
    (TipoEntidad, Nombre, NombreLegal, Descripcion, EstadoRegistro, Activo, EsInstitucional, IdUsuarioCreador)
SELECT N'organizacion',
       N'Plan Nacional de Música para la Convivencia',
       N'Plan Nacional de Música para la Convivencia',
       N'Organización responsable por defecto de los registros que ninguna organización del ecosistema ha reclamado todavía.',
       N'activa', 1, 1, IdUsuario
FROM dbo.Usuarios
WHERE CorreoElectronico = N'sistema@simus.invalid';
GO

INSERT INTO dbo.TerritoriosSonoros (NombreTerritorioSonoro, Slug, OrdenVisualizacion)
VALUES
    (N'Cantos, Pitos y Tambores', N'cantos-pitos-y-tambores', 1),
    (N'Canta y Torbellino', N'canta-y-torbellino', 2),
    (N'Rajaleña y Cucamba', N'rajalena-y-cucamba', 3),
    (N'Marimba', N'marimba', 4),
    (N'Flautas, Cuerdas y Tambores Sureños', N'flautas-cuerdas-y-tambores-surenos', 5),
    (N'Chirimía', N'chirimia', 6),
    (N'Joropo', N'joropo', 7),
    (N'Trova y Parranda', N'trova-y-parranda', 8),
    (N'Amazonas', N'amazonas', 9),
    (N'Insular', N'insular', 10),
    (N'Prácticas de Pueblos Indígenas', N'practicas-de-pueblos-indigenas', 11),
    (N'Músicas Urbanas, Alternativas e Independientes - MUAI', N'muai', 12),
    (N'Comunidades Académicas', N'comunidades-academicas', 13),
    (N'Rrom', N'rrom', 14);

INSERT INTO dbo.FichasConceptualesTerritoriosSonoros (TerritorioSonoroId)
SELECT IdTerritorioSonoro FROM dbo.TerritoriosSonoros;

-- Solo los territorios con delimitación explícita en la fuente institucional.
DECLARE @FuenteTerritorios nvarchar(max) = N'Ministerio de Cultura, Oferta institucional para entidades territoriales (2023), pp. 31–32. https://mng.mincultura.gov.co/prensa/noticias/Documents/Comunicaciones/2023/Kit-nuevos-mandatarios-QR/h.Oferta%20Institucional%20para%20Entidades%20Territoriales.pdf';

UPDATE ficha
SET DefinicionBreve = datos.Texto, RelacionTerritorial = datos.Relacion, Fuentes = @FuenteTerritorios
FROM dbo.FichasConceptualesTerritoriosSonoros AS ficha
JOIN dbo.TerritoriosSonoros AS territorio ON territorio.IdTerritorioSonoro = ficha.TerritorioSonoroId
JOIN (VALUES
    (N'canta-y-torbellino', N'Territorio sonoro asociado a Canta y Torbellino en Boyacá, Cundinamarca, Norte de Santander y Santander.', N'Boyacá, Cundinamarca, Norte de Santander y Santander.'),
    (N'cantos-pitos-y-tambores', N'Territorio sonoro asociado a Cantos, Pitos y Tambores en Atlántico, Bolívar, Cesar, Córdoba, La Guajira, Magdalena, Sucre, norte de Antioquia y norte de Chocó.', N'Atlántico, Bolívar, Cesar, Córdoba, La Guajira, Magdalena, Sucre, norte de Antioquia y norte de Chocó.'),
    (N'chirimia', N'Territorio sonoro asociado a la Chirimía en el departamento del Chocó.', N'Chocó.'),
    (N'flautas-cuerdas-y-tambores-surenos', N'Territorio sonoro asociado a Flautas, Cuerdas y Tambores Sureños en Cauca, Nariño y Putumayo.', N'Cauca, Nariño y Putumayo.'),
    (N'joropo', N'Territorio sonoro asociado al Joropo en Arauca, Casanare, Meta y Vichada.', N'Arauca, Casanare, Meta y Vichada.'),
    (N'marimba', N'Territorio sonoro asociado a la Marimba en Cauca, Nariño y Valle del Cauca.', N'Cauca, Nariño y Valle del Cauca.'),
    (N'rajalena-y-cucamba', N'Territorio sonoro asociado a Rajaleña y Cucamba en Huila, Tolima y Caquetá.', N'Huila, Tolima y Caquetá.'),
    (N'trova-y-parranda', N'Territorio sonoro asociado a Trova y Parranda en Antioquia, Caldas, Quindío y Risaralda.', N'Antioquia, Caldas, Quindío y Risaralda.'),
    (N'insular', N'Territorio sonoro insular de San Andrés, Providencia y Santa Catalina.', N'San Andrés, Providencia y Santa Catalina.'),
    (N'amazonas', N'Territorio sonoro de Amazonía en Caquetá, Guaviare, Vaupés, Guainía y Amazonas.', N'Caquetá, Guaviare, Vaupés, Guainía y Amazonas.')
) AS datos (Slug, Texto, Relacion) ON datos.Slug = territorio.Slug;
GO

INSERT INTO dbo.PracticasMusicales (NombrePracticaMusical, Slug, OrdenVisualizacion)
VALUES
    (N'Expresiones sonoras de pueblos originarios', N'expresiones-sonoras-de-pueblos-originarios', 1),
    (N'Músicas de comunidades negras, afrocolombianas, raizales y palenqueras', N'musicas-de-comunidades-negras-afrocolombianas-raizales-y-palenqueras', 2),
    (N'Músicas campesinas, rurales y de raíz territorial', N'musicas-campesinas-rurales-y-de-raiz-territorial', 3),
    (N'Músicas populares tradicionales, regionales y patrimoniales', N'musicas-populares-tradicionales-regionales-y-patrimoniales', 4),
    (N'Músicas comunitarias y procesos colectivos de práctica musical', N'musicas-comunitarias-y-procesos-colectivos-de-practica-musical', 5),
    (N'Músicas de frontera, diásporas, migraciones e interculturalidad', N'musicas-de-frontera-diasporas-migraciones-e-interculturalidad', 6),
    (N'Músicas urbanas, alternativas e independientes', N'musicas-urbanas-alternativas-e-independientes', 7),
    (N'Músicas populares de amplia circulación, tropicales, bailables y comerciales', N'musicas-populares-de-amplia-circulacion-tropicales-bailables-y-comerciales', 8),
    (N'Músicas vocales, corales y de tradición cantada', N'musicas-vocales-corales-y-de-tradicion-cantada', 9),
    (N'Músicas sinfónicas, bandas, orquestas y grandes formatos instrumentales', N'musicas-sinfonicas-bandas-orquestas-y-grandes-formatos-instrumentales', 10),
    (N'Bandas de marcha, batucadas, comparsas y colectivos sonoros en movimiento', N'bandas-de-marcha-batucadas-comparsas-y-colectivos-sonoros-en-movimiento', 11),
    (N'Músicas académicas, de cámara, contemporáneas, experimentales y de vanguardia', N'musicas-academicas-de-camara-contemporaneas-experimentales-y-de-vanguardia', 12),
    (N'Músicas electrónicas, digitales, producción sonora y nuevas tecnologías', N'musicas-electronicas-digitales-produccion-sonora-y-nuevas-tecnologias', 13),
    (N'Músicas religiosas, rituales, espirituales y devocionales', N'musicas-religiosas-rituales-espirituales-y-devocionales', 14),
    (N'Músicas para escena, danza, audiovisual e interdisciplinariedad', N'musicas-para-escena-danza-audiovisual-e-interdisciplinariedad', 15),
    (N'Prácticas sonoras, arte sonoro, archivo, investigación-creación y paisajes sonoros', N'practicas-sonoras-arte-sonoro-archivo-investigacion-creacion-y-paisajes-sonoros', 16);

INSERT INTO dbo.FichasConceptualesPracticasMusicales (PracticaMusicalId)
SELECT IdPracticaMusical FROM dbo.PracticasMusicales;
GO

INSERT INTO dbo.RegionesOcad (NombreRegionOcad, Slug, OrdenVisualizacion)
VALUES
    (N'Caribe', N'caribe', 1),
    (N'Centro Oriente', N'centro-oriente', 2),
    (N'Eje Cafetero', N'eje-cafetero', 3),
    (N'Pacífico', N'pacifico', 4),
    (N'Centro Sur', N'centro-sur', 5),
    (N'Llano', N'llano', 6);

-- Regiones del Sistema General de Regalías. San Andrés (88) pertenece a la región Caribe.
INSERT INTO dbo.DepartamentosRegionOcad (CodigoDepartamento, RegionOcadId)
SELECT datos.CodigoDepartamento, region.IdRegionOcad
FROM (VALUES
    ('08', N'caribe'), ('13', N'caribe'), ('20', N'caribe'), ('23', N'caribe'), ('44', N'caribe'),
    ('47', N'caribe'), ('70', N'caribe'), ('88', N'caribe'),
    ('11', N'centro-oriente'), ('15', N'centro-oriente'), ('25', N'centro-oriente'),
    ('54', N'centro-oriente'), ('68', N'centro-oriente'),
    ('05', N'eje-cafetero'), ('17', N'eje-cafetero'), ('63', N'eje-cafetero'), ('66', N'eje-cafetero'),
    ('19', N'pacifico'), ('27', N'pacifico'), ('52', N'pacifico'), ('76', N'pacifico'),
    ('18', N'centro-sur'), ('41', N'centro-sur'), ('73', N'centro-sur'), ('86', N'centro-sur'), ('91', N'centro-sur'),
    ('50', N'llano'), ('81', N'llano'), ('85', N'llano'), ('94', N'llano'), ('95', N'llano'),
    ('97', N'llano'), ('99', N'llano')
) AS datos (CodigoDepartamento, SlugRegion)
JOIN dbo.RegionesOcad AS region ON region.Slug = datos.SlugRegion;

INSERT INTO dbo.ZonasUrbanoRural (NombreZonaUrbanoRural, Slug, OrdenVisualizacion)
VALUES (N'Urbana', N'urbana', 1), (N'Rural', N'rural', 2);

INSERT INTO dbo.TitulacionesColectivas (NombreTitulacionColectiva, Slug, OrdenVisualizacion)
VALUES (N'Consejo comunitario', N'consejo-comunitario', 1),
       (N'Resguardo indígena', N'resguardo-indigena', 2),
       (N'No aplica', N'no-aplica', 3);

INSERT INTO dbo.NaturalezasEntidad (NombreNaturalezaEntidad, Slug, OrdenVisualizacion)
VALUES (N'Pública', N'publica', 1), (N'Privada', N'privada', 2), (N'Mixta', N'mixta', 3);
GO

/*
  Correspondencia de los catálogos heredados. Solo se registra un par si la fila heredada existe:
  en una base sin tablas ART_MUS_* no se inserta nada. «N/A» de territorios sonoros no tiene
  destino: la ausencia de territorio se expresa sin filas.
*/
DECLARE @Pares TABLE (TablaHeredada sysname, IdHeredado int, TablaDestino sysname, Clave nvarchar(160));
INSERT INTO @Pares VALUES
    (N'ART_MUS_FESTIVALES_ESTADO', 1, N'EstadosContenido', N'borrador'),
    (N'ART_MUS_FESTIVALES_ESTADO', 2, N'EstadosContenido', N'en_revision'),
    (N'ART_MUS_FESTIVALES_ESTADO', 3, N'EstadosContenido', N'ajustes_solicitados'),
    (N'ART_MUS_FESTIVALES_ESTADO', 4, N'EstadosContenido', N'publicado'),
    (N'ART_MUS_FESTIVALES_ESTADO', 5, N'EstadosContenido', N'rechazado'),
    (N'ART_MUS_FESTIVALES_ESTADO', 6, N'EstadosContenido', N'archivado'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 1, N'TerritoriosSonoros', N'cantos-pitos-y-tambores'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 2, N'TerritoriosSonoros', N'canta-y-torbellino'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 3, N'TerritoriosSonoros', N'rajalena-y-cucamba'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 4, N'TerritoriosSonoros', N'marimba'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 5, N'TerritoriosSonoros', N'flautas-cuerdas-y-tambores-surenos'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 6, N'TerritoriosSonoros', N'chirimia'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 7, N'TerritoriosSonoros', N'joropo'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 8, N'TerritoriosSonoros', N'trova-y-parranda'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 9, N'TerritoriosSonoros', N'amazonas'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 10, N'TerritoriosSonoros', N'insular'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 11, N'TerritoriosSonoros', N'practicas-de-pueblos-indigenas'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 12, N'TerritoriosSonoros', N'muai'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 13, N'TerritoriosSonoros', N'comunidades-academicas'),
    (N'ART_MUS_TERRITORIOS_SONOROS', 14, N'TerritoriosSonoros', N'rrom'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 1, N'RegionesOcad', N'caribe'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 2, N'RegionesOcad', N'centro-oriente'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 3, N'RegionesOcad', N'eje-cafetero'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 4, N'RegionesOcad', N'pacifico'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 5, N'RegionesOcad', N'centro-sur'),
    (N'ART_MUS_FESTIVALES_REGION_OCAD', 6, N'RegionesOcad', N'llano'),
    (N'ART_MUS_FESTIVALES_ZONA', 1, N'ZonasUrbanoRural', N'rural'),
    (N'ART_MUS_FESTIVALES_ZONA', 2, N'ZonasUrbanoRural', N'urbana'),
    (N'ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA', 2, N'TitulacionesColectivas', N'consejo-comunitario'),
    (N'ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA', 3, N'TitulacionesColectivas', N'resguardo-indigena'),
    (N'ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA', 4, N'TitulacionesColectivas', N'no-aplica'),
    (N'ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD', 1, N'NaturalezasEntidad', N'publica'),
    (N'ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD', 2, N'NaturalezasEntidad', N'privada'),
    (N'ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD', 3, N'NaturalezasEntidad', N'mixta');

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
       COALESCE(CAST(ts.IdTerritorioSonoro AS nvarchar(120)), CAST(ro.IdRegionOcad AS nvarchar(120)),
                CAST(zo.IdZonaUrbanoRural AS nvarchar(120)), CAST(ti.IdTitulacionColectiva AS nvarchar(120)),
                CAST(na.IdNaturalezaEntidad AS nvarchar(120)), es.CodigoEstado)
FROM @Pares AS p
JOIN @Heredadas AS h ON h.TablaHeredada = p.TablaHeredada AND h.IdHeredado = p.IdHeredado
LEFT JOIN dbo.TerritoriosSonoros AS ts ON p.TablaDestino = N'TerritoriosSonoros' AND ts.Slug = p.Clave
LEFT JOIN dbo.RegionesOcad AS ro ON p.TablaDestino = N'RegionesOcad' AND ro.Slug = p.Clave
LEFT JOIN dbo.ZonasUrbanoRural AS zo ON p.TablaDestino = N'ZonasUrbanoRural' AND zo.Slug = p.Clave
LEFT JOIN dbo.TitulacionesColectivas AS ti ON p.TablaDestino = N'TitulacionesColectivas' AND ti.Slug = p.Clave
LEFT JOIN dbo.NaturalezasEntidad AS na ON p.TablaDestino = N'NaturalezasEntidad' AND na.Slug = p.Clave
LEFT JOIN dbo.EstadosContenido AS es ON p.TablaDestino = N'EstadosContenido' AND es.CodigoEstado = p.Clave;
GO
