/*
    PNMC - Datos iniciales de bases maestras estaticas.
    Este archivo no incluye Divipola; Divipola se genera desde el TopoJSON oficial local.

    -------------------------------------------------------------------------------------------
    AQUI NO HAY `DELETE` NI `DBCC CHECKIDENT`, Y LOS HUBO HASTA EL 24 DE AGOSTO DE 2026.

    QUE HACIAN. Las dos primeras secciones abrian con
    `DELETE FROM dbo.TerritoriosSonoros; DBCC CHECKIDENT(..., RESEED, 0);` y volvian a insertar
    las catorce filas desde cero. Sobre una base virgen funciona. Sobre una base YA SEMBRADA, no:

      Msg 547  - The DELETE statement conflicted with the REFERENCE constraint
                 "FK_RegistrosEcosistemaTerritoriosSonoros_Territorios"
      Msg 2627 - Violation of PRIMARY KEY constraint 'PK_TerritoriosSonoros'.
                 Cannot insert duplicate key ... (1)

    Las filas que estorban al `DELETE` las crea `seed/V20260519_06__datos_prueba_amplios.sql`,
    que corre DESPUES de este fichero. Es decir: el defecto no estaba aqui cuando se escribio
    esto; lo introdujo el fichero de mas abajo, y nadie lo vio porque el flujo normal es
    construir la base DESDE CERO.

    LO QUE COSTABA. `scripts/seed-local-db.sh` invoca `sqlcmd -b` y lleva `set -e`, asi que el
    primer error aborta LA SIEMBRA ENTERA en su PRIMER fichero: ni las otras siete semillas ni
    `sp_ActualizarMetricasMapa` llegaban a correr. Comprobado sobre una base de
    control construida sin la migracion de SIMUS, para descartar que fuera cosa de ella:
    primera pasada `EXIT=0` y 0 errores; segunda `EXIT=1` y los mismos 4 mensajes.

    COMO SE ARREGLA, Y POR QUE ASI. Convergencia por CLAVE NATURAL -el `Slug`, que es UNIQUE en
    las dos tablas-. Inserta lo que falta, actualiza lo que cambio, y NO BORRA.

    NO SE BORRA A PROPOSITO. Un `WHEN NOT MATCHED BY SOURCE THEN DELETE` -o el `DELETE` de antes-
    devolveria el `Msg 547` en cuanto alguien haya referenciado una fila, que es el defecto que
    esto viene a cerrar. Consecuencia asumida y dicha: si un dia se retira un territorio de la
    lista de abajo, este fichero deja de mencionarlo pero NO lo saca de la base. Retirar una fila
    de catalogo referenciada es un trabajo con su propio guion -como fue
    V20260823_01__retirada_aliados.sql-, no un efecto colateral de sembrar.
    -------------------------------------------------------------------------------------------
*/

-- -------------------------------------------------------------------------------------------
-- dbo.TerritoriosSonoros
-- -------------------------------------------------------------------------------------------
DECLARE @Territorios TABLE
(
    Nombre      nvarchar(140) NOT NULL,
    Slug        nvarchar(160) NOT NULL PRIMARY KEY,
    Descripcion nvarchar(800) NULL,
    Orden       int           NOT NULL
);

INSERT INTO @Territorios (Nombre, Slug, Descripcion, Orden)
VALUES
    (N'Cantos, Pitos y Tambores', N'cantos-pitos-y-tambores', NULL, 1),
    (N'Canta y Torbellino', N'canta-y-torbellino', NULL, 2),
    (N'Rajaleña y Cucamba', N'rajalena-y-cucamba', NULL, 3),
    (N'Marimba', N'marimba', NULL, 4),
    (N'Flautas, Cuerdas y Tambores Sureños', N'flautas-cuerdas-y-tambores-surenos', NULL, 5),
    (N'Chirimía', N'chirimia', NULL, 6),
    (N'Joropo', N'joropo', NULL, 7),
    (N'Trova y Parranda', N'trova-y-parranda', NULL, 8),
    (N'Amazonas', N'amazonas', NULL, 9),
    (N'Insular', N'insular', NULL, 10),
    (N'Prácticas de Pueblos Indígenas', N'practicas-de-pueblos-indigenas', NULL, 11),
    (N'Músicas Urbanas, Alternativas e Independientes - MUAI', N'muai', NULL, 12),
    (N'Comunidades Académicas', N'comunidades-academicas', NULL, 13),
    (N'Rrom', N'rrom', NULL, 14);

