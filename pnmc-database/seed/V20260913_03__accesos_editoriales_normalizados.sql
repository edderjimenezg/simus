/*
 * LOS ENLACES QUE ESTABAN ESCRITOS DENTRO DE UN PARRAFO.
 *
 * EL COMPORTAMIENTO DE ORIGEN. El volcado de origen escribía las direcciones dentro del
 * texto libre —unas veces en la nota del acceso, la mayoría dentro de la UBICACION FISICA, en frases
 * como «Ministerio de Cultura, oficina del Área de Música, Biblioteca Nacional. Se encuentra
 * publicado en: https://…»—. Resultado: <b>28 direcciones en 23 publicaciones</b> que el modelo no
 * reconocía como accesos, y <b>21 publicaciones que en el portal se veían como «solo consulta
 * presencial» teniendo copia en línea</b>. Nadie podía abrirlas desde la ficha: estaban ahí, dentro
 * de una frase, sin ser un enlace.
 *
 * POR QUE ES UNA SEMILLA Y NO UNA MIGRACION. Las semillas corren DESPUES de las migraciones
 * (`scripts/seed-local-db.sh`: primero `migrar`, luego la lista de `seed/`). Una migración habría
 * arreglado esta base y habría dejado nacer sucia la siguiente, porque el acervo se vuelve a cargar
 * desde su semilla. Esto va detrás de `V20260913_02__catalogo_editorial_acervo.sql` y normaliza lo
 * que aquella inserta.
 *
 * QUE HACE, Y QUE NO. Promueve a acceso cada dirección atrapada —no borra ninguna— y después recorta
 * del texto de ubicación la parte que solo servía para introducirla, quitando también el conector
 * que queda colgando («… Biblioteca Nacional. Se encuentra publicado en:» → «… Biblioteca
 * Nacional»). La regla se probó antes contra los 23 textos reales: 22 conservan una ubicación con
 * sentido y uno se queda vacío —`PNMC-ED-101`, cuyo texto era íntegramente «en el enlace: … y en
 * You Tube en el enlace: …»—, y esa fila deja de ser una ubicación porque nunca lo fue.
 *
 * ES IDEMPOTENTE: al repetirse no duplica accesos —comprueba por publicación y dirección— y no
 * vuelve a recortar un texto que ya no contiene direcciones.
 */
SET NOCOUNT ON;
GO

/* ── 1. Cada dirección atrapada pasa a ser un acceso de pleno derecho ─────────── */
WITH trozos AS (
    SELECT a.PublicacionEditorialId,
           /* Se parte por espacios: la fuente separaba con «;», con « y » y con un espacio a secas,
              y deducir el separador de unos pocos ejemplos ya falló una vez. */
           LTRIM(RTRIM(s.value)) AS trozo
    FROM dbo.AccesosEditoriales a
    CROSS APPLY STRING_SPLIT(
        REPLACE(REPLACE(REPLACE(ISNULL(a.UbicacionFisica, N'') + N' ' + ISNULL(a.Nota, N''),
                N';', N' '), CHAR(9), N' '), CHAR(10), N' '), N' ') s
    WHERE LTRIM(RTRIM(s.value)) LIKE N'http%'
),
limpias AS (
    /* Una dirección al final de una frase arrastra el punto: no forma parte de la dirección. */
    SELECT PublicacionEditorialId,
           CASE WHEN RIGHT(trozo, 1) IN (N'.', N',', N';', N')') THEN LEFT(trozo, LEN(trozo) - 1) ELSE trozo END AS Url
    FROM trozos
),
nuevas AS (
    SELECT DISTINCT l.PublicacionEditorialId, l.Url
    FROM limpias l
    WHERE LEN(l.Url) > 12
      AND NOT EXISTS (SELECT 1 FROM dbo.AccesosEditoriales o
                      WHERE o.PublicacionEditorialId = l.PublicacionEditorialId AND o.Url = l.Url)
)
INSERT INTO dbo.AccesosEditoriales
    (PublicacionEditorialId, Tipo, Url, Etiqueta, Orden,
     DerechosEstado, DerechosPermitePublicarFicha, DerechosPermitePublicarArchivo)
