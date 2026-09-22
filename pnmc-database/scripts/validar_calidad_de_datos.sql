-- PNMC - consultas rapidas de calidad de datos locales.
-- Ejecutar manualmente en VS Code MSSQL, Azure Data Studio o sqlcmd.

-- 1) Combinaciones DIVIPOLA duplicadas
SELECT CodigoDepartamento, CodigoMunicipio, COUNT(*) AS RegistrosDuplicados
FROM dbo.Divipola
GROUP BY CodigoDepartamento, CodigoMunicipio
HAVING COUNT(*) > 1
ORDER BY RegistrosDuplicados DESC;

-- 2) News records without title
SELECT TOP (100) IdNoticia, Titulo, FechaPublicacion
FROM dbo.Noticias
WHERE Titulo IS NULL OR LTRIM(RTRIM(Titulo)) = '';

-- 3) Agenda events without date or territory
--
-- CUIDADO CON `IS NULL` AQUI. La version anterior de esta consulta preguntaba
-- `FechaInicio IS NULL OR (NivelCobertura <> N'nacional' AND CodigoDepartamento
-- IS NULL)` y devolvia cero filas por construccion -- no porque los datos
-- estuvieran sanos, sino porque ninguna de las dos condiciones puede darse:
--
--   * `FechaInicio` es `date NOT NULL` (V20260519_03__contenidos_modulos.sql:73)
--     y la entidad la declara `DateTime` no anulable (Rows.cs). Un evento sin
--     fecha no queda en NULL: queda en el centinela 0001-01-01.
--   * `CodigoDepartamento` admite NULL en el DDL, pero el codigo nunca escribe
--     NULL. `DepartmentCode` es `string = string.Empty` (Rows.cs) y la carga
--     guarda `deptCode ?? string.Empty` (AdminDataEndpoints.cs, 1500, 1532,
--     1580, 1604). Lo que queda en la columna es cadena vacia.
--   * Ademas es `char(2)`, asi que la cadena vacia se almacena rellenada con
--     espacios: hay que comparar con LTRIM/RTRIM, no con `= N''` a secas.
--
-- LIMITE CONOCIDO, y hay que decirlo: `ResolveCoverageLevel`
-- (AdminDataEndpoints.cs-2235) convierte "sin departamento" en
-- NivelCobertura = 'nacional'. Un evento al que se le perdio el territorio queda
-- indistinguible de un evento nacional legitimo, y NINGUNA consulta sobre esta
-- tabla puede separarlos. Por eso la parte (c) de abajo cuenta, sin afirmar que
-- sean errores. Detectarlos de verdad exige registrar la cobertura pedida.
SELECT TOP (100)
    IdAgenda,
    Titulo,
    FechaInicio,
    NivelCobertura,
    CodigoDepartamento,
    CodigoMunicipio,
    CASE
        WHEN FechaInicio IS NULL OR FechaInicio < '1900-01-01' THEN 'sin fecha'
        WHEN LTRIM(RTRIM(NivelCobertura)) NOT IN (N'nacional', N'departamental', N'municipal')
            THEN 'nivel de cobertura fuera del vocabulario'
        WHEN LTRIM(RTRIM(NivelCobertura)) IN (N'departamental', N'municipal')
             AND (CodigoDepartamento IS NULL OR LTRIM(RTRIM(CodigoDepartamento)) = N'')
            THEN 'cobertura territorial sin departamento'
        WHEN LTRIM(RTRIM(NivelCobertura)) = N'municipal'
             AND (CodigoMunicipio IS NULL OR LTRIM(RTRIM(CodigoMunicipio)) = N'')
            THEN 'cobertura municipal sin municipio'
        ELSE 'nacional sin departamento (ambiguo, ver nota)'
    END AS Motivo
FROM dbo.Agenda
WHERE
    -- (a) sin fecha: NULL si alguna carga futura lo permite, y el centinela.
    FechaInicio IS NULL
    OR FechaInicio < '1900-01-01'
    -- (b) incoherencia real: dice cubrir un territorio y no lo nombra.
    OR (
        LTRIM(RTRIM(NivelCobertura)) IN (N'departamental', N'municipal')
        AND (CodigoDepartamento IS NULL OR LTRIM(RTRIM(CodigoDepartamento)) = N'')
       )
    OR (
        LTRIM(RTRIM(NivelCobertura)) = N'municipal'
        AND (CodigoMunicipio IS NULL OR LTRIM(RTRIM(CodigoMunicipio)) = N'')
       )
    -- (c) nivel que no pertenece al vocabulario congelado.
    OR LTRIM(RTRIM(NivelCobertura)) NOT IN (N'nacional', N'departamental', N'municipal')
    -- (d) ambiguos: ver la nota. Se listan para contarlos, no para acusarlos.
    OR (
        LTRIM(RTRIM(NivelCobertura)) = N'nacional'
        AND (CodigoDepartamento IS NULL OR LTRIM(RTRIM(CodigoDepartamento)) = N'')
       );

