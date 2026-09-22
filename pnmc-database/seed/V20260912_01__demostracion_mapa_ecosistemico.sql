/*
    PNMC — Semilla de demostración del Mapa Ecosistémico
    ====================================================

    POR QUE EXISTE. El geovisor tiene cinco lecturas analíticas, tres modos de dibujo y una ficha de
    doce campos, y la base local tenía diez Festivales en cuatro departamentos. Con eso se comprueba
    que el código no falla, pero NO se puede decidir nada de lo que estas piezas existen para
    decidir: si el mapa de calor se lee mejor que los símbolos proporcionales, si el ranking
    municipal necesita paginarse, si la leyenda del coroplético escala bien. Un tablero que se juzga
    con tres puntos se rehace entero en cuanto llegan trescientos.

    NO ES «MUCHOS DATOS»: ES DATOS CON LA FORMA CORRECTA. Una distribución plana haría que
    coropletas, símbolos y calor se vieran los tres iguales, y entonces no se podría elegir entre
    ellos —que es justo la decisión que esta semilla existe para permitir—. Aquí la distribución es
    DESIGUAL, hay departamentos en cero a propósito, hay clasificación solapada y hay registros que
    no se pueden situar. Cada una de esas formas es la que hace visible una pieza concreta:

      · Reparto desigual ......... coropletas y símbolos discrepan, y su sesgo se hace visible.
      · Departamentos en cero .... «Cobertura territorial» tiene qué enseñar.
      · ~120 municipios .......... el ranking municipal es largo de verdad.
      · Clasificación solapada ... salta el aviso de «la suma pasa del total».
      · Sin clasificar ........... existe la categoría, y se ve cuánta hay.
      · Sin municipio / nacional . «Se pueden situar» enseña sus causas con cifra.
      · Agenda repartida en meses  los tramos «Próximos / Este mes / Todos» se distinguen.

    DE DONDE SALE CADA COSA. Ni un solo territorio ni una sola clasificación están escritos aquí:
      · Los departamentos y municipios salen de `dbo.Divipola`.
      · Los territorios sonoros, de `dbo.TerritoriosSonoros` (los 14 del Plan).
      · Las prácticas, de `dbo.PracticasMusicales` (las 16 del catálogo).
    Es la regla de fuente única del proyecto, y además hace que la semilla siga siendo válida si un
    catálogo cambia: no hay lista que actualizar en dos sitios.

    INCLUSO EL VOLUMEN SALE DEL DATO REAL. Cuántos Festivales toca cada departamento se deriva de
    cuántos municipios tiene en DIVIPOLA. Antioquia y Boyacá quedan arriba y Guainía abajo sin que
    nadie lo decida a mano, y el reparto resultante se parece al del país porque está calculado
    sobre el país.

    NO PUEDE LLEGAR A PRODUCCION, y no por disciplina sino por construcción: `pnmc-database/seed/`
    no lo aplica `migrar`; sólo lo aplican los guiones locales. Y dentro de los locales tampoco va
    en la lista de `seed-local-db.sh`, que está declarada como «de referencia y soporte, sin datos
    demostrativos»: se pide a mano con `./scripts/sembrar-demo-mapa.sh`.

    TODO LO QUE SIEMBRA QUEDA DECLARADO Y ES REVERSIBLE. Cada registro entra con su fila en
    `dbo.ProcedenciaDeRegistros` y `ContextoOrigen = 'siembra'`, que es uno de los seis orígenes que
    la tabla ya admitía. Eso cumple la regla transversal —ningún registro sin procedencia— y además
    convierte a esa tabla en el inventario de lo sembrado: el guion empieza borrando exactamente lo
    que consta ahí, y por eso se puede correr diez veces seguidas sin acumular basura.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @UsuarioSiembra int = (SELECT TOP 1 IdUsuario FROM dbo.Usuarios WHERE CorreoElectronico = N'admin@pnmc.local');
IF @UsuarioSiembra IS NULL
BEGIN
    RAISERROR(N'No existe admin@pnmc.local. Ejecuta antes ./scripts/seed-local-db.sh.', 16, 1);
    RETURN;
END;

IF (SELECT COUNT(*) FROM dbo.Divipola) < 1000
BEGIN
    RAISERROR(N'DIVIPOLA no esta cargada. Ejecuta antes ./scripts/schema-local.sh divipola-mgn-2025.', 16, 1);
    RETURN;
END;

BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 1. RETIRAR LO QUE ESTA MISMA SEMILLA SEMBRO ANTES
--
-- Se identifica por la procedencia y no por el nombre: un nombre se puede repetir por accidente y
-- una procedencia declarada no. Se borran los hijos antes que los padres porque las claves ajenas
-- no tienen borrado en cascada, y eso es deliberado en este modelo.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @FestivalesSembrados TABLE (Id int PRIMARY KEY);
INSERT INTO @FestivalesSembrados (Id)
SELECT RegistroId FROM dbo.ProcedenciaDeRegistros WHERE Dominio = N'festival' AND ContextoOrigen = N'siembra';

DECLARE @EventosSembrados TABLE (Id int PRIMARY KEY);
INSERT INTO @EventosSembrados (Id)
SELECT RegistroId FROM dbo.ProcedenciaDeRegistros WHERE Dominio = N'agenda' AND ContextoOrigen = N'siembra';

DECLARE @OrganizacionesSembradas TABLE (Id int PRIMARY KEY);
INSERT INTO @OrganizacionesSembradas (Id)
SELECT RegistroId FROM dbo.ProcedenciaDeRegistros WHERE Dominio = N'organizacion' AND ContextoOrigen = N'siembra';

DELETE FROM dbo.FestivalesTerritoriosSonoros WHERE FestivalId IN (SELECT Id FROM @FestivalesSembrados);
DELETE FROM dbo.FestivalesPracticasMusicales WHERE FestivalId IN (SELECT Id FROM @FestivalesSembrados);
DELETE FROM dbo.EventosAgendaTerritoriosSonoros WHERE EventoAgendaId IN (SELECT Id FROM @EventosSembrados);
DELETE FROM dbo.EventosAgendaPracticasMusicales WHERE EventoAgendaId IN (SELECT Id FROM @EventosSembrados);
DELETE FROM dbo.EventosAgenda WHERE IdEventoAgenda IN (SELECT Id FROM @EventosSembrados);
DELETE FROM dbo.Festivales WHERE IdFestival IN (SELECT Id FROM @FestivalesSembrados);
DELETE FROM dbo.Entidades WHERE IdEntidad IN (SELECT Id FROM @OrganizacionesSembradas);
DELETE FROM dbo.ProcedenciaDeRegistros
WHERE ContextoOrigen = N'siembra' AND Dominio IN (N'festival', N'agenda', N'organizacion');

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 2. EL REPARTO TERRITORIAL, DERIVADO DE DIVIPOLA
--
-- `Cuota` es cuántos Festivales toca cada departamento. Sale del número de municipios que tiene en
-- DIVIPOLA, que es dato real y correlaciona con el tamaño: Antioquia (125 municipios) queda arriba
-- y Guainía (1) abajo sin que nadie escriba una lista.
--
-- LOS CUATRO MAS PEQUEÑOS SE QUEDAN EN CERO, a propósito. Sin departamentos vacíos, «Cobertura
-- territorial» diría siempre 100 % y su lista de faltantes estaría siempre vacía: la lectura
-- existiría sin nada que leer.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Departamentos TABLE (
    CodigoDepartamento char(2) PRIMARY KEY,
    NombreDepartamento nvarchar(120),
    Municipios int,
    Puesto int,
    Cuota int
);

INSERT INTO @Departamentos (CodigoDepartamento, NombreDepartamento, Municipios, Puesto, Cuota)
SELECT
    d.CodigoDepartamento,
    MAX(d.NombreDepartamento),
    COUNT(*),
    ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC, d.CodigoDepartamento),
    0
FROM dbo.Divipola d
GROUP BY d.CodigoDepartamento;

UPDATE p
SET Cuota = CASE
        -- Los cuatro con menos municipios se dejan vacíos.
        WHEN p.Puesto > (SELECT COUNT(*) - 4 FROM @Departamentos) THEN 0
        -- Un Festival por cada siete municipios, con suelo de 1 y techo de 22. El techo evita que
        -- Antioquia y Boyacá se coman el mapa y dejen a los demás en un color indistinguible.
        ELSE CASE WHEN CEILING(p.Municipios / 7.0) > 22 THEN 22
                  WHEN CEILING(p.Municipios / 7.0) < 1 THEN 1
                  ELSE CEILING(p.Municipios / 7.0) END
    END
FROM @Departamentos p;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 3. QUE FESTIVAL VA DONDE
--
-- Un municipio distinto por Festival mientras haya municipios; si la cuota supera los municipios
-- del departamento, se reparte en ciclo. `ORDER BY NombreMunicipio` y no aleatorio: la semilla
-- tiene que dar lo mismo dos veces seguidas, o comparar dos capturas no significa nada.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Plan TABLE (
    Orden int IDENTITY(1,1) PRIMARY KEY,
    CodigoDepartamento char(2),
    NombreDepartamento nvarchar(120),
    CodigoMunicipio char(5),
    NombreMunicipio nvarchar(160),
    NombreLegible nvarchar(160),
    NombreDepartamentoLegible nvarchar(160),
    IndiceEnDepartamento int
);

WITH MunicipiosNumerados AS (
    SELECT d.CodigoDepartamento, d.NombreDepartamento, d.CodigoMunicipio, d.NombreMunicipio,
           ROW_NUMBER() OVER (PARTITION BY d.CodigoDepartamento ORDER BY d.NombreMunicipio) AS Fila,
           COUNT(*) OVER (PARTITION BY d.CodigoDepartamento) AS Total
    FROM dbo.Divipola d
),
Cuotas AS (
    SELECT p.CodigoDepartamento, p.Cuota, n.Numero
    FROM @Departamentos p
    CROSS APPLY (
        SELECT TOP (p.Cuota) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Numero
        FROM dbo.Divipola
    ) n
    WHERE p.Cuota > 0
)
INSERT INTO @Plan (CodigoDepartamento, NombreDepartamento, CodigoMunicipio, NombreMunicipio, IndiceEnDepartamento)
SELECT c.CodigoDepartamento, m.NombreDepartamento, m.CodigoMunicipio, m.NombreMunicipio, c.Numero
FROM Cuotas c
JOIN MunicipiosNumerados m
  ON m.CodigoDepartamento = c.CodigoDepartamento
 AND m.Fila = ((c.Numero - 1) % m.Total) + 1
ORDER BY c.CodigoDepartamento, c.Numero;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 3 bis. LOS NOMBRES, ESCRITOS COMO SE LEEN
--
-- DIVIPOLA guarda los topónimos en versales —«ALCALÁ», «SANTA FE DE ANTIOQUIA»— porque así los
-- publica el DANE, y como rótulo de mapa está bien. Dentro del NOMBRE de un Festival, no: un
-- listado entero en mayúsculas se lee peor y no se parece a como se llaman los Festivales de
-- verdad, que es precisamente lo que hay que poder juzgar en pantalla.
--
-- NO SE TOCA EL CATALOGO. La capitalización se calcula aquí y sólo para componer los nombres de la
-- demostración; `dbo.Divipola` se queda exactamente como la entregó el DANE.
--
-- SE HACE CON CONJUNTOS Y SIN CREAR NADA EN LA BASE. Una función escalar habría sido más corta,
-- pero dejaría un objeto permanente en el esquema puesto ahí por una semilla de demostración, que
-- es justo lo que no debe pasar. `STRING_SPLIT` con ordinal resolvería esto en una línea y no está
-- disponible: la base corre en nivel de compatibilidad 150, donde esa variante no existe todavía.
-- Queda recorrer los caracteres, que sí funciona: se pone en mayúscula el primero de cada palabra.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Posiciones TABLE (n int PRIMARY KEY);
INSERT INTO @Posiciones (n)
SELECT TOP (160) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM dbo.Divipola;

-- SE MATERIALIZAN LAS LETRAS ANTES DE JUNTARLAS. `STRING_AGG` dentro de un `CROSS APPLY` que
-- referencia la fila de fuera falla con «if an expression being aggregated contains an outer
-- reference, then that outer reference must be the only column referenced»: la expresión mira a la
-- vez el nombre —de fuera— y la posición —de dentro—. Separarlo en dos pasos lo resuelve sin
-- artificios.
DECLARE @Letras TABLE (Original nvarchar(160), n int, Letra nvarchar(2), PRIMARY KEY (Original, n));
INSERT INTO @Letras (Original, n, Letra)
SELECT origen.Nombre, pos.n,
       CASE WHEN pos.n = 1 OR SUBSTRING(origen.Nombre, pos.n - 1, 1) = N' '
            THEN UPPER(SUBSTRING(origen.Nombre, pos.n, 1))
            ELSE LOWER(SUBSTRING(origen.Nombre, pos.n, 1)) END
FROM (
    SELECT DISTINCT NombreMunicipio AS Nombre FROM @Plan
    UNION SELECT DISTINCT NombreDepartamento FROM @Plan
) origen
JOIN @Posiciones pos ON pos.n <= LEN(origen.Nombre);

DECLARE @Legibles TABLE (Original nvarchar(160) PRIMARY KEY, Legible nvarchar(160));
INSERT INTO @Legibles (Original, Legible)
SELECT Original,
       -- Los conectores vuelven a minúscula: «Santa Fe de Antioquia», no «Santa Fe De Antioquia».
       REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
           STRING_AGG(Letra, N'') WITHIN GROUP (ORDER BY n),
           N' De ', N' de '), N' Del ', N' del '), N' La ', N' la '), N' Y ', N' y '), N' El ', N' el ')
FROM @Letras
GROUP BY Original;

UPDATE p
SET NombreLegible = lm.Legible,
    NombreDepartamentoLegible = ld.Legible
FROM @Plan p
JOIN @Legibles lm ON lm.Original = p.NombreMunicipio
JOIN @Legibles ld ON ld.Original = p.NombreDepartamento;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 4. LOS FESTIVALES
--
-- COBERTURA REPARTIDA, Y NO TODA MUNICIPAL. El modelo tiene tres niveles con una regla dura
-- —`CK_Festivales_NivelCobertura`: nacional sin códigos, departamental sólo con departamento,
-- municipal con los dos— y el mapa reparte distinto cada uno. Con todo municipal nunca se vería
-- qué hace el geovisor con un Festival de cobertura nacional, que es el caso que ninguna vista
-- sabe situar y que la lectura «Se pueden situar» tiene que contar.
--
--   · 1 de cada 11 → nacional      (sin departamento: no se dibuja, y se cuenta aparte)
--   · 1 de cada 9  → departamental (sin municipio: los modos de punto no lo sitúan)
--   · el resto     → municipal
--
-- LOS CAMPOS PUBLICOS VAN TODOS LLENOS, que es el motivo por el que esta semilla se escribió: la
-- ficha del mapa enseña nivel de cobertura, cuándo ocurre, periodicidad, territorios, prácticas,
-- sitio web, Instagram, Facebook y otro enlace. Con cualquiera vacío, esa línea no se dibuja y no
-- se puede juzgar si la ficha se lee bien llena.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Hoy date = CONVERT(date, SYSUTCDATETIME());

-- Separada de `@FestivalesSembrados`, que guarda los de la pasada ANTERIOR y ya se borraron.
DECLARE @Sembrados TABLE (Id int PRIMARY KEY);

-- LOS CATALOGOS, NUMERADOS. Sus identificadores no son contiguos —se han creado y retirado filas—
-- así que repartir «uno de cada N» por el Id daría un reparto con huecos. Se numeran por orden.
DECLARE @Practicas TABLE (Indice int PRIMARY KEY, Id int, Nombre nvarchar(200));
INSERT INTO @Practicas (Indice, Id, Nombre)
SELECT ROW_NUMBER() OVER (ORDER BY OrdenVisualizacion, IdPracticaMusical), IdPracticaMusical, NombrePracticaMusical FROM dbo.PracticasMusicales;

DECLARE @Territorios TABLE (Indice int PRIMARY KEY, Id int, Nombre nvarchar(200));
INSERT INTO @Territorios (Indice, Id, Nombre)
SELECT ROW_NUMBER() OVER (ORDER BY OrdenVisualizacion, IdTerritorioSonoro), IdTerritorioSonoro, NombreTerritorioSonoro FROM dbo.TerritoriosSonoros;

DECLARE @TotalPracticas int = (SELECT COUNT(*) FROM @Practicas);
DECLARE @TotalTerritorios int = (SELECT COUNT(*) FROM @Territorios);

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 3 bis. LAS ORGANIZACIONES QUE RESPONDEN POR LOS FESTIVALES
--
-- `Festivales.OrganizacionPrincipalId` NO ADMITE NULOS, y eso no es un obstáculo de la siembra:
-- es la decisión de producto del proyecto —«la organización es el único actor que registra y
-- administra procesos»— hecha cumplir por el esquema. Un Festival sin organización responsable no
-- puede existir, así que la semilla crea primero quién responde.
--
-- UNA POR DEPARTAMENTO, con sede en su capital de facto (el primer municipio por nombre, que es el
-- mismo criterio determinista que usa el reparto). Así la ficha del mapa tiene una organización
-- distinta por territorio, que es lo que permite ver si el resumen de organización del panel se
-- lee bien; con una sola organización para todo el país, esa pieza no se podría juzgar.
--
-- Y NO SE CONFUNDE CON LA PROCEDENCIA. Que el PNMC incorpore un Festival no lo convierte en su
-- organización responsable: aquí la responsable es la corporación del territorio, y la procedencia
-- —quién metió el registro— se declara aparte, en `ProcedenciaDeRegistros`. Son las dos dimensiones
-- que el proyecto exige no confundir.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Organizaciones TABLE (Id int PRIMARY KEY, CodigoDepartamento char(2));

MERGE dbo.Entidades AS destino
USING (
    SELECT p.CodigoDepartamento,
           MAX(p.NombreDepartamentoLegible) AS NombreDepartamento,
           MIN(p.CodigoMunicipio) AS CodigoMunicipio
    FROM @Plan p
    WHERE p.CodigoMunicipio IS NOT NULL
    GROUP BY p.CodigoDepartamento
) AS origen ON 1 = 0
WHEN NOT MATCHED THEN INSERT (
    TipoEntidad, Nombre, NombreLegal, Descripcion, CorreoContacto, TelefonoContacto,
    SitioWeb, Facebook, Instagram, OtroEnlace, Direccion,
    EstadoRegistro, Activo, EsInstitucional, IdUsuarioCreador,
    CodigoDepartamentoSede, CodigoMunicipioSede, FechaCreacion, FechaActualizacion)
VALUES (
    N'organizacion',
    CONCAT(N'Corporación Musical de ', origen.NombreDepartamento),
    CONCAT(N'Corporación Musical de ', origen.NombreDepartamento, N' S.A.S.'),
    CONCAT(N'Organización cultural que acompaña procesos musicales en ', origen.NombreDepartamento,
           N': formación, circulación y memoria sonora del territorio.'),
    CONCAT(N'contacto.', LOWER(origen.CodigoDepartamento), N'@corporacion.test'),
    CONCAT(N'+57 605 ', RIGHT(CONCAT(N'0000000', CAST(origen.CodigoDepartamento AS int) * 4231), 7)),
    CONCAT(N'https://corporacion-', LOWER(origen.CodigoDepartamento), N'.test'),
    CONCAT(N'https://facebook.com/corp', origen.CodigoDepartamento, N'pnmc'),
    CONCAT(N'https://instagram.com/corp', origen.CodigoDepartamento, N'pnmc'),
    CONCAT(N'https://linktr.ee/corp', origen.CodigoDepartamento, N'pnmc'),
    CONCAT(N'Calle ', 10 + CAST(origen.CodigoDepartamento AS int) % 40, N' # ',
           5 + CAST(origen.CodigoDepartamento AS int) % 30, N'-21'),
    N'activa', 1, 0, @UsuarioSiembra,
    origen.CodigoDepartamento, origen.CodigoMunicipio, SYSUTCDATETIME(), SYSUTCDATETIME())
OUTPUT inserted.IdEntidad, inserted.CodigoDepartamentoSede INTO @Organizaciones (Id, CodigoDepartamento);

INSERT INTO dbo.ProcedenciaDeRegistros (Dominio, RegistroId, ContextoOrigen, UsuarioCreadorId, FechaRegistro)
SELECT N'organizacion', Id, N'siembra', @UsuarioSiembra, SYSUTCDATETIME() FROM @Organizaciones;

INSERT INTO dbo.Festivales (
    NombreFestival, NumeroVersiones, FechaUltimaVersion, Descripcion, Organizador,
    CorreoOrganizador, TelefonoOrganizador, SitioWebOrganizador,
    CorreoFestival, InstagramFestival, FacebookFestival, SitioWebFestival, OtroEnlaceFestival,
    TelefonoFestival, Director, ObservacionesContacto,
    TieneVersionVigenteAnoActual, EstadoVersionAnoActual,
    FechaInicioVersionActual, FechaFinVersionActual,
    NivelCobertura, CodigoDepartamento, CodigoMunicipio,
    Periodicidad, PeriodicidadDetalle, OrganizacionPrincipalId,
    Activo, EstadoRegistro, IdUsuarioCreador, FechaCreacion, FechaActualizacion)
OUTPUT inserted.IdFestival INTO @Sembrados (Id)
SELECT
    -- EL NOMBRE SALE DEL TERRITORIO SONORO Y NO DE LA PRACTICA. Las prácticas del catálogo son
    -- frases enteras —«Bandas de marcha, batucadas, comparsas y colectivos sonoros en movimiento»—
    -- y metidas en un nombre daban rótulos de 90 caracteres que reventaban cada tarjeta y cada
    -- fila del ranking. Los territorios sonoros son nombres cortos y evocadores —Joropo, Chirimía,
    -- Marimba— que es justo como se llaman los Festivales de verdad.
    CONCAT(N'Festival de ', te.Nombre, N' de ', p.NombreLegible),
    2 + (p.Orden % 14),
    DATEADD(YEAR, -1, DATEADD(DAY, (p.Orden * 7) % 300, DATEFROMPARTS(YEAR(@Hoy), 1, 15))),
    CONCAT(N'Encuentro de ', LOWER(te.Nombre), N' que reúne agrupaciones, luthiers y públicos de ',
           p.NombreLegible, N' y su área de influencia. Programación de ',
           N'conciertos, talleres de formación y una muestra de instrumentos de la región. La ',
           N'práctica declarada es «', LOWER(pr.Nombre), N'».'),
    CONCAT(N'Corporación Cultural ', p.NombreLegible),
    CONCAT(N'contacto', p.Orden, N'@corporacion.test'),
    CONCAT(N'+57 320 ', RIGHT(CONCAT(N'0000000', p.Orden * 137), 7)),
    CONCAT(N'https://corporacion', p.Orden, N'.test'),
    CONCAT(N'hola', p.Orden, N'@festival.test'),
    CONCAT(N'https://instagram.com/fest', p.Orden, N'pnmc'),
    CONCAT(N'https://facebook.com/fest', p.Orden, N'pnmc'),
    CONCAT(N'https://festival', p.Orden, N'.test'),
    CONCAT(N'https://linktr.ee/fest', p.Orden, N'pnmc'),
    CONCAT(N'+57 601 ', RIGHT(CONCAT(N'0000000', p.Orden * 211), 7)),
    CONCAT(N'Dirección artística ', p.NombreLegible),
    N'Datos de demostración: no corresponden a un Festival real.',
    1,
    N'programada',
    -- Repartidos por todo el año: el calendario de la Agenda y el «Cuándo ocurre» de la ficha
    -- necesitan meses distintos, y con todos en el mismo mes no se ve si la vista los ordena.
    DATEADD(DAY, (p.Orden * 11) % 330, DATEFROMPARTS(YEAR(@Hoy), 1, 8)),
    DATEADD(DAY, ((p.Orden * 11) % 330) + 2 + (p.Orden % 5), DATEFROMPARTS(YEAR(@Hoy), 1, 8)),
    CASE WHEN p.Orden % 11 = 0 THEN N'nacional'
         WHEN p.Orden % 9 = 0 THEN N'departamental'
         ELSE N'municipal' END,
    CASE WHEN p.Orden % 11 = 0 THEN NULL ELSE p.CodigoDepartamento END,
    CASE WHEN p.Orden % 11 = 0 OR p.Orden % 9 = 0 THEN NULL ELSE p.CodigoMunicipio END,
    CASE p.Orden % 6 WHEN 0 THEN N'bienal' WHEN 1 THEN N'anual' WHEN 2 THEN N'anual'
                     WHEN 3 THEN N'semestral' WHEN 4 THEN N'anual' ELSE N'intermitente' END,
    CASE WHEN p.Orden % 6 = 5 THEN N'Se realiza cuando hay convocatoria departamental.' ELSE NULL END,
    o.Id,
    1,
    -- CASI TODOS PUBLICADOS, PERO NO TODOS. El mapa sólo lee lo publicado; los otros tres estados
    -- existen para que la consola administrativa tenga sobre qué trabajar con el mismo conjunto.
    CASE WHEN p.Orden % 17 = 0 THEN N'borrador'
         WHEN p.Orden % 23 = 0 THEN N'en_revision'
         ELSE N'publicado' END,
    @UsuarioSiembra,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM @Plan p
JOIN @Practicas pr ON pr.Indice = ((p.Orden - 1) % @TotalPracticas) + 1
-- EL TERRITORIO SONORO ES COHERENTE POR DEPARTAMENTO, y esto no es un adorno.
--
-- Repartido en ciclo plano salía «Festival de Amazonas» en Huila y «de Marimba» en Boyacá, uno
-- detrás de otro: con eso el lente de territorios sonoros pinta confeti y no se puede juzgar, que
-- es justo para lo que existe la semilla. Atado al departamento, cada región tiene un territorio
-- dominante y el lente muestra AGRUPACIONES, que es lo que un lente de territorios sonoros tiene
-- que revelar y lo único que permite decidir si se lee bien.
--
-- NO SE AFIRMA QUE SEA EL REPARTO REAL. El emparejamiento sale del código del departamento, no de
-- la cartografía cultural del Plan: es coherente, no es cierto, y nada de lo que se sembró aquí
-- debe leerse como el mapa real de los territorios sonoros de Colombia. La coherencia es lo que
-- hace evaluable la pantalla; la verdad la traerán los datos reales.
JOIN @Territorios te ON te.Indice = ((CAST(p.CodigoDepartamento AS int) * 3) % @TotalTerritorios) + 1
JOIN @Organizaciones o ON o.CodigoDepartamento = p.CodigoDepartamento;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 5. LA CLASIFICACION: TERRITORIOS SONOROS Y PRACTICAS
--
-- CON SOLAPE Y CON HUECOS, que es lo que hace falta para juzgar la lectura de «Composición».
--
--   · Uno de cada siete Festivales NO declara nada. Así existe «Sin clasificar» con una cifra
--     real, que es la categoría que convierte «desaparece» en «no consta».
--   · Los demás declaran entre uno y tres territorios sonoros. Con uno solo, la suma de la
--     composición cuadraría con el total y el aviso de «la suma pasa del total» no saltaría nunca:
--     estaría escrito y nunca se vería, que es como se llega a que deje de ser cierto sin que
--     nadie lo note.
--
-- El reparto es por posición sobre los catálogos numerados, no aleatorio: la semilla tiene que dar
-- lo mismo dos veces seguidas.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Numerados TABLE (Fila int PRIMARY KEY, IdFestival int);
INSERT INTO @Numerados (Fila, IdFestival)
SELECT ROW_NUMBER() OVER (ORDER BY Id), Id FROM @Sembrados;

-- EL PRIMERO ES EL DOMINANTE DEL DEPARTAMENTO —el mismo que da nombre al Festival— y los otros
-- uno o dos son vecinos en el catálogo. Así cada región tiene su color con el lente puesto y aun
-- así los territorios CRUZAN departamentos, que es precisamente lo que un lente de territorios
-- sonoros debe dejar ver y un mapa por departamentos no puede.
INSERT INTO dbo.FestivalesTerritoriosSonoros (FestivalId, TerritorioSonoroId)
SELECT DISTINCT n.IdFestival, t.Id
FROM @Numerados n
JOIN dbo.Festivales f ON f.IdFestival = n.IdFestival
JOIN @Plan p ON p.Orden = n.Fila
CROSS APPLY (SELECT TOP (1 + (n.Fila % 3)) Numero FROM (SELECT 0 AS Numero UNION ALL SELECT 1 UNION ALL SELECT 2) x) d
JOIN @Territorios t
  ON t.Indice = (((CAST(p.CodigoDepartamento AS int) * 3) + d.Numero) % @TotalTerritorios) + 1
WHERE n.Fila % 7 <> 0;

INSERT INTO dbo.FestivalesPracticasMusicales (FestivalId, PracticaMusicalId)
SELECT DISTINCT n.IdFestival, pm.Id
FROM @Numerados n
CROSS APPLY (SELECT TOP (1 + (n.Fila % 2)) Numero FROM (SELECT 0 AS Numero UNION ALL SELECT 1) x) d
JOIN @Practicas pm ON pm.Indice = (((n.Fila + d.Numero * 7) - 1) % @TotalPracticas) + 1
WHERE n.Fila % 7 <> 0;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 6. LA AGENDA
--
-- SE CUELGA DE LOS FESTIVALES SEMBRADOS, y no flota aparte. En el mapa la Agenda es el contexto
-- del territorio que se acaba de abrir; si sus eventos cayeran en departamentos donde no hay
-- Festivales, abrir un territorio enseñaría procesos sin eventos y eventos sin procesos, que no es
-- el caso que hay que poder mirar.
--
-- TRES EVENTOS POR FESTIVAL, REPARTIDOS EN EL TIEMPO: uno pasado, uno dentro del mes y uno más
-- adelante. Es lo que distingue los tres tramos del panel —«Próximos», «Este mes», «Todos»—; con
-- todos en la misma semana, los tres botones darían la misma lista y no se podría ver si filtran.
--
-- LA MODALIDAD OBLIGA A LLENAR UN CAMPO U OTRO. `CK_EventosAgenda_Modalidad` exige lugar si es
-- presencial y dirección si es virtual, cuando está publicado. Se reparten las tres modalidades
-- con su campo correspondiente lleno, porque la ficha del evento las enseña y hay que poder verlas.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Eventos TABLE (Id int PRIMARY KEY);

INSERT INTO dbo.EventosAgenda (
    Slug, Titulo, Descripcion, DescripcionLarga, FechaInicio, FechaFin, HoraInicio, HoraFin,
    Modalidad, Lugar, CodigoDepartamento, CodigoMunicipio, Url, Organizador,
    NivelCobertura, CategoriaId, Estado, Version, OrdenVisualizacion, FestivalId,
    FechaCreacion, FechaActualizacion)
OUTPUT inserted.IdEventoAgenda INTO @Eventos (Id)
SELECT
    CONCAT(N'evento-demo-', n.Fila, N'-', t.Turno),
    CONCAT(CASE t.Turno WHEN 0 THEN N'Apertura de ' WHEN 1 THEN N'Conciertos de ' ELSE N'Taller de ' END,
           REPLACE(f.NombreFestival, N'Festival de ', N'')),
    CONCAT(CASE t.Turno WHEN 0 THEN N'Jornada inaugural' WHEN 1 THEN N'Programación central'
                        ELSE N'Encuentro formativo' END,
           N' del ', f.NombreFestival, N'.'),
    CONCAT(N'Actividad abierta al público dentro de la programación del ', f.NombreFestival,
           N'. Incluye conversatorio con las agrupaciones invitadas y muestra de repertorio.'),
    DATEADD(DAY, CASE t.Turno WHEN 0 THEN -20 - (n.Fila % 40) WHEN 1 THEN (n.Fila % 25)
                              ELSE 45 + (n.Fila % 90) END, @Hoy),
    DATEADD(DAY, CASE t.Turno WHEN 0 THEN -20 - (n.Fila % 40) WHEN 1 THEN (n.Fila % 25)
                              ELSE 45 + (n.Fila % 90) END + 1, @Hoy),
    -- LA HORA DE FIN SE DERIVA DE LA DE INICIO Y NO SE CALCULA APARTE. Con dos fórmulas
    -- independientes, `CK_EventosAgenda_Horas` —que exige fin >= inicio— saltaba en cuanto las dos
    -- se cruzaban: un evento que empieza a las 19:00 y termina a las 18:00.
    CAST(DATEADD(HOUR, 15 + (n.Fila % 5), CAST('00:00' AS time)) AS time),
    CAST(DATEADD(HOUR, 15 + (n.Fila % 5) + 2 + (n.Fila % 3), CAST('00:00' AS time)) AS time),
    CASE (n.Fila + t.Turno) % 5 WHEN 0 THEN N'virtual' WHEN 1 THEN N'mixta' ELSE N'presencial' END,
    -- Presencial y mixta necesitan lugar; virtual no lo lleva, porque decirlo sería mentir.
    CASE WHEN (n.Fila + t.Turno) % 5 = 0 THEN NULL
         ELSE CONCAT(N'Casa de la Cultura de ', ISNULL(f.CodigoMunicipio, N'la ciudad')) END,
    f.CodigoDepartamento,
    f.CodigoMunicipio,
    -- Virtual y mixta necesitan dirección.
    CASE WHEN (n.Fila + t.Turno) % 5 IN (0, 1) THEN CONCAT(N'https://transmision', n.Fila, N'.test/en-vivo')
         ELSE NULL END,
    f.Organizador,
    f.NivelCobertura,
    ((n.Fila + t.Turno) % 3) + 1,
    -- Casi todos publicados: el mapa sólo lee lo publicado, y uno de cada trece se queda en
    -- borrador para que la consola tenga qué revisar sobre el mismo conjunto.
    CASE WHEN (n.Fila * 3 + t.Turno) % 13 = 0 THEN N'borrador' ELSE N'publicado' END,
    1,
    n.Fila,
    f.IdFestival,
    SYSUTCDATETIME(),
    SYSUTCDATETIME()
FROM @Numerados n
JOIN dbo.Festivales f ON f.IdFestival = n.IdFestival
LEFT JOIN (SELECT DISTINCT CodigoMunicipio, NombreLegible FROM @Plan) pl
       ON pl.CodigoMunicipio = f.CodigoMunicipio
CROSS JOIN (SELECT 0 AS Turno UNION ALL SELECT 1 UNION ALL SELECT 2) t
-- Sólo de los Festivales publicados: un evento colgado de un borrador aparecería en el portal
-- anunciando un Festival que el portal no enseña.
WHERE f.EstadoRegistro = N'publicado';

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 7. LA PROCEDENCIA DE TODO LO SEMBRADO
--
-- NINGUN REGISTRO SIN PROCEDENCIA: es la regla transversal del proyecto. `'siembra'` es uno de los
-- seis contextos que la tabla ya admitía, junto a `administrativo`, `externo`, `importacion`,
-- `historico` y `prueba`. Declararlo aquí cumple la regla y, de paso, convierte a esta tabla en el
-- inventario exacto de lo que este guion creó: es lo que el paso 1 lee para poder retirarlo.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
INSERT INTO dbo.ProcedenciaDeRegistros (Dominio, RegistroId, ContextoOrigen, UsuarioCreadorId, FechaRegistro)
SELECT N'festival', Id, N'siembra', @UsuarioSiembra, SYSUTCDATETIME() FROM @Sembrados;

INSERT INTO dbo.ProcedenciaDeRegistros (Dominio, RegistroId, ContextoOrigen, UsuarioCreadorId, FechaRegistro)
SELECT N'agenda', Id, N'siembra', @UsuarioSiembra, SYSUTCDATETIME() FROM @Eventos;

COMMIT TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- 8. LO QUE QUEDO, DICHO EN VOZ ALTA
--
-- El guion cuenta lo que sembró, y con la forma que importa —cuántos departamentos, cuántos
-- vacíos, cuántos sin situar—, no sólo el total. Un «178 festivales» no dice si el mapa va a poder
-- enseñar algo.
-- ─────────────────────────────────────────────────────────────────────────────────────────────
SELECT
    N'Festivales sembrados'            AS Concepto, COUNT(*) AS Cifra FROM @Sembrados
UNION ALL SELECT N'  publicados',        COUNT(*) FROM dbo.Festivales f JOIN @Sembrados s ON s.Id = f.IdFestival WHERE f.EstadoRegistro = N'publicado'
UNION ALL SELECT N'  departamentos',     COUNT(DISTINCT f.CodigoDepartamento) FROM dbo.Festivales f JOIN @Sembrados s ON s.Id = f.IdFestival
UNION ALL SELECT N'  municipios',        COUNT(DISTINCT f.CodigoMunicipio) FROM dbo.Festivales f JOIN @Sembrados s ON s.Id = f.IdFestival
UNION ALL SELECT N'  cobertura nacional', COUNT(*) FROM dbo.Festivales f JOIN @Sembrados s ON s.Id = f.IdFestival WHERE f.NivelCobertura = N'nacional'
UNION ALL SELECT N'  sin municipio',     COUNT(*) FROM dbo.Festivales f JOIN @Sembrados s ON s.Id = f.IdFestival WHERE f.CodigoMunicipio IS NULL
UNION ALL SELECT N'  con territorio sonoro', COUNT(DISTINCT ft.FestivalId) FROM dbo.FestivalesTerritoriosSonoros ft JOIN @Sembrados s ON s.Id = ft.FestivalId
UNION ALL SELECT N'  sin clasificar',    COUNT(*) FROM @Sembrados s WHERE NOT EXISTS (SELECT 1 FROM dbo.FestivalesTerritoriosSonoros ft WHERE ft.FestivalId = s.Id)
UNION ALL SELECT N'Departamentos vacios', (SELECT COUNT(*) FROM @Departamentos WHERE Cuota = 0)
UNION ALL SELECT N'Organizaciones',       (SELECT COUNT(*) FROM @Organizaciones)
UNION ALL SELECT N'Eventos de agenda',    COUNT(*) FROM @Eventos;