-- Las que faltan. El ORDER BY no es cosmetico: en un INSERT ... SELECT ... ORDER BY, SQL Server
-- asigna los valores de identidad en ese orden, de modo que una base recien construida sigue
-- dando los identificadores 1, 2, 3... en el mismo orden que daba el INSERT literal de antes. Sin el, los
-- identificadores de una base nueva dejarian de ser predecibles, y seed/V20260519_06 los usa
-- para repartir sus filas de prueba (`ABS(IdTerritorioSonoro - ((IdRegistroEcosistema % N) + 1))`).
INSERT INTO dbo.TerritoriosSonoros (NombreTerritorioSonoro, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, o.Descripcion, o.Orden
FROM @Territorios AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.TerritoriosSonoros AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

-- Las que ya estaban, por si cambio el nombre, la descripcion o el orden.
UPDATE d
SET d.NombreTerritorioSonoro = o.Nombre,
    d.Descripcion            = o.Descripcion,
    d.OrdenVisualizacion     = o.Orden
FROM dbo.TerritoriosSonoros AS d
JOIN @Territorios AS o ON o.Slug = d.Slug;

-- -------------------------------------------------------------------------------------------
-- dbo.PracticasMusicales
-- -------------------------------------------------------------------------------------------
DECLARE @Practicas TABLE
(
    Nombre      nvarchar(140) NOT NULL,
    Slug        nvarchar(160) NOT NULL PRIMARY KEY,
    Descripcion nvarchar(800) NULL,
    Orden       int           NOT NULL
);

INSERT INTO @Practicas (Nombre, Slug, Descripcion, Orden)
VALUES
    (N'Expresiones sonoras de pueblos originarios', N'expresiones-sonoras-de-pueblos-originarios', NULL, 1),
    (N'Músicas de comunidades negras, afrocolombianas, raizales y palenqueras', N'musicas-de-comunidades-negras-afrocolombianas-raizales-y-palenqueras', NULL, 2),
    (N'Músicas campesinas, rurales y de raíz territorial', N'musicas-campesinas-rurales-y-de-raiz-territorial', NULL, 3),
    (N'Músicas populares tradicionales, regionales y patrimoniales', N'musicas-populares-tradicionales-regionales-y-patrimoniales', NULL, 4),
    (N'Músicas comunitarias y procesos colectivos de práctica musical', N'musicas-comunitarias-y-procesos-colectivos-de-practica-musical', NULL, 5),
    (N'Músicas de frontera, diásporas, migraciones e interculturalidad', N'musicas-de-frontera-diasporas-migraciones-e-interculturalidad', NULL, 6),
    (N'Músicas urbanas, alternativas e independientes', N'musicas-urbanas-alternativas-e-independientes', NULL, 7),
    (N'Músicas populares de amplia circulación, tropicales, bailables y comerciales', N'musicas-populares-de-amplia-circulacion-tropicales-bailables-y-comerciales', NULL, 8),
    (N'Músicas vocales, corales y de tradición cantada', N'musicas-vocales-corales-y-de-tradicion-cantada', NULL, 9),
    (N'Músicas sinfónicas, bandas, orquestas y grandes formatos instrumentales', N'musicas-sinfonicas-bandas-orquestas-y-grandes-formatos-instrumentales', NULL, 10),
    (N'Bandas de marcha, batucadas, comparsas y colectivos sonoros en movimiento', N'bandas-de-marcha-batucadas-comparsas-y-colectivos-sonoros-en-movimiento', NULL, 11),
    (N'Músicas académicas, de cámara, contemporáneas, experimentales y de vanguardia', N'musicas-academicas-de-camara-contemporaneas-experimentales-y-de-vanguardia', NULL, 12),
    (N'Músicas electrónicas, digitales, producción sonora y nuevas tecnologías', N'musicas-electronicas-digitales-produccion-sonora-y-nuevas-tecnologias', NULL, 13),
    (N'Músicas religiosas, rituales, espirituales y devocionales', N'musicas-religiosas-rituales-espirituales-y-devocionales', NULL, 14),
    (N'Músicas para escena, danza, audiovisual e interdisciplinariedad', N'musicas-para-escena-danza-audiovisual-e-interdisciplinariedad', NULL, 15),
    (N'Prácticas sonoras, arte sonoro, archivo, investigación-creación y paisajes sonoros', N'practicas-sonoras-arte-sonoro-archivo-investigacion-creacion-y-paisajes-sonoros', NULL, 16);

-- Las que faltan. El ORDER BY no es cosmetico: en un INSERT ... SELECT ... ORDER BY, SQL Server
-- asigna los valores de identidad en ese orden, de modo que una base recien construida sigue
-- dando los identificadores 1, 2, 3... en el mismo orden que daba el INSERT literal de antes. Sin el, los
-- identificadores de una base nueva dejarian de ser predecibles, y seed/V20260519_06 los usa
-- para repartir sus filas de prueba (`ABS(IdPracticaMusical - ((IdRegistroEcosistema % N) + 1))`).
INSERT INTO dbo.PracticasMusicales (NombrePracticaMusical, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, o.Descripcion, o.Orden
FROM @Practicas AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.PracticasMusicales AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

-- Las que ya estaban, por si cambio el nombre, la descripcion o el orden.
UPDATE d
SET d.NombrePracticaMusical = o.Nombre,
    d.Descripcion            = o.Descripcion,
    d.OrdenVisualizacion     = o.Orden
FROM dbo.PracticasMusicales AS d
JOIN @Practicas AS o ON o.Slug = d.Slug;
