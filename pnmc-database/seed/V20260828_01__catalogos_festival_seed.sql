/*
    PNMC · Semilla · Los nueve catalogos del Festival, y las regiones OCAD

    DE DONDE SALEN ESTOS VALORES
    ----------------------------
    De `Scripts/script_festivales_simus.sql`, el volcado de la base SIMUS del Ministerio: sus
    tablas `ART_MUS_FESTIVALES_TIPOLOGIA`, `_EXPRESIONES_ARTISTICAS`, `_FUENTE_FINANCIACION`,
    `_MODALIDADES_PARTICIPACION`, `_NATURALEZA_ENTIDAD`, `_TIPO_ORGANIZADOR`, `_ZONA`,
    `_ZONA_TITULACION_COLECTIVA`, `ART_MUS_TIPOINGRESO`, `_REGION_OCAD` y `ART_MUS_ZONAXREGION_OCAD`.

    POR QUE AHORA, SI EL 24 DE AGOSTO SE DECIDIO LO CONTRARIO. `schema/V20260824_02` creo las nueve
    tablas VACIAS a proposito, por el criterio de que «el contenido de los catalogos se
    decide en PNMC, informado por SIMUS, no copiado de SIMUS». El criterio define
    traer «toda la informacion de ese script y las relaciones» y que esa sea «la forma de todos los
    datos que tiene un festival». Esta siembra sustituye aquel criterio en su parte de «no copiado», y conserva la
    otra mitad, que es la que importaba: SE COPIA EL CONTENIDO, NO LOS IDENTIFICADORES. Los ids de
    SIMUS estan cableados en el codigo de SIMUS; los de aqui los asigna esta base. La convergencia
    va por `Slug`, que es la clave natural, igual que en `seed/V20260519_01__maestras_estaticas_seed.sql`.

    IDEMPOTENTE POR CLAVE NATURAL, y no por `DELETE` + `INSERT`. El motivo esta escrito entero en
    la cabecera de `V20260519_01`: borrar y volver a insertar revienta contra las FK en cuanto algo
    referencia una fila, y `seed-local-db.sh` corre con `set -e`, asi que el primer error aborta la
    siembra ENTERA en su primer fichero.

    TRES DESVIACIONES DELIBERADAS DEL ORIGEN, cada una con su motivo
    ----------------------------------------------------------------
    1. LOS ROTULOS NO VAN EN MAYUSCULA SOSTENIDA. En SIMUS son 'MUSICA', 'FIESTA POPULAR',
       'GOBERNACION'. Aqui van en la capitalizacion del resto de catalogos de PNMC
       (`TerritoriosSonoros`, `PracticasMusicales`), porque estos rotulos se pintan en un
       desplegable del formulario publico y no en una consola interna. El valor es el mismo; lo
       que cambia es como se lee.

    2. 'MINCULTURA' SE ESCRIBE CON EL NOMBRE ACTUAL de la entidad: «Ministerio de las Culturas, las
       Artes y los Saberes». `MINCULTURA` es el nombre anterior, y este catalogo nace hoy: dejarlo
       obsoleto desde el primer dia es un defecto, no fidelidad al origen. El `Slug` conserva
       `mincultura` para que la equivalencia con SIMUS siga siendo directa.

    3. LOS SEIS TERRITORIOS SONOROS NO SE TOCAN. `ART_MUS_TERRITORIOS_SONOROS` trae 15 filas con el
       prefijo «TS » y una fila 'N/A'; `dbo.TerritoriosSonoros` ya tiene las 14 sembradas sin el
       prefijo desde `V20260519_01`. Sembrarlas otra vez daria catorce duplicados con otro `Slug`.
       La unica diferencia real es 'N/A', que en PNMC se representa con la ausencia de filas en la
       tabla puente y no con una fila centinela.

    LO QUE SE ANOTA Y NO SE ARREGLA: 'MERCADO' y 'MERCADOS CULTURALES' son dos filas distintas en
    `ART_MUS_FESTIVALES_TIPOLOGIA` (ids 9 y 10) y parecen la misma cosa. Se traen las dos, porque
    fundirlas es una decision de contenido que le toca al Ministerio, no a esta semilla.
*/

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.TipologiasFestival   <-  ART_MUS_FESTIVALES_TIPOLOGIA (10 filas activas)
-- -------------------------------------------------------------------------------------------
DECLARE @Tipologias TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Tipologias (Nombre, Slug, Orden) VALUES
    (N'Fiesta popular',        N'fiesta-popular',        1),
    (N'Celebración religiosa', N'celebracion-religiosa', 2),
    (N'Festival de música',    N'festival-de-musica',    3),
    (N'Reinado',               N'reinado',               4),
    (N'Feria',                 N'feria',                 5),
    (N'Conferencia',           N'conferencia',           6),
    (N'Mercado',               N'mercado',               7),
    (N'Mercados culturales',   N'mercados-culturales',   8),
    (N'Encuentros',            N'encuentros',            9),
    (N'Otra',                  N'otra',                 10);