SELECT n.PublicacionEditorialId, N'enlace', n.Url, N'Recurso en línea',
       500 + ROW_NUMBER() OVER (PARTITION BY n.PublicacionEditorialId ORDER BY n.Url),
       N'pendiente', 1, 0
FROM nuevas n;
GO

/* ── 2. El texto de ubicación se queda sin la frase que solo servía de introducción ── */
UPDATE dbo.AccesosEditoriales
SET UbicacionFisica = LEFT(UbicacionFisica, CHARINDEX(N'http', UbicacionFisica) - 1)
WHERE UbicacionFisica LIKE N'%http%';
GO

/* Y sin el conector que queda colgando al final. Se repite porque las frases encadenan varios
   («… y en el enlace:», «… Disponible también en:»).

   OJO CON `LEN`: en SQL Server IGNORA LOS ESPACIOS FINALES, así que `LEFT(x, LEN(x) - 1)` sobre un
   texto acabado en espacio se come una letra de verdad. La primera versión de esta semilla dejó
   «… Ministerio de Cultur». Por eso aquí se recorta SIEMPRE sobre `RTRIM(...)`. */
DECLARE @conectores TABLE (palabra nvarchar(20) PRIMARY KEY);
INSERT INTO @conectores (palabra) VALUES
    (N'en'), (N'y'), (N'e'), (N'el'), (N'la'), (N'los'), (N'las'), (N'de'), (N'del'), (N'a'), (N'al'),
    (N'disponible'), (N'también'), (N'tambien'), (N'se'), (N'encuentra'), (N'publicado'),
    (N'publicada'), (N'enlace'), (N'enlaces'), (N'link'), (N'url'), (N'ver'), (N'está'), (N'esta');

DECLARE @vueltas int = 0;
WHILE @vueltas < 12
BEGIN
    /* Signos de puntuación colgando. */
    UPDATE dbo.AccesosEditoriales
    SET UbicacionFisica = LEFT(RTRIM(UbicacionFisica), LEN(RTRIM(UbicacionFisica)) - 1)
    WHERE UbicacionFisica IS NOT NULL
      AND LEN(RTRIM(UbicacionFisica)) > 0
      AND RIGHT(RTRIM(UbicacionFisica), 1) IN (N',', N'.', N';', N':', N'-');

    /* La última palabra, cuando solo servía para presentar la dirección. */
    UPDATE a
    SET UbicacionFisica =
        CASE
            /* Texto de una sola palabra: si es un conector, no queda nada. */
            WHEN CHARINDEX(N' ', RTRIM(a.UbicacionFisica)) = 0 THEN N''
            ELSE RTRIM(LEFT(RTRIM(a.UbicacionFisica),
                 LEN(RTRIM(a.UbicacionFisica)) - CHARINDEX(N' ', REVERSE(RTRIM(a.UbicacionFisica)))))
        END
    FROM dbo.AccesosEditoriales a
    WHERE a.UbicacionFisica IS NOT NULL
      AND LEN(RTRIM(a.UbicacionFisica)) > 0
      AND EXISTS (
            SELECT 1 FROM @conectores c
            WHERE c.palabra = LOWER(
                CASE WHEN CHARINDEX(N' ', RTRIM(a.UbicacionFisica)) = 0
                     THEN RTRIM(a.UbicacionFisica)
                     ELSE REVERSE(LEFT(REVERSE(RTRIM(a.UbicacionFisica)),
                          CHARINDEX(N' ', REVERSE(RTRIM(a.UbicacionFisica))) - 1))
                END));

    SET @vueltas = @vueltas + 1;
END;
GO