-- 4) Territorial records without DIVIPOLA match
SELECT TOP (200)
    f.IdFestival,
    f.NombreFestival,
    f.CodigoDepartamento,
    f.CodigoMunicipio
FROM dbo.Festivales f
LEFT JOIN dbo.Divipola d
    ON d.CodigoDepartamento = f.CodigoDepartamento
   AND d.CodigoMunicipio = f.CodigoMunicipio
WHERE f.CodigoDepartamento IS NOT NULL
  AND d.CodigoDepartamento IS NULL;

-- 5) Registros de participacion con correo potencialmente invalido
SELECT TOP (200) Referencia, CorreoElectronico, FechaEnvio
FROM dbo.Participaciones
WHERE CorreoElectronico IS NULL
   OR CorreoElectronico NOT LIKE '%_@_%._%';

-- 6) CMS: claves publicadas en blanco  (inventario obligatorio de PNMC-040)
--
-- En `dbo.ContenidoWeb`, `Publicado` distingue tres estados y hay que respetar
-- los tres:
--   * NULL          -> nadie la publico, o se retiro. El sitio sirve el texto
--                      compilado (WebContentEndpoints.cs filtra IS NOT NULL).
--   * cadena vacia  -> alguien PUBLICO el campo en blanco a proposito.
--   * texto         -> lo que ve el visitante.
--
-- Hasta la correccion de PNMC-040, el front-end resolvia el texto con `||` en
-- lugar de `??` (textos-web.service.ts), de modo que la cadena vacia caia al
-- valor compilado: borrar un texto publicado no lo borraba y el panel confirmaba
-- que si. Al corregirlo, TODA clave que aparezca en esta consulta deja de
-- mostrar su texto de fabrica y pasa a verse en blanco en el sitio publico.
--
-- Por eso se corre ANTES de desplegar la correccion y se confirma clave por
-- clave con el equipo editorial (backlog, R2): parecera una regresion y no lo es.
-- Se usa DATALENGTH y no `= N''`: en SQL Server la comparacion de cadenas
-- ignora los espacios finales, asi que `Publicado = N''` tambien casaria con
-- N'   '. Y los dos casos NO son el mismo: una clave exactamente vacia cambia de
-- aspecto al corregir PNMC-040, y una con espacios ya se servia tal cual.
SELECT
    Clave,
    GrupoId,
    Seccion,
    Etiqueta,
    CASE WHEN DATALENGTH(Publicado) = 0
         THEN 'vacia exacta - CAMBIA de aspecto al corregir 040'
         ELSE 'solo espacios - ya se servia asi, no cambia'
    END                    AS Efecto,
    DATALENGTH(Publicado)  AS BytesPublicado,
    LEN(Borrador)          AS LongitudBorrador,
    Version,
    ActualizadoPor,
    FechaActualizacion
FROM dbo.ContenidoWeb
WHERE Publicado IS NOT NULL
  AND LTRIM(RTRIM(Publicado)) = N''
ORDER BY Seccion, GrupoId, Clave;

-- 6b) El mismo inventario, en una sola cifra para el informe de despliegue.
SELECT
    COUNT(*)                                                        AS ClavesTotales,
    SUM(CASE WHEN Publicado IS NULL THEN 1 ELSE 0 END)              AS SinPublicar,
    SUM(CASE WHEN DATALENGTH(Publicado) = 0 THEN 1 ELSE 0 END)      AS PublicadasVaciasExactas,
    SUM(CASE WHEN Publicado IS NOT NULL
                  AND DATALENGTH(Publicado) > 0
                  AND LTRIM(RTRIM(Publicado)) = N'' THEN 1 ELSE 0 END) AS PublicadasSoloEspacios,
    SUM(CASE WHEN Publicado IS NOT NULL
                  AND LTRIM(RTRIM(Publicado)) <> N'' THEN 1 ELSE 0 END) AS PublicadasConTexto
FROM dbo.ContenidoWeb;