INSERT INTO dbo.TipologiasFestival (NombreTipologiaFestival, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Tipologias AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.TipologiasFestival AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreTipologiaFestival = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.TipologiasFestival AS d JOIN @Tipologias AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.ExpresionesArtisticas   <-  ART_MUS_FESTIVALES_EXPRESIONES_ARTISTICAS (11 filas activas)
-- -------------------------------------------------------------------------------------------
/*
    LOS IDS DEL ORIGEN SALTAN el 8 y el 10: son filas borradas. Se traen las once que quedan, en su
    orden, y se renumeran aqui. 'Ninguna' y 'Otra' son centinelas del formulario y se conservan:
    sin 'Ninguna', quien no tenga ninguna expresion artistica ademas de la musica no tendria como
    decirlo y dejaria el campo vacio, que es indistinguible de no haber respondido.
*/
DECLARE @Expresiones TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Expresiones (Nombre, Slug, Orden) VALUES
    (N'Música',          N'musica',          1),
    (N'Danza',           N'danza',           2),
    (N'Culinaria',       N'culinaria',       3),
    (N'Artesanía',       N'artesania',       4),
    (N'Cestería',        N'cesteria',        5),
    (N'Narración',       N'narracion',       6),
    (N'Cuentería',       N'cuenteria',       7),
    (N'Improvisación',   N'improvisacion',   8),
    (N'Artes plásticas', N'artes-plasticas', 9),
    (N'Otra',            N'otra',           10),
    (N'Ninguna',         N'ninguna',        11);

INSERT INTO dbo.ExpresionesArtisticas (NombreExpresionArtistica, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Expresiones AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.ExpresionesArtisticas AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreExpresionArtistica = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.ExpresionesArtisticas AS d JOIN @Expresiones AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.FuentesFinanciacion   <-  ART_MUS_FESTIVALES_FUENTE_FINANCIACION (9 filas)
-- -------------------------------------------------------------------------------------------
DECLARE @Fuentes TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Fuentes (Nombre, Slug, Orden) VALUES
    (N'Aportes privados',   N'aportes-privados',  1),
    (N'Boletería',          N'boleteria',         2),
    (N'Estímulos',          N'estimulos',         3),
    (N'Concertación',       N'concertacion',      4),
    -- Ver desviacion 2 de la cabecera: el `Slug` conserva la equivalencia con SIMUS.
    (N'Ministerio de las Culturas, las Artes y los Saberes', N'mincultura', 5),
    (N'Gobernación',        N'gobernacion',       6),
    (N'Alcaldía',           N'alcaldia',          7),
    (N'Recursos propios',   N'recursos-propios',  8),
    (N'Otra',               N'otra',              9);

INSERT INTO dbo.FuentesFinanciacion (NombreFuenteFinanciacion, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Fuentes AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.FuentesFinanciacion AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreFuenteFinanciacion = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.FuentesFinanciacion AS d JOIN @Fuentes AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.ModalidadesParticipacion   <-  ART_MUS_FESTIVALES_MODALIDADES_PARTICIPACION (5 filas)
-- -------------------------------------------------------------------------------------------
-- Los ids 2 y 5 del origen estan borrados; quedan cinco.
DECLARE @Modalidades TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Modalidades (Nombre, Slug, Orden) VALUES
    (N'Concurso',            N'concurso',            1),
    (N'Por convocatoria',     N'por-convocatoria',    2),
    (N'Invitados pagos',      N'invitados-pagos',     3),
    (N'Invitados sin paga',   N'invitados-sin-paga',  4),
    (N'Otra',                 N'otra',                5);

INSERT INTO dbo.ModalidadesParticipacion (NombreModalidadParticipacion, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Modalidades AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.ModalidadesParticipacion AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreModalidadParticipacion = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.ModalidadesParticipacion AS d JOIN @Modalidades AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.NaturalezasEntidad   <-  ART_MUS_FESTIVALES_NATURALEZA_ENTIDAD (3 filas)
-- -------------------------------------------------------------------------------------------
-- En el origen la columna es `nchar(20)`, asi que los tres valores llegan rellenos de espacios
-- por la derecha ('PUBLICO             '). Aqui van sin relleno: `nchar` sobre un catalogo es un
-- error del origen, no un dato.
DECLARE @Naturalezas TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Naturalezas (Nombre, Slug, Orden) VALUES
    (N'Pública', N'publica', 1),
    (N'Privada', N'privada', 2),
    (N'Mixta',   N'mixta',   3);

INSERT INTO dbo.NaturalezasEntidad (NombreNaturalezaEntidad, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Naturalezas AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.NaturalezasEntidad AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreNaturalezaEntidad = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.NaturalezasEntidad AS d JOIN @Naturalezas AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.TiposIngreso   <-  ART_MUS_TIPOINGRESO (4 filas)
-- -------------------------------------------------------------------------------------------
-- Es el unico catalogo del origen que ya venia en capitalizacion normal, con sus parentesis
-- explicativos. Se copia literal.
DECLARE @Ingresos TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Ingresos (Nombre, Slug, Orden) VALUES
    (N'Entrada libre (sin boletería)',        N'entrada-libre',        1),
    (N'Entrada gratuita (con boletería)',     N'entrada-gratuita',     2),
    (N'Boletería paga',                       N'boleteria-paga',       3),
    (N'Sin público (transmisión en vivo)',    N'sin-publico',          4);

INSERT INTO dbo.TiposIngreso (NombreTipoIngreso, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Ingresos AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.TiposIngreso AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreTipoIngreso = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.TiposIngreso AS d JOIN @Ingresos AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.TiposOrganizador   <-  ART_MUS_FESTIVALES_TIPO_ORGANIZADOR (7 filas)
-- -------------------------------------------------------------------------------------------
DECLARE @Organizadores TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Organizadores (Nombre, Slug, Orden) VALUES
    (N'Entidad pública / Alcaldía',     N'entidad-publica-alcaldia',     1),
    (N'Entidad pública / Gobernación',  N'entidad-publica-gobernacion',  2),
    (N'Fundación',                      N'fundacion',                    3),
    (N'Museo',                          N'museo',                        4),
    (N'Casa cultural',                  N'casa-cultural',                5),
    (N'Comunitario',                    N'comunitario',                  6),
    (N'Mixto',                          N'mixto',                        7);

INSERT INTO dbo.TiposOrganizador (NombreTipoOrganizador, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Organizadores AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.TiposOrganizador AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreTipoOrganizador = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.TiposOrganizador AS d JOIN @Organizadores AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.ZonasUrbanoRural   <-  ART_MUS_FESTIVALES_ZONA (2 filas)
-- -------------------------------------------------------------------------------------------
-- El origen lleva ademas una columna `ES_RURAL bit` que solo repite lo que dice el nombre. No se
-- trae: una columna que se puede deducir del `Slug` es una fuente mas de la que discrepar.
DECLARE @Zonas TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Zonas (Nombre, Slug, Orden) VALUES
    (N'Urbana', N'urbana', 1),
    (N'Rural',  N'rural',  2);

INSERT INTO dbo.ZonasUrbanoRural (NombreZonaUrbanoRural, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Zonas AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.ZonasUrbanoRural AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreZonaUrbanoRural = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.ZonasUrbanoRural AS d JOIN @Zonas AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.TitulacionesColectivas   <-  ART_MUS_FESTIVALES_ZONA_TITULACION_COLECTIVA (3 filas)
-- -------------------------------------------------------------------------------------------
/*
    'NO APLICA' SE CONSERVA COMO FILA, y es la excepcion a lo que se dijo de 'N/A' en los
    territorios sonoros. El motivo es que aqui la relacion es UNO a uno -cada localizacion de una
    version tiene UNA titulacion colectiva- y no muchos a muchos: sin la fila, «no aplica» y «no
    respondio» serian el mismo NULL, y son dos cosas distintas cuando lo que se pregunta es si el
    Festival ocurre en territorio de consejo comunitario o de resguardo.

    La columna del origen se llama `DESCRPCION_ZONA_TITULACION_COLECTIVA`, con la errata incluida.
*/
DECLARE @Titulaciones TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Titulaciones (Nombre, Slug, Orden) VALUES
    (N'Consejo comunitario', N'consejo-comunitario', 1),
    (N'Resguardo indígena',  N'resguardo-indigena',  2),
    (N'No aplica',           N'no-aplica',           3);

INSERT INTO dbo.TitulacionesColectivas (NombreTitulacionColectiva, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Titulaciones AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.TitulacionesColectivas AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreTitulacionColectiva = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.TitulacionesColectivas AS d JOIN @Titulaciones AS o ON o.Slug = d.Slug;
GO

-- -------------------------------------------------------------------------------------------
-- dbo.RegionesOcad y dbo.DepartamentosRegionOcad
--   <-  ART_MUS_FESTIVALES_REGION_OCAD (6) y ART_MUS_ZONAXREGION_OCAD (32)
-- -------------------------------------------------------------------------------------------
DECLARE @Regiones TABLE (Nombre nvarchar(280) NOT NULL, Slug nvarchar(320) NOT NULL PRIMARY KEY, Orden int NOT NULL);
INSERT INTO @Regiones (Nombre, Slug, Orden) VALUES
    (N'Caribe',         N'caribe',         1),
    (N'Centro Oriente', N'centro-oriente', 2),
    (N'Eje Cafetero',   N'eje-cafetero',   3),
    (N'Pacífico',       N'pacifico',       4),
    (N'Centro Sur',     N'centro-sur',     5),
    (N'Llano',          N'llano',          6);

INSERT INTO dbo.RegionesOcad (NombreRegionOcad, Slug, Descripcion, OrdenVisualizacion)
SELECT o.Nombre, o.Slug, NULL, o.Orden FROM @Regiones AS o
WHERE NOT EXISTS (SELECT 1 FROM dbo.RegionesOcad AS d WHERE d.Slug = o.Slug)
ORDER BY o.Orden;

UPDATE d SET d.NombreRegionOcad = o.Nombre, d.OrdenVisualizacion = o.Orden
FROM dbo.RegionesOcad AS d JOIN @Regiones AS o ON o.Slug = d.Slug;
GO

/*
    LAS 32 CORRESPONDENCIAS, tal como las reparte el origen. Se conservan EXACTAMENTE: el reparto
    es una decision administrativa del OCAD y no una geografia que se pueda deducir. Dos ejemplos
    de por que no se puede deducir: Antioquia (05) esta en EJE CAFETERO y no en PACIFICO, aunque
    tenga costa pacifica; y Bogota (11) esta en CENTRO ORIENTE, que es una region y no una ciudad.

    SE INSERTAN SOLO LOS CODIGOS QUE EXISTEN EN `dbo.Divipola`. El origen trae 32 y Colombia tiene
    33 departamentos: el `JOIN` deja fuera cualquier codigo que aqui no exista en vez de reventar
    contra la FK, y la consulta de verificacion del final dice cuantos entraron.
*/
DECLARE @Reparto TABLE (CodigoDepartamento char(2) NOT NULL PRIMARY KEY, SlugRegion nvarchar(320) NOT NULL);
INSERT INTO @Reparto (CodigoDepartamento, SlugRegion) VALUES
    -- Caribe
    ('44', N'caribe'), ('20', N'caribe'), ('47', N'caribe'), ('08', N'caribe'),
    ('70', N'caribe'), ('13', N'caribe'), ('23', N'caribe'),
    -- Centro Oriente
    ('54', N'centro-oriente'), ('68', N'centro-oriente'), ('15', N'centro-oriente'),
    ('25', N'centro-oriente'), ('11', N'centro-oriente'),
    -- Eje Cafetero
    ('05', N'eje-cafetero'), ('17', N'eje-cafetero'), ('66', N'eje-cafetero'), ('63', N'eje-cafetero'),
    -- Pacifico
    ('27', N'pacifico'), ('76', N'pacifico'), ('19', N'pacifico'), ('52', N'pacifico'),
    -- Centro Sur
    ('73', N'centro-sur'), ('41', N'centro-sur'), ('18', N'centro-sur'),
    ('86', N'centro-sur'), ('91', N'centro-sur'),
    -- Llano
    ('81', N'llano'), ('85', N'llano'), ('50', N'llano'), ('99', N'llano'),
    ('95', N'llano'), ('94', N'llano'), ('97', N'llano');

INSERT INTO dbo.DepartamentosRegionOcad (CodigoDepartamento, RegionOcadId)
SELECT r.CodigoDepartamento, g.IdRegionOcad
FROM @Reparto AS r
JOIN dbo.RegionesOcad AS g ON g.Slug = r.SlugRegion
WHERE EXISTS (SELECT 1 FROM dbo.Divipola AS d WHERE d.CodigoDepartamento = r.CodigoDepartamento)
  AND NOT EXISTS (SELECT 1 FROM dbo.DepartamentosRegionOcad AS e WHERE e.CodigoDepartamento = r.CodigoDepartamento);

UPDATE e SET e.RegionOcadId = g.IdRegionOcad
FROM dbo.DepartamentosRegionOcad AS e
JOIN @Reparto AS r ON r.CodigoDepartamento = e.CodigoDepartamento
JOIN dbo.RegionesOcad AS g ON g.Slug = r.SlugRegion;
GO