/* ── 3. La nota se vacía cuando ya no dice nada que no esté dicho ─────────────── */
WITH resto AS (
    SELECT a.IdAccesoEditorial, a.PublicacionEditorialId, LTRIM(RTRIM(s.value)) AS trozo
    FROM dbo.AccesosEditoriales a
    CROSS APPLY STRING_SPLIT(REPLACE(REPLACE(a.Nota, N';', N' '), CHAR(9), N' '), N' ') s
    WHERE a.Nota LIKE N'%http%'
),
marcado AS (
    SELECT r.IdAccesoEditorial,
           CASE
             /* Vacíos y la conjunción que la fuente usaba como separador no cuentan como contenido. */
             WHEN r.trozo = N'' OR LOWER(r.trozo) IN (N'y', N'e', N'en') THEN 0
             WHEN EXISTS (SELECT 1 FROM dbo.AccesosEditoriales o
                          WHERE o.PublicacionEditorialId = r.PublicacionEditorialId
                            AND o.Url IN (r.trozo,
                                          CASE WHEN RIGHT(r.trozo, 1) IN (N'.', N',', N';', N')')
                                               THEN LEFT(r.trozo, LEN(r.trozo) - 1) ELSE r.trozo END)) THEN 0
             ELSE 1
           END AS propio
    FROM resto r
),
sobrante AS (
    SELECT m.IdAccesoEditorial, SUM(m.propio) AS con_contenido_propio
    FROM marcado m GROUP BY m.IdAccesoEditorial
)
UPDATE a SET Nota = NULL
FROM dbo.AccesosEditoriales a
JOIN sobrante s ON s.IdAccesoEditorial = a.IdAccesoEditorial
WHERE s.con_contenido_propio = 0;
GO

/* ── 4. Sin duplicados: esta semilla le cambia el texto a la del acervo ───────────
 *
 * POR QUE HACE FALTA ESTE PASO, Y ES UN EFECTO DE ESTA MISMA SEMILLA. La semilla del acervo evita
 * repetir filas con un `NOT EXISTS` que compara `(Tipo, Url, UbicacionFisica)` contra lo que va a
 * insertar. Al recortar aquí el texto de ubicación, ese texto DEJA DE COINCIDIR con el de la
 * semilla, así que la siguiente siembra no reconoce la fila como ya existente y la vuelve a
 * insertar en crudo; esta semilla la recorta otra vez y quedan dos idénticas. Comprobado: tras tres
 * siembras, `PNMC-ED-050` mostraba la misma ubicación TRES veces en la ficha.
 *
 * Se resuelve donde se causa. Como después del recorte las copias son idénticas, basta con dejar la
 * primera de cada grupo. Con esto la pareja de semillas vuelve a ser idempotente: sembrar dos veces
 * da exactamente lo mismo que sembrar una. */
WITH copias AS (
    SELECT IdAccesoEditorial,
           ROW_NUMBER() OVER (
               PARTITION BY PublicacionEditorialId, Tipo,
                            ISNULL(Url, N''), ISNULL(UbicacionFisica, N''), ISNULL(Etiqueta, N'')
               ORDER BY IdAccesoEditorial) AS puesto
    FROM dbo.AccesosEditoriales
)
DELETE FROM dbo.AccesosEditoriales
WHERE IdAccesoEditorial IN (SELECT IdAccesoEditorial FROM copias WHERE puesto > 1);
GO

/* ── 5. Un agente que son dos personas se parte en dos ────────────────────────────
 *
 * EL COMPORTAMIENTO DE ORIGEN. Nueve agentes del acervo llevan una coma en el nombre, y NO es el orden
 * invertido de apellido y nombre como podría parecer: son DOS PERSONAS metidas en un mismo registro
 * —«Eblis Javier Álvarez Vargas, Carlos Andrés Rico Carvajal»—. La fuente escribió las dos en la
 * misma celda y la carga las tomó por un solo agente.
 *
 * QUE COSTABA. Buscar por «Carlos Andrés Rico Carvajal» no encontraba su obra, porque su nombre solo
 * existía como segunda mitad de otro. Y el catálogo contaba un autor donde había dos, así que el
 * fichero de autoridades estaba mal por partida doble: sobraba una entrada inventada y faltaban dos
 * reales.
 *
 * ES SEGURO, Y ESTA COMPROBADO ANTES DE HACERLO: los nueve llevan EXACTAMENTE una coma —ninguno dos—
 * y cada uno tiene UN solo crédito. Así que partir es: dejar al primero en el registro que ya
 * existía, crear al segundo, y duplicar ese único crédito para que la obra acredite a los dos. No se
 * pierde ninguna atribución; se recupera la que estaba escondida.
 *
 * IDEMPOTENTE: al repetirse ya no queda ningún nombre con coma, así que no hace nada. */
DECLARE @partidos TABLE (
    IdOriginal   bigint       NOT NULL,
    Primero      nvarchar(300) NOT NULL,
    Segundo      nvarchar(300) NOT NULL
);

INSERT INTO @partidos (IdOriginal, Primero, Segundo)
SELECT a.IdAgenteEditorial,
       LTRIM(RTRIM(LEFT(a.NombrePreferido, CHARINDEX(N',', a.NombrePreferido) - 1))),
       LTRIM(RTRIM(SUBSTRING(a.NombrePreferido, CHARINDEX(N',', a.NombrePreferido) + 1, 300)))
FROM dbo.AgentesEditoriales a
WHERE a.Tipo = N'persona'
  AND CHARINDEX(N',', a.NombrePreferido) > 0
  /* Solo los que llevan UNA coma: dos comas serían otra cosa y merecen mirarse a mano. */
  AND LEN(a.NombrePreferido) - LEN(REPLACE(a.NombrePreferido, N',', N'')) = 1;

/* El segundo nombre se crea como agente propio, si no existía ya con ese nombre exacto. */
INSERT INTO dbo.AgentesEditoriales (Codigo, Tipo, NombrePreferido, RequiereRevision)
SELECT CONCAT(N'AG-PARTIDO-', p.IdOriginal), N'persona', p.Segundo, 1
FROM @partidos p
WHERE LEN(p.Segundo) > 2
  AND NOT EXISTS (SELECT 1 FROM dbo.AgentesEditoriales x WHERE x.NombrePreferido = p.Segundo);

/* El crédito se duplica para el segundo, con el mismo papel y el mismo orden. */
INSERT INTO dbo.CreditosEditoriales (PublicacionEditorialId, AgenteEditorialId, RolCodigo, RolEtiqueta, Principal, Orden)
SELECT c.PublicacionEditorialId, nuevo.IdAgenteEditorial, c.RolCodigo, c.RolEtiqueta, c.Principal, c.Orden
FROM @partidos p
JOIN dbo.CreditosEditoriales c ON c.AgenteEditorialId = p.IdOriginal
JOIN dbo.AgentesEditoriales nuevo ON nuevo.NombrePreferido = p.Segundo
WHERE LEN(p.Segundo) > 2
  AND NOT EXISTS (
        SELECT 1 FROM dbo.CreditosEditoriales y
        WHERE y.PublicacionEditorialId = c.PublicacionEditorialId
          AND y.AgenteEditorialId = nuevo.IdAgenteEditorial
          AND y.RolCodigo = c.RolCodigo);

/* Y el registro original se queda solo con el primer nombre, que es el suyo. */
UPDATE a SET NombrePreferido = p.Primero, RequiereRevision = 1
FROM dbo.AgentesEditoriales a
JOIN @partidos p ON p.IdOriginal = a.IdAgenteEditorial
WHERE LEN(p.Primero) > 2;
GO

/* ── 6. Una fila que se quedó sin texto nunca fue una ubicación ───────────────── */
DELETE FROM dbo.AccesosEditoriales
WHERE Tipo = N'ubicacion' AND ArchivoId IS NULL AND Url IS NULL
  AND (UbicacionFisica IS NULL OR LEN(LTRIM(RTRIM(UbicacionFisica))) = 0);
GO
